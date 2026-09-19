using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Wayfarer.Routing;
using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace Wayfarer.Guidance;

/// <summary>Finds a thing in the world inside an area, when a step sends the player to a circle
/// rather than a point. The data behind those circles says where to search and never what for, so
/// the answer can only come from what is actually standing there.
///
/// <para>An object counts when the game itself stamped it as belonging to the event being guided,
/// which it does to everything it spawns for one, or failing that when its id is one the caller
/// listed. It also has to be something the player could act on right now, which is the game's own
/// question and not ours: an object can be present and inert until its step comes round.</para>
///
/// <para>Whether it is inside the circle is judged on the ground only. Heights in the routing data
/// are flat, so counting the drop would push an object on a ledge out of a circle it is plainly
/// standing in.</para></summary>
internal sealed unsafe class ObjectFinder(IObjectTable objects) : IObjectFinder
{
    /// <inheritdoc/>
    public Place? Inside(Place area, IReadOnlyList<uint>? marks, uint? owner)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (area.Radius <= 0f || (owner is null && (marks is null || marks.Count == 0)))
        {
            return null;
        }

        Place? nearest = null;
        var best = area.Radius;
        foreach (var candidate in objects)
        {
            if (!candidate.IsTargetable || !Wanted(candidate.Address, candidate.BaseId, marks, owner))
            {
                continue;
            }

            var position = candidate.Position;
            var apart = OnTheGround(area, position.X, position.Z);
            if (apart <= best)
            {
                best = apart;
                nearest = new Place(area.Territory, area.Map, position.X, position.Y, position.Z);
            }
        }

        return nearest;
    }

    /// <summary>How far apart two points are across the ground, ignoring the drop between them.</summary>
    private static float OnTheGround(Place area, float x, float z)
    {
        var (dx, dz) = (area.X - x, area.Z - z);
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    /// <summary>Whether this is one of the things being looked for: stamped by the game as the
    /// event's own, or named by the module. The stamp is asked first because it is the game's own
    /// answer and covers objects nobody listed.</summary>
    private static bool Wanted(nint address, uint baseId, IReadOnlyList<uint>? marks, uint? owner)
    {
        if (owner is { } id && address != 0 && ((GameObjectStruct*)address)->EventId.Id == id)
        {
            return true;
        }

        return marks is not null && marks.Contains(baseId);
    }
}
