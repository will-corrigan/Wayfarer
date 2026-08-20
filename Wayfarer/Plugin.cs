using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
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

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        IDataManager dataManager,
        IClientState clientState,
        IObjectTable objects,
        ICondition condition,
        ICommandManager commands,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;

        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        void SaveConfig() => pluginInterface.SavePluginConfig(config);

        modules = new ModuleRegistry(log, config);

        var navigator = new QuestNavigator(log, config.QuestHelper, clientState, condition, objects, dataManager);
        var arrowWindow = new ArrowWindow(navigator, modules, config.QuestHelper, objects, clientState, log);
        var questHelperModule = new QuestHelperModule(framework, windows, commands, config.QuestHelper, SaveConfig, navigator, arrowWindow);
        modules.Register(questHelperModule, enabledByDefault: true);

        var unlocks = new UnlockService(log, objects, clientState, pluginInterface, dataManager);
        var unlockWindow = new UnlockWindow(unlocks, modules, objects, clientState);
        var unlockChecklistModule = new UnlockChecklistModule(framework, windows, modules, unlocks, unlockWindow);
        modules.Register(unlockChecklistModule, enabledByDefault: true);

        ipcProvider = new WayfarerIpcProvider(pluginInterface, modules, clientState);

        configWindow = new ConfigWindow(modules, config, SaveConfig);
        windows.AddWindow(configWindow);
        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        pluginInterface.UiBuilder.OpenMainUi += OpenConfig;
        commands.AddHandler("/wayfarer", new CommandInfo((_, _) => configWindow.IsOpen = true)
        { HelpMessage = "Open Wayfarer settings" });

        log.Information("Wayfarer loaded");
    }

    public void Dispose()
    {
        commands.RemoveHandler("/wayfarer");
        pluginInterface.UiBuilder.Draw -= windows.Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        pluginInterface.UiBuilder.OpenMainUi -= OpenConfig;

        ipcProvider.Dispose();

        // Modules are disposed before the windows they may still reference are torn down.
        modules.Dispose();
        windows.RemoveAllWindows();
    }

    private void OpenConfig() => configWindow.IsOpen = true;
}
