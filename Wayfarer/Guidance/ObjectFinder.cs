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
    public Found? Inside(Place area, IReadOnlyList<Mark>? marks, EventId? owner)
    {
        ArgumentNullException.ThrowIfNull(area);

        if (objects.LocalPlayer is not { } player)
        {
            return null;
        }

        var from = new Place(area.Territory, area.Map, player.Position.X, player.Position.Y, player.Position.Z);
        var standing = Standing(area);
        var stamp = owner is { } known ? (uint)known : 0u;

        var (nearest, any) = MarkSearch.Choose(area, standing, marks, stamp, from, interactions.Tried);
        if (nearest is not null)
        {
            return nearest;
        }

        // Everything standing here has been tried and none of them answered. Rather than say there
        // is nothing, the player is sent round them again from the beginning.
        if (any)
        {
            interactions.Forget();
            return MarkSearch.Choose(area, standing, marks, stamp, from).Nearest;
        }

        return null;
    }

    /// <summary>Everything the world holds that could be what a module is looking for, read once so
    /// the choosing is made on one moment's worth of it rather than on a list that moves underneath.
    /// </summary>
    private List<Candidate> Standing(Place area)
    {
        var standing = new List<Candidate>();
        foreach (var candidate in objects)
        {
            var position = candidate.Position;
            var raw = candidate.Address == 0 ? null : (GameObjectStruct*)candidate.Address;
            standing.Add(new Candidate(
                candidate.GameObjectId,
                candidate.BaseId,
                raw == null ? 0u : (uint)raw->EventId,
                candidate.IsTargetable,
                raw == null ? 0u : raw->NamePlateIconId,
                new Place(area.Territory, area.Map, position.X, position.Y, position.Z)));
        }

        return standing;
    }
}
