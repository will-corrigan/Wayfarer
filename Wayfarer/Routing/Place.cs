namespace Wayfarer.Routing;

/// <summary>Somewhere a player can be sent: a zone, the map within it (a floor, or an interior
/// drawn as its own map), a world position, and a radius when the place is an area to search
/// rather than a point to stand on.</summary>
/// <param name="Territory">The game's territory id — the zone.</param>
/// <param name="Map">The map id within the territory. Two places on the same territory but
/// different maps cannot be walked between; a door joins them.</param>
/// <param name="X">World X.</param>
/// <param name="Y">World Y (height).</param>
/// <param name="Z">World Z.</param>
/// <param name="Radius">Zero for a point; the circle's radius for a search area.</param>
public sealed record Place(uint Territory, uint Map, float X, float Y, float Z, float Radius = 0f);
