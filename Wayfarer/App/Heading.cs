using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Wayfarer.Core.Routing;

namespace Wayfarer.App;

/// <summary>Computes the needle and the distance on demand from the route's next walk, the
/// player's position and the camera's yaw. Holds no state of its own.
///
/// <para>Only a walk can be pointed at. When the route's first leg is a walk, the needle and the
/// distance are to where it ends: the next aetheryte, door side or the target itself. When the
/// first leg is a teleport there is nothing to walk to, so there is no needle; the player casts
/// from where they stand.</para></summary>
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

    /// <summary>The camera's yaw as the game keeps it. <see cref="Compass.NeedleAngle"/> expects it
    /// unchanged; the sign convention is settled there.</summary>
    private static float CameraYaw()
    {
        var cameraManager = CameraManager.Instance();
        return cameraManager != null && cameraManager->Camera != null ? cameraManager->Camera->DirH : 0f;
    }

    /// <summary>The vector from the player to the end of the route's first walk, or null when the
    /// route does not start with one or there is no player.</summary>
    private (float Dx, float Dy, float Dz)? Offset()
    {
        if (guidance.Current?.Route?.Legs is not [Leg.Walk walk, ..] || objects.LocalPlayer is not { } player)
        {
            return null;
        }

        var p = player.Position;
        return (walk.To.X - p.X, walk.To.Y - p.Y, walk.To.Z - p.Z);
    }
}
