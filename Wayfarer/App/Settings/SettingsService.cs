using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Wayfarer.App.Settings;

/// <summary>The doors onto the settings window: the plugin installer's cog, its main button, and
/// <c>/wayfarer</c>. Owns the window and opens it on the framework thread, where the toolkit needs
/// it.</summary>
internal sealed class SettingsService : IAsyncDisposable
{
    private const string Command = "/wayfarer";
    private const string DiagnosticsArgument = "nav";
    private const string ToggleArgument = "pad";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly SettingsAddon window;
    private readonly IEnumerable<IDiagnostics> diagnostics;

    public SettingsService(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commands,
        IFramework framework,
        IModuleHost host,
        IEnumerable<IDiagnostics> diagnostics)
    {
        this.diagnostics = diagnostics;
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
        commands.AddHandler(Command, new CommandInfo(OnCommand) { HelpMessage = "Opens Wayfarer's settings. /wayfarer nav writes navigation diagnostics to the log; /wayfarer pad toggles the pad link investigation." });
    }

    /// <summary>Unhooks the doors, then closes the window. The close has to happen on the
    /// framework thread and Dalamud unloads plugins off it, so the toolkit's async dispose is
    /// awaited: it marshals itself there and waits for the window to finish closing.</summary>
    public async ValueTask DisposeAsync()
    {
        commands.RemoveHandler(Command);
        pluginInterface.UiBuilder.OpenConfigUi -= Open;
        pluginInterface.UiBuilder.OpenMainUi -= Open;
        await window.DisposeAsync().ConfigureAwait(false);
    }

    private void OnCommand(string command, string arguments)
    {
        var argument = arguments.Trim();
        if (string.Equals(argument, ToggleArgument, StringComparison.OrdinalIgnoreCase))
        {
            _ = framework.RunOnFrameworkThread(() =>
            {
                foreach (var source in diagnostics)
                {
                    source.Toggle(ToggleArgument);
                }
            });
            return;
        }

        if (string.Equals(argument, DiagnosticsArgument, StringComparison.OrdinalIgnoreCase))
        {
            _ = framework.RunOnFrameworkThread(() =>
            {
                foreach (var source in diagnostics)
                {
                    source.Dump();
                }
            });
            return;
        }

        Open();
    }

    private void Open() => _ = framework.RunOnFrameworkThread(window.Toggle);
}
