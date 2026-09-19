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
public sealed record DoorLink(string Name, Place From, Place To, bool OneWay = false);
