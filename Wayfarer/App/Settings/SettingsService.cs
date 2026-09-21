using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Wayfarer.App.Diagnostics;
using Wayfarer.App.Modules;
using Wayfarer.Surfaces.ScenarioTree;

namespace Wayfarer.App.Settings;

/// <summary>The doors onto the settings window: the plugin installer's cog, its main button, and
/// <c>/wayfarer</c>. Owns the window and opens it on the framework thread, where the toolkit needs
/// it.</summary>
internal sealed class SettingsService : ISettingsWindow, IAsyncDisposable
{
    private const string Command = "/wayfarer";

    /// <summary>The argument that asks for the guidance report instead of the window.</summary>
    private const string WhyArgument = "why";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly SettingsAddon window;
    private readonly GuidanceReport report;
    private readonly IPluginLog log;

    public SettingsService(IDalamudPluginInterface pluginInterface, ICommandManager commands, IFramework framework, IModuleHost host, ScenarioTreeStyleStore styles, ITextureProvider textures, IPluginLog log, GuidanceReport report)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.framework = framework;
        this.report = report;
        this.log = log;

        window = new SettingsAddon(host, styles, textures, log)
        {
            InternalName = "WayfarerSettings",
            Title = "Wayfarer",
            Subtitle = "Settings",
        };

        pluginInterface.UiBuilder.OpenConfigUi += Toggle;
        pluginInterface.UiBuilder.OpenMainUi += Toggle;
        commands.AddHandler(Command, new CommandInfo(Run) { HelpMessage = "Opens Wayfarer's settings. \"/wayfarer why\" says what it is guiding to and why." });
    }

    /// <summary>Unhooks the doors, then closes the window. The close has to happen on the
    /// framework thread and Dalamud unloads plugins off it, so the toolkit's async dispose is
    /// awaited: it marshals itself there and waits for the window to finish closing.</summary>
    public async ValueTask DisposeAsync()
    {
        commands.RemoveHandler(Command);
        pluginInterface.UiBuilder.OpenConfigUi -= Toggle;
        pluginInterface.UiBuilder.OpenMainUi -= Toggle;
        await window.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Toggle() => framework.Hand(window.Toggle, log, "open the settings window");

    /// <summary>What <c>/wayfarer</c> does: the report when asked for it, the window otherwise.
    /// Both run on the framework thread, the report because it reads the object table and the
    /// window because the toolkit requires it.</summary>
    private void Run(string command, string arguments)
    {
        Action work = string.Equals(arguments.Trim(), WhyArgument, StringComparison.OrdinalIgnoreCase)
            ? report.Print
            : window.Toggle;

        framework.Hand(work, log, "answer /wayfarer");
    }
}
