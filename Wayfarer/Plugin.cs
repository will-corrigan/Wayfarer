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
using Wayfarer.Windows;

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

    /// <summary>The single Controller-mode native surface for the whole plugin (Checklist |
    /// Hunting Log | Settings tabs) — owned here, not by either module, since both
    /// <see cref="UnlockChecklistModule"/> and <see cref="HuntingLogModule"/> open into it. See
    /// <see cref="NativeHubWindow"/>'s doc comment.</summary>
    private readonly NativeHubWindow hub;

    /// <summary>The single writer of the game map flag — held here purely so it is unsubscribed
    /// and the player's own flag restored on unload.</summary>
    private readonly MapFlagCoordinator mapFlag;

    private readonly IPluginLog log;

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
        hub = new NativeHubWindow(unlocks, hunting, modules, objects, clientState, framework, config, SaveConfig, log)
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

        configWindow = new(modules, config, SaveConfig);
        windows.AddWindow(configWindow);

        // inputMode.OnFrame runs first so windows.Draw (and every window it draws this same
        // frame) sees the current frame's resolved Mode/Glyphs, not last frame's.
        pluginInterface.UiBuilder.Draw += inputMode.OnFrame;
        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        pluginInterface.UiBuilder.OpenMainUi += OpenMain;
        commands.AddHandler("/wayfarer", new(OnCommand)
        { HelpMessage = "Open Wayfarer settings. \"/wayfarer hunt\" opens the hunting log, \"/wayfarer unlocks\" the checklist." });

        log.Information("Wayfarer loaded");
    }

    public void Dispose()
    {
        commands.RemoveHandler("/wayfarer");
        pluginInterface.UiBuilder.Draw -= windows.Draw;
        pluginInterface.UiBuilder.Draw -= inputMode.OnFrame;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        pluginInterface.UiBuilder.OpenMainUi -= OpenMain;

        contextMenuActions.Dispose();
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

    private void OpenConfig() => configWindow.IsOpen = true;

    /// <summary>The plugin list's main-UI button opens what the plugin is FOR — the checklist —
    /// rather than its settings, which have their own button right beside it.</summary>
    private void OpenMain() => modules.Get<UnlockChecklistModule>()?.OpenChecklist();

    /// <summary>Every window has to be reachable by typing, whatever the input mode: the widget's
    /// buttons are hidden on a controller and the context-menu surface is off by default, so a bare
    /// "/wayfarer" alone would leave the checklist and hunting log with no entry point at all.</summary>
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
            default:
                OpenConfig();
                break;
        }
    }

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
        var arrowWindow = new ArrowWindow(
            guidance.Navigator,
            modules,
            config.QuestHelper,
            objects,
            clientState,
            log,
            inputMode,
            config.InputMode,
            saveConfig,
            () => hub.OpenTab(HubTab.Checklist));
        return new QuestHelperModule(
            framework,
            windows,
            commands,
            config.QuestHelper,
            config.Guidance,
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
        var unlockWindow = new UnlockWindow(unlocks, modules, objects, clientState, inputMode, config.InputMode, saveConfig);
        return new UnlockChecklistModule(
            framework,
            windows,
            unlocks,
            unlockWindow,
            hub,
            inputMode,
            config.UnlockChecklist,
            saveConfig,
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
        var huntingWindow = new HuntingWindow(hunting, modules, objects, inputMode, config.InputMode, saveConfig);
        return new HuntingLogModule(
            framework,
            windows,
            hunting,
            huntingWindow,
            hub,
            inputMode,
            config.HuntingLog,
            saveConfig,
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
