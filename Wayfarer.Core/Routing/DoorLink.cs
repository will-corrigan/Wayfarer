namespace Wayfarer.Core.Routing;

/// <summary>A door between two maps, walkable in both directions: a building's entrance drawn on
/// the city map, a staircase between two floors.</summary>
/// <param name="Name">What a surface calls it.</param>
/// <param name="From">One side.</param>
/// <param name="To">The other side.</param>
public sealed record DoorLink(string Name, Place From, Place To);
