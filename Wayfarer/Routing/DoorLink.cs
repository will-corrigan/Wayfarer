namespace Wayfarer.Routing;

/// <summary>A door between two maps: a building's entrance drawn on the city map, a staircase
/// between two floors, a ledge you can drop off.</summary>
/// <param name="Name">What a surface calls it.</param>
/// <param name="From">The side you enter from.</param>
/// <param name="To">The side you come out on.</param>
/// <param name="OneWay">True when it can only be passed from <paramref name="From"/> to
/// <paramref name="To"/> — a drop with no way back up, or a door whose return side the game's data
/// does not mark. Treating a missing return as one-way is the safe reading: no route ever leads
/// through a door that cannot be opened from that side.</param>
/// <param name="Npc">Who to talk to, when the door is passed by asking someone: a lift attendant,
/// an airship's purser. Null for a door walked through.</param>
/// <param name="Quests">Quests that must be complete before whoever keeps the door will let the
/// player through, or null for none. A door the player cannot use yet is left out of their route.</param>
/// <param name="Warp">The game's Warp row the door sends you through, or zero for a door walked
/// through. What the door is; its name is only what it is called.</param>
/// <param name="Person">The game's id for whoever is asked, the ENpcBase row, or zero when nobody
/// is.</param>
/// <param name="Festival">The game's id for the seasonal event the door is only there during, such
/// as the Moonfire Faire, or zero for a door that is always there.</param>
/// <param name="FestivalPhase">Which phase of that event, or zero for any.</param>
public sealed record DoorLink(
    string Name,
    Place From,
    Place To,
    bool OneWay = false,
    string? Npc = null,
    IReadOnlyList<uint>? Quests = null,
    uint Warp = 0,
    uint Person = 0,
    ushort Festival = 0,
    ushort FestivalPhase = 0);
