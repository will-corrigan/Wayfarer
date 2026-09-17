using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;
using Wayfarer.Windows.Native;

namespace Wayfarer.Windows;

/// <summary>Turns the live guidance snapshot into one frame for the block under the game's own
/// banner: the lines <see cref="ScenarioTreeContent"/> composes, the compass bearing measured
/// against the player and the camera, and the player's own arrow and text preferences. Everything
/// that needs a Dalamud service is resolved here, so the node's per-frame path stays arithmetic and
/// string assignment.</summary>
internal sealed class GuidanceFrameSource(
    ReadoutFeed feed,
    QuestHelperConfig cfg,
    IObjectTable objects) : IGuidanceFrames
{
    /// <inheritdoc/>
    public ReadoutFrame? Next()
    {
        if (!feed.ShouldShow())
        {
            return null;
        }

        var content = ScenarioTreeContent.Compose(feed.Inputs());
        if (content.IsEmpty)
        {
            return null;
        }

        var (radians, hidden) = Bearing(content);
        return new ReadoutFrame(
            content,
            radians,
            hidden,
            cfg.ArrowIcon,
            cfg.ArrowScale,
            cfg.TextScale,
            MoveMode: false,
            cfg.ClickTeleportEnabled);
    }

    private static unsafe float CameraYaw()
    {
        var cameraManager = CameraManager.Instance();
        return cameraManager != null && cameraManager->Camera != null ? cameraManager->Camera->DirH : 0f;
    }

    /// <summary>The compass's rotation, or the reason there isn't one — carried rather than
    /// discarded because "missing" and "pointing at nothing" are otherwise indistinguishable from
    /// outside the game. An objective in another zone still gets a bearing: the composer hands back
    /// the entrance or aetheryte leg as the target, which is the leg the player can actually walk.
    /// </summary>
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

        var radians = NavMath.ArrowAngle(NavMath.Bearing(tx - player.Position.X, tz - player.Position.Z), CameraYaw());
        return (radians, ArrowHiddenReason.None);
    }
}
