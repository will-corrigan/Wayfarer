using System.Globalization;
using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using KamiToolKit.UiOverlay;

namespace Wayfarer.Spike;

/// <summary>THROWAWAY SPIKE CODE — see <see cref="SpikeNavTarget"/>. The one entry point for every
/// spike experiment, behind <c>/wayspike</c>. Owns everything the spike allocates and unwinds it in
/// reverse, so deleting the <c>Wayfarer/Spike</c> folder plus the handful of lines in
/// <see cref="Plugin"/> removes the spike completely.</summary>
internal sealed class SpikeController : IDisposable
{
    private const string Command = "/wayspike";

    private readonly ICommandManager commands;
    private readonly IChatGui chat;
    private readonly IFramework framework;
    private readonly IPluginLog log;

    private readonly SpikeNavWindow navWindow;
    private readonly SpikeNamePlateMarker namePlateMarker;
    private readonly SpikeFlagMarker flagMarker;
    private readonly SpikeTrackerProbe trackerProbe;

    private OverlayController? overlayController;
    private SpikeOverlayNode? overlayNode;

    public SpikeController(
        ICommandManager commands,
        IChatGui chat,
        IFramework framework,
        IClientState clientState,
        IObjectTable objects,
        INamePlateGui namePlates,
        HuntingLogService hunting,
        IUnlockProvider unlocks,
        InputModeService inputMode,
        IPluginLog log)
    {
        this.commands = commands;
        this.chat = chat;
        this.framework = framework;
        this.log = log;

        navWindow = new SpikeNavWindow(inputMode, framework, log)
        {
            InternalName = "WayfarerSpikeNav",
            Title = "Wayfarer spike",
            Size = new Vector2(560.0f, 540.0f),
        };

        namePlateMarker = new SpikeNamePlateMarker(namePlates, objects, clientState, hunting, unlocks, log);
        flagMarker = new SpikeFlagMarker(objects, log);
        trackerProbe = new SpikeTrackerProbe(log);

        commands.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Temporary native UX spike. Run '/wayspike help' for the sub-commands.",
        });
    }

    public void Dispose()
    {
        commands.RemoveHandler(Command);
        namePlateMarker.Dispose();
        DisposeOverlay();
        navWindow.Dispose();
    }

    /// <summary>Plays one of the game's own UI sound effects so the right ids for cursor move,
    /// confirm, cancel and error can be identified by ear rather than guessed from a table.</summary>
    private static string PlaySound(string argument)
    {
        if (!uint.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var effectId))
        {
            return "Usage: /wayspike sound <id> — try 1 through 20.";
        }

        unsafe
        {
            UIGlobals.PlaySoundEffect(effectId);
        }

        return $"Played the game's UI sound effect {effectId}.";
    }

    private void OnCommand(string command, string arguments)
    {
        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var verb = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;
        var argument = parts.Length > 1 ? parts[1] : string.Empty;

        try
        {
            switch (verb)
            {
                case "":
                case "nav":
                    var wasOpen = navWindow.IsOpen;
                    navWindow.Toggle();
                    Say(wasOpen ? "Spike window closing." : "Spike window opening.");
                    break;

                case "plates":
                    Say($"Nameplate marking is now {namePlateMarker.CycleMode()} (icon {namePlateMarker.MarkerIconId}, marking {namePlateMarker.Summary}).");
                    break;

                case "icon":
                    Say(int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iconId)
                        ? SetIcon(iconId)
                        : $"Marker icon is now {namePlateMarker.NextCandidateIcon()}.");
                    break;

                case "flag":
                    Say(RunFlag(argument));
                    break;

                case "todo":
                    Say(trackerProbe.Dump(argument.Length > 0 ? argument : "_ToDoList"));
                    break;

                case "overlay":
                    Say(ToggleOverlay());
                    break;

                case "sound":
                    Say(PlaySound(argument));
                    break;

                default:
                    SayHelp();
                    break;
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Spike command failed");
            Say($"Spike command failed: {ex.Message}");
        }
    }

    private string SetIcon(int iconId)
    {
        namePlateMarker.SetIcon(iconId);
        return $"Marker icon is now {iconId}.";
    }

    private string RunFlag(string argument) => argument.ToLowerInvariant() switch
    {
        "clear" => flagMarker.Clear(),
        "restore" => flagMarker.Restore(),
        "map" => flagMarker.OpenMap(),
        "status" => flagMarker.Status(),
        _ => flagMarker.Set(),
    };

    private string ToggleOverlay()
    {
        if (overlayNode is not null)
        {
            DisposeOverlay();
            return "Overlay probe removed.";
        }

        overlayController ??= new OverlayController();
        overlayNode = new SpikeOverlayNode();
        overlayController.AddNode(overlayNode);
        return "Overlay probe added — it should render in the game's font at the game's HUD scale, and vanish with /hud off and in cutscenes.";
    }

    private void DisposeOverlay()
    {
        if (overlayController is null)
        {
            return;
        }

        var controller = overlayController;
        overlayController = null;
        overlayNode = null;

        if (framework.IsInFrameworkUpdateThread)
        {
            controller.Dispose();
            return;
        }

        try
        {
            framework.RunOnFrameworkThread(controller.Dispose).Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            log.Warning(ex, "SpikeController: overlay disposal on the framework thread failed or timed out.");
        }
    }

    private void SayHelp()
    {
        Say("/wayspike — open or close the navigation test window.");
        Say("/wayspike plates — cycle nameplate marking: off, every-frame, on-change.");
        Say("/wayspike icon [id] — next candidate marker icon, or a specific one.");
        Say("/wayspike flag [clear|restore|map|status] — the game's real flag marker.");
        Say("/wayspike overlay — toggle the HUD overlay text probe.");
        Say("/wayspike todo [addon] — dump the quest tracker's node tree to the log.");
        Say("/wayspike sound <id> — play one of the game's UI sound effects.");
    }

    private void Say(string message)
    {
        chat.Print(message);
        log.Information($"Spike: {message}");
    }
}
