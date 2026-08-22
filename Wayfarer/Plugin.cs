using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;
using Wayfarer.Guidance;
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

        modules.Register(
            BuildQuestHelperModule(framework, dataManager, clientState, objects, condition, inputMode, config, SaveConfig, log),
            enabledByDefault: true);

        modules.Register(
            BuildUnlockChecklistModule(framework, objects, clientState, unlocks, inputMode, config, SaveConfig, log),
            enabledByDefault: true);

        modules.Register(
            BuildHuntingLogModule(framework, objects, hunting, inputMode, config, SaveConfig, log),
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
        pluginInterface.UiBuilder.OpenMainUi += OpenConfig;
        commands.AddHandler("/wayfarer", new((_, _) => configWindow.IsOpen = true)
        { HelpMessage = "Open Wayfarer settings" });

        log.Information("Wayfarer loaded");
    }

    public void Dispose()
    {
        commands.RemoveHandler("/wayfarer");
        pluginInterface.UiBuilder.Draw -= windows.Draw;
        pluginInterface.UiBuilder.Draw -= inputMode.OnFrame;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        pluginInterface.UiBuilder.OpenMainUi -= OpenConfig;

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
            modules.Dispose();
            windows.RemoveAllWindows();
            hub.Dispose();
        }
        finally
        {
            KamiToolKitLibrary.Cleanup();
        }
    }

    private void OpenConfig() => configWindow.IsOpen = true;

    /// <summary>Factored out of the constructor purely to stay under the method-length analyzer —
    /// builds the widget (with its Controller-mode "Open Wayfarer ▸" entry point wired straight to
    /// <see cref="hub"/>) and its owning module.</summary>
    private QuestHelperModule BuildQuestHelperModule(
        IFramework framework,
        IDataManager dataManager,
        IClientState clientState,
        IObjectTable objects,
        ICondition condition,
        InputModeService inputMode,
        Configuration config,
        Action saveConfig,
        IPluginLog log)
    {
        var router = new GuidanceRouter(dataManager);
        var navigator = new QuestNavigator(log, config.QuestHelper, clientState, condition, objects, dataManager, router);
        var arrowWindow = new ArrowWindow(
            navigator,
            modules,
            config.QuestHelper,
            objects,
            clientState,
            log,
            inputMode,
            config.InputMode,
            saveConfig,
            () => hub.OpenTab(HubTab.Checklist));
        return new QuestHelperModule(framework, windows, commands, config.QuestHelper, saveConfig, navigator, arrowWindow);
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
        IPluginLog log)
    {
        var unlockWindow = new UnlockWindow(unlocks, modules, objects, clientState, inputMode, config.InputMode, saveConfig);
        return new UnlockChecklistModule(
            framework, windows, modules, unlocks, unlockWindow, hub, inputMode, config.UnlockChecklist, saveConfig, log);
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
        IPluginLog log)
    {
        var huntingWindow = new HuntingWindow(hunting, modules, objects, inputMode, config.InputMode, saveConfig);
        return new HuntingLogModule(
            framework, windows, hunting, huntingWindow, hub, inputMode, config.HuntingLog, saveConfig, log);
    }
}
