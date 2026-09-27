using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;

namespace Wayfarer.Modules.Treasure;

/// <summary>The treasure the game has loaded near the player and not yet opened: coffers in the
/// Occult Crescent, chests in dungeons. An opened one stops being targetable, which is how it drops
/// out. Looked up once a frame however many markers ask. Game thread only.</summary>
internal sealed class LiveTreasure(IObjectTable objects, IClientState clientState, IFramework framework)
{
    private readonly List<Vector3> positions = [];
    private readonly List<string> names = [];
    private DateTime readAt;

    /// <summary>Where each loaded chest stands.</summary>
    public IReadOnlyList<Vector3> Positions
    {
        get
        {
            Refresh();
            return positions;
        }
    }

    /// <summary>What each loaded chest is called, in the same order as <see cref="Positions"/>.</summary>
    public IReadOnlyList<string> Names
    {
        get
        {
            Refresh();
            return names;
        }
    }

    /// <summary>The zone the chests are in.</summary>
    public uint Territory => clientState.TerritoryType;

    /// <summary>The map the player is on, which is the one the chests are drawn on.</summary>
    public uint Map => clientState.MapId;

    private void Refresh()
    {
        if (readAt == framework.LastUpdateUTC)
        {
            return;
        }

        readAt = framework.LastUpdateUTC;
        positions.Clear();
        names.Clear();

        // The whole table, not only its event-object range: which range the game files treasure
        // under is not written down anywhere, and this only runs while the map is open.
        foreach (var thing in objects)
        {
            if (thing.ObjectKind == ObjectKind.Treasure && thing.IsTargetable)
            {
                positions.Add(thing.Position);
                names.Add(thing.Name.TextValue);
            }
        }
    }
}
