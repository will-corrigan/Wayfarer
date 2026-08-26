using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;
using KamiToolKit.Nodes;
using Wayfarer.Core.Guidance;
using Wayfarer.Guidance;
using Wayfarer.Guidance.Coordinators;
using Wayfarer.Guidance.Sources;
using Wayfarer.Modules;
using Wayfarer.Settings;
using Wayfarer.Windows;
using Wayfarer.Windows.Native;

namespace Wayfarer;

/// <summary>Composition root: the only class allowed to hold Dalamud services or a
/// <see cref="Configuration"/> instance directly. Acquires services, loads config, builds
/// the object graph via constructor injection, registers modules, wires IPC, and disposes
/// everything in exact reverse of construction order.</summary>
public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly WindowSystem windows = new("Wayfarer");
    private readonly ModuleRegistry modules;
    private readonly ConfigWindow configWindow;
    private readonly WayfarerIpcProvider ipcProvider;
    private readonly InputModeService inputMode;
    private readonly ContextMenuActions contextMenuActions;
    private readonly NamePlateMarkers namePlateMarkers;
    private readonly SettingsCatalog settings;

    /// <summary>The single owner of where the readout sits: read by the readout's host and its fallback every frame,
    /// written by the Settings tab's position sliders and by a mouse drag.</summary>
    private readonly ReadoutPlacement readoutPlacement;

    /// <summary>The plugin's one entry in Dalamud's server info bar — see its own doc comment for
    /// why it exists. Built from the same <see cref="ReadoutFeed"/> the readout and its ImGui
    /// fallback already share, so all three surfaces read from one place.</summary>
    private readonly DtrEntry dtrEntry;

    /// <summary>The one window the plugin has — Checklist, Hunting Log, Quests and Settings — for
    /// mouse and controller alike. Owned here rather than by any module, since every module opens
    /// into it. See <see cref="NativeHubWindow"/>'s doc comment.</summary>
    private readonly NativeHubWindow hub;

    /// <summary>The single writer of the game map flag — held here purely so it is unsubscribed
    /// and the player's own flag restored on unload.</summary>
    private readonly MapFlagCoordinator mapFlag;

    private readonly IPluginLog log;

    /// <summary>The one overlay controller the plugin ever creates — a second would duplicate
    /// KamiToolKit's addon-creation state machine. Built inside the quest-helper module factory
    /// because that is where the readout's inputs are assembled, held here because it outlives any
    /// single module and must be disposed on the framework thread exactly once.</summary>
    private GuidanceOverlay overlay = null!;

    /// <summary>What the readout, its ImGui fallback, <see cref="dtrEntry"/> and the window's Quests
    /// tab all compose their own presentation from, so no two of them can say different things.</summary>
    private ReadoutFeed feed = null!;

    /// <summary>Every action Wayfarer offers, decided once. Both menus onto them — the game's own
    /// right-click menu and the one the readout drops for a controller — render this, so neither can
    /// offer something the other does not.</summary>
    private GuidanceActions guidanceActions = null!;

    private bool loggedHubFallback;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        IDataManager dataManager,
        IClientState clientState,
        IObjectTable objects,
        ICondition condition,
        ICommandManager commands,
        IGameConfig gameConfig,
        IGamepadState gamepadState,
        IContextMenu contextMenu,
        INamePlateGui namePlateGui,
        ITextureProvider textureProvider,
        IDtrBar dtrBar,
        IUnlockState unlockState,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.log = log;

        // Required before any KamiToolKit type (native windows, nodes) is touched.
        KamiToolKitLibrary.Initialize(pluginInterface, "Wayfarer");

        var config = LoadConfig(pluginInterface, log);
        void SaveConfig() => pluginInterface.SavePluginConfig(config);

        // Where the readout sits — one owner shared by the host, its fallback and the settings, so a
        // nudge from the Settings tab and a drag with the mouse write to the same place.
        readoutPlacement = new ReadoutPlacement(config.QuestHelper, SaveConfig);

        modules = new(log, config);

        inputMode = new InputModeService(gameConfig, gamepadState, config.InputMode, log);

        var unlocks = new UnlockService(log, objects, clientState, pluginInterface, dataManager);
        var hunting = new HuntingLogService(log, objects, clientState, pluginInterface, dataManager);

        // Declared once, rendered by the native window and by the ImGui fallback alike.
        settings = new SettingsCatalog(config, modules, readoutPlacement, SaveConfig);

        var guidance = BuildGuidance(
            log, config, clientState, condition, objects, dataManager, hunting, unlockState);
        mapFlag = guidance.MapFlag;

        // Built here, before the window that reads it: the readout, its ImGui fallback, the info-bar
        // entry and the window's Quests tab all compose their presentation from this one feed.
        feed = new ReadoutFeed(guidance.Navigator, modules, config.QuestHelper, objects);
        hub = BuildHub(unlocks, hunting, objects, clientState, framework, config, textureProvider, dataManager);

        var readoutHosts = new ReadoutHosts(framework, clientState, objects, textureProvider);
        RegisterModules(readoutHosts, unlocks, hunting, config, SaveConfig, log, guidance);

        ipcProvider = new(pluginInterface, modules, clientState);
        contextMenuActions = BuildContextMenuActions(contextMenu, config, log);
        namePlateMarkers = new(namePlateGui, textureProvider, framework, modules, config.Guidance, log);
        namePlateMarkers.Start();

        dtrEntry = BuildDtrEntry(dtrBar, framework, config.QuestHelper);
        dtrEntry.Start();

        configWindow = new(settings);
        windows.AddWindow(configWindow);

        SubscribeAndStart(pluginInterface);

        // The version belongs in this line: it is the first question asked of every pasted log,
        // and the plugin list's answer is whatever is installed now, not what was running then.
        log.Information($"Wayfarer {typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "?"} loaded.");
    }

    public void Dispose()
    {
        commands.RemoveHandler("/wayfarer");
        pluginInterface.UiBuilder.Draw -= windows.Draw;
        pluginInterface.UiBuilder.Draw -= inputMode.OnFrame;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        pluginInterface.UiBuilder.OpenMainUi -= OpenMain;

        dtrEntry.Dispose();
        contextMenuActions.Dispose();
        namePlateMarkers.Dispose();
        ipcProvider.Dispose();

        try
        {
            // Modules are disposed before the hub they call into is torn down. ModuleRegistry's
            // own Dispose() guards each module individually, and NativeHubWindow.Dispose() guards
            // its own main-thread marshalling — but this try/finally is the actual fix for the
            // unload crash + leaked hook: whatever throws or however long disposal takes
            // above, KamiToolKitLibrary.Cleanup() below is what releases the static FireCallback
            // hook, and it must always run.
            mapFlag.Dispose();
            modules.Dispose();
            windows.RemoveAllWindows();
            overlay.Dispose();
            hub.Dispose();
        }
        finally
        {
            KamiToolKitLibrary.Cleanup();
        }
    }

    /// <summary>Reads the player's config and brings it up to date. The migration is written back
    /// immediately rather than left to ride along with the next setting change: one that only lands
    /// when the player happens to touch something else is one that runs again every session.</summary>
    private static Configuration LoadConfig(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (config.Migrate())
        {
            pluginInterface.SavePluginConfig(config);
            log.Information($"Wayfarer: configuration migrated to version {Configuration.CurrentVersion}.");
        }

        return config;
    }

    /// <summary>The guidance object graph, built once: one arbiter (the single writer for what the
    /// arrow follows), one router (how to get anywhere), one source per feature (what to guide to
    /// and — crucially — when it is done), the per-frame service, and the adapter the existing
    /// windows still talk to. Each module registers its own source when it is enabled.</summary>
    private static GuidanceGraph BuildGuidance(
        IPluginLog log,
        Configuration config,
        IClientState clientState,
        ICondition condition,
        IObjectTable objects,
        IDataManager dataManager,
        HuntingLogService hunting,
        IUnlockState unlockState)
    {
        var arbiter = new GuidanceArbiter((message, ex) => log.Error(ex, message));
        var router = new GuidanceRouter(dataManager);
        var questSource = new QuestObjectiveSource(dataManager);
        var unlockSource = new UnlockRouteSource(arbiter);
        var huntingSource = new HuntingSource(arbiter, hunting, router, clientState, objects);

        // Built here rather than beside the other feature services because it needs nothing they
        // need: no framework subscription and no cached progress, since every aether-current fact is
        // either fixed in a sheet or a free local bit read. Its module comes with it, having no
        // window to assemble first.
        var aetherCurrents = new AetherCurrentService(log, dataManager, clientState, unlockState);
        var aetherCurrentSource =
            new AetherCurrentSource(arbiter, aetherCurrents, router, clientState, objects);
        var aetherCurrentModule = new AetherCurrentsModule(arbiter, aetherCurrents, aetherCurrentSource);
        var service = new GuidanceService(
            log, config.QuestHelper, clientState, condition, objects, arbiter, router);
        var navigator = new QuestNavigator(service, questSource, unlockSource, huntingSource);

        // The only writer of the game's single, destructive map flag. Objectives declare that they
        // want to be flagged; this performs it, snapshots the player's own flag first and gives it
        // back on exit.
        var gameFlag = new GameMapFlag(clientState, log);
        var flagCoordinator = new MapFlagCoordinator(
            arbiter,
            () => config.Guidance.MarkObjectiveWithMapFlag,
            gameFlag.Read,
            gameFlag.Set,
            gameFlag.Restore).Start();

        return new GuidanceGraph(
            arbiter, questSource, unlockSource, huntingSource, aetherCurrentModule, navigator, flagCoordinator);
    }

    /// <summary>Every module, in registration order, which is also disposal order reversed. Factored
    /// out of the constructor purely to stay under the method-length analyzer; the Dalamud services
    /// the builders need come bundled in <paramref name="services"/> so this stays inside the
    /// parameter-count one too.</summary>
    private void RegisterModules(
        ReadoutHosts services,
        UnlockService unlocks,
        HuntingLogService hunting,
        Configuration config,
        Action saveConfig,
        IPluginLog log,
        GuidanceGraph guidance)
    {
        modules.Register(
            BuildQuestHelperModule(services, config, saveConfig, log, guidance), enabledByDefault: true);

        modules.Register(
            BuildUnlockChecklistModule(
                services.Framework, services.Objects, services.ClientState, unlocks, inputMode, config, log, guidance),
            enabledByDefault: true);

        modules.Register(
            BuildHuntingLogModule(services.Framework, services.Objects, hunting, config, log, guidance),
            enabledByDefault: true);

        // No factory: this one owns no window, so there is nothing to assemble before it.
        modules.Register(guidance.AetherCurrents, enabledByDefault: true);
    }

    private void SubscribeAndStart(IDalamudPluginInterface pluginInterface)
    {
        // inputMode.OnFrame runs first so windows.Draw (and every window it draws this same
        // frame) sees the current frame's resolved Mode, not last frame's.
        pluginInterface.UiBuilder.Draw += inputMode.OnFrame;
        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        pluginInterface.UiBuilder.OpenMainUi += OpenMain;

        // The readout is native from here on. Start() marshals onto the framework thread, because
        // every node constructor asserts it and plugin construction is not guaranteed to be on it.
        overlay.Start();

        commands.AddHandler("/wayfarer", new(OnCommand)
        {
            HelpMessage = "Opens Wayfarer. Also: unlocks, hunt, quests, settings, stop.",
        });
    }

    /// <summary>Dalamud's settings cog lands on the Settings tab of the one Wayfarer window rather
    /// than on a separate ImGui panel — there is one window, and settings are a tab in it. The
    /// ImGui config window remains only as the fallback when the native one cannot be opened.</summary>
    private void OpenConfig() => OpenHub(HubTab.Settings, () => configWindow.IsOpen = true);

    /// <summary>What the game's own Follow submenu hands off to for "A Quest..." — the one tab
    /// listing every followable thing. The readout's own follow caret no longer opens this: it drops
    /// the game's own menu in place (see <see cref="Windows.Native.ReadoutAddon"/>), reading the same
    /// <see cref="NativeHubWindow.GetFollowChoices"/> this tab does — one source of truth for what
    /// is followable, two doors onto it.</summary>
    private void OpenFollowing() => OpenHub(HubTab.Quests, () => configWindow.IsOpen = true);

    /// <summary>The plugin list's main button opens what the plugin is FOR — the unlocks list —
    /// rather than its settings, which have their own button right beside it.</summary>
    private void OpenMain() => modules.Get<UnlockChecklistModule>()?.OpenChecklist();

    private void OpenHub(HubTab tab, Action fallback)
    {
        try
        {
            hub.OpenTab(tab);
        }
        catch (Exception ex)
        {
            // Once: this is reachable from the plugin list, the cog and every command, and the
            // reason it would not open does not change between attempts.
            if (!loggedHubFallback)
            {
                loggedHubFallback = true;
                const string message =
                    "Wayfarer: the game-styled Wayfarer window would not open, so the plugin-drawn settings "
                    + "window is being used instead. Nothing is lost; it is best driven with a mouse. "
                    + "Reported once.";
                log.Warning(ex, message);
            }

            fallback();
        }
    }

    /// <summary>Typed shortcuts into the one window, plus the universal exit. These are
    /// convenience aliases: the window is reachable from the plugin list, the readout and the
    /// Dalamud cog, and nothing here is the only route to anything.</summary>
    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "hunt" or "hunting":
                modules.Get<HuntingLogModule>()?.OpenLog();
                break;
            case "unlocks" or "checklist":
                modules.Get<UnlockChecklistModule>()?.OpenChecklist();
                break;
            case "quest" or "quests":
                OpenHub(HubTab.Quests, () => configWindow.IsOpen = true);
                break;
            case "settings" or "config":
                OpenConfig();
                break;
            case "stop":
                modules.Get<QuestHelperModule>()?.Navigator.ClearPickup();
                break;
            default:
                modules.Get<UnlockChecklistModule>()?.OpenChecklist();
                break;
        }
    }

    /// <summary>Factored out of the constructor purely to stay under the method-length analyzer. Both
    /// menus onto Wayfarer's actions — this one and the readout's own — render the same
    /// <see cref="GuidanceActions"/>.</summary>
    private ContextMenuActions BuildContextMenuActions(
        IContextMenu contextMenu,
        Configuration config,
        IPluginLog log) =>
        new(contextMenu, modules, config.QuestHelper, guidanceActions, inputMode, log);

    /// <summary>Factored out of the constructor purely to stay under the method-length analyzer.</summary>
    private NativeHubWindow BuildHub(
        IUnlockProvider unlocks,
        HuntingLogService hunting,
        IObjectTable objects,
        IClientState clientState,
        IFramework framework,
        Configuration config,
        ITextureProvider textures,
        IDataManager dataManager) =>
        new(
            unlocks,
            hunting,
            feed,
            modules,
            objects,
            clientState,
            framework,
            config,
            settings,
            inputMode,
            new HubStatusIcons(textures, log),
            new HubRewardIcons(dataManager, log),
            new HubJournalFacts(dataManager, textures, log),
            BuildJournal(dataManager, framework),
            log)
        {
            InternalName = "WayfarerHubNative",
            Title = "Wayfarer",

            // Explicitly empty. KamiToolKit draws a subtitle beside the title and defaults it to the
            // plugin name passed to KamiToolKitLibrary.Initialize, which is what made the title bar
            // read "Wayfarer Wayfarer" — its own guidance is to drop the subtitle when the window's
            // title is already the plugin's name, and here it is.
            Subtitle = string.Empty,
        };

    /// <summary>The journal page's own window — the second half of the game's own Journal, which is
    /// a plain list beside an ornate parchment page rather than one rectangle.
    ///
    /// <para>Chromeless: the window node is supplied already invisible, so the frame, the title bar
    /// and the draggable header are all allocated and none of them drawn. That is deliberate and it
    /// is what <c>JournalDetail</c> itself does — the page's chrome <i>is</i> its parchment and its
    /// gilt border, and a standard window frame around that would be a frame inside a frame. The
    /// same trick, for the same reason, as the readout's own host.</para>
    ///
    /// <para>Handed to the hub window, which owns its lifetime: it is opened, moved, and closed
    /// entirely in response to what is happening in that window's list.</para></summary>
    private JournalWindow BuildJournal(IDataManager dataManager, IFramework framework) =>
        new(new JournalWords(dataManager, log), framework, log)
        {
            InternalName = "WayfarerJournal",
            Title = "Wayfarer",
            Subtitle = string.Empty,
            CreateWindowNode = () => new WindowNode { NodeId = 2, IsVisible = false },
            EnableContextMenu = false,
            RememberClosePosition = false,

            // The page is positioned by the hub window, exactly as the game's own detail page is
            // positioned by its list — so it must not be clamped away from the deliberate overlap
            // that lets the border's ornament cross the seam between the two.
            OpenInBounds = false,

            // No fade on the way out. A closing addon stays allocated until its hide transition
            // finishes, and for those frames it reports itself not-open while Open() still refuses
            // to do anything — so a Cancel followed straight away by a Confirm on another row would
            // ask for a page that could not be built. The hub retries the open regardless; this
            // narrows the window it has to retry across from many frames to one.
            DisableCloseTransition = true,
        };

    /// <summary>Factored out of the constructor purely to stay under the method-length analyzer.
    /// Left-click reuses the exact path the plugin list's own main button takes
    /// (<see cref="OpenMain"/>); right-click is the settings equivalent of the Dalamud cog
    /// (<see cref="OpenConfig"/>); shift-click is the same universal exit
    /// <c>/wayfarer stop</c> and the hub's own Stop buttons use.</summary>
    private DtrEntry BuildDtrEntry(IDtrBar dtrBar, IFramework framework, QuestHelperConfig cfg) => new(
        dtrBar,
        feed,
        cfg,
        framework,
        () => modules.Get<UnlockChecklistModule>()?.OpenChecklist(),
        OpenConfig,
        () => modules.Get<QuestHelperModule>()?.Navigator.ClearPickup(),
        log);

    /// <summary>Factored out of the constructor purely to stay under the method-length analyzer —
    /// builds the widget (with its "Open Wayfarer ▸" entry point wired straight to
    /// <see cref="hub"/>) and its owning module.</summary>
    private QuestHelperModule BuildQuestHelperModule(
        ReadoutHosts services,
        Configuration config,
        Action saveConfig,
        IPluginLog log,
        GuidanceGraph guidance)
    {
        // Built here, where the readout's inputs are assembled, and held on the plugin because the
        // game's own right-click menu renders the same actions. Every action reads the module
        // registry at the moment a menu opens, so building it before the modules are registered is
        // safe — nothing is resolved now.
        guidanceActions = new GuidanceActions(
            modules, config.QuestHelper, services.Objects, services.ClientState, OpenConfig, OpenFollowing, log);

        overlay = new GuidanceOverlay(
            feed,
            config.QuestHelper,
            readoutPlacement,
            services.Objects,
            services.ClientState,
            services.Framework,
            services.Textures,
            OpenConfig,
            hub.GetFollowChoices,
            guidanceActions,
            log);
        var arrowWindow = new ArrowWindow(
            guidance.Navigator,
            feed,
            overlay,
            modules,
            config.QuestHelper,
            services.Objects,
            services.ClientState,
            log);
        return new QuestHelperModule(
            services.Framework,
            windows,
            commands,
            config.QuestHelper,
            saveConfig,
            guidance.Navigator,
            arrowWindow,
            guidance.Arbiter,
            guidance.QuestSource);
    }

    /// <summary>Factored out of the constructor purely to stay under the method-length analyzer —
    /// same construction shape as the hunting log module below (ImGui window + shared native hub →
    /// module), just wrapped in its own method.</summary>
    private UnlockChecklistModule BuildUnlockChecklistModule(
        IFramework framework,
        IObjectTable objects,
        IClientState clientState,
        UnlockService unlocks,
        InputModeService inputMode,
        Configuration config,
        IPluginLog log,
        GuidanceGraph guidance)
    {
        var unlockWindow = new UnlockWindow(unlocks, modules, objects, clientState, inputMode);
        return new UnlockChecklistModule(
            framework,
            windows,
            unlocks,
            unlockWindow,
            hub,
            config.UnlockChecklist,
            log,
            guidance.Arbiter,
            guidance.UnlockSource);
    }

    /// <summary>Factored out of the constructor purely to stay under the method-length analyzer —
    /// same construction shape as the unlock checklist module above (ImGui window + shared native
    /// hub → module), just wrapped in its own method.</summary>
    private HuntingLogModule BuildHuntingLogModule(
        IFramework framework,
        IObjectTable objects,
        HuntingLogService hunting,
        Configuration config,
        IPluginLog log,
        GuidanceGraph guidance)
    {
        var huntingWindow = new HuntingWindow(hunting, modules, objects);
        return new HuntingLogModule(
            framework,
            windows,
            hunting,
            huntingWindow,
            hub,
            config.HuntingLog,
            log,
            guidance.Arbiter,
            guidance.HuntingSource);
    }

    /// <summary>The Dalamud services the readout's host and its overlay fallback need, bundled so the
    /// builder below stays inside the parameter-count analyzer rather than growing a ninth
    /// argument.</summary>
    private sealed record ReadoutHosts(
        IFramework Framework,
        IClientState ClientState,
        IObjectTable Objects,
        ITextureProvider Textures);

    /// <summary>What <see cref="BuildGuidance"/> hands back, so the module builders can take one
    /// parameter instead of five.</summary>
    private sealed record GuidanceGraph(
        GuidanceArbiter Arbiter,
        QuestObjectiveSource QuestSource,
        UnlockRouteSource UnlockSource,
        HuntingSource HuntingSource,
        AetherCurrentsModule AetherCurrents,
        QuestNavigator Navigator,
        MapFlagCoordinator MapFlag);
}
