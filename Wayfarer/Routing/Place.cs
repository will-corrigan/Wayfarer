using System.Numerics;

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
public sealed record Place(uint Territory, uint Map, float X, float Y, float Z, float Radius = 0f)
{
    /// <summary>How far this is from another across the ground, ignoring the drop between them.
    ///
    /// <para>Heights in the routing data are flat, and a step's own places are written where the
    /// data says rather than where a thing stands, so counting the drop would push something on a
    /// ledge or a floor above out of ground it is plainly standing in. Says nothing about whether
    /// the two are even in the same zone: ask that first, because two zones hold the very same
    /// numbers.</para></summary>
    /// <param name="other">Where to measure to.</param>
    public float OnTheGround(Place other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Vector2.Distance(new Vector2(X, Z), new Vector2(other.X, other.Z));
    }
}
