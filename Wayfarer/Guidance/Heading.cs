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
/// When the module picked out one particular thing, the needle follows that thing as it moves,
/// which is all this does with it: which thing, and why, was settled before it was published.</para>
/// </summary>
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

    /// <summary>How far to the thing being walked to, and how much of that distance does not count
    /// because the thing is an area with room to stand in.</summary>
    private (float Dx, float Dy, float Dz, float Slack)? Offset()
    {
        if (guidance.Current?.Route?.Legs is not [Leg.Walk walk, ..] || objects.LocalPlayer is not { } player)
        {
            return null;
        }

        var position = player.Position;
        if (Following() is { } thing)
        {
            var (x, y, z) = (thing.X, thing.Y, thing.Z);
            return (x - position.X, y - position.Y, z - position.Z, 0f);
        }

        return (walk.To.X - position.X, walk.To.Y - position.Y, walk.To.Z - position.Z, walk.To.Radius);
    }

    /// <summary>Where the thing the module picked is standing now, or null when it picked none.
    /// It is looked up again every frame because it walks about: a person who was twenty yalms off
    /// when guidance was published is not there a moment later, and the needle has to follow. Where
    /// it stood when it was picked answers while it is not loaded.</summary>
    private Place? Following()
    {
        if (guidance.Current?.Target?.Where is not Destination.AtObject thing)
        {
            return null;
        }

        if (objects.SearchById(thing.Id) is not { } standing)
        {
            return thing.At;
        }

        var at = standing.Position;
        return new Place(thing.At.Territory, thing.At.Map, at.X, at.Y, at.Z);
    }
}
