using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
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
/// everything the plugin has to show, for mouse and controller alike. The game's own windows are
/// mouse-first and cursor-navigable at the same time; copying that is what lets one surface serve
/// both players instead of two parallel stacks drifting apart.
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
    private const float RowHeight = 24f;
    private const float ChecklistControlsHeight = 92f;
    private const float HuntingControlsHeight = 60f;
    private const float ButtonHintHeight = 20f;

    private static readonly string[] GroupModes = ["Zone", "Level", "Type"];

    private static readonly (string Key, string Label)[] CategoryChips =
        [("content", "Content"), ("system", "Systems"), ("cosmetic", "Cosmetics"), ("zone", "Zones")];

    private static readonly (string Key, string Label)[] PriorityChips =
        [("essential", "Essential"), ("nice", "Nice"), ("optional", "Optional")];

    private readonly IUnlockProvider unlocks;
    private readonly HuntingLogService hunting;
    private readonly ModuleRegistry modules;
    private readonly IObjectTable objects;
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly Configuration config;
    private readonly SettingsCatalog settings;
    private readonly InputModeService inputMode;
    private readonly IPluginLog log;

    private readonly FilterState filter = new();
    private readonly List<NodeBase> checklistNodes = [];
    private readonly List<NodeBase> huntingNodes = [];
    private readonly List<NodeBase> settingsNodes = [];
    private readonly List<HubListRow> rows = [];
    private readonly List<(HubListRow Row, Core.Hunting.HuntingMonster Monster)> distanceRows = [];

    private int groupMode;
    private HubTab pendingTab = HubTab.Checklist;
    private HubTab currentTab = HubTab.Checklist;
    private Vector2 tabContentStart;
    private Vector2 tabContentSize;
    private int lastChecklistSignature;
    private int lastHuntingSignature;
    private int lastPopulatedRows;
    private bool navigationWarningLogged;

    private TabBarNode? hubTabs;
    private ListNode<HubListRow, HubListRowNode>? list;

    private VerticalListNode? checklistControls;
    private TextButtonNode? groupButton;
    private TextButtonNode? routeButton;

    private VerticalListNode? huntingControls;
    private TextNode? huntingHeaderNode;
    private TextButtonNode? huntHereButton;

    private TextButtonNode? stopButton;
    private ScrollingNode<VerticalListNode>? settingsArea;
    private CheckboxNode? firstSettingControl;
    private TextNode? buttonHintNode;
    private bool lastReverseConfirmCancel;

    public NativeHubWindow(
        IUnlockProvider unlocks,
        HuntingLogService hunting,
        ModuleRegistry modules,
        IObjectTable objects,
        IClientState clientState,
        IFramework framework,
        Configuration config,
        SettingsCatalog settings,
        InputModeService inputMode,
        IPluginLog log)
    {
        this.unlocks = unlocks;
        this.hunting = hunting;
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
            log.Warning(ex, "NativeHubWindow: dispose on the framework thread failed or timed out.");
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
            AddNode(new TextNode
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
        hubTabs.AddTab("Checklist", () => SelectTab(HubTab.Checklist));
        hubTabs.AddTab("Hunting Log", () => SelectTab(HubTab.Hunting));
        hubTabs.AddTab("Settings", () => SelectTab(HubTab.Settings));
        AddNode(hubTabs);

        var y = contentStart.Y + TabBarHeight + 6f;
        tabContentStart = new Vector2(contentStart.X, y);
        tabContentSize = new Vector2(contentSize.X, contentSize.Y - (y - contentStart.Y) - ButtonHintHeight);

        BuildButtonHint(contentStart, contentSize);
        BuildSharedList();
        BuildChecklistControls();
        BuildHuntingControls();
        BuildSettingsTab();

        SelectTab(pendingTab);

        framework.Update += OnFrameworkUpdate;
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        framework.Update -= OnFrameworkUpdate;
        distanceRows.Clear();
        rows.Clear();
        checklistNodes.Clear();
        huntingNodes.Clear();
        settingsNodes.Clear();
        hubTabs = null;
        list = null;
        checklistControls = null;
        groupButton = null;
        routeButton = null;
        huntingControls = null;
        huntingHeaderNode = null;
        huntHereButton = null;
        stopButton = null;
        settingsArea = null;
        firstSettingControl = null;
        buttonHintNode = null;
    }

    // ----- Private static helpers (grouped together — SA1204) ------------------------------
    private static unsafe Vector2 ComputeDefaultSize()
    {
        var screen = new Vector2(AtkStage.Instance()->ScreenSize.Width, AtkStage.Instance()->ScreenSize.Height);
        var scale = AtkUnitBase.GetGlobalUIScale();
        var width = Math.Clamp(600f * scale, 460f, screen.X * 0.6f);

        // Tall by default on purpose: the Settings tab's controls are real components in a plain
        // column, and a controller can only reach the ones that are actually laid out on screen.
        var height = Math.Clamp(screen.Y * 0.78f, 460f, screen.Y * 0.9f);
        return new Vector2(width, height);
    }

    private static void SetBucketVisible(List<NodeBase> bucket, bool visible)
    {
        foreach (var node in bucket)
        {
            node.IsVisible = visible;
        }
    }

    private static string TabLabel(HubTab tab) => tab switch
    {
        HubTab.Checklist => "Checklist",
        HubTab.Hunting => "Hunting Log",
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

    private static (string Label, Vector4 Color) StatusPresentation(UnlockStatus status) => status switch
    {
        UnlockStatus.Done => ("Complete", GameColors.Dimmed),
        UnlockStatus.Accepted => ("Accepted", GameColors.ListText),
        UnlockStatus.Available => ("Available", GameColors.Good),
        UnlockStatus.LockedOut => ("Missed", GameColors.Bad),
        UnlockStatus.UnknownGate => ("Unknown", GameColors.Dimmed),
        _ => ("Locked", GameColors.Dimmed),
    };

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
            UnlockStatus.UnknownGate => 8,
            UnlockStatus.LockedOut => 9,
            _ => 10,
        }).ThenBy(u => u.QuestLevel);

    private static void OpenDuty(uint? cfcId)
    {
        if (cfcId is not { } id)
        {
            return;
        }

        var agent = AgentContentsFinder.Instance();
        if (agent != null)
        {
            agent->OpenRegularDuty(id, false);
        }
    }

    private static FloatSliderNode BuildScale(SettingDefinition setting) => new()
    {
        Height = 24f,
        Min = setting.Minimum,
        Max = setting.Maximum,
        Step = setting.Step,
        Value = setting.ReadValue?.Invoke() ?? setting.Minimum,
        OnValueChanged = value => setting.WriteValue?.Invoke(value),
    };

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

    private static TextNode BuildHeadingNode(string text) => new()
    {
        Height = 22f,
        FontType = FontType.TrumpGothic,
        FontSize = 20,
        TextColor = GameColors.Heading,
        TextOutlineColor = GameColors.HeadingEdge,
        TextFlags = TextFlags.Edge,
        String = text,
    };

    private void AddTabNode(List<NodeBase> bucket, NodeBase node)
    {
        AddNode(node);
        bucket.Add(node);
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
        AddNode(buttonHintNode);
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
        AddNode(list);
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
        SetBucketVisible(settingsNodes, tab == HubTab.Settings);

        if (list is not null)
        {
            var controlsHeight = tab switch
            {
                HubTab.Checklist => ChecklistControlsHeight,
                HubTab.Hunting => HuntingControlsHeight,
                _ => 0f,
            };

            list.IsVisible = tab != HubTab.Settings;
            if (list.IsVisible)
            {
                list.Position = new Vector2(tabContentStart.X, tabContentStart.Y + controlsHeight);
                list.Size = new Vector2(tabContentSize.X, Math.Max(tabContentSize.Y - controlsHeight, RowHeight));
            }
        }

        switch (tab)
        {
            case HubTab.Checklist:
                RebuildChecklist();
                break;
            case HubTab.Hunting:
                RebuildHunting();
                break;
            default:
                RebuildSettings();
                break;
        }

        FocusTabAnchor(tab);
    }

    /// <summary>Seeds the cursor somewhere valid when a tab opens, and puts it back somewhere valid
    /// after a rebuild that could have pulled the row out from under it. Only ever targets a real
    /// component that is on screen right now.</summary>
    private void FocusTabAnchor(HubTab tab)
    {
        if (!config.InputMode.CursorNavigation)
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
        }

        RestoreListDownwardExit();
        UpdateStopButton();
        RefreshButtonHint();
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
            return;
        }

        var populated = PopulatedRowCount();
        var firstRow = populated > 0 ? NavListBlock.RowIndex(HubNavPlan.List, 0) : HubNavPlan.TabBar;

        var regionEnd = HubNavPlan.Region;
        if (controls is not null)
        {
            regionEnd = NavigationWalker.Apply(controls, HubNavPlan.Region, HubNavPlan.TabBar, firstRow);
        }

        var lastRegionIndex = regionEnd > HubNavPlan.Region ? regionEnd - 1 : HubNavPlan.TabBar;
        if (list.IsVisible)
        {
            list.NavUp = lastRegionIndex;
            RepairLastPopulatedRow(populated, lastRegionIndex);
        }

        LogGraph(controls, regionEnd, populated);
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

    // Verbose, once per rebuild: if a report ever comes back as "the cursor got stuck", the index
    // map at that moment is already in the log and nobody has to reproduce it.
    private void LogGraph(NodeBase? controls, int regionEnd, int populated)
    {
        var controlCount = controls is null ? 0 : NavigationWalker.CountTargets(controls);
        log.Verbose(
            $"Wayfarer nav [{currentTab}]: tabs {HubNavPlan.TabBar}..{HubNavPlan.TabBarLast}, " +
            $"controls {HubNavPlan.Region}..{Math.Max(regionEnd - 1, HubNavPlan.Region)} ({controlCount}), " +
            $"list {HubNavPlan.List} rows {populated}/{list?.OptionNodes.Count ?? 0} of {rows.Count}.");

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

        var previous = lastPopulatedRows;
        list.OptionsList = [.. rows];
        lastPopulatedRows = PopulatedRowCount();

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
            String = "Route me",
            OnClick = OnRouteClicked,
        };
        row.AddNode(routeButton);

        stopButton = new TextButtonNode
        {
            Width = 110f,
            Height = 24f,
            String = "Stop",
            IsEnabled = false,
            OnClick = OnStopClicked,
        };
        row.AddNode(stopButton);

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
        var (label, color) = StatusPresentation(u.Status);
        var where = u.ZoneName is { Length: > 0 } zone ? $"{zone} · " : string.Empty;
        var giver = u.GiverName is { Length: > 0 } name ? $" — {name}" : string.Empty;

        return new HubListRow
        {
            Kind = HubRowKind.Entry,
            Label = $"{u.Def.Unlock}{giver}",
            Detail = $"{where}Lv{u.QuestLevel} · {label}",
            LabelColor = color,
            Activate = navigator is null ? null : () => OnChecklistRowActivated(u),
        };
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

        rows.Add(new HubListRow { Kind = HubRowKind.Heading, Label = "Unverified", Detail = $"{unverified.Count}" });
        foreach (var u in unverified)
        {
            rows.Add(new HubListRow
            {
                Kind = HubRowKind.Note,
                Label = u.Def.Unlock,
                Detail = $"Lv{u.Def.Level}",
            });
        }
    }

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

        huntingHeaderNode = new TextNode
        {
            Height = 22f,
            FontType = FontType.TrumpGothic,
            FontSize = 20,
            TextColor = GameColors.Heading,
            TextOutlineColor = GameColors.HeadingEdge,
            TextFlags = TextFlags.Edge,
        };
        huntingControls.AddNode(huntingHeaderNode);

        var actions = new AlignedHorizontalListNode { Height = 26f, FitToContentHeight = true, ItemSpacing = 8f };
        huntHereButton = new TextButtonNode
        {
            Width = 170f,
            Height = 24f,
            String = "Start hunting",
            OnClick = OnHuntClicked,
        };
        actions.AddNode(huntHereButton);
        huntingControls.AddNode(actions);

        AddTabNode(huntingNodes, huntingControls);
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

        huntingHeaderNode.String = hunting.ActiveLogLabel is { } label
            ? $"{label} — Rank {hunting.CurrentRank}"
            : hunting.NoLogReason ?? "No hunting log active.";

        var navigator = ResolveNavigator();
        var remaining = hunting.HuntHereOrder.Count;
        huntHereButton.String = remaining > 0 ? $"Start hunting ({remaining})" : "Start hunting";
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
                Detail = $"{target.Killed}/{target.Required} · {target.DutyName}",
                Activate = target.DutyContentFinderConditionId is null
                    ? null
                    : () => OpenDuty(target.DutyContentFinderConditionId),
            };
        }

        var row = new HubListRow
        {
            Kind = HubRowKind.Entry,
            Label = target.MonsterName,
            Detail = $"{target.Killed}/{target.Required}",
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
        if (stopButton is null)
        {
            return;
        }

        var engaged = ResolveNavigator()?.Current.Engaged == true;
        if (stopButton.IsEnabled != engaged)
        {
            stopButton.IsEnabled = engaged;
        }
    }

    // ----- Settings tab ----------------------------------------------------------------------
    private void BuildSettingsTab()
    {
        settingsArea = new ScrollingNode<VerticalListNode>
        {
            ContentNode = { FitWidth = true, FitContents = true, ItemSpacing = 4f },
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
        firstSettingControl = null;

        foreach (var section in settings.Build())
        {
            settingsArea.ContentNode.AddNode(BuildHeadingNode(section.Title));
            foreach (var setting in section.Settings)
            {
                settingsArea.ContentNode.AddNode(BuildSettingControl(setting));
            }
        }

        settingsArea.RecalculateSizes();
        ApplyNavigation(settingsArea.ContentNode);
    }

    private NodeBase BuildSettingControl(SettingDefinition setting) => setting.Kind switch
    {
        SettingKind.Toggle => BuildToggle(setting),
        SettingKind.Scale => BuildScale(setting),
        _ => BuildChoice(setting),
    };

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
    private unsafe void ApplyPositionPreset(HubPositionPreset preset)
    {
        if (!IsOpen)
        {
            return;
        }

        const float margin = 12f;
        var screen = new Vector2(AtkStage.Instance()->ScreenSize.Width, AtkStage.Instance()->ScreenSize.Height);
        var position = preset switch
        {
            HubPositionPreset.TopLeft => new Vector2(margin, margin),
            HubPositionPreset.TopRight => new Vector2(screen.X - Size.X - margin, margin),
            HubPositionPreset.BottomLeft => new Vector2(margin, screen.Y - Size.Y - margin),
            HubPositionPreset.BottomRight => new Vector2(screen.X - Size.X - margin, screen.Y - Size.Y - margin),
            _ => (screen - Size) / 2f,
        };
        SetWindowPosition(position);
    }
}
