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
    private readonly UnlockService unlockService;
    private readonly IPluginLog log;

    // TEMPORARY task-B1 spike field - see WaynativeSpikeWindow's doc comment. Lazily created by
    // the /waynative debug command, disposed in Dispose() below; delete both alongside the
    // window class once task B2 lands the real native checklist window.
    private WaynativeSpikeWindow? waynativeSpike;

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
        unlockService = unlocks;
        var unlockWindow = new UnlockWindow(unlocks, modules, objects, clientState, inputMode, config.InputMode, SaveConfig);
        var unlockChecklistModule = new UnlockChecklistModule(
            framework, windows, modules, unlocks, unlockWindow, config.UnlockChecklist, SaveConfig);
        modules.Register(unlockChecklistModule, enabledByDefault: true);

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

        // TEMPORARY task-B1 spike command - see WaynativeSpikeWindow's doc comment. Remove
        // alongside the field/window class once task B2 lands the real native window.
        commands.AddHandler("/waynative", new((_, _) => ToggleWaynativeSpike())
        { HelpMessage = "[TEMP] Toggle the native-window prototype (task B1 spike)" });

        log.Information("Wayfarer loaded");
    }

    public void Dispose()
    {
        commands.RemoveHandler("/wayfarer");
        commands.RemoveHandler("/waynative");
        pluginInterface.UiBuilder.Draw -= windows.Draw;
        pluginInterface.UiBuilder.Draw -= inputMode.OnFrame;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        pluginInterface.UiBuilder.OpenMainUi -= OpenConfig;

        // TEMPORARY task-B1 spike - see WaynativeSpikeWindow's doc comment. Disposed before
        // KamiToolKitLibrary.Cleanup() below so it doesn't get counted as a leaked addon.
        waynativeSpike?.Dispose();
        waynativeSpike = null;

        contextMenuActions.Dispose();
        ipcProvider.Dispose();

        // Modules are disposed before the windows they may still reference are torn down.
        modules.Dispose();
        windows.RemoveAllWindows();

        KamiToolKitLibrary.Cleanup();
    }

    private void OpenConfig() => configWindow.IsOpen = true;

    // TEMPORARY task-B1 spike - see WaynativeSpikeWindow's doc comment.
    private void ToggleWaynativeSpike()
    {
        if (waynativeSpike is null)
        {
            // Framework-thread state read synchronously like UnlockWindow.OnOpen does - the
            // command handler already runs on the main/framework thread.
            unlockService.Recompute();
            waynativeSpike = new WaynativeSpikeWindow(unlockService, log)
            {
                InternalName = "WayfarerNativeSpike",
                Title = "Wayfarer (native spike - temp)",
                Size = new Vector2(420f, 480f),
            };
        }

        waynativeSpike.Toggle();
    }
}
