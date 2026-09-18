using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Wayfarer.Core.Routing;

namespace Wayfarer.App;

/// <summary>Computes the needle and the distance on demand from the current route's end, the
/// player's position and the camera's yaw. Holds no state of its own.</summary>
internal sealed unsafe class Heading(IGuidance guidance, IObjectTable objects) : IHeading
{
    /// <inheritdoc/>
    public float? Needle
    {
        get
        {
            if (Offset() is not var (dx, _, dz))
            {
                return null;
            }

            return Compass.NeedleAngle(Compass.Bearing(dx, dz), CameraYaw());
        }
    }

    /// <inheritdoc/>
    public float? DistanceYalms
    {
        get
        {
            if (Offset() is not var (dx, dy, dz))
            {
                return null;
            }

            return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }
    }

    private static float CameraYaw()
    {
        var cameraManager = CameraManager.Instance();
        return cameraManager != null && cameraManager->Camera != null ? -cameraManager->Camera->DirH : 0f;
    }

    /// <summary>The vector from the player to the route's end, or null when either is missing.
    /// </summary>
    private (float Dx, float Dy, float Dz)? Offset()
    {
        if (guidance.Current?.Route?.End is not { } end || objects.LocalPlayer is not { } player)
        {
            return null;
        }

        var p = player.Position;
        return (end.X - p.X, end.Y - p.Y, end.Z - p.Z);
    }
}
