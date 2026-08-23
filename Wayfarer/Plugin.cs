using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;
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

    /// <summary>The plugin's one entry in Dalamud's server info bar — see its own doc comment for
    /// why it exists. Built from the same <see cref="ReadoutFeed"/> the readout and its ImGui
    /// fallback already share, so all three surfaces read from one place.</summary>
    private readonly DtrEntry dtrEntry;

    /// <summary>The one window the plugin has — Checklist, Hunting Log and Settings — for mouse and
    /// controller alike. Owned here rather than by any module, since every module opens into it.
    /// See <see cref="NativeHubWindow"/>'s doc comment.</summary>
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

    /// <summary>What the readout, its ImGui fallback and <see cref="dtrEntry"/> all compose their
    /// own presentation from — held here purely so <see cref="dtrEntry"/> can be wired to it after
    /// <see cref="BuildQuestHelperModule"/> creates it.</summary>
    private ReadoutFeed feed = null!;

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
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.log = log;

        // Required before any KamiToolKit type (native windows, nodes) is touched.
        KamiToolKitLibrary.Initialize(pluginInterface, "Wayfarer");

        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        void SaveConfig() => pluginInterface.SavePluginConfig(config);

        modules = new(log, config);

        inputMode = new InputModeService(gameConfig, gamepadState, config.InputMode, log);

        var unlocks = new UnlockService(log, objects, clientState, pluginInterface, dataManager);
        var hunting = new HuntingLogService(log, objects, clientState, pluginInterface, dataManager);

        // Declared once, rendered by the native window and by the ImGui fallback alike.
        settings = new SettingsCatalog(config, modules, SaveConfig);
        hub = new NativeHubWindow(unlocks, hunting, modules, objects, clientState, framework, config, settings, inputMode, log)
        {
            InternalName = "WayfarerHubNative",
            Title = "Wayfarer",
        };

        var guidance = BuildGuidance(log, config, clientState, condition, objects, dataManager, hunting);
        mapFlag = guidance.MapFlag;

        modules.Register(
            BuildQuestHelperModule(framework, clientState, objects, inputMode, config, SaveConfig, log, guidance),
            enabledByDefault: true);

        modules.Register(
            BuildUnlockChecklistModule(framework, objects, clientState, unlocks, inputMode, config, SaveConfig, log, guidance),
            enabledByDefault: true);

        modules.Register(
            BuildHuntingLogModule(framework, objects, hunting, inputMode, config, SaveConfig, log, guidance),
            enabledByDefault: true);

        ipcProvider = new(pluginInterface, modules, clientState);
        contextMenuActions = new(contextMenu, objects, modules, config.QuestHelper, clientState, inputMode, log);
        namePlateMarkers = new(namePlateGui, textureProvider, framework, modules, config.Guidance, log);
        namePlateMarkers.Start();

        dtrEntry = BuildDtrEntry(dtrBar, framework, config.QuestHelper);
        dtrEntry.Start();

        configWindow = new(settings);
        windows.AddWindow(configWindow);

        SubscribeAndStart(pluginInterface);
        log.Information("Wayfarer loaded");
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
            // unload crash + leaked hook (task 2): whatever throws or however long disposal takes
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
        HuntingLogService hunting)
    {
        var arbiter = new GuidanceArbiter((message, ex) => log.Error(ex, message));
        var router = new GuidanceRouter(dataManager);
        var questSource = new QuestObjectiveSource(dataManager);
        var unlockSource = new UnlockRouteSource(arbiter);
        var huntingSource = new HuntingSource(arbiter, hunting, router, clientState, objects);
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

        return new GuidanceGraph(arbiter, questSource, unlockSource, huntingSource, navigator, flagCoordinator);
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
            HelpMessage = "Shortcut for the Wayfarer window and its Stop button — everything here is also a click or "
                + "a d-pad press away: the server info bar entry, the plugin list, and the window's own controls. "
                + "\"/wayfarer hunt\" opens the hunting log, \"/wayfarer settings\" the settings, \"/wayfarer stop\" "
                + "ends the current route or hunt.",
        });
    }

    /// <summary>Dalamud's settings cog lands on the Settings tab of the one Wayfarer window rather
    /// than on a separate ImGui panel — there is one window, and settings are a tab in it. The
    /// ImGui config window remains only as the fallback when the native one cannot be opened.</summary>
    private void OpenConfig() => OpenHub(HubTab.Settings, () => configWindow.IsOpen = true);

    /// <summary>The plugin list's main button opens what the plugin is FOR — the checklist —
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
            log.Error(ex, "Plugin: the native Wayfarer window failed to open — falling back to the ImGui settings.");
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
    /// builds the widget (with its Controller-mode "Open Wayfarer ▸" entry point wired straight to
    /// <see cref="hub"/>) and its owning module.</summary>
    private QuestHelperModule BuildQuestHelperModule(
        IFramework framework,
        IClientState clientState,
        IObjectTable objects,
        InputModeService inputMode,
        Configuration config,
        Action saveConfig,
        IPluginLog log,
        GuidanceGraph guidance)
    {
        feed = new ReadoutFeed(guidance.Navigator, modules, config.QuestHelper, objects);
        overlay = new GuidanceOverlay(feed, config.QuestHelper, objects, framework, log);
        var arrowWindow = new ArrowWindow(
            guidance.Navigator,
            feed,
            overlay,
            modules,
            inputMode,
            config.QuestHelper,
            objects,
            clientState,
            log);
        return new QuestHelperModule(
            framework,
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
        Action saveConfig,
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
        InputModeService inputMode,
        Configuration config,
        Action saveConfig,
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

    /// <summary>What <see cref="BuildGuidance"/> hands back, so the module builders can take one
    /// parameter instead of five.</summary>
    private sealed record GuidanceGraph(
        GuidanceArbiter Arbiter,
        QuestObjectiveSource QuestSource,
        UnlockRouteSource UnlockSource,
        HuntingSource HuntingSource,
        QuestNavigator Navigator,
        MapFlagCoordinator MapFlag);
}
