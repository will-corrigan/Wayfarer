using Dalamud.Game.ClientState.Objects.Types;
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
/// standing in.</para>
///
/// <para>Being inside the circle is what makes something a candidate; which one is guided to is
/// then decided by how near it is to the player, not to the middle of the circle. A search area
/// often holds several of the same thing, so they are counted as well: only one of them is the
/// one, and the player has to try them.</para></summary>
internal sealed unsafe class ObjectFinder(IObjectTable objects, IInteractions interactions) : IObjectFinder
{
    /// <inheritdoc/>
    public Found? Inside(Place area, IReadOnlyList<uint>? marks, uint? owner)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (area.Radius <= 0f || !AnythingToLookFor(marks, owner) || objects.LocalPlayer is not { } player)
        {
            return null;
        }

        var standing = player.Position;
        Place? nearest = null;
        var count = 0;
        var best = float.MaxValue;
        foreach (var candidate in objects)
        {
            if (!candidate.IsTargetable || !Wanted(candidate, marks, owner))
            {
                continue;
            }

            var position = candidate.Position;
            if (OnTheGround(area.X, area.Z, position.X, position.Z) > area.Radius)
            {
                continue;
            }

            count++;
            if (interactions.Tried(candidate.BaseId))
            {
                continue;
            }

            var apart = OnTheGround(standing.X, standing.Z, position.X, position.Z);
            if (apart < best)
            {
                best = apart;
                nearest = new Place(area.Territory, area.Map, position.X, position.Y, position.Z);
            }
        }

        if (nearest is { } at)
        {
            return new Found(at, Math.Max(1, count - interactions.Count));
        }

        // Everything standing here has been tried and none of them answered. Rather than say there
        // is nothing, the player is sent round them again from the beginning.
        return Again(area, marks, owner, count);
    }

    /// <summary>How far apart two points are across the ground, ignoring the drop between them.</summary>
    private static float OnTheGround(float fromX, float fromZ, float toX, float toZ)
    {
        var (dx, dz) = (fromX - toX, fromZ - toZ);
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    /// <summary>Whether this is one of the things being looked for: stamped by the game as the
    /// event's own, or named by the module. The stamp is asked first because it is the game's own
    /// answer and covers objects nobody listed.</summary>
    private static bool Wanted(IGameObject candidate, IReadOnlyList<uint>? marks, uint? owner)
    {
        if (owner is { } id && candidate.Address != 0 && ((GameObjectStruct*)candidate.Address)->EventId.Id == id)
        {
            return true;
        }

        return marks is not null && marks.Contains(candidate.BaseId);
    }

    /// <summary>Whether the caller said anything at all to look for. Without that there is nothing
    /// to recognise and the circle is just a circle.</summary>
    private static bool AnythingToLookFor(IReadOnlyList<uint>? marks, uint? owner) =>
        owner is not null || marks?.Count > 0;

    /// <summary>The nearest of them all when every one has been tried, so a step whose answer was
    /// missed still leads somewhere.</summary>
    private Found? Again(Place area, IReadOnlyList<uint>? marks, uint? owner, int count)
    {
        if (count == 0)
        {
            return null;
        }

        interactions.Forget();
        return Inside(area, marks, owner);
    }
}
