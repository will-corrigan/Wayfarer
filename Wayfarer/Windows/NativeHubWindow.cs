using System.Globalization;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;
using Wayfarer.Core.Guidance;
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

    /// <summary>The tab bar. The game's own category selectors are 36-pixel radio buttons (Journal
    /// <c>1025</c>, ContentsFinder <c>1029</c>); KamiToolKit's <c>TabBarNode</c> draws a flat strip
    /// rather than that art, so it takes the height of a control instead.</summary>
    private const float TabBarHeight = GameMetrics.Control.DropDownHeight;

    private const float TabBarGap = GameMetrics.Window.RuleGap;

    /// <summary>The Following strip, above the tab bar and on screen whatever tab is open.
    ///
    /// <para>Mirrors the vanilla quest tracker: a persistent line naming what you are on, sitting
    /// above everything else. The player asked "how do I toggle between MSQ and another quest or
    /// unlocks?" while the tab that does it was three tabs from the left and called Quests — the
    /// feature worked and the affordance was invisible. A tab is a <i>place</i>, not a state: it can
    /// say where to go to change something, but it cannot say what the current value is, and the
    /// question was both halves at once.</para></summary>
    private const float StripHeight = GameMetrics.Control.ButtonHeight;

    private const float StripGap = GameMetrics.Window.RuleGap;

    /// <summary>The floor any content area is clamped to — one of the game's own list rows, because
    /// a tab body shorter than a single row is not a tab body.</summary>
    private const float RowHeight = GameMetrics.Row.Height;

    /// <summary>Section headings inside a tab. Journal's own section row sets TrumpGothic 23 in a
    /// 28-tall box (<c>1021 #3</c>), and the Duty Finder's title block does the same at 26
    /// (<c>#24</c>). The taller of the two, because a 22-pixel box was measured in-game to clip this
    /// face's outline top and bottom.</summary>
    private const float HuntingHeaderHeight = GameMetrics.Row.SectionHeight;

    /// <summary>The Unlocks tab's control block: two labelled filter rows, the rule the game draws
    /// under a cluster of controls, and the action row beneath it.
    ///
    /// <para>It used to reserve a <see cref="HuntingHeaderHeight"/> for a heading node this tab
    /// never had, so thirty-six pixels of nothing sat between the buttons and the first row. The
    /// sum is now what is actually in the block, and the rule is the game's own separator —
    /// ContentsFinder stacks its condition controls, draws a 4-pixel rule (<c>#55</c>) and leaves 4
    /// before the block under it.</para></summary>
    private const float ChecklistControlsHeight =
        (GameMetrics.Control.CheckboxHeight * 2f) + GameMetrics.Window.RuleHeight
        + GameMetrics.Control.ButtonHeight + (GameMetrics.Window.RuleGap * 3f);

    private const float HuntingControlsHeight =
        HuntingHeaderHeight + GameMetrics.Control.ButtonHeight + GameMetrics.Window.RuleGap;

    /// <summary>The Following tab's control block: what is being followed, what it wants you to do
    /// next, and the buttons that act on it — a heading, an objective line, a note line and one row
    /// of controls, in that order, because that is the order the three of them are read in.
    ///
    /// <para>The objective used to be a list row, several rows below the heading that named the
    /// thing it belonged to and separated from it by two buttons. A quest, its objective and its
    /// actions are one block and are now drawn as one.</para></summary>
    private const float QuestControlsHeight =
        HuntingHeaderHeight + GameMetrics.Type.BodyLine + GameMetrics.Type.SecondaryLine
        + GameMetrics.Control.ButtonHeight + (GameMetrics.Window.RuleGap * 3f);

    /// <summary>The controller button-hint line along the bottom. One line of the game's dimmed
    /// caption face (Journal <c>1022 #2</c> is Axis 12 in a 21-tall box).</summary>
    private const float ButtonHintHeight = GameMetrics.Row.SecondaryTextHeight;

    // All four are screen pixels, not addon units — see ComputeDefaultSize for why the difference
    // matters. The width cap is absolute rather than a fraction of the viewport because a fraction
    // is exactly what stretched this window across an ultrawide: 60% of 5120px is 3072px of mostly
    // empty list. The viewport fraction is the ceiling either axis may never exceed.
    private const float MinWindowWidth = 460f;
    private const float MaxWindowWidth = 760f;
    private const float MinWindowHeight = 300f;
    private const float ViewportFraction = 0.9f;

    /// <summary>How many frames a page that was asked for and did not appear is retried across.
    /// Roughly half a second at 60fps: far longer than the addon needs to finish a close it is in the
    /// middle of, and short enough that a page which can never open stops being asked for.</summary>
    private const int PendingPageFrames = 30;

    /// <summary>The caption one — and only one — follow choice wears. Exactly one entry carries it at
    /// any moment, and which one is decided by <see cref="MainScenarioReturn.ModeOf"/> rather than by
    /// each entry deciding for itself.</summary>
    private const string Following = "Following";

    /// <summary>Available first, and it is the default: the player's question is "what should I do
    /// next", and the default view used to be "Zone", which answers "what is near me" whether or not
    /// any of it can be done. Then the three browse axes, domain first because a domain is a window
    /// the game already has and the other two are ways of slicing one.</summary>
    private static readonly UnlockGrouping[] GroupModes =
        [UnlockGrouping.AvailableNow, UnlockGrouping.Domain, UnlockGrouping.Zone, UnlockGrouping.Level];

    /// <summary>The seven domain chips, generated from <see cref="UnlockDomains"/> rather than
    /// written out here. Written out, this list could omit a domain — and a domain missing from the
    /// chips is a domain whose entries the player has no way to isolate, with nothing on screen
    /// saying so. The count is asserted against the nav budget by <c>HubNavPlanTests</c>.</summary>
    private static readonly (string Key, string Label)[] DomainChips =
        [.. UnlockDomains.All.Select(d => (d, UnlockDomains.Label(d)))];

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
    private readonly HubRewardIcons rewardIcons;
    private readonly HubJournalFacts journalFacts;

    /// <summary>The journal page, which is its own window rather than a node in this one.
    ///
    /// <para><b>Why it is a second addon.</b> The game's own Journal is two: a plain list on the left
    /// and an ornate parchment page on the right, and <c>Journal.uld</c>'s empty <c>Res</c> node
    /// <c>#9</c> reserves the page's rectangle outright. Drawing the page in here instead meant it had
    /// to fit whatever width this window had been dragged to — so it could not wear the gilt border,
    /// which is authored for one width and one only — and it meant hiding the list to make room, so
    /// the game's own "the cursor moves, the page updates" contract was impossible. Both go away with
    /// a window of its own, and a third thing goes with them: Cancel on a pad closes the addon that
    /// has focus, which is now the page rather than everything.</para>
    ///
    /// <para>Owned here rather than by the plugin because its whole lifetime is "a row of this list
    /// is being read": this window opens it, closes it, positions it, and disposes it.</para>
    /// </summary>
    private readonly JournalWindow journal;

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

    /// <summary>The row the journal window is currently showing, or null when it is closed. Kept so
    /// the cursor can be put back on that row when the window goes away, by whatever route it went.
    ///
    /// <para>Only ever set for a page that is genuinely on screen. It is read as "the page is open",
    /// and that answer stops the tab refreshing — so a row recorded here for a page that never
    /// appeared would freeze the tab on stale rows while the rest of the window carried on looking
    /// alive. <see cref="pendingPage"/> is where a requested-but-not-yet-open page lives.</para>
    /// </summary>
    private HubListRow? pageRow;

    /// <summary>A page that has been asked for but is not open yet, retried once per frame until it
    /// is. Both of the ways an open can fail are transient or terminal rather than instantaneous:
    /// the addon refuses to reopen while its previous close is still finishing, and the page switches
    /// itself off permanently if one of its steps throws. Retrying covers the first; the frame budget
    /// and <see cref="JournalWindow.IsAvailable"/> stop the second turning into a retry every frame
    /// for the rest of the session.</summary>
    private (HubListRow Row, HubRowDetail Detail)? pendingPage;

    /// <summary>Frames left to keep retrying <see cref="pendingPage"/>.</summary>
    private int pendingPageFrames;

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
    private TextNode? questObjectiveNode;
    private TextNode? questNoteNode;
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

    /// <summary>What <see cref="LogDragDiagnostics"/> last wrote, so it only writes again when one
    /// of the three actually changed — the same signature-gating every other per-tick refresh here
    /// uses, and for the same reason: this runs every frame the window is open.</summary>
    private Vector2 lastDiagnosticPosition = new(float.NaN, float.NaN);
    private Vector2 lastDiagnosticSize = new(float.NaN, float.NaN);
    private bool lastDiagnosticOpen = true;

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
        HubRewardIcons rewardIcons,
        HubJournalFacts journalFacts,
        JournalWindow journal,
        IPluginLog log)
    {
        this.journal = journal;
        this.statusIcons = statusIcons;
        this.rewardIcons = rewardIcons;
        this.journalFacts = journalFacts;
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

        // The inset the game gives its own window contents. Journal and the Duty Finder both start
        // everything 16 in from the frame; KamiToolKit's default is 8, and applies X symmetrically
        // where the game uses 16/14 — a two-pixel difference on the right that nothing depends on.
        ContentPadding = new Vector2(GameMetrics.Window.InsetLeft, GameMetrics.Window.BlockGap);

        settings.OnWindowPositionChanged += ApplyPositionPreset;

        journal.OnBack = journal.Close;
        journal.OnClosed = OnJournalClosed;
    }

    /// <summary>Whether the journal window is open on one of this list's rows. Both halves are asked
    /// every time, because this is what stops the tab refreshing: the row is what this window
    /// believes, and the other window's own open state is what is actually on screen. Trusting the
    /// belief alone is how the tab wedged — a page that failed to open left the refresh switched off
    /// with nothing to switch it back on, and the tab went on saying a finished unlock was Available
    /// and a dead hunting target was still there.</summary>
    private bool IsPageOpen => pageRow is not null && journal.IsOpen;

    /// <summary>Where this window actually is on screen, in screen pixels — which is the space the
    /// journal window has to be positioned in, because a second addon's own position is set in the
    /// same space. <see cref="NativeAddon.Size"/> is in addon units and has to be scaled to
    /// match.</summary>
    private Vector2 ScreenPosition =>
        InternalAddon is null ? Vector2.Zero : new Vector2(InternalAddon->X, InternalAddon->Y);

    /// <summary>Whether the cursor should be moved for the player. Both halves matter: the setting is
    /// the player's own opt-out, and the mode is what says a pad is actually in their hands.
    /// </summary>
    private bool TakesFocus =>
        config.InputMode.CursorNavigation && inputMode.Mode == Core.Input.InputMode.Controller;

    /// <inheritdoc/>
    public override void Dispose()
    {
        // Belt-and-suspenders alongside the OnFinalize unsubscribe below: NativeAddon.Close() only
        // starts the native closing animation (finishes several frames later), but Dispose() must
        // leave nothing subscribed to IFramework the moment it returns, regardless of that timing.
        framework.Update -= OnFrameworkUpdate;
        settings.OnWindowPositionChanged -= ApplyPositionPreset;

        // The journal page's window is this window's to own, so it is this window's to take down —
        // and before base.Dispose(), because its callbacks point back in here.
        journal.OnBack = null;
        journal.OnClosed = null;
        journal.Dispose();

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

    /// <summary>Called by a module's <c>Disable()</c> when its own tab (<paramref name="ownedTab"/>)
    /// might be the one on screen. Disabling a module must never destroy the surface the player is
    /// currently interacting with, so this only ever moves the cursor off a tab that has just gone
    /// stale — it never closes the hub. If <paramref name="ownedTab"/> is not the tab currently
    /// showing (the overwhelmingly common case: the only control that reaches this is the Settings
    /// tab's own checkbox, so the player is on Settings, not on the tab being disabled), this is a
    /// no-op and the hub is left exactly as the player left it. Settings is always a safe landing
    /// spot: unlike the module tabs it has no live service data to go stale, and it is where every
    /// reachable caller of this method already is.</summary>
    internal void LeaveTabIfActive(HubTab ownedTab)
    {
        if (!IsOpen)
        {
            return;
        }

        var resolved = TabOwnership.ResolveAfterModuleDisabled(currentTab, ownedTab, HubTab.Settings);
        if (resolved != currentTab)
        {
            SelectTab(resolved);
        }
    }

    /// <summary>The one list of everything Wayfarer can be told to follow — this tab's own rows and
    /// the readout's switcher dropdown both build from this and nothing else, so the two surfaces
    /// can never disagree about what the choices are or what picking one does. Always the same four
    /// kinds, in the same order — the main scenario, the unlock route, the hunting log, then every
    /// accepted quest — whether or not they currently have anything to offer, because a choice that
    /// vanishes when it is empty cannot be learned.
    ///
    /// <para>Deliberately thin: a label, whether it is the one being followed, what activating it
    /// does, and whether there is anything to start right now. The richer per-row cosmetics below —
    /// descriptions, icons, detail panes — stay in this tab, which is the one surface that has room
    /// for them; the dropdown draws its rows from the same fields, so nothing about the pickable set
    /// itself is defined twice.</para>
    ///
    /// <para><b>Every entry acts.</b> "Listed whether or not it has anything to offer" used to mean
    /// listed with a null action, which is a row that can be reached, focused and confirmed and does
    /// nothing — the exact failure this list was supposed to prevent. An entry with nothing to start
    /// now opens the tab that explains why instead, and <see cref="FollowChoice.Ready"/> is what the
    /// rows colour themselves from.</para></summary>
    internal IReadOnlyList<FollowChoice> GetFollowChoices()
    {
        var navigator = ResolveNavigator();
        var choices = new List<FollowChoice>();

        // Which of the four things is actually being followed, from the source that holds the arrow
        // rather than from the followed-quest override alone. That override is null during a hunt and
        // during an unlock route, so reading it as "following the main scenario" made this list say
        // "Main Scenario - Following" in the middle of a hunt AND disable the entry that ends it,
        // which is how a controller player came to have no way home from the readout.
        var mode = navigator?.FollowMode ?? FollowMode.MainScenario;
        var followingMsq = mode == FollowMode.MainScenario;

        // Always live while there is a navigator to drive, whichever mode is running: it is the
        // guaranteed way home, and the reset it performs is the one MainScenarioReturn describes.
        choices.Add(new FollowChoice(
            "Main Scenario",
            followingMsq ? Following : string.Empty,
            followingMsq,
            navigator is null ? null : OnFollowMsqClicked,
            Ready: !followingMsq));

        // Nothing routable does NOT mean nothing to press: the entry opens the Unlocks tab, which is
        // where "nothing available right now" is said in words. An entry that is listed so it can be
        // learned and then does nothing when it is pressed teaches the opposite lesson.
        var routingUnlocks = mode == FollowMode.UnlockRoute;
        var routable = navigator is null
            ? 0
            : ComputeVisibleUnlocks().Count(u => u.Status == UnlockStatus.Available && u.Routable);
        var unlocksReady = routable > 0 && navigator is not null;

        // "8 of 47" rather than "47": this entry starts the same capped plan the button does, so the
        // number beside it has to be the number of stops it will queue. Saying 47 here would put the
        // honest count on one surface and the flattering one on the other.
        choices.Add(new FollowChoice(
            "Unlock Route",
            UnlockRouteCap.Caption(routable),
            routingUnlocks,
            unlocksReady ? OnRouteClicked : OpenUnlocksTab,
            unlocksReady));

        // The RANK's remaining count, because that is what starting a hunt attempts — see
        // HuntingPlan.StartLabel. Counted from the current zone, as it was, this entry went inert the
        // moment the player walked out of the zone she started in, mid-hunt, with the rest of the rank
        // still waiting — which is what "she can't click the arrow to change what to hunt" was.
        var onTheHunt = mode == FollowMode.Hunting;
        var remaining = hunting.RemainingTargets.Count;
        var huntReady = HuntingPlan.CanStart(remaining) && navigator is not null;
        var huntLabel = hunting.ActiveLogLabel is { Length: > 0 } log ? $"Hunting Log - {log}" : "Hunting Log";
        choices.Add(new FollowChoice(
            huntLabel,
            HuntingPlan.CanStart(remaining) ? $"{remaining}" : string.Empty,
            onTheHunt,
            huntReady ? OnHuntClicked : OpenHuntingTab,
            huntReady));

        AddAcceptedQuestChoices(choices, navigator, mode);
        return choices;
    }

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        if (!unlocks.Loaded && !hunting.Loaded)
        {
            AddOwnedNode(new TextNode
            {
                Position = ContentStartPosition,
                Size = new Vector2(ContentSize.X, GameMetrics.Row.EntryHeight),
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

        // The journal page goes with the list it was opened from. The game does the same: its detail
        // addon is closed by its list, not independently, and a parchment page left floating over the
        // world with nothing behind it would be the plainest possible bug.
        DismissJournalPage();

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

        ForgetTree();
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
        var perRow = HubListRowNode.ItemHeight + GameMetrics.Row.Spacing;
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

    /// <summary>What colour a row's <b>name</b> is, which is very nearly always the list's own.
    ///
    /// <para>The name used to take the state's colour outright, and since most of the catalogue is
    /// locked at any given level that meant most names were drawn in the same dimmed grey as the
    /// description underneath them — "the same weight and nearly the same colour, so the eye has
    /// nothing to land on". The state has a shape and a column of its own to be said in. The one
    /// exception left is a permanently missed entry, where the row is not merely waiting and the
    /// game's own red is the honest thing to draw it in.</para></summary>
    private static Vector4 NameColor(UnlockStatus status) =>
        UnlockStatusDisplay.Tone(status) == UnlockStatusTone.Bad ? GameColors.Bad : GameColors.ListText;

    /// <summary>A category key in the word the filter chip uses for it. Grouping by the key alone put
    /// lowercase "content" and "cosmetic" on screen as section headings.</summary>
    /// <summary>What a row's right-hand caption column is allowed to hold: a count, and nothing
    /// else.
    ///
    /// <para>The column is 48 pixels — Journal <c>1023 #4</c> — and a word does not fit in it. A
    /// <see cref="FollowChoice"/>'s detail is a count for three of the four kinds and the word
    /// "Following" for the one that is currently being followed, and that word came out as
    /// "Follow…": the same defect as the Unlocks tab's "Lv 53…", one column over. Being followed is
    /// already said three ways — the row's name is drawn in the green reserved for it, its marker
    /// is the in-progress one, and the strip above every tab names it in full — so what this drops
    /// is a truncated fourth.</para></summary>
    /// <summary>How a followable row states itself: what is being followed reads as in progress, what
    /// can be started reads as available, and what has nothing to start reads as done. One answer for
    /// the two rows that share the shape, so neither can drift into claiming to be available while it
    /// is the thing already running.</summary>
    private static UnlockStatus FollowRowStatus(FollowChoice choice) => choice switch
    {
        { IsFollowed: true } => UnlockStatus.Accepted,
        { Ready: true } => UnlockStatus.Available,
        _ => UnlockStatus.Done,
    };

    /// <summary>The pane's button, when there is something for it to start. Absent while this is what
    /// is already being followed — the way out of that is the Stop on the strip above, and a button
    /// offering to start what is already running would be a press that does nothing.</summary>
    private static IReadOnlyList<HubDetailAction> FollowRowActions(FollowChoice choice, string label) =>
        !choice.IsFollowed && choice.Ready && choice.Activate is { } activate
            ? [new HubDetailAction(label, activate)]
            : [];

    private static string CountCaption(string detail) =>
        detail.Length > 0 && detail.All(char.IsAsciiDigit) ? detail : string.Empty;

    /// <summary>The status whose icon stands for a band. Borrowed from the states in it rather than
    /// invented, so the shape over a band heading is the same shape as on the rows beneath it.</summary>
    private static UnlockStatus BandIconStatus(UnlockBand band) => band switch
    {
        UnlockBand.Available => UnlockStatus.Available,
        UnlockBand.Blocked => UnlockStatus.QuestLocked,
        UnlockBand.NotKnown => UnlockStatus.RequirementsUnknown,
        _ => UnlockStatus.Done,
    };

    /// <summary>The catalogue's nine <c>type</c> values in the game's own words. A closed set — the
    /// dataset validator enforces it — so there is no unknown branch to design for, only a default
    /// that would mean the catalogue had grown a tenth.</summary>
    private static string UnlockTypeWord(string type) => type switch
    {
        "dungeon" => "Dungeon",
        "trial" => "Trial",
        "raid" => "Raid",
        "alliance-raid" => "Alliance Raid",
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
    /// plainly locked and lists nothing reads as a bug.
    ///
    /// <para>An Available entry still carrying a knowable-but-unverifiable condition (a partner, or
    /// a future requirement of the same shape) is the one exception to "Available lists nothing
    /// here": <see cref="ResolvedUnlock.AvailableConditionDetail"/> is the full statement of that
    /// condition — the game's own words where one was resolved — and this is the requirement-list
    /// pane it belongs in, even though nothing here is actually blocking the entry.</para></summary>
    private static List<string> MissingFor(ResolvedUnlock u)
    {
        if (u.Status is UnlockStatus.Available or UnlockStatus.Accepted or UnlockStatus.Done)
        {
            return u.AvailableConditionDetail is { Length: > 0 } detail ? [detail] : [];
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
            return u.Def.Requires?.Unverifiable == true ? "No quest giver." : string.Empty;
        }

        if (giver is null)
        {
            return $"In {zone}";
        }

        return zone is null ? $"From {giver}" : $"From {giver} · {zone}";
    }

    // The thirteen-way status sort that used to order a group is gone. It ordered the rows correctly
    // and marked nothing, so a player scrolling a zone saw the available entries run into the locked
    // ones with no line between them — and the three entries nothing could grade sorted into the
    // middle of the locked ones, reading as "locked" without ever having said so. Bands say it:
    // UnlockSections.Band.

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
            Height = SettingsLayout.ControlHeight(SettingKind.Choice),

            // Widened by ApplySettingWidths to the container; this is only the seed.
            Width = GameMetrics.Control.ButtonWidthLarge,
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

    /// <summary>A row of buttons, at the game's own button height with the smallest gap that reads
    /// as two buttons — see <see cref="GameMetrics.Control.ButtonGap"/> for why the game's own zero
    /// does not work here.</summary>
    private static AlignedHorizontalListNode NewActionRow() => new()
    {
        Height = GameMetrics.Control.ButtonHeight,
        FitToContentHeight = true,
        ItemSpacing = GameMetrics.Control.ButtonGap,
    };

    /// <summary>One labelled row of filter chips.
    ///
    /// <para><b>Why the label.</b> Two rows of bare checkboxes read as one undifferentiated block —
    /// nothing on screen said that the first row narrowed by <i>kind</i> and the second by <i>how
    /// much it matters</i>, so eight identical controls looked like eight of the same thing. The
    /// game labels a cluster of controls rather than leaving it to be inferred, in the dimmed
    /// caption face and in a fixed column so the chips beside each label start at the same x. The
    /// column is <see cref="GameMetrics.Detail.KindWidth"/> — ContentsFinder's own 64-wide caption
    /// column, which is the width the game uses for exactly this.</para></summary>
    private static AlignedHorizontalListNode BuildFilterRow(string label, IEnumerable<CheckboxNode> chips)
    {
        var row = new AlignedHorizontalListNode
        {
            Height = GameMetrics.Control.CheckboxHeight,
            FitToContentHeight = true,
            ItemSpacing = GameMetrics.Window.BlockGap,
        };

        row.AddNode(new TextNode
        {
            Width = GameMetrics.Detail.KindWidth,
            Height = GameMetrics.Control.CheckboxHeight,
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.SecondarySize,
            LineSpacing = GameMetrics.Type.SecondaryLine,
            AlignmentType = AlignmentType.Left,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.Dimmed,
            String = label,
        });

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
        FontSize = GameMetrics.Type.TitleSize,
        AlignmentType = AlignmentType.TopLeft,
        TextColor = GameColors.Heading,
        TextOutlineColor = GameColors.HeadingEdge,
        TextFlags = TextFlags.Edge,
        String = HeadingText.Plain(text),
    };

    /// <summary>Drops every reference into the node tree the game has just deallocated. Nothing here
    /// frees anything — <see cref="OnFinalize"/> has already done that — but a field still pointing
    /// at a disposed node is a pointer into memory the game has reclaimed, and the window is rebuilt
    /// from scratch on the next open.</summary>
    private void ForgetTree()
    {
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
        pageRow = null;
        pendingPage = null;
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
        questObjectiveNode = null;
        questNoteNode = null;
        followMsqButton = null;
        teleportButton = null;
        dutyFinderButton = null;
        stripStopButton = null;
        settingsArea = null;
        firstSettingControl = null;
        buttonHintNode = null;
    }

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
            FontSize = GameMetrics.Type.SecondarySize,
            LineSpacing = GameMetrics.Type.SecondaryLine,
            AlignmentType = AlignmentType.Right,
            TextColor = GameColors.Dimmed,
            String = ControllerGlyphs.WindowHint(lastReverseConfirmCancel),
            IsVisible = inputMode.Mode == Core.Input.InputMode.Controller,
        };
        AddOwnedNode(buttonHintNode);
    }

    /// <summary>Keeps the hint line saying what the buttons actually do.
    ///
    /// <para>One wording, where there used to be two. The journal page was drawn inside this window,
    /// so Cancel on a pad closed the whole thing and the hint had to say <i>Close</i> while the page
    /// was up — otherwise the player would press it once and lose the window. The page is its own
    /// addon now, and Cancel closes whichever of the two has focus, so "Back" is true on both
    /// surfaces and the special case is gone with the thing that needed it.</para></summary>
    private void RefreshButtonHint()
    {
        if (buttonHintNode is null)
        {
            return;
        }

        buttonHintNode.IsVisible = inputMode.Mode == Core.Input.InputMode.Controller;
        if (inputMode.ReverseConfirmCancel == lastReverseConfirmCancel)
        {
            return;
        }

        lastReverseConfirmCancel = inputMode.ReverseConfirmCancel;
        buttonHintNode.String = ControllerGlyphs.WindowHint(lastReverseConfirmCancel);
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

            ItemSpacing = GameMetrics.Row.Spacing,

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
            FontSize = GameMetrics.Type.BodySize,
            LineSpacing = GameMetrics.Type.BodyLine,
            AlignmentType = AlignmentType.Left,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.ListText,
            String = "Following: Main Scenario",
        };
        AddOwnedNode(stripLabelNode);

        stripControls = new AlignedHorizontalListNode
        {
            Height = GameMetrics.Control.ButtonHeight,
            FitToContentHeight = true,
            ItemSpacing = GameMetrics.Control.ButtonGap,
        };

        stripControls.AddNode(new TextButtonNode
        {
            Width = GameMetrics.Control.ButtonWidthSmall,
            Height = GameMetrics.Control.ButtonHeight,
            String = "Change",

            // Goes to the tab that owns the choice rather than opening a popup: a popup has to be
            // registered into the host addon's focusable nodes before a cursor can enter it, and a
            // popup a controller cannot reach is the trap this whole window exists to avoid.
            OnClick = () => SelectTab(HubTab.Quests),
        });

        // The one Stop, on the same line that says what is running. There used to be three.
        stripStopButton = new TextButtonNode
        {
            Width = GameMetrics.Control.ButtonWidthSmall,
            Height = GameMetrics.Control.ButtonHeight,
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
        foreach (var line in feed.Compose().Lines)
        {
            if (line.Emphasis == ReadoutEmphasis.Heading)
            {
                return line.Text;
            }
        }

        return "nothing yet";
    }

    /// <summary>Notes which row the cursor is on, and moves the journal page with it. One shared
    /// delegate is handed to every row of a rebuild rather than a closure each.
    ///
    /// <para><b>There used to be a detail strip across the bottom of the window for this to fill.</b>
    /// It is gone from every tab now. The Unlocks and Following tabs had already given theirs up —
    /// 291 pixels, six of the game's own rows, for a summary of a page one press away — and the
    /// Hunting Log was the last tab carrying one. On that tab it restated the row and nothing else:
    /// the creature's name was the row's name, "3 of 5 killed" was the row's own count, the zone was
    /// the row's second line, and its one button did exactly what confirming the row already did.
    /// The player could not tell what the bottom half of the tab was for, which is the correct
    /// reading of a panel that says nothing new. Removing it gives those 291 units back to the
    /// list, which is what stopped a rank's targets fitting without a scroll bar.</para></summary>
    private void PublishDetail(HubListRow row)
    {
        if (ReferenceEquals(row, hoveredRow))
        {
            return;
        }

        hoveredRow = row;

        // The journal window follows the cursor — that is the game's own contract for its two-window
        // journal, and it is the whole reason the page is a second addon rather than a node in here.
        FollowJournal(row);
    }

    /// <summary>Forgets which row the cursor was on. Called whenever the list is rebuilt, because
    /// that row may no longer exist.</summary>
    private void ResetDetail() => hoveredRow = null;

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
                Label = "Turn Quest Helper on in Settings to be guided from here.",
            });
        }
    }

    /// <summary>Confirm on a row. An unlock entry opens the journal window beside this one;
    /// everything else — a heading, a hunting target, a quest — acts, as it always did.
    ///
    /// <para>This is the game's own contract: the Journal's list <i>selects</i> and its page
    /// <i>acts</i>.</para></summary>
    private void OnRowClicked(HubListRow? row)
    {
        list?.ClearSelection();

        if (row is { OpensPage: true, Pane: { } detail })
        {
            OpenJournal(row, detail);
            return;
        }

        row?.Activate?.Invoke();
    }

    /// <summary>Opens the journal window on one row, beside this one.
    ///
    /// <para>The focus is only taken on a controller, and that is the difference between the two
    /// input modes on this surface: a pad has nowhere else to be, so the cursor is moved into the
    /// page and Cancel brings it back; a mouse player has just clicked a row and is still holding the
    /// mouse, so taking their focus would be taking it from under them.</para></summary>
    private void OpenJournal(HubListRow row, HubRowDetail detail)
    {
        journal.Show(detail, TakesFocus);

        // Asked, not necessarily done. Open() allocates the addon on the frame it is called, but it
        // refuses outright while a previous close is still finishing, and the page switches itself
        // off if one of its own steps throws. So the row is only recorded as the open page once the
        // page says it is open; otherwise it is parked and retried, and until then the tab keeps
        // refreshing rather than freezing on rows the player can no longer trust.
        if (journal.IsOpen)
        {
            pageRow = row;
            pendingPage = null;
        }
        else
        {
            pageRow = null;
            pendingPage = (row, detail);
            pendingPageFrames = PendingPageFrames;
        }

        journal.PlaceBeside(ScreenPosition, Size * UiScale());
        RefreshButtonHint();
    }

    /// <summary>One more attempt at a page that was asked for and did not appear, run per frame
    /// until it does or the budget runs out.
    ///
    /// <para>The budget exists so that a page which can never open does not turn into a
    /// <c>Show</c> call every frame for the rest of the session. Half a second at 60fps is far more
    /// than a hide transition needs and short enough that a player who pressed confirm and got
    /// nothing is not left wondering.</para></summary>
    private void RetryPendingPage()
    {
        if (pendingPage is not { } pending)
        {
            return;
        }

        if (journal.IsOpen)
        {
            pageRow = pending.Row;
            pendingPage = null;
            RefreshButtonHint();
            return;
        }

        pendingPageFrames--;
        if (pendingPageFrames <= 0 || !journal.IsAvailable)
        {
            pendingPage = null;
            log.Warning(
                $"Wayfarer hub: the journal page for '{pending.Detail.Title}' did not open, so the list is left as "
                + "it was. Press confirm again to retry.");
            return;
        }

        journal.Show(pending.Detail, TakesFocus);
    }

    /// <summary>Keeps the open journal window on whatever the cursor is now over — the game's own
    /// contract, and the thing that was impossible while the page lived inside this window and had to
    /// hide the list to be seen.
    ///
    /// <para>Focus is deliberately <i>not</i> taken here: the player is moving the cursor in the
    /// list, and moving it into the page under them would strand them.</para></summary>
    private void FollowJournal(HubListRow row)
    {
        if (!journal.IsOpen || row.Pane is not { } detail || !row.OpensPage)
        {
            return;
        }

        pageRow = row;
        journal.Show(detail, takeFocus: false);
    }

    /// <summary>The journal window has gone away — by its own Back button, by Cancel on a pad, or by
    /// the game's close-all. Whichever it was, the cursor belongs back on the row it was opened
    /// from.</summary>
    private void OnJournalClosed()
    {
        var row = pageRow;
        pageRow = null;

        // A page still waiting to open is a page the player has since closed. Dropping it here is
        // what stops the retry re-opening a window that has just been dismissed.
        pendingPage = null;

        // The page can be closed while this window is on its way out — DismissJournalPage runs from
        // OnFinalize — and the game's close is not instantaneous, so the callback can land after this
        // window's tree has gone. Nothing below is worth touching a freed node for.
        if (!IsOpen || row is null)
        {
            return;
        }

        RefreshButtonHint();
        FocusRow(row);
    }

    /// <summary>Puts the cursor back on the row the journal was opened from. The list is not rebuilt
    /// while the journal is open, so the row is still in the same recycled node — and when it is not
    /// (a background refresh moved it), the tab's own anchor is the fallback rather than a cursor
    /// left nowhere.</summary>
    private void FocusRow(HubListRow row)
    {
        if (list is null || !TakesFocus)
        {
            return;
        }

        foreach (var node in list.OptionNodes)
        {
            if (node.IsVisible && ReferenceEquals(node.ItemData, row))
            {
                node.TakeFocus();
                return;
            }
        }

        FocusTabAnchor(currentTab);
    }

    /// <summary>Shuts the journal window without waiting for it to say so, for callers that are about
    /// to re-lay out and renumber anyway — a tab switch, or a rebuild that is about to replace the
    /// very row the page was built from.</summary>
    private void DismissJournalPage()
    {
        // A page that was asked for and has not appeared yet is dropped too: it was built from a row
        // this caller is about to replace, so letting the retry succeed later would put a page on
        // screen describing an object that no longer exists.
        pendingPage = null;

        if (pageRow is null)
        {
            return;
        }

        pageRow = null;
        if (journal.IsOpen)
        {
            journal.Close();
        }
    }

    private List<NodeBase> TabNodes(HubTab tab) => tab switch
    {
        HubTab.Checklist => checklistNodes,
        HubTab.Hunting => huntingNodes,
        HubTab.Quests => questNodes,
        _ => settingsNodes,
    };

    private VerticalListNode? TabControls(HubTab tab) => tab switch
    {
        HubTab.Checklist => checklistControls,
        HubTab.Hunting => huntingControls,
        HubTab.Quests => questControls,
        _ => settingsArea?.ContentNode,
    };

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

        // The journal page is a separate addon now, so a resize of this window moves it rather than
        // re-laying it out: it keeps its own fixed width and its own height whatever happens here.
        if (journal.IsOpen)
        {
            journal.PlaceBeside(ScreenPosition, Size * UiScale());
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

        PositionList(controlsHeight);
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

        // The two buttons the strip carries, plus the gap between them — derived rather than
        // restated, so moving a button width cannot leave the strip label overlapping it.
        const float ControlsWidth =
            (GameMetrics.Control.ButtonWidthSmall * 2f) + GameMetrics.Control.ButtonGap;

        stripControls.Position = new Vector2(tabContentStart.X + tabContentSize.X - ControlsWidth, top);
        stripControls.Size = new Vector2(ControlsWidth, StripHeight);
        stripControls.RecalculateLayout();

        if (stripLabelNode is not null)
        {
            stripLabelNode.Position = new Vector2(tabContentStart.X, top + GameMetrics.Row.TextTop);
            stripLabelNode.Size = new Vector2(
                Math.Max(tabContentSize.X - ControlsWidth - GameMetrics.Window.BlockGap, 0f),
                GameMetrics.Row.TextHeight);
        }
    }

    /// <summary>Puts the list under the tab's control block, filling the rest of the tab body.
    /// </summary>
    private void PositionList(float controlsHeight)
    {
        if (list is not { IsVisible: true })
        {
            return;
        }

        // The list keeps a fixed viewport and scrolls what does not fit — it is the one thing in
        // here that must never grow the window, because "the window grew instead of scrolling"
        // is precisely what put it off the edge of the screen.
        var available = tabContentSize.Y - controlsHeight;
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

    /// <summary>What the open tab actually needs, so the window can shrink to it.
    ///
    /// <para>This used to open with an arm that floored the height at what a full journal entry
    /// needed, because the page was drawn in here and the list had to hand over its rectangle to
    /// make room. That is what the list gave up for the page, and it is what the page moving into a
    /// window of its own gives back: the list asks for exactly the room its own rows want, and
    /// opening an entry no longer changes the shape of this window at all.</para></summary>
    private float TabBodyHeight()
    {
        return currentTab switch
        {
            HubTab.Settings => settingsArea?.ContentNode.Height ?? tabContentSize.Y,
            _ => ControlsHeight(currentTab) + ListHeightForRows(),
        };
    }

    private float ListHeightForRows()
    {
        var spacing = list?.ItemSpacing ?? 1f;

        // Capped rather than unbounded: a checklist can hold hundreds of rows and the window is not
        // allowed to ask for a screen it does not have. Past the cap the list scrolls, which is the
        // behaviour that was missing.
        //
        // Expressed as a list height rather than a row count, so it does not have to be re-derived
        // every time the row height moves: 630 addon units is the amount of list the window has
        // always asked for, and the cap is however many of the game's rows fit in it.
        const float MaxListHeight = 630f;
        var maxRows = (int)(MaxListHeight / (HubListRowNode.ItemHeight + spacing));
        var count = Math.Clamp(rows.Count, 1, Math.Max(maxRows, 1));
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
        // A tab switch is a hard exit from the journal page: the entry it is showing belongs to a
        // list that is about to be rebuilt or put away.
        DismissJournalPage();

        currentTab = tab;
        hubTabs?.SelectTab(TabLabel(tab));

        // The virtual list is shared across every list-backed tab and lives outside the per-tab
        // buckets SetBucketVisible walks. Settings has no list of its own to hide it with,
        // so without this the list stays visible (and clickable) under the Settings tab's controls
        // forever after the first time any list tab is shown.
        if (list is not null)
        {
            list.IsVisible = TabOwnership.IsVisibleOn(tab, HubTab.Settings);
        }

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
        LogDragDiagnostics();

        // Belt-and-suspenders against a defect neither side of this window's own code can cause:
        // NativeAddon's Hide() hook forces a Close() on ANY native call to the addon's Hide vtable
        // slot, and that Close() only starts the closing animation — the deallocation that would
        // unsubscribe this handler (OnFinalize) runs several frames later. So there is a real window
        // in which this addon has gone not-visible but this method keeps being called. A journal
        // page left open beside a host that is no longer there is worse than no page — it looks
        // alive — so it is put away the moment this is noticed, and nothing else here touches native
        // state for an addon that is on its way out.
        if (!IsOpen)
        {
            DismissJournalPage();
            return;
        }

        // Before the refresh, because it is what decides whether there is a page open to refresh
        // around: a page asked for on the frame the previous one was still closing arrives here.
        RetryPendingPage();

        RefreshTab();
        RestoreListDownwardExit();
        UpdateStopButton();
        RefreshFollowingStrip();
        RefreshButtonHint();

        // Cheap, and only ever writes when the window is genuinely outside the viewport — which is
        // what makes it safe to run every tick. It catches a resolution or interface-scale change
        // under an open window, the case a one-shot clamp on open cannot.
        ClampIntoViewport();

        // Same reasoning for the journal page: it is a separate addon, so it does not move when this
        // window is dragged, and PlaceBeside writes nothing when the answer has not changed. This is
        // also what catches the frame in which the page has just opened — Open() is not guaranteed to
        // have made it open by the time the caller's next line runs.
        if (journal.IsOpen)
        {
            journal.PlaceBeside(ScreenPosition, Size * UiScale());
        }
    }

    /// <summary>Writes this window's position, size and open state to the log the moment any of
    /// them changes, so a report of the window disappearing mid-drag can be matched against exactly
    /// what the addon was doing at the time — hidden outright, or merely moved somewhere degenerate
    /// (off-screen, zero-sized). Gated behind the same verbose-diagnostics switch the readout's own
    /// per-change logging uses, and cheap enough to call unconditionally: the early-out is a handful
    /// of comparisons, and the game is only ever asked to format a string when one of them differs
    /// from the last tick's.</summary>
    private unsafe void LogDragDiagnostics()
    {
        if (!config.QuestHelper.LogDiagnostics || InternalAddon is null)
        {
            return;
        }

        var position = ScreenPosition;
        var open = InternalAddon->IsVisible;

        if (position == lastDiagnosticPosition && Size == lastDiagnosticSize && open == lastDiagnosticOpen)
        {
            return;
        }

        lastDiagnosticPosition = position;
        lastDiagnosticSize = Size;
        lastDiagnosticOpen = open;

        log.Information(
            $"Wayfarer hub: pos={position.X:0}x{position.Y:0} size={Size.X:0}x{Size.Y:0} "
            + $"scale={InternalAddon->Scale:0.###} visible={open} journalOpen={journal.IsOpen}.");
    }

    /// <summary>The open tab's per-tick refresh.
    ///
    /// <para>Skipped entirely while the journal page is open. The page is built from a row object in
    /// the list underneath it, and a rebuild replaces every one of those objects — so a level-up or
    /// a zone change would leave the page describing something that no longer exists, or yank it
    /// away from under a player who was reading it. The signature is left stale and the rebuild
    /// happens on the first tick after the page closes.</para></summary>
    private void RefreshTab()
    {
        if (IsPageOpen)
        {
            return;
        }

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
            RemoveFromCursorGraph(controls);
            return;
        }

        ApplyStripNavigation();

        if (hubTabs is not null)
        {
            hubTabs.NavDown = HubNavPlan.Region;
        }

        var populated = PopulatedRowCount();

        // A hidden list is not a destination. IsVisible is asked as well as the row count because
        // the two disagree on the Settings tab: SelectTab hides the list without emptying it, so the
        // count is still whatever the previous tab published, and "down" from the last setting
        // pointed at the first row of a list that is not on screen.
        //
        // With no list under it, "down" out of the region goes NOWHERE rather than to the tab bar.
        // The tab bar is at the TOP of the window, so pointing there made the last control's down
        // press throw the cursor to the top of the tab — which is exactly the report: "she gets as
        // far down as the hud position toggle bit and then the next down press takes her back to the
        // top of the menu". The game's own scrolling lists stop at their last row; this one now does
        // too, and up still walks back to the tabs the way it always did.
        var listBelow = populated > 0 && list.IsVisible;
        var regionExit = listBelow ? NavListBlock.RowIndex(HubNavPlan.List, 0) : NavGraphPlanner.NoNavigation;

        var regionEnd = HubNavPlan.Region;
        if (controls is not null)
        {
            regionEnd = NavigationWalker.Apply(
                controls,
                HubNavPlan.Region,
                HubNavPlan.TabBar,
                regionExit,
                HubNavPlan.Region + HubNavPlan.RegionCapacity - 1);
        }

        var lastRegionIndex = regionEnd > HubNavPlan.Region ? regionEnd - 1 : HubNavPlan.TabBar;
        ApplyListNavigation(populated, lastRegionIndex, HubNavPlan.TabBar);
        LogGraph(controls, regionEnd, populated);
    }

    /// <summary>The list's own two exits, published and then repaired, in that order.</summary>
    private void ApplyListNavigation(int populated, int lastRegionIndex, int paneEntry)
    {
        if (list is null)
        {
            return;
        }

        if (list.IsVisible)
        {
            list.NavUp = lastRegionIndex;

            // Down out of the list lands on the pane's buttons when there are any, and on the tab
            // bar when there are not. Left and right stay pinned to the tab bar whatever happens:
            // that is the escape hatch no graph defect can take away, and the pane must not become
            // a second way to get stuck.
            list.NavDown = paneEntry;
        }

        // Publish before repairing, never after: publishing renumbers every row node from the list's
        // own values, which would undo the repair.
        PublishOwnLinks();

        if (list.IsVisible)
        {
            RepairLastPopulatedRow(populated, lastRegionIndex);
        }
    }

    /// <summary>Makes the tab bar's and the list's own <c>Nav*</c> assignments actually take effect.
    ///
    /// <para>Neither type acts on the assignment. <c>TabBarNode</c> copies its values onto its radio
    /// buttons inside a <b>private</b> <c>RecalculateLayout()</c>, and <c>ListNode</c> copies its own
    /// onto its two scroll sentinels inside a <b>private</b> <c>RecalculateScroll()</c>; the only
    /// public trigger for either is a size change, and this window's layout pass fires that
    /// <i>before</i> the numbering rather than after. So every value written above was one generation
    /// stale: on the first open the tab bar's "up" was still the <c>NoNavigation</c> it was built
    /// with, which made the Following strip — and the Stop button, the universal exit — unreachable
    /// by pad until the player happened to switch tab; and after a tab switch "up" out of the list
    /// still carried the previous tab's last control index, which is a dead direction.</para>
    ///
    /// <para>Re-assigning the size is the trigger, and it is safe to do every time: the setter fires
    /// unconditionally rather than on a change, the recalculations are idempotent, and neither
    /// rebuilds anything while the size is the size it already was.</para></summary>
    private void PublishOwnLinks()
    {
        if (hubTabs is not null)
        {
            hubTabs.Size = new Vector2(hubTabs.Width, hubTabs.Height);
        }

        if (list is not null)
        {
            list.Size = new Vector2(list.Width, list.Height);
        }
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

    /// <summary>Takes the window out of the cursor graph entirely when the player has turned
    /// controller navigation off.
    ///
    /// <para>"Off" has to mean unwired, not half-wired. The tab bar and the list are given their
    /// indices when they are built, before this setting is consulted, and only the control region
    /// is numbered from here — so simply declining to number the region left a window that was
    /// still navigable but with a hole in the middle of it: down from the tabs pointed at index 10,
    /// nothing occupied index 10, and the list was unreachable from the tab bar. Someone turning
    /// this off to fix a problem got a worse window, not a plainer one. Now they get the mouse
    /// window, which is what the setting says.</para>
    ///
    /// <para>Which means every region has to be unwired and not merely left unnumbered. The Following
    /// strip and the control region both keep whatever indices the last numbering pass gave them, so
    /// declining to renumber leaves live nav targets with nothing pointing at them — the same
    /// half-wired window from the other direction.</para></summary>
    private void RemoveFromCursorGraph(NodeBase? controls)
    {
        if (stripControls is not null)
        {
            NavigationWalker.Remove(stripControls);
        }

        if (controls is not null)
        {
            NavigationWalker.Remove(controls);
        }

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

    /// <summary>Takes out any heading with nothing under it, immediately before the rows are
    /// published.
    ///
    /// <para>A section header is a promise that something follows it, and a list that breaks that
    /// promise reads as content that failed to load — the field report was a heading with, in the
    /// player's own words, nothing under it. Enforced here rather than at each of the four places
    /// that build rows, because the guarantee wanted is about the list and not about any one
    /// builder: whatever put a heading there, it does not reach the screen without content.</para>
    ///
    /// <para>Back to front so a heading left orphaned by dropping the heading beneath it goes as
    /// well — an empty section at the end of a list of empty sections must not survive because it
    /// was followed by another heading at the moment it was checked.</para>
    ///
    /// <para>A heading that can be <i>activated</i> stays whatever is under it. The collapsed
    /// Unverified section is one: it is a control rather than a label, its whole job is to have
    /// nothing under it until it is pressed, and taking it away would take the way back with
    /// it.</para></summary>
    private void DropEmptyHeadings()
    {
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i].Kind != HubRowKind.Heading || rows[i].Activate is not null)
            {
                continue;
            }

            if (i + 1 >= rows.Count || rows[i + 1].Kind == HubRowKind.Heading)
            {
                rows.RemoveAt(i);
            }
        }
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

        DropEmptyHeadings();

        // The page was built from a row in the list that is about to be replaced. Reaching here with
        // it open should not be possible — the per-tick refresh leaves the tab alone while it is up
        // — but a page describing an object that no longer exists is the one state worth spending a
        // line to make unreachable.
        DismissJournalPage();

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
            ItemSpacing = GameMetrics.Window.RuleGap,
            Position = tabContentStart,
            Size = new Vector2(tabContentSize.X, ChecklistControlsHeight),
        };

        checklistControls.AddNode(BuildFilterRow("Domain", BuildDomainChips()));
        checklistControls.AddNode(BuildFilterRow("Priority", BuildPriorityChips()));

        // The game's own separator between a cluster of controls and the block under it —
        // ContentsFinder draws one (#55) between its condition checkboxes and the row of buttons
        // beneath them. It is what makes two rows of chips read as a filter cluster rather than as
        // the top of an undifferentiated pile of controls.
        checklistControls.AddNode(new HorizontalLineNode { Height = GameMetrics.Window.RuleHeight });
        checklistControls.AddNode(BuildChecklistActionRow());

        AddTabNode(checklistNodes, checklistControls);
    }

    private AlignedHorizontalListNode BuildChecklistActionRow()
    {
        var row = NewActionRow();

        // "Complete" is not a category and was never one — it says whether finished entries are
        // listed at all, which is a property of the list rather than of what is in it. It sat at
        // the head of the category row purely because that row had space, and that is what stopped
        // the two chip rows from reading as "kinds" and "priorities".
        row.AddNode(new CheckboxNode
        {
            // The button row's height rather than the checkbox row's, because that is the row it is
            // in: AlignedHorizontalListNode pins every child to y=0, so a 24 beside a 28 sits four
            // pixels high. The game varies this itself — Journal places its checkbox at 20 and
            // MonsterNoteBook at 24 — so the control takes the height of the block it belongs to.
            Height = GameMetrics.Control.ButtonHeight,
            String = "Show complete",
            IsChecked = filter.ShowDone,
            OnClick = isOn =>
            {
                filter.ShowDone = isOn;
                RebuildChecklist();
            },
        });

        // A cycling button rather than a nested tab bar: a TabBarNode consumes one index per tab,
        // which the walker (which numbers one index per element) cannot account for — nesting one
        // inside a numbered region would overlap the elements that follow it.
        groupButton = new TextButtonNode
        {
            Width = GameMetrics.Control.ButtonWidthMedium,
            Height = GameMetrics.Control.ButtonHeight,
            String = GroupButtonLabel(),
            OnClick = () =>
            {
                groupMode = (groupMode + 1) % GroupModes.Length;
                RebuildChecklist();
            },
        };
        row.AddNode(groupButton);

        routeButton = new TextButtonNode
        {
            Width = GameMetrics.Control.ButtonWidthMedium,
            Height = GameMetrics.Control.ButtonHeight,
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

    private IEnumerable<CheckboxNode> BuildDomainChips()
    {
        foreach (var (key, label) in DomainChips)
        {
            var chipKey = key;
            yield return new CheckboxNode
            {
                Height = GameMetrics.Control.CheckboxHeight,
                String = label,
                IsChecked = filter.Domains.Contains(chipKey),
                OnClick = isOn =>
                {
                    ToggleMembership(filter.Domains, chipKey, isOn);
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
                Height = GameMetrics.Control.CheckboxHeight,
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
                Label = "The unlock catalogue could not be read.",
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
        AddChecklistSectionRows(visible, navigator);
        AddUnverifiedRows();

        if (rows.Count == 0)
        {
            rows.Add(new HubListRow { Kind = HubRowKind.Note, Label = EmptyListNote() });
        }

        PublishRows(checklistControls);
        lastChecklistSignature = ComputeChecklistSignature();
    }

    /// <summary>The group headings, the band headings and the rows, flattened into the list in the
    /// order <see cref="UnlockSections"/> put them in. Nothing is decided here — that is the point of
    /// the sections existing — so this stays a walk over a shape a test already checked.</summary>
    private void AddChecklistSectionRows(List<ResolvedUnlock> visible, INavigationProvider? navigator)
    {
        foreach (var group in ChecklistSections(visible))
        {
            rows.Add(new HubListRow
            {
                Kind = HubRowKind.Heading,
                Label = group.Heading,
                Detail = group.Count.ToString(CultureInfo.InvariantCulture),
            });

            foreach (var band in group.Bands)
            {
                // A band heading on every band, including the one whose name is the good news — the
                // "Not known" band is the reason the headings exist at all, and a row cannot say
                // "nothing checked this" about itself without repeating the sentence down the whole
                // band. Suppressed only in the Available-now view, whose own heading already said it.
                if (group.ShowBandHeadings)
                {
                    rows.Add(BuildBandHeadingRow(band));
                }

                foreach (var u in band.Entries)
                {
                    rows.Add(BuildChecklistRow(u, navigator));
                }
            }
        }
    }

    private void UpdateRouteRow(List<ResolvedUnlock> visible, INavigationProvider? navigator)
    {
        if (routeButton is null || groupButton is null)
        {
            return;
        }

        groupButton.String = GroupButtonLabel();
        var routable = visible.Count(u => u.Status == UnlockStatus.Available && u.Routable);

        // Through UnlockRouteCap rather than composed here: the button is the only place the cap is
        // visible before it applies, and a second phrasing of it is a second chance to omit it.
        routeButton.String = UnlockRouteCap.ButtonLabel(routable);
        routeButton.IsEnabled = navigator != null && routable > 0;
    }

    private void OnRouteClicked()
    {
        var navigator = ResolveNavigator();
        if (navigator is null)
        {
            return;
        }

        var routable = ComputeVisibleUnlocks().Where(u => u.Status == UnlockStatus.Available && u.Routable).ToList();
        if (routable.Count == 0)
        {
            return;
        }

        var player = objects.LocalPlayer;
        var ordered = RoutePlanner.Plan(routable, clientState.TerritoryType, player?.Position.X ?? 0, player?.Position.Z ?? 0);
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
            StatusColor = StatusColor(u.Status),
            LabelColor = NameColor(u.Status),
            Pane = BuildUnlockDetail(u, navigator),
            Hover = PublishDetail,

            // Confirm opens the page; the actions live on it. The row keeps an Activate for the
            // surfaces that still call it directly — the strip's own buttons hand it back.
            OpensPage = true,
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
        var number = UnlockRowText.LevelNumber(u);
        var kind = UnlockTypeWord(u.Def.Type);

        // The level has moved onto the badge, where the Journal puts it, so the caption beside the
        // title is now just the kind word. The level-LESS entries keep the composite: they get no
        // badge, and the catalogue's category is the only thing standing in for one.
        var token = number.Length > 0 ? string.Empty : UnlockRowText.LevelToken(u);
        var reward = u.Def.Reward;

        return new HubRowDetail
        {
            Title = UnlockRowText.Name(u),
            Kind = token.Length > 0 ? $"{token} · {kind}" : kind,
            Level = number,
            RewardName = RewardLine(u, reward),
            RewardIconId = rewardIcons.For(reward),
            RewardIconSize = reward is null ? Vector2.Zero : HubRewardIcons.SourceSize(reward.Kind),
            BannerIconId = journalFacts.Banner(u),
            StatusIconId = statusIcons.For(u.Status),
            StatusSentence = UnlockStatusDisplay.Sentence(u),
            StatusWord = UnlockStatusDisplay.Word(u.Status),
            Body = UnlockRowText.Description(u),
            Requirements = MissingFor(u),
            GatedByQuest = u.Status == UnlockStatus.QuestLocked,
            From = FromLine(u),
            Coordinates = journalFacts.Coordinates(u),
            QuestName = u.Def.Quest ?? string.Empty,
            Provenance = string.Equals(u.Def.Confidence, "unverified", StringComparison.Ordinal)
                ? "Not certain about this entry."
                : string.Empty,
            Actions = UnlockActions(u, navigator),
        };
    }

    /// <summary>The reward tray's own line: an item's name unchanged, a duty's name with its sync
    /// level appended, or — for the 272 of 587 shipped entries with no sheet-backed reward at all,
    /// 223 of them <c>system</c> — the capability the entry itself grants, said in the catalogue's
    /// own words. The unlock IS the reward when there is no item behind it, so this is never empty:
    /// see <see cref="UnlockRowText.GrantedCapability"/> for why. The heading above it stays
    /// "Reward" either way — that is the game's own word for this slot (<c>JournalWords.Reward</c>,
    /// Addon row 463) and it does not have a second one for "a capability rather than a thing", so
    /// nothing here invents one.</summary>
    private string RewardLine(ResolvedUnlock u, UnlockReward? reward) => reward switch
    {
        { Kind: "ContentFinderCondition" } duty =>
            UnlockRowText.DutyReward(DisplayNames.SheetCase(duty.Name), journalFacts.DutyLevel(duty.Id)),

        // Sheet text, cased the way the client cases it: Companion and Mount store 'wind-up
        // brickman' and 'company chocobo' in lower case. Same transform as UnlockRowText.Name.
        not null => DisplayNames.SheetCase(reward.Name),
        null => UnlockRowText.GrantedCapability(u),
    };

    /// <summary>What can be done about an entry, in the three slots JournalDetail gives a quest —
    /// <c>InitiateButton</c>, the duty's own entry point, and <c>AcceptMapButton</c>, whose whole job
    /// is "take me to this" — plus the wiki link, which fits the same three-slot budget because
    /// <c>Accepted</c> and <c>Available</c> are mutually exclusive statuses: "Follow this quest" and
    /// "Route me there" never both show, so at most two of the game's three slots are ever taken
    /// before the wiki link claims the third.
    ///
    /// <para>Hidden rather than greyed when they do not apply, which is the game's own habit and the
    /// opposite of the "nothing in here works" report. Most entries offer one; a duty you have
    /// already accepted the quest for offers all three.</para></summary>
    private List<HubDetailAction> UnlockActions(ResolvedUnlock u, INavigationProvider? navigator)
    {
        var actions = new List<HubDetailAction>();
        if (navigator is null)
        {
            return actions;
        }

        // Slot one, InitiateButton: start the thing. For an unlock that means following the quest
        // that grants it, which only means anything once it has been taken.
        if (u.Status == UnlockStatus.Accepted && u.QuestRowId is not null)
        {
            actions.Add(new HubDetailAction("Follow this quest", () => OnChecklistRowActivated(u)));
        }

        // Slot two: the duty's own front door. The 269 duty entries are the ones a player is most
        // likely to want to queue for straight off the page, and the plugin already opens the Duty
        // Finder on a content id from its own context menu.
        if (u.Def.Reward is { Kind: "ContentFinderCondition" } duty)
        {
            var contentId = duty.Id;
            actions.Add(new HubDetailAction("Duty Finder", () => OpenDuty(contentId)));
        }

        // The requirement, in the window that already draws it. An entry whose description comes
        // from an Achievement row can hand the player straight to that row: the game states the
        // condition, the progress and the reward there, already in the player's own language, and
        // none of that is worth redrawing. This is what an entry with no place offers in place of a
        // route — see ResolvedUnlock.Routable.
        if (u.Def.DescriptionSource is { Sheet: "Achievement" } achievement)
        {
            var achievementRowId = achievement.Row;
            actions.Add(new HubDetailAction(
                "Show the requirement", () => AchievementWindowAction.Execute(achievementRowId)));
        }

        // Slot three, AcceptMapButton: the button whose job is "open the map at this". Ours plans
        // the route instead, which is the same promise kept better.
        if (u.Status == UnlockStatus.Available && unlocks.ToPickupTarget(u) is not null)
        {
            actions.Add(new HubDetailAction("Route me there", () => OnChecklistRowActivated(u)));
        }

        // The player-facing backup for "the game does not say": a verified link to the entry's own
        // wiki page, only offered when the generator confirmed one exists (see data/README.md).
        // Absent rather than disabled when there is none — the same habit every other slot here
        // follows.
        if (u.Def.WikiUrl is { Length: > 0 } wikiUrl)
        {
            actions.Add(new HubDetailAction("View on wiki", () => OpenWiki(wikiUrl)));
        }

        return actions;
    }

    /// <summary>Opens a verified wiki URL in the player's default browser. <c>Util.OpenLink</c>
    /// rather than shelling out directly: it is Dalamud's own facility for exactly this, and it
    /// additionally attempts to focus the newly launched browser window.</summary>
    private void OpenWiki(string url)
    {
        try
        {
            Util.OpenLink(url);
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"Wayfarer wiki link: could not open '{url}' in a browser.");
        }
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
                StatusColor = StatusColor(UnlockStatus.Unverified),

                // The one place a dimmed name is still right: these rows are not waiting on
                // anything, they are entries the plugin cannot vouch for, and reading as "not like
                // the others" is the whole point of showing them separately at all.
                LabelColor = GameColors.Dimmed,
                Pane = BuildUnlockDetail(u, navigator: null),
                Hover = PublishDetail,
            });
        }
    }

    /// <summary>The heading over one band inside a group.
    ///
    /// <para>A <see cref="HubRowKind.Heading"/> like the group's own rather than a third row kind:
    /// the list virtualizes on one row height per kind, so a distinct band-heading kind would be a
    /// second height to keep in step for no gain the player can see. The nesting is legible from the
    /// words — a group heading is a place or a domain, a band heading is one of four fixed states —
    /// and the game's own lists nest headings the same way.</para>
    ///
    /// <para>It carries a pane. That is how the "Not known" band actually gets labelled for a
    /// controller player: they walk onto the heading and the pane says the game states nothing these
    /// can be checked against, which is a sentence too long to fit on a row and too important to
    /// leave to a player to infer from an icon.</para></summary>
    private HubListRow BuildBandHeadingRow(UnlockBandSection band)
    {
        var count = band.Entries.Count.ToString(CultureInfo.InvariantCulture);
        return new HubListRow
        {
            Kind = HubRowKind.Heading,
            Label = UnlockBands.Label(band.Band),
            Detail = count,
            Pane = new HubRowDetail
            {
                Title = UnlockBands.Label(band.Band),
                Kind = $"{count} entries",
                StatusIconId = statusIcons.For(BandIconStatus(band.Band)),
                Body = UnlockBands.Explanation(band.Band),
            },
            Hover = PublishDetail,
        };
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
        StatusSentence = unverifiedExpanded ? "Showing." : "Hidden.",
        Body =
            "Nothing in the game's data backs these up. Wayfarer cannot check them, and never calls one "
            + "available.",
    };

    /// <summary>The sections to draw. Grouping, banding and ordering all live in
    /// <see cref="UnlockSections"/> — this window cannot be tested and that can, and the ImGui
    /// fallback calls the same thing so the two cannot order the list differently.</summary>
    private List<UnlockGroupSection> ChecklistSections(List<ResolvedUnlock> visible) =>
        UnlockSections.Build(visible, GroupModes[groupMode], ViewPoint());

    /// <summary>Where the player is, for the zone group that floats to the top and for what "nearest
    /// first" means in the Available-now view. Falls back to <see cref="UnlockViewPoint.Unknown"/>
    /// with no local player, which is a state this window can be drawn in.</summary>
    private UnlockViewPoint ViewPoint()
    {
        var player = objects.LocalPlayer;
        return new UnlockViewPoint(
            CurrentZoneName(),
            clientState.TerritoryType,
            player?.Position.X ?? 0f,
            player?.Position.Z ?? 0f);
    }

    /// <summary>What an empty list says. In the Available-now view an empty list is not a filter
    /// problem and must not be reported as one: it means every entry Wayfarer can check is either
    /// done or waiting on something, which is a real state and a different instruction — switch views
    /// to see what is in the way, rather than go and loosen a filter that is not set.</summary>
    private string EmptyListNote() =>
        GroupModes[groupMode] == UnlockGrouping.AvailableNow
            ? "Nothing is available right now. Switch views to see what is blocked."
            : "Nothing to show with these filters.";

    /// <summary>"Available now" is a view rather than a grouping, so the button does not call it one.
    /// Naming it "Group: AvailableNow" would say the list is grouped by something it is not.</summary>
    private string GroupButtonLabel() =>
        GroupModes[groupMode] == UnlockGrouping.AvailableNow
            ? UnlockSections.AvailableNowHeading
            : $"Group: {GroupModes[groupMode]}";

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
            ItemSpacing = GameMetrics.Window.RuleGap,
            Position = tabContentStart,
            Size = new Vector2(tabContentSize.X, HuntingControlsHeight),
        };

        huntingHeaderNode = BuildHeadingNode(string.Empty);
        huntingControls.AddNode(huntingHeaderNode);

        var actions = NewActionRow();
        huntHereButton = new TextButtonNode
        {
            Width = GameMetrics.Control.ButtonWidthLarge,
            Height = GameMetrics.Control.ButtonHeight,
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
                : hunting.RemainingTargets.FirstOrDefault(t => t.Monster == monster);
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

        // The rank and what is left of it, in one line above the list — which is where the game's
        // own Hunting Log puts its progress (MonsterNoteBook 1018: a label, a count and a bar,
        // directly over the monster list). The count is what makes the tab say what it is for
        // without the reader having to total the rows themselves.
        var left = hunting.RemainingTargets.Count;
        SetHuntingHeader(hunting.ActiveLogLabel is { } label
            ? $"{label} - Rank {hunting.CurrentRank} - {left} left"
            : hunting.NoLogReason ?? "No hunting log active.");

        // The button counts the same set the list above it draws and the header line totals: the rank,
        // because that is what pressing it plans. It used to count the targets in the player's own
        // zone, which is how a tab showing thirteen monsters came to carry a button saying "Start
        // Hunting (3)" — and how the button came to be greyed out in a zone with nothing left in it
        // while the rest of the rank was still waiting a teleport away.
        var navigator = ResolveNavigator();
        huntHereButton.String = HuntingPlan.StartLabel(left);
        huntHereButton.IsEnabled = navigator != null && HuntingPlan.CanStart(left);

        rows.Clear();
        distanceRows.Clear();
        AddGuidanceUnavailableNote(navigator);

        // Every remaining target on the rank, in the dataset's own order — which is the order the
        // game's Hunting Log lists them in.
        //
        // This used to list HuntHereOrder, which is only the targets in the zone the player happens
        // to be standing in. That is the right set for the Start Hunting button, which chains a
        // route through this zone, and the wrong set for a log: a rank has around ten targets and
        // the tab showed the nought-to-three of them that were local, so most of the rank was
        // simply absent. "Half of it is cropped away" was not a clipping bug — the rows were never
        // built.
        foreach (var target in hunting.RemainingTargets)
        {
            rows.Add(BuildHuntingRow(target, navigator));
        }

        var shown = hunting.RemainingTargets.Select(t => t.Monster).ToHashSet();
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
            // A duty-gated target the game cannot be asked to queue has nothing for a press to do, so
            // it says so in its status word rather than wearing "Available" over a confirm that does
            // nothing. Visibly unavailable beats present-and-silent, and the row is still listed with
            // its count and its duty name so the player can see what is left on the rank.
            var queueable = target.DutyContentFinderConditionId is not null;

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
                Portrait = true,
                StatusWord = UnlockStatusDisplay.Word(
                    queueable ? UnlockStatus.Available : UnlockStatus.RequirementsUnknown),
                StatusColor = StatusColor(queueable ? UnlockStatus.Available : UnlockStatus.RequirementsUnknown),
                Activate = queueable ? () => OpenDuty(target.DutyContentFinderConditionId) : null,
            };
        }

        var row = new HubListRow
        {
            Kind = HubRowKind.Entry,
            Label = target.MonsterName,
            Description = HuntingRowWhere(target),
            Detail = $"{target.Killed}/{target.Required}",
            IconId = HuntingRowIcon(target),
            Portrait = true,
            StatusWord = UnlockStatusDisplay.Word(UnlockStatus.Available),
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

    /// <summary>The picture in a hunting row's left column: the creature's own art, whatever the
    /// count says.
    ///
    /// <para><b>It used to swap the art for a green check on completion</b>, on the belief that this
    /// was the vanilla Hunting Log's behaviour. It is not. MonsterNoteBook's own monster row draws
    /// the portrait (<c>1017 #3</c>) and its completion mark (<c>1017 #2</c>) as two separate nodes,
    /// so the creature never goes away. Swapping also moved the row's text back to the status
    /// column's x=24 while its neighbours kept the portrait column's x=56, which gave one list two
    /// left edges. The count on the right of the row is what says a target is finished — the same
    /// place the game puts it.</para>
    ///
    /// <para>The id still goes through the same runtime validation every other icon does, so a
    /// creature whose art a patch has moved falls back to the row saying its state in words rather
    /// than to a hole in the column.</para></summary>
    private uint HuntingRowIcon(HuntingTargetView target) => statusIcons.Resolve(target.IconId);

    /// <summary>Starts a hunt through the whole rank — the same call the readout's menu and the ImGui
    /// fallback make, and gated on the same number the button's own label prints, so a lit button
    /// cannot be a press that does nothing.</summary>
    private void OnHuntClicked()
    {
        if (ResolveNavigator() is not { } navigator || !HuntingPlan.CanStart(hunting.RemainingTargets.Count))
        {
            return;
        }

        navigator.StartHunt();
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
    /// <b>Teleport and Duty Finder</b> are here for whoever has this window open rather than the
    /// readout in view. The readout recommends "Teleport to Horizon first" and its own line can be
    /// pressed, but the Duty Finder has never been on it at all, and the game's context menu is off
    /// by default for a mouse. These are real buttons on a real window, reachable with either device,
    /// and they run the same <see cref="TeleportAction"/> gate the context menu does.</summary>
    private void BuildQuestControls()
    {
        questControls = new VerticalListNode
        {
            FitWidth = true,
            ItemSpacing = GameMetrics.Window.RuleGap,
            Position = tabContentStart,
            Size = new Vector2(tabContentSize.X, QuestControlsHeight),
        };

        questHeaderNode = BuildHeadingNode(string.Empty);
        questControls.AddNode(questHeaderNode);

        // The objective, in the register the game gives a body line, directly under the name of the
        // thing it belongs to.
        questObjectiveNode = new TextNode
        {
            Height = GameMetrics.Type.BodyLine,
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.BodySize,
            LineSpacing = GameMetrics.Type.BodyLine,
            AlignmentType = AlignmentType.Left,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.Body,
        };
        questControls.AddNode(questObjectiveNode);

        // Whatever else the readout is saying — "you have arrived", "teleport to Foundation first".
        // Always drawn, empty or not, so the list below does not move up and down as the guidance
        // changes under the cursor.
        questNoteNode = new TextNode
        {
            Height = GameMetrics.Type.SecondaryLine,
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.SecondarySize,
            LineSpacing = GameMetrics.Type.SecondaryLine,
            AlignmentType = AlignmentType.Left,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.Dimmed,
        };
        questControls.AddNode(questNoteNode);

        questControls.AddNode(BuildQuestActionRow());

        AddTabNode(questNodes, questControls);
    }

    private AlignedHorizontalListNode BuildQuestActionRow()
    {
        var row = NewActionRow();

        // One row of actions rather than two, and every one of them acts on the thing named above
        // it. This button is the way back from a side quest to the default loop, so it says so and
        // it is live only when there is something to come back from — the label used to read
        // "Follow the Main Scenario" while the heading over it already said Main Scenario, which is
        // what made it look like a caption somebody had put a box around.
        followMsqButton = new TextButtonNode
        {
            Width = GameMetrics.Control.ButtonWidthLarge,
            Height = GameMetrics.Control.ButtonHeight,
            String = "Resume Main Scenario",
            IsEnabled = false,
            OnClick = OnFollowMsqClicked,
        };
        row.AddNode(followMsqButton);

        teleportButton = new TextButtonNode
        {
            Width = GameMetrics.Control.ButtonWidthLarge,
            Height = GameMetrics.Control.ButtonHeight,
            String = "Teleport",
            IsEnabled = false,
            OnClick = OnTeleportClicked,
        };
        row.AddNode(teleportButton);

        dutyFinderButton = new TextButtonNode
        {
            Width = GameMetrics.Control.ButtonWidthMedium,
            Height = GameMetrics.Control.ButtonHeight,
            String = "Duty Finder",
            IsEnabled = false,
            OnClick = OnDutyFinderClicked,
        };
        row.AddNode(dutyFinderButton);

        return row;
    }

    /// <summary>Where an Unlock Route entry with nothing to route to sends the press: the tab that
    /// says so in words. This window's own tab rather than the module's opener, because this window is
    /// necessarily already open when the press happens.</summary>
    private void OpenUnlocksTab() => SelectTab(HubTab.Checklist);

    /// <inheritdoc cref="OpenUnlocksTab"/>
    private void OpenHuntingTab() => SelectTab(HubTab.Hunting);

    /// <summary>Back to the default loop, from wherever the player is: release whatever is engaged,
    /// then drop the followed quest. Both halves, always — see <see cref="MainScenarioReturn"/> for
    /// why either one alone is not a way home.</summary>
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
        var content = feed.Compose();
        var choices = GetFollowChoices();

        rows.Clear();
        distanceRows.Clear();
        AddGuidanceUnavailableNote(navigator);
        SetGuidanceBlock(content);
        AddFollowableRows(navigator, choices);
        AddAcceptedQuestRows(navigator, choices);

        if (rows.Count == 0)
        {
            rows.Add(new HubListRow { Kind = HubRowKind.Note, Label = "Nothing to follow." });
        }

        RefreshQuestActions();
        PublishRows(questControls);
        lastQuestSignature = ComputeQuestSignature();
    }

    /// <summary>The whole guidance block, verbatim — the same words the readout puts at the top of
    /// the HUD, so the tab and the HUD can never disagree about what is being followed.
    ///
    /// <para>Three registers over three lines: the heading names the thing, the objective says what
    /// it wants, and whatever the readout adds after that is the note. They used to be a heading
    /// node and then a run of dimmed list rows separated from it by two buttons, which is how a tab
    /// whose whole subject is one quest came to have no relationship on screen between that quest,
    /// its objective and the buttons that act on them.</para></summary>
    private void SetGuidanceBlock(ReadoutContent content)
    {
        if (questHeaderNode is null)
        {
            return;
        }

        var heading = "Wayfarer";
        var objective = string.Empty;
        var note = new List<string>();

        foreach (var line in content.Lines)
        {
            if (line.Emphasis == ReadoutEmphasis.Heading)
            {
                heading = line.Text;
            }
            else if (objective.Length == 0)
            {
                objective = line.Text;
            }
            else
            {
                note.Add(line.Text);
            }
        }

        questHeaderNode.String = HeadingText.Plain(heading);
        questHeaderNode.Height = HuntingHeaderHeight;

        if (questObjectiveNode is not null)
        {
            questObjectiveNode.String = objective;
        }

        if (questNoteNode is not null)
        {
            questNoteNode.String = string.Join(" · ", note);
        }

        questControls?.RecalculateLayout();
    }

    /// <summary>Everything Wayfarer can be told to follow, as rows in one list, so the answer to
    /// "how do I toggle between MSQ and another quest or unlocks?" is a single screen with the
    /// choices on it rather than a feature the player has to already know is there.
    ///
    /// <para>Four things, always all four, in the same order, whether or not they currently have
    /// anything to offer — a choice that vanishes when it is empty cannot be learned. The ones with
    /// nothing to start say so on their second line, and pressing one opens the tab that says it in
    /// full rather than doing nothing.</para></summary>
    private void AddFollowableRows(QuestNavigator? navigator, IReadOnlyList<FollowChoice> choices)
    {
        rows.Add(new HubListRow { Kind = HubRowKind.Heading, Label = "Following" });

        var msq = choices[0];
        rows.Add(new HubListRow
        {
            Kind = HubRowKind.Entry,
            Label = msq.Label,
            Description = navigator?.Current.QuestName is { Length: > 0 } questName
                ? questName
                : "The next step of the main story.",
            Detail = CountCaption(msq.Detail),
            IconId = statusIcons.For(msq.IsFollowed ? UnlockStatus.Accepted : UnlockStatus.Available),
            StatusWord = UnlockStatusDisplay.Word(msq.IsFollowed ? UnlockStatus.Accepted : UnlockStatus.Available),
            StatusColor = StatusColor(msq.IsFollowed ? UnlockStatus.Accepted : UnlockStatus.Available),
            LabelColor = msq.IsFollowed ? GameColors.Good : null,
            Pane = FollowableDetail(
                "Main Scenario",
                msq.IsFollowed ? "Following." : "Not followed.",
                "Points at your next main scenario step.",
                msq.Activate is null ? [] : [new HubDetailAction("Follow the Main Scenario", msq.Activate)]),
            Hover = PublishDetail,
            Activate = msq.Activate,
        });

        AddUnlockRouteRow(choices[1]);
        AddHuntingFollowRow(choices[2]);
    }

    private void AddUnlockRouteRow(FollowChoice choice)
    {
        // Followed-ness first, exactly as the main-scenario row above reads it — this row could not
        // report itself at all before, so an engaged unlock route was invisible on the one tab whose
        // job is saying what is being followed.
        var status = FollowRowStatus(choice);
        var sentence = choice.IsFollowed
            ? "Following this route."
            : choice.Ready
                ? $"{choice.Detail} nearby, nearest first."
                : "Nothing to route to.";

        rows.Add(new HubListRow
        {
            Kind = HubRowKind.Entry,
            Label = choice.Label,
            Description = sentence,
            Detail = CountCaption(choice.Detail),
            IconId = statusIcons.For(status),
            StatusWord = UnlockStatusDisplay.Word(status),
            StatusColor = StatusColor(status),
            LabelColor = choice.IsFollowed ? GameColors.Good : null,
            Pane = FollowableDetail(
                choice.Label,
                sentence,
                "Walks every available unlock nearby, nearest first.",
                FollowRowActions(choice, "Follow this route")),
            Hover = PublishDetail,
            Activate = choice.Activate,
        });
    }

    private void AddHuntingFollowRow(FollowChoice choice)
    {
        // The rank, not the zone — the same set the Hunting tab lists and the same set the press
        // plans. See HuntingPlan.StartLabel.
        var remaining = hunting.RemainingTargets.Count;
        var huntingSentence = choice.IsFollowed
            ? $"Following this hunt — {remaining} left on this rank."
            : HuntingPlan.CanStart(remaining)
                ? $"{remaining} targets left on this rank."
                : hunting.NoLogReason ?? "Nothing left on this rank.";

        var status = FollowRowStatus(choice);

        rows.Add(new HubListRow
        {
            Kind = HubRowKind.Entry,
            Label = choice.Label,
            Description = hunting.ActiveLogLabel is null
                ? hunting.NoLogReason ?? "No hunting log active."
                : $"Rank {hunting.CurrentRank} · {remaining} left on this rank",
            Detail = CountCaption(choice.Detail),
            IconId = statusIcons.For(status),
            StatusWord = UnlockStatusDisplay.Word(status),
            StatusColor = StatusColor(status),
            LabelColor = choice.IsFollowed ? GameColors.Good : null,
            Pane = FollowableDetail(
                choice.Label,
                huntingSentence,
                "Walks this rank's remaining targets: this zone first, nearest first, then on by zone.",
                FollowRowActions(choice, "Start Hunting")),
            Hover = PublishDetail,
            Activate = choice.Activate,
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

    /// <summary>Every accepted quest, in the order the navigator returned them — which is the order
    /// the Following tab's own rows are matched against.
    ///
    /// <para>A quest is marked as followed only while a quest is what the arrow is actually on. The
    /// override can be set underneath a running hunt, and a list with two entries both claiming to be
    /// what is being followed is a list that cannot be read.</para></summary>
    private void AddAcceptedQuestChoices(List<FollowChoice> choices, QuestNavigator? navigator, FollowMode mode)
    {
        if (navigator is null)
        {
            return;
        }

        var followed = navigator.FollowedOverride;
        foreach (var (id, name) in navigator.GetAcceptedQuests())
        {
            var questId = id;
            var isFollowed = mode == FollowMode.Quest && followed == questId;
            choices.Add(new FollowChoice(
                name,
                isFollowed ? Following : string.Empty,
                isFollowed,
                () => FollowQuest(questId),
                Ready: !isFollowed));
        }
    }

    private void AddAcceptedQuestRows(QuestNavigator? navigator, IReadOnlyList<FollowChoice> choices)
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
            rows.Add(new HubListRow { Kind = HubRowKind.Note, Label = "No accepted quests." });
            return;
        }

        // The three fixed entries — main scenario, unlock route, hunting log — come first in
        // GetFollowChoices; every accepted quest follows in the same order GetAcceptedQuests()
        // returned them, which is the order choices was built in.
        const int FixedChoiceCount = 3;
        for (var i = 0; i < accepted.Count; i++)
        {
            var (questId, name) = accepted[i];
            var choice = choices[FixedChoiceCount + i];
            rows.Add(new HubListRow
            {
                Kind = HubRowKind.Entry,
                Label = name,

                // The objective moves to line two, where a whole sentence fits. In the gutter it
                // was the longest string on the row competing for the narrowest space on it.
                Description = navigator.GetAcceptedQuestObjective(questId) ?? string.Empty,
                Detail = CountCaption(choice.Detail),
                IconId = statusIcons.For(UnlockStatus.Accepted),
                StatusWord = UnlockStatusDisplay.Word(UnlockStatus.Accepted),
                StatusColor = StatusColor(UnlockStatus.Accepted),
                LabelColor = choice.IsFollowed ? GameColors.Good : null,
                Pane = BuildQuestDetail(name, navigator.GetAcceptedQuestObjective(questId), choice.IsFollowed, questId),
                Hover = PublishDetail,
                Activate = choice.Activate,
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
                ? "Following."
                : "In progress.",
            Body = objective is { Length: > 0 } text ? text : "No objective text for this step.",
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
            // Live whenever there is something to come back FROM — an engaged hunt or unlock route as
            // well as a chosen quest. This read the followed-quest override alone, which is null
            // during a hunt, so the one button on this window named "Resume Main Scenario" was greyed
            // out in exactly the mode a player most wants it. Following the main scenario really is
            // this plugin's null state, and on it the button still has nothing to do.
            followMsqButton.IsEnabled = navigator?.MainScenarioReset.Acts == true;
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

        // The readout's own wording, so the two surfaces say the one thing: the verb, the game's
        // aetheryte crystal, then the place. A framed button is right here — a window is where the
        // game itself puts framed buttons — but the words on it are not a second vocabulary.
        var label = offered ? $"Teleport to {state!.AetheryteName}" : "Teleport";
        if (!string.Equals(label, lastTeleportLabel, StringComparison.Ordinal))
        {
            lastTeleportLabel = label;

            var builder = new SeStringBuilder();
            if (offered)
            {
                builder.AddText("Teleport to ").AddIcon(BitmapFontIcon.Aetheryte).AddText(state!.AetheryteName!);
            }
            else
            {
                builder.AddText(label);
            }

            teleportButton.String = new ReadOnlySeString(builder.Build().Encode());
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

            // WHICH source is engaged, not merely that one is: swapping a hunt for an unlock route
            // leaves Engaged true throughout, and the "Following" caption belongs to a different row
            // afterwards. Without this the rows kept the old one marked.
            hash = (hash * 31) + (state.SourceId?.GetHashCode(StringComparison.Ordinal) ?? 0);

            // And what the hunting row counts, which is the rank rather than the player's own zone —
            // a kill changes it without changing anything else here. A Count on a list already in
            // hand; nothing is read or allocated for it.
            hash = (hash * 31) + hunting.RemainingTargets.Count;

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
            ContentNode = { FitWidth = false, FitContents = true, ItemSpacing = SettingsLayout.ItemSpacing },
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
            var heading = BuildHeadingNode(section.Title);
            settingsArea.ContentNode.AddNode(heading);

            // The heading rides along with the first control under it and with nothing else: walking
            // up into a section otherwise parks its first control flush at the top of the tab with
            // the words that say what the section is one pixel above the clip, which is the same
            // "the page doesn't follow" complaint one row smaller.
            NodeBase? lead = heading;
            foreach (var setting in section.Settings)
            {
                var control = BuildSettingControl(setting);
                settingsArea.ContentNode.AddNode(control);
                WireScrollFollowsFocus(control, lead);
                lead = null;
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
    /// <para><b>Which signal says "the cursor has arrived".</b> Not <c>FocusStart</c>: that is what
    /// this was first written against and it never fired once for a pad, because the game raises it
    /// through the component's own vtable for the text-input flow rather than dispatching it at the
    /// node handlers a plugin can register — so the wiring was present, the arithmetic was right,
    /// and the tab still walked the cursor off the bottom of the window. The signal that does fire
    /// is <c>InputReceived</c>: the focused component gets the d-pad press, and the toolkit's own
    /// <c>NavFocusNode</c> — the thing every row on the list-backed tabs is steered by, and the one
    /// arrival signal in this codebase known to work on a controller — reads exactly the release of
    /// an up/down press off it to mean "this node is the one the cursor is on now". The conditions
    /// below are that node's, deliberately identical: the press moves focus, so the release is
    /// delivered to the control the cursor has landed on.</para>
    ///
    /// <para>Left and right are left alone. A slider steps its value with them and never moves the
    /// cursor, so scrolling on them could only ever be a no-op or a lurch.</para></summary>
    private unsafe void WireScrollFollowsFocus(NodeBase control, NodeBase? lead)
    {
        // A scale setting is a caption plus a slider, and only the slider can be focused — so the
        // event goes on the slider while the scroll target stays the whole row, or the caption
        // scrolls off the top of the tab the moment the cursor arrives on its slider.
        var focusable = control is SettingSliderNode row ? row.Slider : control;
        if (focusable is not KamiToolKit.BaseTypes.ComponentNode.ComponentNode component)
        {
            return;
        }

        component.AddEvent(AtkEventType.InputReceived, (_, _, _, _, data) =>
        {
            if (data is null)
            {
                return;
            }

            var input = data->InputData;
            if (input.State is not InputState.Up)
            {
                return;
            }

            if ((InputId)input.InputId is not (InputId.UP or InputId.DOWN))
            {
                return;
            }

            ScrollSettingIntoView(control, lead);
        });
    }

    /// <summary>Scrolls the container by the least it can and still show all of
    /// <paramref name="control"/> — and <paramref name="lead"/> with it, when the control is the
    /// first under a section heading.</summary>
    private void ScrollSettingIntoView(NodeBase control, NodeBase? lead)
    {
        if (settingsArea is null)
        {
            return;
        }

        try
        {
            var bar = settingsArea.ScrollBarNode;

            // Content coordinates throughout: the controls are children of the container's content
            // node, so their Y is already measured from the top of the scrollable content, which is
            // the same origin the scroll position is expressed in.
            var viewport = settingsArea.Height;
            var current = bar.ScrollPosition;
            var ceiling = bar.ScrollMaxPosition;
            var bottom = control.Y + control.Height;
            var target = ScrollIntoView.Adjust(control.Y, control.Height, viewport, current, ceiling);

            // Only ever on the way up, and only when there is room for both. Walking down to a
            // section's first control puts it flush at the bottom with its heading above the clip,
            // and pulling the heading back in from there would move the page under a cursor that had
            // not moved — the same control focused twice has to settle in the same place.
            if (lead is not null && target < current && bottom - lead.Y <= viewport)
            {
                target = ScrollIntoView.Adjust(lead.Y, bottom - lead.Y, viewport, current, ceiling);
            }

            if (Math.Abs(target - current) >= 1f)
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
            Height = SettingsLayout.ControlHeight(SettingKind.Toggle),
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
