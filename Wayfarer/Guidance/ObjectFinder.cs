using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
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
/// often holds several of the same thing and only one is the one, so what the player has already
/// tried is passed over and the next nearest is guided to instead. They are never told how many
/// there are: the answer they want is which one to walk to now.</para></summary>
internal sealed unsafe class ObjectFinder(IObjectTable objects, IInteractions interactions) : IObjectFinder
{
    /// <inheritdoc/>
    public Place? Inside(Place area, IReadOnlyList<Mark>? marks, EventId? owner, IReadOnlyList<Place>? expected)
    {
        ArgumentNullException.ThrowIfNull(area);

        // One sort of thing is what a place with room in it is about, and the other only turns up
        // because of it: the thing to act on is there first, and once acted on whatever it called
        // is there too. So the things are looked for first and the creatures only when there are
        // none, and only when neither is standing there does where they are known to stand answer
        // instead. That is judged on what is there now, so nothing has to know what sort of errand
        // this is.
        return Nearest(area, marks, owner, MarkKind.Thing)
            ?? Nearest(area, marks, owner, MarkKind.Creature)
            ?? Awaited(area, expected);
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
    private static bool Wanted(IGameObject candidate, List<Mark>? marks, EventId? owner, MarkKind sort)
    {
        // The game stamps what it spawned for an event, which is how a thing nobody listed is still
        // recognised. It says nothing about which sort it is, so it answers for things to act on:
        // that is what a step sends the player to before anything has been summoned.
        if (sort is MarkKind.Thing && owner is { } stamped && candidate.Address != 0
            && ((GameObjectStruct*)candidate.Address)->EventId == stamped)
        {
            return true;
        }

        return marks is not null && marks.Any(mark => mark.Id == candidate.BaseId);
    }

    /// <summary>Whether the caller said anything at all to look for. Without that there is nothing
    /// to recognise and the circle is just a circle.</summary>
    private static bool AnythingToLookFor(List<Mark>? marks, EventId? owner, MarkKind sort) =>
        (sort is MarkKind.Thing && owner is not null) || marks?.Count > 0;

    /// <summary>Where the things are known to stand, nearest the player, for when none of them is
    /// standing there yet.</summary>
    private Place? Awaited(Place area, IReadOnlyList<Place>? expected)
    {
        if (expected is null || objects.LocalPlayer is not { } player)
        {
            return null;
        }

        var standing = player.Position;
        return expected
            .Where(place => place.Territory == area.Territory && OnTheGround(area.X, area.Z, place.X, place.Z) <= area.Radius)
            .OrderBy(place => OnTheGround(standing.X, standing.Z, place.X, place.Z))
            .FirstOrDefault();
    }

    /// <summary>The nearest untried thing of one sort standing inside an area.</summary>
    private Place? Nearest(Place area, IReadOnlyList<Mark>? marks, EventId? owner, MarkKind sort)
    {
        List<Mark>? wanted = marks is null ? null : [.. marks.Where(mark => mark.Kind == sort)];
        if (area.Radius <= 0f || !AnythingToLookFor(wanted, owner, sort) || objects.LocalPlayer is not { } player)
        {
            return null;
        }

        var standing = player.Position;
        Place? nearest = null;
        var any = false;
        var best = float.MaxValue;
        foreach (var candidate in objects)
        {
            if (!candidate.IsTargetable || !Wanted(candidate, wanted, owner, sort))
            {
                continue;
            }

            var position = candidate.Position;
            if (OnTheGround(area.X, area.Z, position.X, position.Z) > area.Radius)
            {
                continue;
            }

            any = true;
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

        // Everything standing here has been tried and none of them answered. Rather than say there
        // is nothing, the player is sent round them again from the beginning.
        return nearest ?? Again(area, marks, owner, sort, any);
    }

    /// <summary>The nearest of them all when every one has been tried, so a step whose answer was
    /// missed still leads somewhere.</summary>
    private Place? Again(Place area, IReadOnlyList<Mark>? marks, EventId? owner, MarkKind sort, bool any)
    {
        if (!any)
        {
            return null;
        }

        interactions.Forget();
        return Nearest(area, marks, owner, sort);
    }
}
