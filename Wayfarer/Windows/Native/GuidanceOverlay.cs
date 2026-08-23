using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using KamiToolKit.UiOverlay;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>Owns the plugin's one overlay controller and the guidance readout node inside it.
///
/// The readout replaces the old ImGui widget for mouse and controller alike. That is safe to do
/// because of the shape of its failure modes: an overlay that never attaches draws nothing, and
/// "nothing appears" is recoverable — <see cref="IsActive"/> reports false and the ImGui widget
/// takes over. An overlay cannot take focus, cannot be clicked and is explicitly outside controller
/// navigation, so unlike a window it can neither be in the way nor trap anyone.</summary>
internal sealed class GuidanceOverlay(
    ReadoutFeed feed,
    QuestHelperConfig cfg,
    IObjectTable objects,
    IFramework framework,
    IPluginLog log) : IDisposable
{
    private OverlayController? controller;
    private GuidanceOverlayNode? node;
    private bool started;

    /// <summary>Whether the readout is actually on screen. False before it has been created and
    /// permanently false if creation failed — which is what the ImGui fallback keys off, so a
    /// player is never left with no guidance at all.</summary>
    public bool IsActive => node is not null && cfg.UseNativeReadout;

    /// <summary>Creates the overlay, marshalling onto the framework thread because every node
    /// constructor and the overlay controller itself assert it. Idempotent.</summary>
    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;

        // Fire and forget: Create() swallows and logs its own failures, and there is nothing for
        // the constructor to do with the task beyond that.
        _ = framework.RunOnFrameworkThread(Create);
    }

    public void Dispose()
    {
        if (controller is null)
        {
            return;
        }

        // Same marshalling as the native window, and for the same reason: Dalamud unloads plugins
        // on a thread-pool thread while the controller's Dispose asserts the framework thread.
        // Disposing a node off-thread is worse than throwing — it logs nothing and leaks the node.
        var owned = controller;
        controller = null;
        node = null;

        if (framework.IsInFrameworkUpdateThread)
        {
            owned.Dispose();
            return;
        }

        try
        {
            framework.RunOnFrameworkThread(owned.Dispose).Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Wayfarer readout: disposing the overlay on the framework thread failed or timed out.");
        }
    }

    private static unsafe float CameraYaw()
    {
        var cameraManager = CameraManager.Instance();
        return cameraManager != null && cameraManager->Camera != null ? cameraManager->Camera->DirH : 0f;
    }

    private void Create()
    {
        try
        {
            // Exactly one controller for the plugin's lifetime — a second would duplicate the
            // addon-creation state machine and its per-frame handler.
            controller = new OverlayController();
            node = new GuidanceOverlayNode(BuildFrame, log);
            controller.AddNode(node);
        }
        catch (Exception ex)
        {
            controller = null;
            node = null;
            log.Error(ex, "Wayfarer readout: the overlay could not be created — falling back to the plugin-drawn widget.");
        }
    }

    private ReadoutFrame? BuildFrame()
    {
        if (!cfg.UseNativeReadout || !feed.ShouldShow())
        {
            return null;
        }

        var content = feed.Compose(teleportOnClick: false);
        var (radians, hidden) = Bearing(content);
        return new ReadoutFrame(content, radians, hidden, cfg.ArrowIcon, cfg.TextScale, cfg.ReadoutPosition);
    }

    /// <summary>The arrow's rotation, or the reason there isn't one. The reason is carried rather
    /// than discarded because "the arrow is missing" is otherwise indistinguishable from "the arrow
    /// is pointing at nothing" from outside the game — see <see cref="ArrowHiddenReason"/>.
    ///
    /// Note what is deliberately <b>not</b> special-cased here: an objective in another zone still
    /// gets an arrow, because the composer hands back the entrance or aetheryte leg as the target
    /// (<c>ReadoutComposer.AddOtherZone</c>). That was the pre-rewrite widget's behaviour and it is
    /// preserved — the arrow points at the leg you can actually walk.</summary>
    private (float? Radians, ArrowHiddenReason Hidden) Bearing(ReadoutContent content)
    {
        if (!content.ShowArrow)
        {
            return (null, ArrowHiddenReason.NotRequested);
        }

        if (content.TargetX is not { } tx || content.TargetZ is not { } tz)
        {
            return (null, ArrowHiddenReason.NoTargetCoordinates);
        }

        var player = objects.LocalPlayer;
        if (player is null)
        {
            return (null, ArrowHiddenReason.NoPlayer);
        }

        var radians = NavMath.ArrowAngle(
            NavMath.Bearing(tx - player.Position.X, tz - player.Position.Z), CameraYaw());
        return (radians, ArrowHiddenReason.None);
    }
}
