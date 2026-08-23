using System.Globalization;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;
using Wayfarer.Core.Unlocks;
using Wayfarer.Modules;
using Wayfarer.Settings;
using Wayfarer.Windows.Native;

namespace Wayfarer.Windows;

/// <summary>The Wayfarer window — one native (KamiToolKit <see cref="NativeAddon"/>) window holding
/// everything the plugin has to show, in four tabs (Checklist, Hunting Log, Quests, Settings), for
/// mouse and controller alike. The game's own windows are mouse-first and cursor-navigable at the
/// same time; copying that is what lets one surface serve both players instead of two parallel
/// stacks drifting apart.
///
/// <b>Navigation.</b> The game drives a controller through any window with an explicit index graph
/// stored per interactive component (<c>AtkCursorNavigationInfo</c>: five bytes, self index plus
/// four neighbours). It is entirely opt-in — a component whose indices are all zero is a dead end,
/// and KamiToolKit only fills the graph in automatically for the children of its own list
/// containers. This window previously set no indices at all, which is why the cursor arrived on
/// whichever button took initial focus and could never leave it. The layout below is numbered
/// against <see cref="HubNavPlan"/>, in full, after every change that could move anything.
///
/// <b>Why the lists are <c>ListNode</c>s.</b> <c>ScrollingNode&lt;VerticalListNode&gt;</c> — what
/// this window used before — has no navigation implementation and no scroll-follows-focus, and
/// cannot be given either from the outside. <c>ListNode&lt;T, TU&gt;</c> has both: it reserves four
/// index slots per row and parks invisible sentinel components above and below the viewport that
/// catch a held d-pad and scroll instead of moving. It costs a uniform row height, which is why
/// section headings are rows here rather than separate nodes.
///
/// <b>Rebuild model.</b> <see cref="NativeAddon"/> deallocates its whole node tree on close, so
/// everything is built from scratch in <see cref="OnSetup"/> on every open.</summary>
internal sealed unsafe class NativeHubWindow : NativeAddon
{
    // Lumina's Quest sheet offsets row ids by this amount — see UnlockWindow's identical constant
    // for why FollowedOverride/GetAcceptedQuestObjective work in the raw (unoffset) ushort space.
    private const uint QuestRowIdOffset = 65536;

    private const float TabBarHeight = 26f;
    private const float TabBarGap = 6f;

    /// <summary>The Following strip, above the tab bar and on screen whatever tab is open.
    ///
    /// <para>Mirrors the vanilla quest tracker: a persistent line naming what you are on, sitting
    /// above everything else. The player asked "how do I toggle between MSQ and another quest or
    /// unlocks?" while the tab that does it was three tabs from the left and called Quests — the
    /// feature worked and the affordance was invisible. A tab is a <i>place</i>, not a state: it can
    /// say where to go to change something, but it cannot say what the current value is, and the
    /// question was both halves at once.</para></summary>
    private const float StripHeight = 26f;

    private const float StripGap = 4f;
    private const float RowHeight = 24f;
    private const float ChecklistControlsHeight = 92f;
    private const float HuntingHeaderHeight = 28f;
    private const float HuntingControlsHeight = 64f;
    private const float QuestControlsHeight = 96f;
    private const float ButtonHintHeight = 20f;

    // All four are screen pixels, not addon units — see ComputeDefaultSize for why the difference
    // matters. The width cap is absolute rather than a fraction of the viewport because a fraction
    // is exactly what stretched this window across an ultrawide: 60% of 5120px is 3072px of mostly
    // empty list. The viewport fraction is the ceiling either axis may never exceed.
    private const float MinWindowWidth = 460f;
    private const float MaxWindowWidth = 760f;
    private const float MinWindowHeight = 300f;
    private const float ViewportFraction = 0.9f;

    private static readonly string[] GroupModes = ["Zone", "Level", "Type"];

    private static readonly (string Key, string Label)[] CategoryChips =
        [("content", "Content"), ("system", "Systems"), ("cosmetic", "Cosmetics"), ("zone", "Zones")];

    private static readonly (string Key, string Label)[] PriorityChips =
        [("essential", "Essential"), ("nice", "Nice"), ("optional", "Optional")];

    private readonly IUnlockProvider unlocks;
    private readonly HuntingLogService hunting;
    private readonly ReadoutFeed feed;
    private readonly ModuleRegistry modules;
    private readonly IObjectTable objects;
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly Configuration config;
    private readonly SettingsCatalog settings;
    private readonly InputModeService inputMode;
    private readonly HubStatusIcons statusIcons;
    private readonly IPluginLog log;

    private readonly FilterState filter = new();
    private readonly List<NodeBase> checklistNodes = [];
    private readonly List<NodeBase> huntingNodes = [];
    private readonly List<NodeBase> ownedNodes = [];
    private readonly List<NodeBase> questNodes = [];
    private readonly List<NodeBase> settingsNodes = [];
    private readonly List<HubListRow> rows = [];

    /// <summary>Every slider currently on the Settings tab, so they can be re-read from their
    /// settings each tick — see <see cref="RefreshSettings"/>.</summary>
    private readonly List<SettingSliderNode> settingSliders = [];

    private readonly List<(HubListRow Row, Core.Hunting.HuntingMonster Monster)> distanceRows = [];

    /// <summary>Tabs already reported as having more controls than their reserved index block
    /// holds. The list rebuilds whenever its data changes; whether a tab's controls fit is a
    /// property of that tab's layout, not of the rebuild, so it is worth saying once and no
    /// more.</summary>
    private readonly HashSet<HubTab> crowdedTabsLogged = [];

    private int groupMode;

    /// <summary>Whether the unverified section is open. One flag replaces the per-entry expansion
    /// set the requirement rows used: the pane says what an entry needs now, so the only thing left
    /// that has to fold is the pile of entries the plugin cannot vouch for at all.</summary>
    private bool unverifiedExpanded;
    private HubTab pendingTab = HubTab.Quests;
    private HubTab currentTab = HubTab.Quests;
    private Vector2 tabContentStart;
    private Vector2 tabContentSize;
    private int lastChecklistSignature;
    private int lastHuntingSignature;
    private int lastQuestSignature;
    private int lastPopulatedRows;
    private bool navigationWarningLogged;

    private TabBarNode? hubTabs;
    private ListNode<HubListRow, HubListRowNode>? list;
    private HubDetailPaneNode? detailPane;

    /// <summary>The row the pane is currently describing, by reference. A held d-pad fires the
    /// hover callback once per step and every pane assignment builds SeStrings, so the guard is
    /// what keeps walking a long list from being a per-step allocation storm — the same reasoning
    /// as <see cref="lastTeleportLabel"/>.</summary>
    private HubListRow? hoveredRow;

    private VerticalListNode? checklistControls;
    private TextButtonNode? groupButton;
    private TextButtonNode? routeButton;

    private VerticalListNode? huntingControls;
    private TextNode? huntingHeaderNode;
    private TextButtonNode? huntHereButton;

    private VerticalListNode? questControls;
    private TextNode? questHeaderNode;
    private TextButtonNode? followMsqButton;
    private TextButtonNode? teleportButton;
    private TextButtonNode? dutyFinderButton;

    private TextNode? stripLabelNode;
    private AlignedHorizontalListNode? stripControls;
    private TextButtonNode? stripStopButton;
    private int lastStripSignature = int.MinValue;
    private ScrollingNode<VerticalListNode>? settingsArea;
    private CheckboxNode? firstSettingControl;
    private TextNode? buttonHintNode;
    private bool lastReverseConfirmCancel;
    private string lastTeleportLabel = string.Empty;

    public NativeHubWindow(
        IUnlockProvider unlocks,
        HuntingLogService hunting,
        ReadoutFeed feed,
        ModuleRegistry modules,
        IObjectTable objects,
        IClientState clientState,
        IFramework framework,
        Configuration config,
        SettingsCatalog settings,
        InputModeService inputMode,
        HubStatusIcons statusIcons,
        IPluginLog log)
    {
        this.statusIcons = statusIcons;
        this.unlocks = unlocks;
        this.hunting = hunting;
        this.feed = feed;
        this.modules = modules;
        this.objects = objects;
        this.clientState = clientState;
        this.framework = framework;
        this.config = config;
        this.settings = settings;
        this.inputMode = inputMode;
        this.log = log;

        settings.OnWindowPositionChanged += ApplyPositionPreset;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        // Belt-and-suspenders alongside the OnFinalize unsubscribe below: NativeAddon.Close() only
        // starts the native closing animation (finishes several frames later), but Dispose() must
        // leave nothing subscribed to IFramework the moment it returns, regardless of that timing.
        framework.Update -= OnFrameworkUpdate;
        settings.OnWindowPositionChanged -= ApplyPositionPreset;

        // Dalamud unloads plugins on a thread-pool thread (game exit); base.Dispose() calls
        // Close(), which asserts the main thread and throws otherwise. Marshal onto the framework
        // thread and block for it — IFramework's own docs call this the correct use of
        // RunOnFrameworkThread's returned Task.Wait(). Bounded by a timeout so a truly-unloading
        // framework (game process exiting, tick loop already stopped) can never hang plugin
        // teardown — Plugin.Dispose()'s finally still reaches KamiToolKitLibrary.Cleanup() either way.
        if (framework.IsInFrameworkUpdateThread)
        {
            base.Dispose();
            return;
        }

        try
        {
            framework.RunOnFrameworkThread(() => base.Dispose()).Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            const string message =
                "Wayfarer hub: disposing the window on the framework thread failed or timed out, so its nodes "
                + "are leaked until the game is restarted.";
            log.Warning(ex, message);
        }
    }

    /// <summary>Opens the window on <paramref name="tab"/>, or — if already open — just switches to
    /// it. Every entry point into Wayfarer comes through here.</summary>
    public void OpenTab(HubTab tab)
    {
        if (IsOpen)
        {
            SelectTab(tab);
            return;
        }

        pendingTab = tab;
        Size = ComputeDefaultSize();
        Open();
        ApplyPositionPreset(config.Hub.Position);
    }

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        if (!unlocks.Loaded && !hunting.Loaded)
        {
            AddOwnedNode(new TextNode
            {
                Position = ContentStartPosition,
                Size = new Vector2(ContentSize.X, 40f),
                String = "Wayfarer could not load its data.",
            });
            return;
        }

        var contentStart = ContentStartPosition;
        var contentSize = ContentSize;

        // Nav must be assigned before the LAST thing that triggers TabBarNode's private
        // RecalculateLayout() — which is a Size assignment OR an AddTab call. Assigning it in the
        // initializer and adding the tabs afterwards is the ordering that is correct either way;
        // do it the other way round and the indices never reach the radio buttons, silently.
        hubTabs = new TabBarNode
        {
            NavIndex = HubNavPlan.TabBar,
            NavUp = NavGraphPlanner.NoNavigation,
            NavDown = HubNavPlan.Region,
            Position = contentStart,
            Size = new Vector2(contentSize.X, TabBarHeight),
        };

        // Following first, and the tab the window opens on: the default loop is main-scenario
        // autopilot, so the tab that owns it should be the one you land on. The enum keeps its old
        // member names; this is what is on screen.
        hubTabs.AddTab(TabLabel(HubTab.Quests), () => SelectTab(HubTab.Quests));
        hubTabs.AddTab(TabLabel(HubTab.Checklist), () => SelectTab(HubTab.Checklist));
        hubTabs.AddTab(TabLabel(HubTab.Hunting), () => SelectTab(HubTab.Hunting));
        hubTabs.AddTab(TabLabel(HubTab.Settings), () => SelectTab(HubTab.Settings));
        AddOwnedNode(hubTabs);

        MeasureTabArea();

        BuildFollowingStrip();
        BuildButtonHint(contentStart, contentSize);
        BuildSharedList();
        BuildDetailPane();
        BuildChecklistControls();
        BuildHuntingControls();
        BuildQuestControls();
        BuildSettingsTab();

        SelectTab(pendingTab);

        framework.Update += OnFrameworkUpdate;
    }

    /// <summary>Runs on the framework thread, immediately before the game deallocates the addon —
    /// which is the last moment the tree this window built is still valid to take apart.
    ///
    /// <para>Disposing the roots is enough: a node disposes its own children first, then detaches
    /// itself from native, from the addon's UldManager object list, and from its parent, exactly
    /// reversing the attach. Every dispose is guarded against being run twice and against running
    /// off-thread or during a game shutdown, so the reverse order here is tidiness rather than a
    /// requirement.</para></summary>
    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        framework.Update -= OnFrameworkUpdate;

        // One line for the whole close, not one per node: whatever breaks a node's dispose breaks
        // every sibling's too, and this window owns hundreds of them.
        Exception? firstDisposeFailure = null;
        var disposeFailures = 0;
        for (var i = ownedNodes.Count - 1; i >= 0; i--)
        {
            try
            {
                ownedNodes[i].Dispose();
            }
            catch (Exception ex)
            {
                disposeFailures++;
                firstDisposeFailure ??= ex;
            }
        }

        if (firstDisposeFailure is not null)
        {
            var message =
                $"Wayfarer hub: {disposeFailures} of {ownedNodes.Count} nodes would not dispose while closing "
                + "the window, so those are leaked until the plugin is reloaded. The first failure is attached.";
            log.Warning(firstDisposeFailure, message);
        }

        ownedNodes.Clear();
        distanceRows.Clear();
        rows.Clear();
        settingSliders.Clear();
        checklistNodes.Clear();
        huntingNodes.Clear();
        questNodes.Clear();
        settingsNodes.Clear();
        hubTabs = null;
        list = null;
        detailPane = null;
        hoveredRow = null;
        checklistControls = null;
        groupButton = null;
        routeButton = null;
        huntingControls = null;
        huntingHeaderNode = null;
        huntHereButton = null;
        stripLabelNode = null;
        stripControls = null;
        questControls = null;
        questHeaderNode = null;
        followMsqButton = null;
        teleportButton = null;
        dutyFinderButton = null;
        stripStopButton = null;
        settingsArea = null;
        firstSettingControl = null;
        buttonHintNode = null;
    }

    // ----- Private static helpers (grouped together — SA1204) ------------------------------

    /// <summary>The window's size, in the units <see cref="NativeAddon.Size"/> actually wants.
    ///
    /// <b>Addon size is not screen pixels.</b> The game renders a normal addon at the player's
    /// interface scale (<c>InternalAddon-&gt;Scale == GetGlobalUIScale()</c> — KamiToolKit's own
    /// addon-config round-trip divides by exactly that), so a size computed in screen pixels comes
    /// out multiplied by it. The previous version <i>multiplied</i> by the scale as well, which on a
    /// 200% interface asked for four times the intended area — that is how this window came to be
    /// larger than an ultrawide screen. Everything here is therefore reasoned about in screen pixels
    /// and divided by the scale exactly once, at the end.</summary>
    private static Vector2 ComputeDefaultSize()
    {
        var screen = ViewportSize();

        // Tall by default on purpose: the Settings tab's controls are real components in a plain
        // column, and a controller can only reach the ones that are actually laid out on screen.
        // ResizeToContent shrinks this to whatever the open tab actually needs.
        return ToAddonUnits(new Vector2(
            ClampWidth(MaxWindowWidth, screen),
            ClampHeight(screen.Y * 0.7f, screen)));
    }

    private static unsafe Vector2 ViewportSize() =>
        new(AtkStage.Instance()->ScreenSize.Width, AtkStage.Instance()->ScreenSize.Height);

    // Floored so a pathological scale can never divide by zero and produce an infinite size.
    private static float UiScale() => Math.Max(AtkUnitBase.GetGlobalUIScale(), 0.1f);

    private static Vector2 ToAddonUnits(Vector2 screenPixels) => screenPixels / UiScale();

    private static float ClampWidth(float screenPixels, Vector2 screen) =>
        Math.Clamp(screenPixels, MinWindowWidth, Math.Max(screen.X * ViewportFraction, MinWindowWidth));

    private static float ClampHeight(float screenPixels, Vector2 screen) =>
        Math.Clamp(screenPixels, MinWindowHeight, Math.Max(screen.Y * ViewportFraction, MinWindowHeight));

    /// <summary>Caps the list's height at what <see cref="HubNavPlan.ListPoolLimit"/> allows.
    ///
    /// <para>KamiToolKit derives the recycled row pool straight from the list's height, and the pool
    /// is what decides how many nav indices the list block consumes. Capping the height is therefore
    /// the only way to pin the block's last index, which is what makes an index region <i>after</i>
    /// the list — the detail pane's buttons — placeable at all. Thirty 44px rows is 1,320px, taller
    /// than this window can ever be, so in practice this never bites; it exists so that the nav plan
    /// is a guarantee rather than a hope.</para></summary>
    private static float ClampListHeight(float wanted)
    {
        var perRow = HubListRowNode.ItemHeight + 1f;
        return Math.Min(wanted, HubNavPlan.MaxListPoolSize * perRow);
    }

    private static float ControlsHeight(HubTab tab) => tab switch
    {
        HubTab.Checklist => ChecklistControlsHeight,
        HubTab.Hunting => HuntingControlsHeight,
        HubTab.Quests => QuestControlsHeight,
        _ => 0f,
    };

    private static void SetBucketVisible(List<NodeBase> bucket, bool visible)
    {
        foreach (var node in bucket)
        {
            node.IsVisible = visible;
        }
    }

    private static string TabLabel(HubTab tab) => tab switch
    {
        // "Unlocks", not "Checklist": the player could not tell what a Checklist tab held —
        // "Is checklist the unlocks section?" — and the word for what is in it is unlocks. The
        // enum member keeps its name; this is what is on screen.
        HubTab.Checklist => "Unlocks",
        HubTab.Hunting => "Hunting Log",

        // "Following", not "Quests". The tab already chose what the arrow points at; nothing on it
        // said so, and the player asked how to toggle between the main scenario and something else
        // while looking straight at it. It is a sentence a player can finish: Wayfarer is following
        // the main scenario / this quest / an unlock route / your hunting log.
        HubTab.Quests => "Following",
        _ => "Settings",
    };

    private static void ToggleMembership(HashSet<string> set, string key, bool isOn)
    {
        if (isOn)
        {
            set.Add(key);
        }
        else
        {
            set.Remove(key);
        }
    }

    /// <summary>Colour as the second channel only. <c>Good</c> (the clean green) is deliberately
    /// retired from the checklist: it was the whole of "does green mean I can do it now?", and an
    /// available row is now a normal row carrying the game's own gold marker. The green survives in
    /// exactly one place — the "following" marker, where it means one thing.</summary>
    private static Vector4 StatusColor(UnlockStatus status) => UnlockStatusDisplay.Tone(status) switch
    {
        UnlockStatusTone.Bad => GameColors.Bad,
        UnlockStatusTone.Dimmed => GameColors.Dimmed,
        _ => GameColors.ListText,
    };

    /// <summary>The catalogue's nine <c>type</c> values in the game's own words. A closed set — the
    /// dataset validator enforces it — so there is no unknown branch to design for, only a default
    /// that would mean the catalogue had grown a tenth.</summary>
    private static string UnlockTypeWord(string type) => type switch
    {
        "dungeon" => "Dungeon",
        "trial" => "Trial",
        "raid" => "Raid",
        "alliance-raid" => "Alliance raid",
        "zone" => "Zone",
        "mount" => "Mount",
        "minion" => "Minion",
        "emote" => "Emote",
        "system" => "System",
        _ => "Unlock",
    };

    /// <summary>Everything standing in this entry's way, for the pane's "Requirements not met"
    /// block — the game's own label, over the game's own kind of content. Falls back through the
    /// computed lock reason and then the curated requirement label, because an entry that is
    /// plainly locked and lists nothing reads as a bug.</summary>
    private static List<string> MissingFor(ResolvedUnlock u)
    {
        if (u.Status is UnlockStatus.Available or UnlockStatus.Accepted or UnlockStatus.Done)
        {
            return [];
        }

        if (u.MissingRequirements.Count > 0)
        {
            return [.. u.MissingRequirements];
        }

        if (u.LockReason is { Length: > 0 } reason)
        {
            return [reason];
        }

        return u.Def.Requires?.Label is { Length: > 0 } label ? [label] : [];
    }

    /// <summary>Where to go and who to talk to. The giver's name lives here rather than on the
    /// row's title because it is <i>where you go</i>, not <i>what it is</i> — the game's own journal
    /// puts it in the body for the same reason.</summary>
    private static string FromLine(ResolvedUnlock u)
    {
        var giver = u.GiverName is { Length: > 0 } name ? name : null;
        var zone = u.ZoneName is { Length: > 0 } z ? z : null;

        if (giver is null && zone is null)
        {
            // The handful of system entries that simply happen to you. Saying nothing here would
            // read as missing data rather than as "there is nowhere to go".
            return u.Def.Requires?.Unverifiable == true ? "This unlocks on its own — there is nobody to visit." : string.Empty;
        }

        if (giver is null)
        {
            return $"In {zone}";
        }

        return zone is null ? $"From {giver}" : $"From {giver} · {zone}";
    }

    private static IEnumerable<ResolvedUnlock> OrderInGroup(IEnumerable<ResolvedUnlock> group) =>
        group.OrderBy(u => u.Status switch
        {
            UnlockStatus.Available => 0,
            UnlockStatus.Accepted => 1,
            UnlockStatus.QuestLocked => 2,
            UnlockStatus.LevelLocked => 3,
            UnlockStatus.InstanceLocked => 4,
            UnlockStatus.GrandCompanyLocked => 5,
            UnlockStatus.BeastTribeLocked => 6,
            UnlockStatus.MountLocked => 7,
            UnlockStatus.CollectionLocked => 8,
            UnlockStatus.RequirementsUnknown => 9,
            UnlockStatus.UnknownGate => 10,
            UnlockStatus.LockedOut => 11,
            _ => 12,
        }).ThenBy(u => u.QuestLevel);

    /// <summary>Line two of a hunting row: where the thing is. The zone alone for an overworld
    /// target, the zone and the duty for the Grand Company elites that live inside instanced
    /// content — the row previously said neither, so two targets on the same rank read as the same
    /// row with different numbers.</summary>
    private static string HuntingRowWhere(HuntingTargetView target)
    {
        var zone = target.ZoneName is { Length: > 0 } name ? name : string.Empty;
        if (target.DutyName is not { Length: > 0 } duty)
        {
            return zone;
        }

        return zone.Length == 0 ? duty : $"{zone} · {duty}";
    }

    private static void OpenDuty(uint? cfcId)
    {
        if (cfcId is { } id)
        {
            DutyFinderAction.Execute(id);
        }
    }

    // A cycling button rather than a drop-down: a DropDownNode's popup has to be registered into
    // the host addon's AdditionalFocusableNodes before a cursor can reach it, and a popup the
    // controller cannot enter is exactly the trap this whole pass exists to remove.
    private static TextButtonNode BuildChoice(SettingDefinition setting)
    {
        TextButtonNode? node = null;
        node = new TextButtonNode
        {
            Height = 24f,
            Width = 320f,
            String = $"{setting.Label}: {setting.CurrentValueText()}",
            OnClick = () =>
            {
                setting.CycleOption();
                if (node is not null)
                {
                    node.String = $"{setting.Label}: {setting.CurrentValueText()}";
                }
            },
        };
        return node;
    }

    private static AlignedHorizontalListNode BuildFilterRow(IEnumerable<CheckboxNode> chips)
    {
        var row = new AlignedHorizontalListNode
        {
            Height = 22f,
            FitToContentHeight = true,
            ItemSpacing = 10f,
        };

        foreach (var chip in chips)
        {
            row.AddNode(chip);
        }

        return row;
    }

    /// <summary>A section heading with a line of its own.
    ///
    /// The height and the alignment are both load-bearing and were both wrong. A text node's
    /// default alignment (<c>AlignmentType.Left</c>) centres the glyphs <b>vertically</b> inside the
    /// node, so a 20pt TrumpGothic line with an outline — which draws taller than 20 pixels — spilled
    /// out of a 22-pixel box at the top and the bottom, and the four pixels of column spacing below
    /// were not enough to keep it off the button row underneath. Anchoring at the top and reserving
    /// the height the font actually occupies is what gives the heading its own line.
    ///
    /// <para>Trump Gothic's glyph repertoire is narrow, which is how the readout's own heading came
    /// to show "Hunting Log tt warrior" where a middle dot had been written. Everything drawn in it
    /// goes through <see cref="HeadingText"/> first, here as well as there.</para></summary>
    private static TextNode BuildHeadingNode(string text) => new()
    {
        Height = HuntingHeaderHeight,
        FontType = FontType.TrumpGothic,
        FontSize = 20,
        AlignmentType = AlignmentType.TopLeft,
        TextColor = GameColors.Heading,
        TextOutlineColor = GameColors.HeadingEdge,
        TextFlags = TextFlags.Edge,
        String = HeadingText.Plain(text),
    };

    private void AddTabNode(List<NodeBase> bucket, NodeBase node)
    {
        AddOwnedNode(node);
        bucket.Add(node);
    }

    /// <summary>Attaches a node to the addon root and remembers that this window built it, so
    /// <see cref="OnFinalize"/> can take it apart again.
    ///
    /// <para>The addon is reallocated from scratch on every open, node tree and all, and the
    /// toolkit's unload-time safety net deliberately skips anything still parented. Nothing else
    /// will ever free these: dropping the field references, which is what this used to do, leaves
    /// the whole subtree behind holding pointers into memory the game has since reclaimed.</para></summary>
    private void AddOwnedNode(NodeBase node)
    {
        AddNode(node);
        ownedNodes.Add(node);
    }

    /// <summary>The game's own button-hint line, along the bottom edge. Drawn only on a controller,
    /// where it doubles as the mode indicator: the glyphs render as Ⓐ/Ⓑ or ✕/○ according to the
    /// player's own pad setting, so their presence and their shape both say "this window knows what
    /// you are holding". Every glyph is paired with a word so the line still reads if the icon
    /// does not render.</summary>
    private void BuildButtonHint(Vector2 contentStart, Vector2 contentSize)
    {
        lastReverseConfirmCancel = inputMode.ReverseConfirmCancel;
        buttonHintNode = new TextNode
        {
            Position = new Vector2(contentStart.X, contentStart.Y + contentSize.Y - ButtonHintHeight),
            Size = new Vector2(contentSize.X, ButtonHintHeight),
            FontType = FontType.Axis,
            FontSize = 12,
            AlignmentType = AlignmentType.Right,
            TextColor = GameColors.Dimmed,
            String = ControllerGlyphs.WindowHint(lastReverseConfirmCancel),
            IsVisible = inputMode.Mode == Core.Input.InputMode.Controller,
        };
        AddOwnedNode(buttonHintNode);
    }

    private void RefreshButtonHint()
    {
        if (buttonHintNode is null)
        {
            return;
        }

        buttonHintNode.IsVisible = inputMode.Mode == Core.Input.InputMode.Controller;
        if (inputMode.ReverseConfirmCancel != lastReverseConfirmCancel)
        {
            lastReverseConfirmCancel = inputMode.ReverseConfirmCancel;
            buttonHintNode.String = ControllerGlyphs.WindowHint(lastReverseConfirmCancel);
        }
    }

    // ----- Shared virtual list --------------------------------------------------------------
    private void BuildSharedList()
    {
        list = new ListNode<HubListRow, HubListRowNode>
        {
            NavIndex = HubNavPlan.List,
            NavUp = HubNavPlan.Region,
            NavDown = HubNavPlan.TabBar,

            // The left/right escape is the guaranteed way out of the list, and it is deliberate
            // rather than decorative: KamiToolKit's own downward exit dies permanently the first
            // time the list scrolls (OnDownNavReceived zeroes it and guards the restore with a
            // condition a just-incremented counter can never satisfy). Left or right always
            // returns to the tab bar, so no graph defect can strand the cursor inside the list.
            NavLeft = HubNavPlan.TabBar,
            NavRight = HubNavPlan.TabBar,

            ItemSpacing = 1f,

            // Suppresses ListNode's "selection follows scroll", which otherwise raises OnClick for
            // every row a held d-pad scrolls past — on this window that would fire navigation at
            // each one. Selection highlight is cleared explicitly after every activation instead.
            AllowMultipleSelection = true,
            AutoResetScroll = false,
            Position = tabContentStart,
            Size = new Vector2(tabContentSize.X, tabContentSize.Y),
            OptionsList = [],
            OnItemSelected = OnRowClicked,
        };
        AddOwnedNode(list);
    }

    /// <summary>The one line that says what Wayfarer is following, above the tab bar and on screen
    /// whatever tab is open, with the two controls that act on it.
    ///
    /// <para>It says it in the readout's own words — the heading line <c>ReadoutComposer</c> puts at
    /// the top of the HUD — so the window and the world can never disagree about what is being
    /// followed. "Following" is the single word for this concept on every surface now: the strip,
    /// the leftmost tab, and the Wayfarer entry in the game's own right-click menu.</para></summary>
    private void BuildFollowingStrip()
    {
        stripLabelNode = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = 13,
            AlignmentType = AlignmentType.Left,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.ListText,
            String = "Following: Main Scenario",
        };
        AddOwnedNode(stripLabelNode);

        stripControls = new AlignedHorizontalListNode
        {
            Height = 24f,
            FitToContentHeight = true,
            ItemSpacing = 6f,
        };

        stripControls.AddNode(new TextButtonNode
        {
            Width = 90f,
            Height = 22f,
            String = "Change",

            // Goes to the tab that owns the choice rather than opening a popup: a popup has to be
            // registered into the host addon's focusable nodes before a cursor can enter it, and a
            // popup a controller cannot reach is the trap this whole window exists to avoid.
            OnClick = () => SelectTab(HubTab.Quests),
        });

        // The one Stop, on the same line that says what is running. There used to be three.
        stripStopButton = new TextButtonNode
        {
            Width = 80f,
            Height = 22f,
            String = "Stop",
            IsEnabled = false,
            OnClick = OnStopClicked,
        };
        stripControls.AddNode(stripStopButton);
        AddOwnedNode(stripControls);
    }

    /// <summary>Keeps the strip saying what the readout says. Signature-gated because assigning the
    /// string builds a SeString and this is on screen on every tab, every tick.</summary>
    private void RefreshFollowingStrip()
    {
        if (stripLabelNode is null)
        {
            return;
        }

        var signature = ComputeQuestSignature();
        if (signature == lastStripSignature)
        {
            return;
        }

        lastStripSignature = signature;
        stripLabelNode.String = $"Following: {GuidanceHeading()}";
    }

    /// <summary>The readout's own heading line, verbatim. <c>GuidanceProjection</c> guarantees an
    /// engaged objective always has one, so this is the mode indicator the plugin already had —
    /// it is being shown in a second place, not invented in one.</summary>
    private string GuidanceHeading()
    {
        foreach (var line in feed.Compose(teleportOnClick: false).Lines)
        {
            if (line.Emphasis == ReadoutEmphasis.Heading)
            {
                return line.Text;
            }
        }

        return "nothing yet";
    }

    /// <summary>The pane across the bottom of the window that says what the cursor is on. Built
    /// once and shared by every list tab — one component, three call sites, so the Unlocks, Hunting
    /// Log and Following tabs cannot disagree about how a selected thing is described.</summary>
    private void BuildDetailPane()
    {
        detailPane = new HubDetailPaneNode
        {
            Position = tabContentStart,
            Size = new Vector2(tabContentSize.X, HubDetailPaneNode.PaneHeight),
        };
        AddOwnedNode(detailPane);
        detailPane.Show(null);
    }

    /// <summary>Publishes a row to the pane. One shared delegate is handed to every row of a
    /// rebuild rather than a closure each.</summary>
    private void PublishDetail(HubListRow row)
    {
        if (ReferenceEquals(row, hoveredRow))
        {
            return;
        }

        hoveredRow = row;
        detailPane?.Show(row.Pane);

        // The set of buttons on the pane changes with the row, and the indices are absolute — a
        // button that appeared without being numbered is a button a controller cannot reach.
        RenumberDetailPane();
    }

    /// <summary>Renumbers the pane after its buttons change, and re-points the list's downward exit
    /// at whatever the pane now offers.</summary>
    private void RenumberDetailPane()
    {
        if (list is null || !config.InputMode.CursorNavigation)
        {
            return;
        }

        var firstRow = PopulatedRowCount() > 0 ? NavListBlock.RowIndex(HubNavPlan.List, 0) : HubNavPlan.TabBar;
        list.NavDown = ApplyDetailPaneNavigation(firstRow);
    }

    /// <summary>Puts the pane back to its key. Called whenever the list is rebuilt, because the row
    /// the pane was describing may no longer exist — a stale pane over a list that has moved on is
    /// worse than no pane, since it looks current.</summary>
    private void ResetDetail()
    {
        hoveredRow = null;
        detailPane?.Show(null);
        RenumberDetailPane();
    }

    /// <summary>A disabled button with no explanation is the shape of the original "nothing in
    /// here works" report: the action buttons go inert when Quest Helper is off, and nothing said
    /// so. This says so, in the list, where the eye already is.</summary>
    private void AddGuidanceUnavailableNote(INavigationProvider? navigator)
    {
        if (navigator is null)
        {
            rows.Add(new HubListRow
            {
                Kind = HubRowKind.Note,
                Label = "Turn Quest Helper on in Settings to be guided anywhere from here.",
            });
        }
    }

    private void OnRowClicked(HubListRow? row)
    {
        list?.ClearSelection();
        row?.Activate?.Invoke();
    }

    // ----- Geometry ---------------------------------------------------------------------------

    /// <summary>Re-derives the tab body's rectangle from the window's current content area. Called
    /// on setup and again after every resize, because <see cref="NativeAddon.ContentSize"/> is a
    /// live read off the window node rather than a value anyone can cache.</summary>
    private void MeasureTabArea()
    {
        var contentStart = ContentStartPosition;
        var contentSize = ContentSize;
        var top = contentStart.Y + StripHeight + StripGap + TabBarHeight + TabBarGap;

        tabContentStart = new Vector2(contentStart.X, top);
        tabContentSize = new Vector2(
            contentSize.X,
            Math.Max(contentSize.Y - (top - contentStart.Y) - ButtonHintHeight, RowHeight));
    }

    /// <summary>Puts the current tab's control block and the shared list where the measured tab
    /// area says they go. Separate from <see cref="SelectTab"/> so a resize can re-run the same
    /// layout without re-selecting anything.</summary>
    private void PositionTabContent()
    {
        var tabBarTop = tabContentStart.Y - TabBarHeight - TabBarGap;
        if (hubTabs is not null)
        {
            hubTabs.Position = new Vector2(tabContentStart.X, tabBarTop);
            hubTabs.Size = new Vector2(tabContentSize.X, TabBarHeight);
        }

        PositionFollowingStrip(tabBarTop - StripGap - StripHeight);

        if (buttonHintNode is not null)
        {
            buttonHintNode.Position = new Vector2(tabContentStart.X, tabContentStart.Y + tabContentSize.Y);
            buttonHintNode.Size = new Vector2(tabContentSize.X, ButtonHintHeight);
        }

        var controlsHeight = ControlsHeight(currentTab);
        if (checklistControls is not null)
        {
            checklistControls.Position = tabContentStart;
            checklistControls.Size = new Vector2(tabContentSize.X, ChecklistControlsHeight);
        }

        if (huntingControls is not null)
        {
            huntingControls.Position = tabContentStart;
            huntingControls.Size = new Vector2(tabContentSize.X, HuntingControlsHeight);
        }

        if (questControls is not null)
        {
            questControls.Position = tabContentStart;
            questControls.Size = new Vector2(tabContentSize.X, QuestControlsHeight);
        }

        if (settingsArea is not null)
        {
            settingsArea.Position = tabContentStart;
            settingsArea.Size = tabContentSize;

            // Assigning Size forces the content node back to the container's full width, taking
            // every control with it, so the scroll-bar reservation has to be re-applied here.
            ApplySettingWidths();
        }

        if (list is null)
        {
            return;
        }

        PositionListAndPane(controlsHeight);
    }

    /// <summary>Lays the Following strip out along the top: the words on the left, at the same left
    /// edge as everything below them, and the two controls right-aligned where a game panel puts
    /// its own.</summary>
    private void PositionFollowingStrip(float top)
    {
        if (stripControls is null)
        {
            return;
        }

        const float ControlsWidth = 90f + 6f + 80f;

        stripControls.Position = new Vector2(tabContentStart.X + tabContentSize.X - ControlsWidth, top + 2f);
        stripControls.Size = new Vector2(ControlsWidth, StripHeight - 2f);
        stripControls.RecalculateLayout();

        if (stripLabelNode is not null)
        {
            stripLabelNode.Position = new Vector2(tabContentStart.X, top + 4f);
            stripLabelNode.Size = new Vector2(
                Math.Max(tabContentSize.X - ControlsWidth - 8f, 0f), StripHeight - 4f);
        }
    }

    /// <summary>Puts the list and the pane below it. The pane is pinned to the bottom of the tab
    /// body and the list gets what is left, so the pane never moves as rows come and go — a detail
    /// view that slides up the window every time the list shortens is harder to use than no detail
    /// view at all.</summary>
    private void PositionListAndPane(float controlsHeight)
    {
        if (list is null)
        {
            return;
        }

        if (detailPane is not null)
        {
            detailPane.IsVisible = list.IsVisible;
            detailPane.Position = new Vector2(
                tabContentStart.X,
                tabContentStart.Y + tabContentSize.Y - HubDetailPaneNode.PaneHeight);
            detailPane.Size = new Vector2(tabContentSize.X, HubDetailPaneNode.PaneHeight);
        }

        if (!list.IsVisible)
        {
            return;
        }

        // The list keeps a fixed viewport and scrolls what does not fit — it is the one thing in
        // here that must never grow the window, because "the window grew instead of scrolling"
        // is precisely what put it off the edge of the screen.
        var available = tabContentSize.Y - controlsHeight - HubDetailPaneNode.PaneHeight;
        list.Position = new Vector2(tabContentStart.X, tabContentStart.Y + controlsHeight);
        list.Size = new Vector2(tabContentSize.X, ClampListHeight(Math.Max(available, RowHeight)));
    }

    /// <summary>Shrinks the window to what the open tab actually holds, up to the viewport cap —
    /// the fix for "one hunting row and a screenful of nothing below it". Only ever changes the
    /// height: the width is the reading measure and stays put so the window does not jump about as
    /// rows come and go.</summary>
    private void ResizeToContent()
    {
        if (!IsOpen || hubTabs is null)
        {
            return;
        }

        // Title bar plus content padding, measured rather than assumed — it is whatever the window
        // node currently reserves, in the same addon units Size is expressed in.
        var chrome = Math.Max(Size.Y - ContentSize.Y, 0f);
        var desiredUnits = chrome + StripHeight + StripGap + TabBarHeight + TabBarGap + ButtonHintHeight + TabBodyHeight();

        var screen = ViewportSize();
        var scale = UiScale();
        var height = ClampHeight(desiredUnits * scale, screen) / scale;

        // A pixel of hysteresis: the measured chrome and the content heights are both rounded on
        // their way through ushort node sizes, and resizing on a sub-pixel difference every frame
        // would rebuild the list's node pool every frame with it.
        if (Math.Abs(height - Size.Y) < 1f)
        {
            return;
        }

        SetWindowSize(new Vector2(Size.X, height));
        MeasureTabArea();
        PositionTabContent();
        ClampIntoViewport();
    }

    private float TabBodyHeight() => currentTab switch
    {
        HubTab.Settings => settingsArea?.ContentNode.Height ?? tabContentSize.Y,
        _ => ControlsHeight(currentTab) + ListHeightForRows() + HubDetailPaneNode.PaneHeight,
    };

    private float ListHeightForRows()
    {
        var spacing = list?.ItemSpacing ?? 1f;

        // Capped rather than unbounded: a checklist can hold hundreds of rows and the window is not
        // allowed to ask for a screen it does not have. Past the cap the list scrolls, which is the
        // behaviour that was missing.
        //
        // 14 rather than the previous 24 because the rows are now 44px instead of 26px: 14 × 45 is
        // the same 630px of list the old cap asked for, so the window this produces is the same
        // height it already was. Changing the row height without changing this would have grown the
        // default window by 40%, which is one of the sizing fixes already on main being undone.
        var count = Math.Clamp(rows.Count, 1, 14);
        return (count * (HubListRowNode.ItemHeight + spacing)) + spacing;
    }

    /// <summary>Keeps the whole window inside the viewport. Written only when it is actually out of
    /// bounds, so dragging the window around inside the screen is never fought — the game's own
    /// addons behave the same way.</summary>
    private unsafe void ClampIntoViewport()
    {
        if (InternalAddon is null)
        {
            return;
        }

        var screen = ViewportSize();
        var onScreen = Size * UiScale();
        var current = new Vector2(InternalAddon->X, InternalAddon->Y);
        var clamped = new Vector2(
            Math.Clamp(current.X, 0f, Math.Max(screen.X - onScreen.X, 0f)),
            Math.Clamp(current.Y, 0f, Math.Max(screen.Y - onScreen.Y, 0f)));

        if (clamped != current)
        {
            SetWindowPosition(clamped);
        }
    }

    // ----- Tab switching / background polling -----------------------------------------------

    /// <summary>Switches the visible tab, force-refreshing its content (the background poll only
    /// refreshes the active tab, so the others can be arbitrarily stale), re-laying out the shared
    /// list under whichever control block that tab uses, and renumbering the whole navigation
    /// graph afterwards.</summary>
    private void SelectTab(HubTab tab)
    {
        currentTab = tab;
        hubTabs?.SelectTab(TabLabel(tab));

        SetBucketVisible(checklistNodes, tab == HubTab.Checklist);
        SetBucketVisible(huntingNodes, tab == HubTab.Hunting);
        SetBucketVisible(questNodes, tab == HubTab.Quests);
        SetBucketVisible(settingsNodes, tab == HubTab.Settings);

        PositionTabContent();

        switch (tab)
        {
            case HubTab.Checklist:
                RebuildChecklist();
                break;
            case HubTab.Hunting:
                RebuildHunting();
                break;
            case HubTab.Quests:
                RebuildQuests();
                break;
            default:
                RebuildSettings();
                break;
        }

        FocusTabAnchor(tab);
    }

    /// <summary>Seeds the cursor somewhere valid when a tab opens, and puts it back somewhere valid
    /// after a rebuild that could have pulled the row out from under it. Only ever targets a real
    /// component that is on screen right now.
    ///
    /// <para>Controller only. This calls the game's own SetFocus, and it runs on every tab switch,
    /// every open and every automatic list shrink — including shrinks triggered in the background
    /// by a hunting target dying while the player is doing something else. On a mouse that is
    /// game focus being taken away from whatever the player was actually using, by a window that
    /// was only sitting there. A controller has to have its cursor somewhere; a mouse does
    /// not.</para></summary>
    private void FocusTabAnchor(HubTab tab)
    {
        if (!config.InputMode.CursorNavigation || inputMode.Mode != Core.Input.InputMode.Controller)
        {
            return;
        }

        switch (tab)
        {
            case HubTab.Checklist:
                routeButton?.SetFocus();
                break;
            case HubTab.Hunting:
                huntHereButton?.SetFocus();
                break;
            case HubTab.Quests:
                followMsqButton?.SetFocus();
                break;
            default:
                firstSettingControl?.SetFocus();
                break;
        }
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        switch (currentTab)
        {
            case HubTab.Checklist:
                if (ComputeChecklistSignature() != lastChecklistSignature)
                {
                    RebuildChecklist();
                }

                break;
            case HubTab.Hunting:
                if (ComputeHuntingSignature() != lastHuntingSignature)
                {
                    RebuildHunting();
                }
                else
                {
                    RefreshHuntingDistances();
                }

                break;
            case HubTab.Quests:
                if (ComputeQuestSignature() != lastQuestSignature)
                {
                    RebuildQuests();
                }
                else
                {
                    RefreshQuestActions();
                }

                break;
            case HubTab.Settings:
                // Not a rebuild: only the values are re-read. The readout's position can change
                // while this tab is open — a mouse drag, a preset, a resolution change — and the
                // sliders have to say where it actually is, not where they last left it.
                RefreshSettings();
                break;
        }

        RestoreListDownwardExit();
        UpdateStopButton();
        RefreshFollowingStrip();
        RefreshButtonHint();

        // Cheap, and only ever writes when the window is genuinely outside the viewport — which is
        // what makes it safe to run every tick. It catches a resolution or interface-scale change
        // under an open window, the case a one-shot clamp on open cannot.
        ClampIntoViewport();
    }

    /// <summary>Undoes KamiToolKit's <c>ListNode</c> defect 1 from the outside: its downward scroll
    /// sentinel zeroes its own exit link on the first scroll and restores it only when
    /// <c>scrollPosition</c> is 0, which cannot happen straight after an increment — so once the
    /// player scrolls, down stops leaving the list forever. Re-pointing the link once the list is
    /// scrolled as far as it goes gives the exit back without taking away scroll-past-the-bottom.
    /// Cheap enough to check every frame and idempotent.</summary>
    private void RestoreListDownwardExit()
    {
        if (list?.DownwardsNavNode is not { } sentinel || sentinel.NavDown != NavGraphPlanner.NoNavigation)
        {
            return;
        }

        if (list.ScrollBarNode.ScrollPosition >= list.ScrollBarNode.ScrollMaxPosition)
        {
            sentinel.NavDown = list.NavDown;
        }
    }

    // Re-evaluated fresh on every call — mirrors the ImGui windows' navigator field being
    // recomputed every frame, so a Quest Helper toggle flip between opens is picked up on the
    // very next click/rebuild rather than only on the next background poll.
    private QuestNavigator? ResolveNavigator() =>
        modules.Get<QuestHelperModule>() is { Enabled: true } questHelper ? questHelper.Navigator : null;

    // ----- Navigation -----------------------------------------------------------------------

    /// <summary>Renumbers the entire graph. Never patches: the indices are absolute and dense, so a
    /// partial update leaves collisions (the cursor teleports) or holes (it stops moving). Called
    /// after every content, filter, visibility or tab change.</summary>
    private void ApplyNavigation(NodeBase? controls)
    {
        if (list is null)
        {
            return;
        }

        if (!config.InputMode.CursorNavigation)
        {
            RemoveFromCursorGraph();
            return;
        }

        ApplyStripNavigation();

        var populated = PopulatedRowCount();
        var firstRow = populated > 0 ? NavListBlock.RowIndex(HubNavPlan.List, 0) : HubNavPlan.TabBar;

        var regionEnd = HubNavPlan.Region;
        if (controls is not null)
        {
            regionEnd = NavigationWalker.Apply(
                controls,
                HubNavPlan.Region,
                HubNavPlan.TabBar,
                firstRow,
                HubNavPlan.Region + HubNavPlan.RegionCapacity - 1);
        }

        var lastRegionIndex = regionEnd > HubNavPlan.Region ? regionEnd - 1 : HubNavPlan.TabBar;
        var paneEntry = ApplyDetailPaneNavigation(firstRow);
        if (list.IsVisible)
        {
            list.NavUp = lastRegionIndex;

            // Down out of the list lands on the pane's buttons when there are any, and on the tab
            // bar when there are not. Left and right stay pinned to the tab bar whatever happens:
            // that is the escape hatch no graph defect can take away, and the pane must not become
            // a second way to get stuck.
            list.NavDown = paneEntry;
            RepairLastPopulatedRow(populated, lastRegionIndex);
        }

        LogGraph(controls, regionEnd, populated);
    }

    /// <summary>Numbers the Following strip, and points the tab bar's "up" at it. The strip is above
    /// the tab bar on screen and above it in the graph, so a d-pad walks off the top of the tabs
    /// onto Change and Stop rather than into nothing — which is what the tab bar's <c>NavUp</c> was
    /// before there was anything up there.</summary>
    private void ApplyStripNavigation()
    {
        if (stripControls is null)
        {
            return;
        }

        var end = NavigationWalker.Apply(
            stripControls,
            HubNavPlan.Strip,
            NavGraphPlanner.NoNavigation,
            HubNavPlan.TabBar,
            HubNavPlan.Strip + HubNavPlan.StripCapacity - 1);

        if (hubTabs is not null)
        {
            hubTabs.NavUp = end > HubNavPlan.Strip ? HubNavPlan.Strip : NavGraphPlanner.NoNavigation;
        }
    }

    /// <summary>Numbers the detail pane's action buttons into their own reserved block above the
    /// list's, and reports where "down out of the list" should now land — the first button when
    /// there is one, the tab bar when the pane has nothing to offer this row.</summary>
    private int ApplyDetailPaneNavigation(int firstRow)
    {
        if (detailPane is not { IsVisible: true })
        {
            return HubNavPlan.TabBar;
        }

        var end = NavigationWalker.Apply(
            detailPane.ActionRow,
            HubNavPlan.DetailPane,
            firstRow,
            HubNavPlan.TabBar,
            HubNavPlan.DetailPane + HubNavPlan.DetailPaneCapacity - 1);

        return end > HubNavPlan.DetailPane ? HubNavPlan.DetailPane : HubNavPlan.TabBar;
    }

    /// <summary>Takes the window out of the cursor graph entirely when the player has turned
    /// controller navigation off.
    ///
    /// <para>"Off" has to mean unwired, not half-wired. The tab bar and the list are given their
    /// indices when they are built, before this setting is consulted, and only the control region
    /// is numbered from here — so simply declining to number the region left a window that was
    /// still navigable but with a hole in the middle of it: down from the tabs pointed at index 10,
    /// nothing occupied index 10, and the list was unreachable from the tab bar. Someone turning
    /// this off to fix a problem got a worse window, not a plainer one. Now they get the mouse
    /// window, which is what the setting says.</para></summary>
    private void RemoveFromCursorGraph()
    {
        if (hubTabs is not null)
        {
            hubTabs.NavIndex = NavGraphPlanner.NoNavigation;
            hubTabs.NavUp = NavGraphPlanner.NoNavigation;
            hubTabs.NavDown = NavGraphPlanner.NoNavigation;
        }

        if (list is null)
        {
            return;
        }

        list.NavIndex = NavGraphPlanner.NoNavigation;
        list.NavUp = NavGraphPlanner.NoNavigation;
        list.NavDown = NavGraphPlanner.NoNavigation;
        list.NavLeft = NavGraphPlanner.NoNavigation;
        list.NavRight = NavGraphPlanner.NoNavigation;
    }

    /// <summary>Undoes KamiToolKit's <c>ListNode</c> defect 2 from the outside: its per-row loop
    /// iterates the recycled node pool but tests for "last row" against a different count, so when
    /// the list holds fewer items than the pool the real last row's downward link points at a row
    /// node that is currently invisible and pressing down there does nothing. Re-pointing that one
    /// row at the list's own exit is the whole fix.</summary>
    private void RepairLastPopulatedRow(int populated, int navUp)
    {
        if (list is null || populated <= 0 || populated >= list.OptionNodes.Count)
        {
            return;
        }

        var index = NavListBlock.RowIndex(HubNavPlan.List, populated - 1);
        var up = populated > 1 ? NavListBlock.RowIndex(HubNavPlan.List, populated - 2) : navUp;
        list.OptionNodes[populated - 1].ProcessNav(index, up, list.NavDown, list.NavLeft, list.NavRight);
    }

    private int PopulatedRowCount() =>
        list is null ? 0 : Math.Min(list.OptionsList.Count, list.OptionNodes.Count);

    // The index map is developer detail and the list rebuilds every time its data changes, so the
    // dump itself is behind the diagnostics setting; the two warnings below are not, because they
    // both mean a tab is unreachable with a controller.
    private void LogGraph(NodeBase? controls, int regionEnd, int populated)
    {
        var controlCount = controls is null ? 0 : NavigationWalker.CountTargets(controls);
        if (config.QuestHelper.LogDiagnostics)
        {
            log.Verbose(
                $"Wayfarer nav [{currentTab}]: tabs {HubNavPlan.TabBar}..{HubNavPlan.TabBarLast}, " +
                $"controls {HubNavPlan.Region}..{Math.Max(regionEnd - 1, HubNavPlan.Region)} ({controlCount}), " +
                $"list {HubNavPlan.List} rows {populated}/{list?.OptionNodes.Count ?? 0} of {rows.Count}.");
        }

        // The walker hands back its start index unchanged when it refuses a region that would not
        // fit. That is the safe outcome, but it means every control on this tab is unreachable with
        // a controller, so it must not be a silent one.
        if (controlCount > 0 && regionEnd == HubNavPlan.Region && crowdedTabsLogged.Add(currentTab))
        {
            log.Warning(
                $"Wayfarer nav [{currentTab}]: {controlCount} controls do not fit the " +
                $"{HubNavPlan.RegionCapacity} indices reserved from {HubNavPlan.Region}, so this tab's " +
                "controls cannot be reached with a controller — use a mouse for them, or reach them from " +
                "the game's own menus. Raise HubNavPlan.RegionCapacity (and the list block with it) or " +
                "split the tab.");
        }

        var pool = list?.OptionNodes.Count ?? 0;
        if (navigationWarningLogged || NavListBlock.Fits(HubNavPlan.List, pool))
        {
            return;
        }

        navigationWarningLogged = true;
        log.Warning(
            $"Wayfarer nav: a {pool}-row list at index {HubNavPlan.List} exceeds the {NavGraphPlanner.MaxIndex} " +
            "index ceiling; the lower rows will not be reachable with a controller.");
    }

    /// <summary>Pushes the current row models into the list and renumbers everything. The one place
    /// that touches <c>OptionsList</c>, so the sequence — content, then geometry, then graph, then
    /// focus — is stated once.</summary>
    private void PublishRows(NodeBase? controls)
    {
        if (list is null)
        {
            return;
        }

        // Geometry first: resizing rebuilds the list's recycled node pool, so publishing into it
        // beforehand would populate nodes that are about to be thrown away.
        ResizeToContent();

        var previous = lastPopulatedRows;
        list.OptionsList = [.. rows];
        lastPopulatedRows = PopulatedRowCount();

        // The row the pane was describing belongs to the list that has just been replaced. Even
        // when the same entry is still there it is a different object, so keeping the pane would be
        // keeping something that only looks current — the key is the honest state until the cursor
        // lands somewhere again.
        ResetDetail();

        ApplyNavigation(controls);

        if (lastPopulatedRows < previous)
        {
            // The list shrank. The cursor is a node pointer, not an index, and ListNode recycles
            // its row nodes rather than freeing them, so nothing dangles — but the row under the
            // cursor may now be showing something else or be hidden entirely. Putting focus back
            // on this tab's own action button is cheap, idempotent, and the only guard against the
            // cursor sitting on a row that no longer exists.
            FocusTabAnchor(currentTab);
        }

        if (InternalAddon is not null)
        {
            InternalAddon->UpdateCollisionNodeList(false);
        }
    }

    // ----- Checklist tab ---------------------------------------------------------------------
    private void BuildChecklistControls()
    {
        checklistControls = new VerticalListNode
        {
            FitWidth = true,
            ItemSpacing = 4f,
            Position = tabContentStart,
            Size = new Vector2(tabContentSize.X, ChecklistControlsHeight),
        };

        checklistControls.AddNode(BuildFilterRow(BuildDoneAndCategoryChips()));
        checklistControls.AddNode(BuildFilterRow(BuildPriorityChips()));
        checklistControls.AddNode(BuildChecklistActionRow());

        AddTabNode(checklistNodes, checklistControls);
    }

    private AlignedHorizontalListNode BuildChecklistActionRow()
    {
        var row = new AlignedHorizontalListNode { Height = 26f, FitToContentHeight = true, ItemSpacing = 8f };

        // A cycling button rather than a nested tab bar: a TabBarNode consumes one index per tab,
        // which the walker (which numbers one index per element) cannot account for — nesting one
        // inside a numbered region would overlap the elements that follow it.
        groupButton = new TextButtonNode
        {
            Width = 140f,
            Height = 24f,
            String = $"Group: {GroupModes[groupMode]}",
            OnClick = () =>
            {
                groupMode = (groupMode + 1) % GroupModes.Length;
                RebuildChecklist();
            },
        };
        row.AddNode(groupButton);

        routeButton = new TextButtonNode
        {
            Width = 150f,
            Height = 24f,
            String = "Route Me",
            OnClick = OnRouteClicked,
        };
        row.AddNode(routeButton);

        // No Stop button here. There were three of them — one per tab — doing one thing and kept in
        // lockstep by hand, which is three places to look for the way out of something and three
        // places for it to be wrong. It lives on the Following strip now, which is the same line
        // that says what is running.
        return row;
    }

    private IEnumerable<CheckboxNode> BuildDoneAndCategoryChips()
    {
        yield return new CheckboxNode
        {
            Height = 20f,
            String = "Complete",
            IsChecked = filter.ShowDone,
            OnClick = isOn =>
            {
                filter.ShowDone = isOn;
                RebuildChecklist();
            },
        };

        foreach (var (key, label) in CategoryChips)
        {
            var chipKey = key;
            yield return new CheckboxNode
            {
                Height = 20f,
                String = label,
                IsChecked = filter.Categories.Contains(chipKey),
                OnClick = isOn =>
                {
                    ToggleMembership(filter.Categories, chipKey, isOn);
                    RebuildChecklist();
                },
            };
        }
    }

    private IEnumerable<CheckboxNode> BuildPriorityChips()
    {
        foreach (var (key, label) in PriorityChips)
        {
            var chipKey = key;
            yield return new CheckboxNode
            {
                Height = 20f,
                String = label,
                IsChecked = filter.Priorities.Contains(chipKey),
                OnClick = isOn =>
                {
                    ToggleMembership(filter.Priorities, chipKey, isOn);
                    RebuildChecklist();
                },
            };
        }
    }

    private List<ResolvedUnlock> ComputeVisibleUnlocks() =>
        [.. unlocks.Entries.Where(u => u.Status != UnlockStatus.Unverified && UnlockFilters.Matches(u, filter))];

    private int ComputeChecklistSignature()
    {
        unchecked
        {
            var hash = 17;
            foreach (var u in unlocks.Entries)
            {
                hash = (hash * 31) + (int)u.Status;
            }

            return hash;
        }
    }

    private void RebuildChecklist()
    {
        if (list is null)
        {
            return;
        }

        var navigator = ResolveNavigator();
        var visible = ComputeVisibleUnlocks();
        UpdateRouteRow(visible, navigator);

        rows.Clear();
        distanceRows.Clear();

        // Before anything else, because an empty checklist reads as "you have done everything" and
        // a catalogue that would not parse must not be allowed to look like that.
        if (!unlocks.Loaded)
        {
            rows.Add(new HubListRow
            {
                Kind = HubRowKind.Note,
                Label = "Wayfarer could not read its unlock catalogue — this list is empty for that reason, not because there is nothing left to do.",
            });
            if (unlocks.LoadError is { Length: > 0 } why)
            {
                rows.Add(new HubListRow { Kind = HubRowKind.Note, Label = why });
            }

            PublishRows(checklistControls);
            lastChecklistSignature = ComputeChecklistSignature();
            return;
        }

        AddGuidanceUnavailableNote(navigator);
        foreach (var group in GroupUnlockEntries(visible))
        {
            rows.Add(new HubListRow { Kind = HubRowKind.Heading, Label = group.Key, Detail = $"{group.Count()}" });
            foreach (var u in OrderInGroup(group))
            {
                rows.Add(BuildChecklistRow(u, navigator));
            }
        }

        AddUnverifiedRows();

        if (rows.Count == 0)
        {
            rows.Add(new HubListRow { Kind = HubRowKind.Note, Label = "Nothing to show with these filters." });
        }

        PublishRows(checklistControls);
        lastChecklistSignature = ComputeChecklistSignature();
    }

    private void UpdateRouteRow(List<ResolvedUnlock> visible, INavigationProvider? navigator)
    {
        if (routeButton is null || groupButton is null)
        {
            return;
        }

        groupButton.String = $"Group: {GroupModes[groupMode]}";
        var routable = visible.Count(u => u.Status == UnlockStatus.Available && u.GiverTerritory != null);
        routeButton.String = routable > 0 ? $"Route me ({routable})" : "Route me";
        routeButton.IsEnabled = navigator != null && routable > 0;
    }

    private void OnRouteClicked()
    {
        var navigator = ResolveNavigator();
        if (navigator is null)
        {
            return;
        }

        var routable = ComputeVisibleUnlocks().Where(u => u.Status == UnlockStatus.Available && u.GiverTerritory != null).ToList();
        if (routable.Count == 0)
        {
            return;
        }

        var player = objects.LocalPlayer;
        var ordered = RoutePlanner.Order(routable, clientState.TerritoryType, player?.Position.X ?? 0, player?.Position.Z ?? 0);
        var targets = ordered.Select(unlocks.ToPickupTarget).Where(t => t != null).Select(t => t!).ToList();
        if (targets.Count > 0)
        {
            navigator.SetRoute(targets);
        }
    }

    private HubListRow BuildChecklistRow(ResolvedUnlock u, INavigationProvider? navigator)
    {
        return new HubListRow
        {
            Kind = HubRowKind.Entry,
            Label = UnlockRowText.Name(u),
            Description = UnlockRowText.Description(u),
            Detail = UnlockRowText.Trailing(u),
            IconId = statusIcons.For(u.Status),
            StatusWord = UnlockStatusDisplay.Word(u.Status),
            LabelColor = StatusColor(u.Status),
            Pane = BuildUnlockDetail(u, navigator),
            Hover = PublishDetail,
            Activate = navigator is null ? null : () => OnChecklistRowActivated(u),
        };
    }

    /// <summary>What the pane says about one unlock, assembled from data the plugin already had.
    ///
    /// <para>This is what retires the expand-in-place requirement rows. They existed because a
    /// controller has no hover and an entry waiting on a list of collectibles had to be able to show
    /// that list somehow — but expanding reflows the list under the cursor, which is the condition
    /// that trips the vendored list's recycling defect, and it makes the list's length depend on
    /// what you have opened. The pane shows the same lines without moving anything.</para></summary>
    private HubRowDetail BuildUnlockDetail(ResolvedUnlock u, INavigationProvider? navigator)
    {
        var level = UnlockRowText.LevelToken(u);
        var kind = UnlockTypeWord(u.Def.Type);

        return new HubRowDetail
        {
            Title = u.Def.Unlock,
            Kind = level.Length > 0 ? $"{level} · {kind}" : kind,
            StatusIconId = statusIcons.For(u.Status),
            StatusSentence = UnlockStatusDisplay.Sentence(u),
            Body = UnlockRowText.Description(u),
            Requirements = MissingFor(u),
            From = FromLine(u),
            Provenance = string.Equals(u.Def.Confidence, "unverified", StringComparison.Ordinal)
                ? "Wayfarer is not certain about this entry."
                : string.Empty,
            Actions = UnlockActions(u, navigator),
        };
    }

    private List<HubDetailAction> UnlockActions(ResolvedUnlock u, INavigationProvider? navigator)
    {
        var actions = new List<HubDetailAction>();
        if (navigator is null)
        {
            return actions;
        }

        switch (u.Status)
        {
            case UnlockStatus.Available when unlocks.ToPickupTarget(u) is not null:
                actions.Add(new HubDetailAction("Guide me there", () => OnChecklistRowActivated(u)));
                break;

            case UnlockStatus.Accepted when u.QuestRowId is not null:
                actions.Add(new HubDetailAction("Follow this quest", () => OnChecklistRowActivated(u)));
                break;
        }

        return actions;
    }

    private void OnChecklistRowActivated(ResolvedUnlock u)
    {
        var navigator = ResolveNavigator();
        if (navigator is null)
        {
            return;
        }

        if (u.Status == UnlockStatus.Available && unlocks.ToPickupTarget(u) is { } target)
        {
            navigator.SetPickup(target);
        }
        else if (u.Status == UnlockStatus.Accepted && u.QuestRowId is { } questRowId)
        {
            navigator.ClearPickup();
            navigator.FollowedOverride = (ushort)(questRowId - QuestRowIdOffset);
        }
    }

    private void AddUnverifiedRows()
    {
        var unverified = unlocks.Entries.Where(u => u.Status == UnlockStatus.Unverified).ToList();
        if (unverified.Count == 0)
        {
            return;
        }

        // One row, closed by default, rather than fifty-odd inert lines of unexplained text at the
        // bottom of every list. The pile was a meaningful part of "it's impossible to tell what any
        // of that really is": nothing on screen said what "unverified" meant or why those entries
        // were different, and there were more of them than most zones have real entries.
        rows.Add(new HubListRow
        {
            Kind = HubRowKind.Heading,
            Label = unverifiedExpanded ? "Unverified (showing)" : "Unverified",
            Detail = $"{unverified.Count}",
            Pane = UnverifiedSectionDetail(unverified.Count),
            Hover = PublishDetail,
            Activate = ToggleUnverified,
        });

        if (!unverifiedExpanded)
        {
            return;
        }

        foreach (var u in unverified)
        {
            // Entry rather than Note, even though there is nothing to activate: an entry gets the
            // two-line treatment and a note does not, and these are exactly the rows the player
            // could make least sense of. Dimmed, so they still read as "not like the others".
            rows.Add(new HubListRow
            {
                Kind = HubRowKind.Entry,
                Label = UnlockRowText.Name(u),
                Description = UnlockRowText.Description(u),
                Detail = UnlockRowText.Trailing(u),
                IconId = statusIcons.For(UnlockStatus.Unverified),
                StatusWord = UnlockStatusDisplay.Word(UnlockStatus.Unverified),
                LabelColor = GameColors.Dimmed,
                Pane = BuildUnlockDetail(u, navigator: null),
                Hover = PublishDetail,
            });
        }
    }

    private void ToggleUnverified()
    {
        unverifiedExpanded = !unverifiedExpanded;
        RebuildChecklist();
    }

    private HubRowDetail UnverifiedSectionDetail(int count) => new()
    {
        Title = "Unverified entries",
        Kind = $"{count} entries",
        StatusIconId = statusIcons.For(UnlockStatus.Unverified),
        StatusSentence = unverifiedExpanded ? "Showing — select to hide again." : "Hidden — select to show them.",
        Body =
            "Nothing in the game's own data backs these entries up. Wayfarer found them in its catalogue "
            + "but could not confirm the quest that grants them, so it cannot tell whether you have done "
            + "them or what they need — and it will never claim one of them is available.",
    };

    private IEnumerable<IGrouping<string, ResolvedUnlock>> GroupUnlockEntries(List<ResolvedUnlock> visible) =>
        GroupModes[groupMode] switch
        {
            "Level" => visible.GroupBy(u => $"Level {(u.QuestLevel / 10) * 10}–{((u.QuestLevel / 10) * 10) + 9}", StringComparer.Ordinal)
                               .OrderBy(g => g.Min(u => u.QuestLevel)),
            "Type" => visible.GroupBy(u => UnlockFilters.Category(u.Def), StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal),
            _ => GroupByZone(visible),
        };

    private IEnumerable<IGrouping<string, ResolvedUnlock>> GroupByZone(List<ResolvedUnlock> visible)
    {
        var currentZone = CurrentZoneName();
        return visible.GroupBy(u => u.ZoneName ?? "Unknown location", StringComparer.Ordinal)
                      .OrderByDescending(g => string.Equals(g.Key, currentZone, StringComparison.Ordinal))
                      .ThenBy(g => g.Key, StringComparer.Ordinal);
    }

    private string? CurrentZoneName()
    {
        var here = clientState.TerritoryType;
        return unlocks.Entries.FirstOrDefault(u => u.GiverTerritory == here)?.ZoneName;
    }

    // ----- Hunting tab -----------------------------------------------------------------------
    private void BuildHuntingControls()
    {
        huntingControls = new VerticalListNode
        {
            FitWidth = true,
            ItemSpacing = 4f,
            Position = tabContentStart,
            Size = new Vector2(tabContentSize.X, HuntingControlsHeight),
        };

        huntingHeaderNode = BuildHeadingNode(string.Empty);
        huntingControls.AddNode(huntingHeaderNode);

        var actions = new AlignedHorizontalListNode { Height = 26f, FitToContentHeight = true, ItemSpacing = 8f };
        huntHereButton = new TextButtonNode
        {
            Width = 170f,
            Height = 24f,
            String = "Start Hunting",
            OnClick = OnHuntClicked,
        };
        actions.AddNode(huntHereButton);
        huntingControls.AddNode(actions);

        AddTabNode(huntingNodes, huntingControls);
    }

    /// <summary>Writes the hunting heading and re-runs the column it lives in. This is the one
    /// heading in the window whose words arrive after its container was laid out, so the layout is
    /// re-run here rather than trusted to still hold.</summary>
    private void SetHuntingHeader(string text)
    {
        if (huntingHeaderNode is null)
        {
            return;
        }

        // Through HeadingText for the same reason BuildHeadingNode is: this node is Trump Gothic.
        huntingHeaderNode.String = HeadingText.Plain(text);
        huntingHeaderNode.Height = HuntingHeaderHeight;
        huntingControls?.RecalculateLayout();
    }

    private int ComputeHuntingSignature()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + (hunting.ActiveLogLabel?.GetHashCode(StringComparison.Ordinal) ?? 0);
            hash = (hash * 31) + (hunting.CurrentRank ?? 0);
            foreach (var m in hunting.RemainingOnPage)
            {
                hash = (hash * 31) + (int)m.BNpcNameId;
            }

            return hash;
        }
    }

    // Per-tick refresh of the distance captions only: player movement and live-tracking position
    // updates change distances without changing ComputeHuntingSignature, so the rows would
    // otherwise show the distance from whenever the list was last rebuilt.
    private void RefreshHuntingDistances()
    {
        var player = objects.LocalPlayer;
        if (player is null || distanceRows.Count == 0)
        {
            return;
        }

        foreach (var (row, monster) in distanceRows)
        {
            var view = hunting.CurrentTarget is { } current && current.Monster == monster
                ? current
                : hunting.HuntHereOrder.FirstOrDefault(t => t.Monster == monster);
            if (view is null)
            {
                continue;
            }

            var distance = NavMath.Distance(view.WorldX - player.Position.X, view.WorldY - player.Position.Y, view.WorldZ - player.Position.Z);
            row.Detail = $"{view.Killed}/{view.Required} · {NavMath.FormatDistance(distance)}";
        }

        list?.Update();
    }

    private void RebuildHunting()
    {
        if (list is null || huntingHeaderNode is null || huntHereButton is null)
        {
            return;
        }

        SetHuntingHeader(hunting.ActiveLogLabel is { } label
            ? $"{label} - Rank {hunting.CurrentRank}"
            : hunting.NoLogReason ?? "No hunting log active.");

        var navigator = ResolveNavigator();
        var remaining = hunting.HuntHereOrder.Count;
        huntHereButton.String = remaining > 0 ? $"Start Hunting ({remaining})" : "Start Hunting";
        huntHereButton.IsEnabled = navigator != null && remaining > 0;

        rows.Clear();
        distanceRows.Clear();
        AddGuidanceUnavailableNote(navigator);
        foreach (var target in hunting.HuntHereOrder)
        {
            rows.Add(BuildHuntingRow(target, navigator));
        }

        var shown = hunting.HuntHereOrder.Select(t => t.Monster).ToHashSet();
        if (hunting.CurrentTarget is { } current && !shown.Contains(current.Monster))
        {
            rows.Add(BuildHuntingRow(current, navigator));
        }

        if (rows.Count == 0)
        {
            rows.Add(new HubListRow { Kind = HubRowKind.Note, Label = "Nothing left on this rank." });
        }

        PublishRows(huntingControls);
        RefreshHuntingDistances();
        lastHuntingSignature = ComputeHuntingSignature();
    }

    private HubListRow BuildHuntingRow(HuntingTargetView target, QuestNavigator? navigator)
    {
        if (!target.IsRoutable)
        {
            return new HubListRow
            {
                Kind = HubRowKind.Entry,
                Label = target.MonsterName,

                // The duty's name is where you go, so it belongs on the "where" line with the zone
                // rather than crammed into the gutter beside the kill count, which is where it used
                // to be and where it was the first thing to be ellipsised away.
                Description = HuntingRowWhere(target),
                Detail = $"{target.Killed}/{target.Required}",
                IconId = HuntingRowIcon(target),
                StatusWord = UnlockStatusDisplay.Word(UnlockStatus.Available),
                Pane = BuildHuntingDetail(target, navigator),
                Hover = PublishDetail,
                Activate = target.DutyContentFinderConditionId is null
                    ? null
                    : () => OpenDuty(target.DutyContentFinderConditionId),
            };
        }

        var row = new HubListRow
        {
            Kind = HubRowKind.Entry,
            Label = target.MonsterName,
            Description = HuntingRowWhere(target),
            Detail = $"{target.Killed}/{target.Required}",
            IconId = HuntingRowIcon(target),
            StatusWord = UnlockStatusDisplay.Word(UnlockStatus.Available),
            Pane = BuildHuntingDetail(target, navigator),
            Hover = PublishDetail,
            Activate = navigator is null ? null : () =>
            {
                if (hunting.ToPickupTarget(target) is { } pickup)
                {
                    navigator.SetPickup(pickup);
                }
            },
        };

        distanceRows.Add((row, target.Monster));
        return row;
    }

    /// <summary>The picture in a hunting row's left column: the creature's own art while there is
    /// still something to kill, the green check once there is not.
    ///
    /// <para>That swap is the vanilla Hunting Log's own behaviour, not an invention — the art is the
    /// entry's identity and the checkmark replaces it on completion. The icon still goes through the
    /// same runtime validation every other icon does, so a creature whose art a patch has moved
    /// falls back to the row saying its state in words rather than to a hole in the column.</para></summary>
    private uint HuntingRowIcon(HuntingTargetView target) =>
        target.Killed >= target.Required
            ? statusIcons.Resolve(UnlockStatusDisplay.CompleteIcon)
            : statusIcons.Resolve(target.IconId);

    /// <summary>What the pane says about one hunting target. The same component the Unlocks tab
    /// uses, so "what is selected" is described the same way whichever list the player is in.</summary>
    private HubRowDetail BuildHuntingDetail(HuntingTargetView target, QuestNavigator? navigator)
    {
        var done = target.Killed >= target.Required;
        var actions = new List<HubDetailAction>();

        if (!done && target.IsRoutable && navigator is not null)
        {
            actions.Add(new HubDetailAction("Guide me there", () =>
            {
                if (hunting.ToPickupTarget(target) is { } pickup)
                {
                    navigator.SetPickup(pickup);
                }
            }));
        }
        else if (!done && target.DutyContentFinderConditionId is { } cfcId)
        {
            actions.Add(new HubDetailAction("Open Duty Finder", () => OpenDuty(cfcId)));
        }

        return new HubRowDetail
        {
            Title = target.MonsterName,
            Kind = $"{target.Killed} of {target.Required} killed",
            StatusIconId = HuntingRowIcon(target),
            StatusSentence = done
                ? "Complete — this one is done."
                : $"{target.Required - target.Killed} left to kill.",
            Body = target.IsRoutable
                ? "Wayfarer can point you at this one and chain it with the rest of the rank."
                : "This one lives inside an instanced Grand Company duty, so it has to be queued for rather than walked to.",
            From = HuntingRowWhere(target),
        };
    }

    private void OnHuntClicked()
    {
        var navigator = ResolveNavigator();
        if (navigator is null || hunting.HuntHereOrder.Count == 0)
        {
            return;
        }

        var targets = hunting.HuntHereOrder.Select(hunting.ToPickupTarget).Where(t => t != null).Select(t => t!).ToList();
        if (targets.Count > 0)
        {
            navigator.SetRoute(targets);
        }
    }

    /// <summary>The universal exit. Whenever an explicit mode owns the arrow the player must have a
    /// reachable way out of it — this is that way, and it is a real focusable button rather than a
    /// menu item hidden behind a popup.</summary>
    private void OnStopClicked() => ResolveNavigator()?.ClearPickup();

    private void UpdateStopButton()
    {
        var engaged = ResolveNavigator()?.Current.Engaged == true;
        if (stripStopButton is not null && stripStopButton.IsEnabled != engaged)
        {
            stripStopButton.IsEnabled = engaged;
        }
    }

    // ----- Quests tab ------------------------------------------------------------------------

    /// <summary>The tab that gives guidance the two things a click-through overlay physically
    /// cannot have: a choice of what to follow, and buttons.
    ///
    /// <b>Choosing a quest</b> was lost in the move to the native readout — the picker lived in a
    /// popup on the old ImGui widget, and <c>GetAcceptedQuests</c>/<c>FollowedOverride</c> were left
    /// with no caller at all. It is a tab rather than a popup because a popup has to be registered
    /// into the host addon's focusable nodes before a cursor can enter it, and a popup a controller
    /// cannot reach is the exact trap this window exists to avoid. Here the quests are ordinary list
    /// rows: a mouse clicks them, a d-pad walks them.
    ///
    /// <b>Teleport and Duty Finder</b> are here for the mouse player. The readout recommends
    /// "Teleport to Horizon first" and the overlay it is drawn on is click-through by construction,
    /// so before this the only way to act on that advice was the game's context menu — which is
    /// off by default for a mouse. These are real buttons on a real window, reachable with either
    /// device, and they run the same <see cref="TeleportAction"/> gate the context menu does.</summary>
    private void BuildQuestControls()
    {
        questControls = new VerticalListNode
        {
            FitWidth = true,
            ItemSpacing = 4f,
            Position = tabContentStart,
            Size = new Vector2(tabContentSize.X, QuestControlsHeight),
        };

        questHeaderNode = BuildHeadingNode(string.Empty);
        questControls.AddNode(questHeaderNode);
        questControls.AddNode(BuildQuestFollowRow());
        questControls.AddNode(BuildQuestActionRow());

        AddTabNode(questNodes, questControls);
    }

    private AlignedHorizontalListNode BuildQuestFollowRow()
    {
        var row = new AlignedHorizontalListNode { Height = 26f, FitToContentHeight = true, ItemSpacing = 8f };

        followMsqButton = new TextButtonNode
        {
            Width = 220f,
            Height = 24f,
            String = "Follow the Main Scenario",
            OnClick = OnFollowMsqClicked,
        };
        row.AddNode(followMsqButton);
        return row;
    }

    private AlignedHorizontalListNode BuildQuestActionRow()
    {
        var row = new AlignedHorizontalListNode { Height = 26f, FitToContentHeight = true, ItemSpacing = 8f };

        teleportButton = new TextButtonNode
        {
            Width = 230f,
            Height = 24f,
            String = "Teleport",
            IsEnabled = false,
            OnClick = OnTeleportClicked,
        };
        row.AddNode(teleportButton);

        dutyFinderButton = new TextButtonNode
        {
            Width = 150f,
            Height = 24f,
            String = "Duty Finder",
            IsEnabled = false,
            OnClick = OnDutyFinderClicked,
        };
        row.AddNode(dutyFinderButton);

        return row;
    }

    private void OnFollowMsqClicked()
    {
        if (ResolveNavigator() is not { } navigator)
        {
            return;
        }

        navigator.ClearPickup();
        navigator.FollowedOverride = null;
        RebuildQuests();
    }

    private void OnTeleportClicked()
    {
        if (ResolveNavigator()?.Current.AetheryteId is { } aetheryteId)
        {
            TeleportAction.Execute(aetheryteId, config.QuestHelper, clientState, log);
        }
    }

    private void OnDutyFinderClicked() => OpenDuty(ResolveNavigator()?.Current.DutyContentFinderConditionId);

    private void RebuildQuests()
    {
        if (list is null || questControls is null)
        {
            return;
        }

        var navigator = ResolveNavigator();
        var content = feed.Compose(teleportOnClick: false);

        rows.Clear();
        distanceRows.Clear();
        AddGuidanceUnavailableNote(navigator);
        SetQuestHeader(content);
        AddGuidanceLines(content);
        AddFollowableRows(navigator);
        AddAcceptedQuestRows(navigator);

        if (rows.Count == 0)
        {
            rows.Add(new HubListRow { Kind = HubRowKind.Note, Label = "Nothing to follow right now." });
        }

        RefreshQuestActions();
        PublishRows(questControls);
        lastQuestSignature = ComputeQuestSignature();
    }

    /// <summary>The guidance heading, verbatim — the same words the readout puts at the top of the
    /// HUD, so the tab and the HUD can never disagree about what is being followed.</summary>
    private void SetQuestHeader(ReadoutContent content)
    {
        if (questHeaderNode is null)
        {
            return;
        }

        var heading = "Wayfarer";
        foreach (var line in content.Lines)
        {
            if (line.Emphasis == ReadoutEmphasis.Heading)
            {
                heading = line.Text;
                break;
            }
        }

        questHeaderNode.String = heading;
        questHeaderNode.Height = HuntingHeaderHeight;
        questControls?.RecalculateLayout();
    }

    // The readout's own lines, minus the heading that is already above them. Display only: they say
    // what the guidance is doing, and the buttons beside them are what acts on it.
    private void AddGuidanceLines(ReadoutContent content)
    {
        foreach (var line in content.Lines)
        {
            if (line.Emphasis != ReadoutEmphasis.Heading)
            {
                rows.Add(new HubListRow { Kind = HubRowKind.Note, Label = line.Text });
            }
        }
    }

    /// <summary>Everything Wayfarer can be told to follow, as rows in one list, so the answer to
    /// "how do I toggle between MSQ and another quest or unlocks?" is a single screen with the
    /// choices on it rather than a feature the player has to already know is there.
    ///
    /// <para>Four things, always all four, in the same order, whether or not they currently have
    /// anything to offer — a choice that vanishes when it is empty cannot be learned. The ones with
    /// nothing to do say so on their second line and are inert.</para></summary>
    private void AddFollowableRows(QuestNavigator? navigator)
    {
        rows.Add(new HubListRow { Kind = HubRowKind.Heading, Label = "What Wayfarer is following" });

        var followingMsq = navigator is not null && navigator.FollowedOverride is null;
        rows.Add(new HubListRow
        {
            Kind = HubRowKind.Entry,
            Label = "Main Scenario",
            Description = navigator?.Current.QuestName is { Length: > 0 } msq
                ? msq
                : "The next step of the main story, picked automatically.",
            Detail = followingMsq ? "Following" : string.Empty,
            IconId = statusIcons.For(followingMsq ? UnlockStatus.Accepted : UnlockStatus.Available),
            StatusWord = UnlockStatusDisplay.Word(followingMsq ? UnlockStatus.Accepted : UnlockStatus.Available),
            LabelColor = followingMsq ? GameColors.Good : null,
            Pane = FollowableDetail(
                "Main Scenario",
                followingMsq ? "Following. The arrow points at your current main scenario objective." : "Not being followed right now.",
                "Wayfarer's default: it reads the main scenario quest you are on and points at whatever it wants next.",
                navigator is null ? [] : [new HubDetailAction("Follow the Main Scenario", OnFollowMsqClicked)]),
            Hover = PublishDetail,
            Activate = navigator is null ? null : OnFollowMsqClicked,
        });

        AddUnlockRouteRow(navigator);
        AddHuntingFollowRow(navigator);
    }

    private void AddUnlockRouteRow(QuestNavigator? navigator)
    {
        var routable = navigator is null
            ? 0
            : ComputeVisibleUnlocks().Count(u => u.Status == UnlockStatus.Available && u.GiverTerritory != null);

        rows.Add(new HubListRow
        {
            Kind = HubRowKind.Entry,
            Label = "Unlock route",
            Description = routable > 0
                ? $"{routable} available nearby, walked in order."
                : "Nothing available to route to with the current filters.",
            Detail = routable > 0 ? $"{routable}" : string.Empty,
            IconId = statusIcons.For(routable > 0 ? UnlockStatus.Available : UnlockStatus.Done),
            StatusWord = UnlockStatusDisplay.Word(routable > 0 ? UnlockStatus.Available : UnlockStatus.Done),
            Pane = FollowableDetail(
                "Unlock route",
                routable > 0 ? $"{routable} unlocks are available and reachable." : "Nothing to route to right now.",
                "Chains every available unlock you can walk to into one route, nearest first, and points the arrow at each in turn.",
                routable > 0 && navigator is not null ? [new HubDetailAction("Follow this route", OnRouteClicked)] : []),
            Hover = PublishDetail,
            Activate = routable > 0 && navigator is not null ? OnRouteClicked : null,
        });
    }

    private void AddHuntingFollowRow(QuestNavigator? navigator)
    {
        var remaining = hunting.HuntHereOrder.Count;
        var label = hunting.ActiveLogLabel is { Length: > 0 } log
            ? $"Hunting Log - {log}"
            : "Hunting Log";

        rows.Add(new HubListRow
        {
            Kind = HubRowKind.Entry,
            Label = label,
            Description = hunting.ActiveLogLabel is null
                ? hunting.NoLogReason ?? "No hunting log active."
                : $"Rank {hunting.CurrentRank} · {remaining} left in this zone",
            Detail = remaining > 0 ? $"{remaining}" : string.Empty,
            IconId = statusIcons.For(remaining > 0 ? UnlockStatus.Available : UnlockStatus.Done),
            StatusWord = UnlockStatusDisplay.Word(remaining > 0 ? UnlockStatus.Available : UnlockStatus.Done),
            Pane = FollowableDetail(
                label,
                remaining > 0 ? $"{remaining} targets left in this zone." : hunting.NoLogReason ?? "Nothing left on this rank here.",
                "Chains this rank's remaining targets in the zone you are standing in, nearest first.",
                remaining > 0 && navigator is not null ? [new HubDetailAction("Start hunting", OnHuntClicked)] : []),
            Hover = PublishDetail,
            Activate = remaining > 0 && navigator is not null ? OnHuntClicked : null,
        });
    }

    private HubRowDetail FollowableDetail(
        string title, string sentence, string body, IReadOnlyList<HubDetailAction> actions) => new()
        {
            Title = title,
            Kind = "Something to follow",
            StatusIconId = statusIcons.For(UnlockStatus.Accepted),
            StatusSentence = sentence,
            Body = body,
            Actions = actions,
        };

    private void AddAcceptedQuestRows(QuestNavigator? navigator)
    {
        if (navigator is null)
        {
            return;
        }

        var accepted = navigator.GetAcceptedQuests();
        rows.Add(new HubListRow
        {
            Kind = HubRowKind.Heading,
            Label = "Accepted Quests",
            Detail = $"{accepted.Count}",
        });

        if (accepted.Count == 0)
        {
            rows.Add(new HubListRow { Kind = HubRowKind.Note, Label = "You have not accepted any quests." });
            return;
        }

        var followed = navigator.FollowedOverride;
        foreach (var (id, name) in accepted)
        {
            var isFollowed = followed == id;
            var questId = id;
            rows.Add(new HubListRow
            {
                Kind = HubRowKind.Entry,
                Label = name,

                // The objective moves to line two, where a whole sentence fits. In the gutter it
                // was the longest string on the row competing for the narrowest space on it.
                Description = navigator.GetAcceptedQuestObjective(questId) ?? string.Empty,
                Detail = isFollowed ? "Following" : string.Empty,
                IconId = statusIcons.For(UnlockStatus.Accepted),
                StatusWord = UnlockStatusDisplay.Word(UnlockStatus.Accepted),
                LabelColor = isFollowed ? GameColors.Good : null,
                Pane = BuildQuestDetail(name, navigator.GetAcceptedQuestObjective(questId), isFollowed, questId),
                Hover = PublishDetail,
                Activate = () => FollowQuest(questId),
            });
        }
    }

    /// <summary>What the pane says about one accepted quest. The action is the whole point of this
    /// tab, so it is a real button on the pane rather than only a confirm on the row — a mouse
    /// player should not have to guess that a list row is clickable.</summary>
    private HubRowDetail BuildQuestDetail(string name, string? objective, bool isFollowed, ushort questId)
    {
        var actions = new List<HubDetailAction>();
        if (isFollowed)
        {
            actions.Add(new HubDetailAction("Stop following", OnStopFollowingClicked));
        }
        else
        {
            actions.Add(new HubDetailAction("Follow this quest", () => FollowQuest(questId)));
        }

        return new HubRowDetail
        {
            Title = name,
            Kind = "Accepted quest",
            StatusIconId = statusIcons.For(UnlockStatus.Accepted),
            StatusSentence = isFollowed
                ? "Following — the arrow is pointing at this."
                : "In progress — you have accepted this.",
            Body = objective is { Length: > 0 } text ? text : "The game records no objective text for this step.",
            Actions = actions,
        };
    }

    /// <summary>Back to the main scenario, which is what "not following anything in particular"
    /// means for this plugin — there is no null state, only the default loop.</summary>
    private void OnStopFollowingClicked() => OnFollowMsqClicked();

    private void FollowQuest(ushort questId)
    {
        if (ResolveNavigator() is not { } navigator)
        {
            return;
        }

        navigator.ClearPickup();
        navigator.FollowedOverride = questId;

        // Deliberately not rebuilding from here. This runs inside the list's own activation
        // callback, and republishing OptionsList can rebuild the very row node whose handler is
        // executing. ComputeQuestSignature folds in the followed quest, so the background poll
        // repaints on the next tick — which is how the checklist rows behave too.
    }

    /// <summary>Keeps the four action buttons honest against the live guidance snapshot, without
    /// rebuilding the list under the cursor. Labels are only assigned when they actually change:
    /// assigning one builds a SeString, and this runs every tick the tab is open.</summary>
    private void RefreshQuestActions()
    {
        var navigator = ResolveNavigator();
        var state = navigator?.Current;

        if (followMsqButton is not null)
        {
            followMsqButton.IsEnabled = navigator is not null
                && (navigator.FollowedOverride is not null || state?.Engaged == true);
        }

        UpdateTeleportButton(state);

        if (dutyFinderButton is not null)
        {
            dutyFinderButton.IsEnabled = state?.DutyContentFinderConditionId is not null;
        }
    }

    private void UpdateTeleportButton(NavigationState? state)
    {
        if (teleportButton is null)
        {
            return;
        }

        var offered = config.QuestHelper.ClickTeleportEnabled
            && state is { AetheryteUnlocked: true, AetheryteId: not null }
            && state.AetheryteName is { Length: > 0 };

        var label = offered ? $"Teleport to {state!.AetheryteName}" : "Teleport";
        if (!string.Equals(label, lastTeleportLabel, StringComparison.Ordinal))
        {
            lastTeleportLabel = label;
            teleportButton.String = label;
        }

        teleportButton.IsEnabled = offered;
    }

    private int ComputeQuestSignature()
    {
        var navigator = ResolveNavigator();
        if (navigator is null)
        {
            return 0;
        }

        var state = navigator.Current;
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + (navigator.FollowedOverride ?? 0);
            hash = (hash * 31) + (state.QuestName?.GetHashCode(StringComparison.Ordinal) ?? 0);
            hash = (hash * 31) + (state.StepLabel?.GetHashCode(StringComparison.Ordinal) ?? 0);
            hash = (hash * 31) + state.Mode.GetHashCode(StringComparison.Ordinal);
            hash = (hash * 31) + (state.Engaged ? 1 : 0);

            // The accepted-quest list itself is not folded in: reading it allocates, and this runs
            // every tick. Accepting or finishing a quest changes the followed quest's name, step or
            // mode in practice, and the tab rebuilds whole on every open regardless.
            return hash;
        }
    }

    // ----- Settings tab ----------------------------------------------------------------------
    private void BuildSettingsTab()
    {
        settingsArea = new ScrollingNode<VerticalListNode>
        {
            // FitWidth is deliberately OFF. It stretches every control to the container's own
            // width, and the container draws its scroll bar inside that width and clips at its own
            // edge — which is the reported "the sliders clip outside the border". The widths are set
            // explicitly instead, by ApplySettingWidths, which reserves the bar's gutter. Leaving
            // FitWidth on would simply undo that on the next layout pass.
            ContentNode = { FitWidth = false, FitContents = true, ItemSpacing = 4f },
            AutoHideScrollBar = true,
            Position = tabContentStart,
            Size = tabContentSize,
        };
        AddTabNode(settingsNodes, settingsArea);
    }

    private void RebuildSettings()
    {
        if (settingsArea is null)
        {
            return;
        }

        settingsArea.ContentNode.Clear();
        settingSliders.Clear();
        firstSettingControl = null;

        foreach (var section in settings.Build())
        {
            settingsArea.ContentNode.AddNode(BuildHeadingNode(section.Title));
            foreach (var setting in section.Settings)
            {
                var control = BuildSettingControl(setting);
                settingsArea.ContentNode.AddNode(control);
                WireScrollFollowsFocus(control);
            }
        }

        ApplySettingWidths();
        settingsArea.RecalculateSizes();
        ResizeToContent();
        ApplySettingWidths();
        settingsArea.RecalculateSizes();
        ApplyNavigation(settingsArea.ContentNode);
        RefreshSettings();
    }

    /// <summary>Keeps every control on the Settings tab inside the area the tab actually clips at.
    ///
    /// <para>The container's scroll bar is drawn against its own right edge, inside its width, so a
    /// control stretched to the full container width runs under the bar and off the edge — the
    /// reported "the sliders clip outside the border". <c>ScrollingNode</c> forces its content node
    /// back to its own width on every size change, so the reservation cannot live there; it is
    /// applied to the controls themselves, and re-applied after anything that could have resized the
    /// tab. This is also why the list's own <c>FitWidth</c> is off — see
    /// <see cref="BuildSettingsTab"/>.</para></summary>
    private void ApplySettingWidths()
    {
        if (settingsArea is null)
        {
            return;
        }

        var width = SettingsLayout.ControlWidth(tabContentSize.X);
        foreach (var node in settingsArea.ContentNode.Nodes)
        {
            node.Width = width;
        }

        settingsArea.ContentNode.RecalculateLayout();
    }

    /// <summary>Re-reads every value-bearing control on the Settings tab from the setting behind it.
    ///
    /// <para>This is what makes the readout-position sliders honest. They are one of two ways to
    /// move the readout — the other is dragging it with the mouse — and a preset or a resolution
    /// change moves it as well. All of those write the same stored fraction, and this reads it back,
    /// so the sliders say where the readout is rather than where they last left it.</para></summary>
    private void RefreshSettings()
    {
        foreach (var slider in settingSliders)
        {
            slider.Refresh();
        }
    }

    /// <summary>Gives the Settings tab the one thing its container does not have: scroll-follows-focus.
    ///
    /// <para><b>This is a reported defect, not a nicety.</b> The Settings tab is a
    /// <c>ScrollingNode</c>, which clips what is out of view but has no navigation implementation at
    /// all — so a controller cursor walked straight onto settings that were scrolled off the bottom
    /// and the player was pressing Confirm on controls nobody could see. The list-backed tabs get
    /// this from KamiToolKit's <c>ListNode</c>; this supplies it here from the outside, which is the
    /// only place it can be supplied from.</para>
    ///
    /// <para>Driven by the node's own <c>FocusStart</c> event rather than by polling the game's
    /// focus state every frame: it fires exactly when the cursor arrives, costs nothing the rest of
    /// the time, and it is the same event the toolkit's own text-input node uses to know it has been
    /// focused. Registered on the component's collision node, which is what the component nominates
    /// as its focus target.</para></summary>
    private void WireScrollFollowsFocus(NodeBase control)
    {
        // A scale setting is a caption plus a slider, and only the slider can be focused — so the
        // event goes on the slider while the scroll target stays the whole row, or the caption
        // scrolls off the top of the tab the moment the cursor arrives on its slider.
        var focusable = control is SettingSliderNode row ? row.Slider : control;
        if (focusable is not KamiToolKit.BaseTypes.ComponentNode.ComponentNode component)
        {
            return;
        }

        component.CollisionNode.AddEvent(AtkEventType.FocusStart, () => ScrollSettingIntoView(control));
    }

    private void ScrollSettingIntoView(NodeBase control)
    {
        if (settingsArea is null)
        {
            return;
        }

        try
        {
            var bar = settingsArea.ScrollBarNode;
            var target = ScrollIntoView.Adjust(
                control.Y,
                control.Height,
                settingsArea.Height,
                bar.ScrollPosition,
                bar.ScrollMaxPosition);

            if (Math.Abs(target - bar.ScrollPosition) >= 1f)
            {
                bar.ScrollPosition = target;
            }
        }
        catch (Exception ex)
        {
            // A focus event that throws would throw again on the next cursor move, so this is
            // swallowed rather than allowed to make the tab unusable. Worst case is the old
            // behaviour: no scrolling, which is what this is fixing.
            log.Warning(ex, "Wayfarer nav: scrolling a focused setting into view failed.");
        }
    }

    private NodeBase BuildSettingControl(SettingDefinition setting) => setting.Kind switch
    {
        SettingKind.Toggle => BuildToggle(setting),
        SettingKind.Scale => BuildScale(setting),
        _ => BuildChoice(setting),
    };

    private SettingSliderNode BuildScale(SettingDefinition setting)
    {
        var node = new SettingSliderNode(setting);
        settingSliders.Add(node);
        return node;
    }

    private CheckboxNode BuildToggle(SettingDefinition setting)
    {
        var node = new CheckboxNode
        {
            Height = 22f,
            String = setting.Label,
            IsChecked = setting.ReadFlag?.Invoke() ?? false,
            OnClick = value => setting.WriteFlag?.Invoke(value),
        };
        firstSettingControl ??= node;
        return node;
    }

    // Immediate reposition, because a controller cannot reach the game's own title-bar
    // right-click Move/Scale menu. NativeAddon persists whatever position is current when the
    // window is next hidden, so nothing extra is needed for it to survive a reopen.
    private void ApplyPositionPreset(HubPositionPreset preset)
    {
        if (!IsOpen)
        {
            return;
        }

        const float margin = 12f;
        var screen = ViewportSize();

        // The window's size on screen, not its size in addon units — the two differ by the player's
        // interface scale, and using the unscaled figure here put every right/bottom preset off the
        // edge on any interface larger than 100%.
        var size = Size * UiScale();
        var position = preset switch
        {
            HubPositionPreset.TopLeft => new Vector2(margin, margin),
            HubPositionPreset.TopRight => new Vector2(screen.X - size.X - margin, margin),
            HubPositionPreset.BottomLeft => new Vector2(margin, screen.Y - size.Y - margin),
            HubPositionPreset.BottomRight => new Vector2(screen.X - size.X - margin, screen.Y - size.Y - margin),
            _ => (screen - size) / 2f,
        };

        SetWindowPosition(position);
        ClampIntoViewport();
    }
}
