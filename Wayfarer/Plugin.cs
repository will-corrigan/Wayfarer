using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;
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

        var navigator = new QuestNavigator(log, config.QuestHelper, clientState, condition, objects, dataManager);
        var arrowWindow = new ArrowWindow(
            navigator, modules, config.QuestHelper, objects, clientState, log, inputMode, config.InputMode, SaveConfig);
        var questHelperModule = new QuestHelperModule(framework, windows, commands, config.QuestHelper, SaveConfig, navigator, arrowWindow);
        modules.Register(questHelperModule, enabledByDefault: true);

        var unlocks = new UnlockService(log, objects, clientState, pluginInterface, dataManager);
        var unlockWindow = new UnlockWindow(unlocks, modules, objects, clientState, inputMode, config.InputMode, SaveConfig);
        var nativeUnlockWindow = new NativeUnlockWindow(unlocks, modules, objects, clientState, framework)
        {
            InternalName = "WayfarerUnlocksNative",
            Title = "Unlocks",
            Size = new Vector2(560f, 640f),
        };
        var unlockChecklistModule = new UnlockChecklistModule(
            framework, windows, modules, unlocks, unlockWindow, nativeUnlockWindow, inputMode, config.UnlockChecklist, SaveConfig, log);
        modules.Register(unlockChecklistModule, enabledByDefault: true);

        modules.Register(
            BuildHuntingLogModule(framework, dataManager, clientState, objects, inputMode, config, SaveConfig, log),
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

        // Modules are disposed before the windows they may still reference are torn down —
        // this includes UnlockChecklistModule disposing NativeUnlockWindow before
        // KamiToolKitLibrary.Cleanup() below, so it doesn't get counted as a leaked addon.
        modules.Dispose();
        windows.RemoveAllWindows();

        KamiToolKitLibrary.Cleanup();
    }

    private void OpenConfig() => configWindow.IsOpen = true;

    /// <summary>Factored out of the constructor purely to stay under the method-length analyzer —
    /// same construction shape as the unlock checklist module above (service → ImGui window →
    /// native window → module), just wrapped in its own method.</summary>
    private HuntingLogModule BuildHuntingLogModule(
        IFramework framework,
        IDataManager dataManager,
        IClientState clientState,
        IObjectTable objects,
        InputModeService inputMode,
        Configuration config,
        Action saveConfig,
        IPluginLog log)
    {
        var hunting = new HuntingLogService(log, objects, clientState, pluginInterface, dataManager);
        var huntingWindow = new HuntingWindow(hunting, modules, objects, inputMode, config.InputMode, saveConfig);
        var nativeHuntingWindow = new NativeHuntingWindow(hunting, modules, objects, framework)
        {
            InternalName = "WayfarerHuntingNative",
            Title = "Hunting Log",
            Size = new Vector2(420f, 560f),
        };
        return new HuntingLogModule(
            framework, windows, hunting, huntingWindow, nativeHuntingWindow, inputMode, config.HuntingLog, saveConfig, log);
    }
}
