using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Wayfarer.Routing;

namespace Wayfarer.Guidance;

/// <summary>The needle and the distance, computed on demand to the end of the route's first walk.
/// A route that starts with a teleport has nothing to walk to, so it has no needle: the player
/// casts from where they stand.
///
/// <para>A walk to an area rather than a point is measured to the edge of the circle, so standing
/// anywhere inside it reads as nought rather than counting down to a middle that means nothing.
/// And while the destination names things to look for, the first of them actually spawned inside
/// the circle is aimed at instead of the circle: that is the sparkling thing the step is about,
/// which the data never says and the world does.</para></summary>
internal sealed unsafe class Heading(IGuidance guidance, IObjectTable objects) : IHeading
{
    /// <inheritdoc/>
    public float? Needle => Offset() is var (dx, _, dz, _) ? Compass.NeedleAngle(Compass.Bearing(dx, dz), CameraYaw()) : null;

    /// <inheritdoc/>
    public float? DistanceYalms => Offset() is var (dx, dy, dz, slack)
        ? MathF.Max(0f, MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz)) - slack)
        : null;

    /// <inheritdoc/>
    public float? RiseYalms => Offset() is var (_, dy, _, _) ? dy : null;

    /// <summary>Which way the camera faces, which is what the needle turns against. Zero while
    /// there is no camera, which points the needle due north rather than nowhere.</summary>
    private static float CameraYaw()
    {
        var cameras = CameraManager.Instance();
        var camera = cameras == null ? null : cameras->Camera;
        return camera == null ? 0f : camera->DirH;
    }

    private static float Apart(Place place, Vector3 position)
    {
        var (dx, dy, dz) = (place.X - position.X, place.Y - position.Y, place.Z - position.Z);
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    /// <summary>How far to the thing being walked to, and how much of that distance does not count
    /// because the thing is an area with room to stand in.</summary>
    private (float Dx, float Dy, float Dz, float Slack)? Offset()
    {
        if (guidance.Current?.Route?.Legs is not [Leg.Walk walk, ..] || objects.LocalPlayer is not { } player)
        {
            return null;
        }

        var position = player.Position;
        if (Spawned(walk.To) is { } mark)
        {
            return (mark.X - position.X, mark.Y - position.Y, mark.Z - position.Z, 0f);
        }

        return (walk.To.X - position.X, walk.To.Y - position.Y, walk.To.Z - position.Z, walk.To.Radius);
    }

    /// <summary>The nearest thing the destination named that is in the world right now and inside
    /// the area being walked to, or null. Only asked of an area: a place with no room in it is
    /// already the answer, and a thing outside the circle is some other step's.</summary>
    private Vector3? Spawned(Place area)
    {
        if (area.Radius <= 0f
            || guidance.Current?.Target?.Where is not Destination.Reachable { Marks: { Count: > 0 } marks })
        {
            return null;
        }

        Vector3? nearest = null;
        var best = area.Radius;
        foreach (var candidate in objects)
        {
            if (!marks.Contains(candidate.BaseId))
            {
                continue;
            }

            var apart = Apart(area, candidate.Position);
            if (apart <= best)
            {
                best = apart;
                nearest = candidate.Position;
            }
        }

        return nearest;
    }
}
