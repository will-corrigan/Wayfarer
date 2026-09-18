using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Wayfarer.Core.Routing;

namespace Wayfarer.App;

/// <summary>The needle and the distance, computed on demand to the end of the route's first walk.
/// A route that starts with a teleport has nothing to walk to, so it has no needle: the player
/// casts from where they stand.</summary>
internal sealed unsafe class Heading(IGuidance guidance, IObjectTable objects) : IHeading
{
    /// <inheritdoc/>
    public float? Needle => Offset() is var (dx, _, dz) ? Compass.NeedleAngle(Compass.Bearing(dx, dz), CameraYaw()) : null;

    /// <inheritdoc/>
    public float? DistanceYalms => Offset() is var (dx, dy, dz) ? MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz)) : null;

    private static float CameraYaw() => CameraManager.Instance()->Camera->DirH;

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
