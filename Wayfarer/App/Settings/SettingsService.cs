using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Wayfarer.App.Settings;

/// <summary>The doors onto the settings window: the plugin installer's cog, its main button, and
/// <c>/wayfarer</c>. Owns the window and opens it on the framework thread, where the toolkit needs
/// it.</summary>
internal sealed class SettingsService : IDisposable
{
    private const string Command = "/wayfarer";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly SettingsAddon window;

    public SettingsService(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commands,
        IFramework framework,
        IModuleHost host)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.framework = framework;

        window = new SettingsAddon(host)
        {
            InternalName = "WayfarerSettings",
            Title = "Wayfarer",
            Subtitle = "Settings",
        };

        pluginInterface.UiBuilder.OpenConfigUi += Open;
        pluginInterface.UiBuilder.OpenMainUi += Open;
        commands.AddHandler(Command, new CommandInfo((_, _) => Open()) { HelpMessage = "Opens Wayfarer's settings." });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        commands.RemoveHandler(Command);
        pluginInterface.UiBuilder.OpenConfigUi -= Open;
        pluginInterface.UiBuilder.OpenMainUi -= Open;
        window.Dispose();
    }

    private void Open() => _ = framework.RunOnFrameworkThread(window.Toggle);
}
