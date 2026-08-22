namespace Wayfarer.Core.Navigation;

/// <summary>How a live quest marker relates to the player's current location. Pure
/// decision extracted from QuestNavigator's marker scan so it's unit-testable without
/// any Dalamud/ClientStructs dependency. See <see cref="MarkerSelection.Select"/> for
/// the tie-break rules and the caller-side routing this decision drives.</summary>
public enum MarkerMatch
{
    /// <summary>A marker matches both the player's current territory AND map — the
    /// unambiguous case: walk straight there (SameZone, unchanged from before this
    /// decision was extracted).</summary>
    Exact,

    /// <summary>A marker's territory matches the player's but its map does not.
    /// Verified live for one shape of this: an entrance marker for an interior
    /// objective (Fortemps Manor) sitting in the player's outdoor zone, where no
    /// cross-map route data exists at all — falling back to a direct arrow at the
    /// marker is the right, least-bad call there. NOT verified, and NOT assumed safe,
    /// for the general case: a marker on a genuinely different map layer of the same
    /// territory (e.g. a basement/upper floor reached by stairs) could just as easily
    /// need real routing through a map-link entrance, and a raw straight-line arrow
    /// would point through the floor/wall between them. Because this enum alone can't
    /// tell the two apart, the caller MUST try cross-map candidate routing (aethernet/
    /// entrance/teleport) first and only fall back to a direct arrow at this marker
    /// when no such candidate exists — never jump straight to SameZone on this
    /// verdict.</summary>
    TerritoryOnly,

    /// <summary>No marker anywhere in the player's current territory (the marker, if
    /// any, is for a step the player hasn't reached this zone for yet).</summary>
    None,
}

/// <summary>One live quest marker's position and the map/territory it belongs to, in
/// that map's own coordinate space.</summary>
public sealed record MarkerPoint(float X, float Y, float Z, uint TerritoryId, uint MapId);

public static class MarkerSelection
{
    /// <summary>Picks the tier (<see cref="MarkerMatch"/>) and, within that tier, the
    /// nearest marker to the player. An exact (territory+map) match always wins over a
    /// closer territory-only marker — exactness is a hard precedence, not something
    /// distance can override, because only an exact match is safe to arrow straight
    /// at. Ties within a tier are broken by straight-line distance to the player.
    /// Returns (<see cref="MarkerMatch.None"/>, null) when <paramref name="markers"/>
    /// has nothing in the player's current territory (it may still have entries for
    /// other territories — the caller's cross-territory fallback path handles those,
    /// this function only reasons about same-territory candidates).</summary>
    public static (MarkerMatch Match, MarkerPoint? Marker) Select(
        IReadOnlyList<MarkerPoint> markers,
        uint currentTerritory,
        uint currentMapId,
        float px,
        float py,
        float pz)
    {
        MarkerPoint? bestExact = null;
        var bestExactDist = float.MaxValue;
        MarkerPoint? bestTerritoryOnly = null;
        var bestTerritoryOnlyDist = float.MaxValue;

        foreach (var m in markers)
        {
            if (m.TerritoryId != currentTerritory)
            {
                continue;
            }

            var d = NavMath.Distance(m.X - px, m.Y - py, m.Z - pz);
            if (m.MapId == currentMapId)
            {
                if (d < bestExactDist)
                {
                    bestExact = m;
                    bestExactDist = d;
                }
            }
            else if (d < bestTerritoryOnlyDist)
            {
                bestTerritoryOnly = m;
                bestTerritoryOnlyDist = d;
            }
        }

        if (bestExact is { } exact)
        {
            return (MarkerMatch.Exact, exact);
        }

        if (bestTerritoryOnly is { } territoryOnly)
        {
            return (MarkerMatch.TerritoryOnly, territoryOnly);
        }

        return (MarkerMatch.None, null);
    }
}
