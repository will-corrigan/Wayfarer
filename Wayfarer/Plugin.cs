using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Wayfarer.Modules;
using Wayfarer.Windows;

namespace Wayfarer;

public sealed class Plugin : IDalamudPlugin
{
    internal readonly IDalamudPluginInterface PluginInterface;
    internal readonly IFramework Framework;
    internal readonly IDataManager DataManager;
    internal readonly IClientState ClientState;
    internal readonly IObjectTable Objects;
    internal readonly ICondition Condition;
    internal readonly ICommandManager Commands;
    internal readonly IPluginLog Log;
    internal readonly Configuration Config;
    internal readonly ModuleRegistry Modules;
    internal readonly WindowSystem Windows = new("Wayfarer");

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
        PluginInterface = pluginInterface;
        Framework = framework;
        DataManager = dataManager;
        ClientState = clientState;
        Objects = objects;
        Condition = condition;
        Commands = commands;
        Log = log;

        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Modules = new ModuleRegistry(Log, Config);
        Modules.Register(new QuestHelperModule(this), enabledByDefault: true);
        Modules.Register(new UnlockChecklistModule(this), enabledByDefault: true);
        ipcProvider = new WayfarerIpcProvider(this);

        configWindow = new ConfigWindow(this);
        Windows.AddWindow(configWindow);
        pluginInterface.UiBuilder.Draw += Windows.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        pluginInterface.UiBuilder.OpenMainUi += OpenConfig;
        commands.AddHandler("/wayfarer", new CommandInfo((_, _) => configWindow.IsOpen = true)
        { HelpMessage = "Open Wayfarer settings" });

        Log.Information("Wayfarer loaded");
    }

    public void Dispose()
    {
        Commands.RemoveHandler("/wayfarer");
        PluginInterface.UiBuilder.Draw -= Windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        PluginInterface.UiBuilder.OpenMainUi -= OpenConfig;

        ipcProvider.Dispose();

        // Modules are disposed before the windows they may still reference are torn down.
        Modules.Dispose();
        Windows.RemoveAllWindows();
    }

    internal void SaveConfig() => PluginInterface.SavePluginConfig(Config);

    private void OpenConfig() => configWindow.IsOpen = true;
}
