namespace Wayfarer.Core.Guidance;

/// <summary>How to order the zones a plan visits.</summary>
public enum ZoneOrderPolicy
{
    /// <summary>Nearest-zone-first by the injected zone-to-zone cost: stay where you are, then take
    /// zones you can reach on the free aethernet, then the ones that need a teleport. This is what
    /// "minimise teleports" means in practice.</summary>
    CheapestFirst,

    /// <summary>Lowest level first, matching the unlock route's existing ordering. Requires a level
    /// selector.</summary>
    LowestLevelFirst,
}

/// <summary>Orders a multi-zone plan: group by zone, chain the zones, chain within each zone. The
/// zone-ordering policy and the zone-to-zone cost are INJECTED, so this stays pure and the plugin
/// side supplies real aetheryte/route facts.
///
/// Within a zone the walk starts from that zone's ARRIVAL POINT — the aetheryte the router would
/// send you to — not from an arbitrary member. That is what "one teleport per zone, then walk"
/// actually means, and it is why this is not just a nearest-neighbour chain applied twice.</summary>
public static class ChainPlanner
{
    /// <summary>Orders a plan: the zone the player is in first, then the remaining zones by policy,
    /// with each zone's legs walked greedily from that zone's arrival point.</summary>
    /// <typeparam name="T">Whatever a leg is to the calling source. The planner only ever asks it
    /// for a territory, a position and (optionally) a level.</typeparam>
    /// <param name="legs">The legs to order.</param>
    /// <param name="territoryOf">Which zone a leg is in.</param>
    /// <param name="positionOf">Where a leg is, in its own zone's coordinate space.</param>
    /// <param name="currentTerritory">Where the plan starts from.</param>
    /// <param name="playerX">Start X within <paramref name="currentTerritory"/>.</param>
    /// <param name="playerZ">Start Z within <paramref name="currentTerritory"/>.</param>
    /// <param name="zoneToZoneCost">Cost of travelling from one territory to another. Only the
    /// ORDER it induces matters, never the units.</param>
    /// <param name="arrivalPointOf">Where the player would arrive in a territory, or null when
    /// nothing is known — the chain then starts from that zone's first leg.</param>
    /// <param name="policy">How the zones themselves are ordered.</param>
    /// <param name="levelOf">Required by <see cref="ZoneOrderPolicy.LowestLevelFirst"/>, ignored
    /// otherwise.</param>
    public static List<T> Order<T>(
        IReadOnlyList<T> legs,
        Func<T, uint> territoryOf,
        Func<T, (float X, float Z)> positionOf,
        uint currentTerritory,
        float playerX,
        float playerZ,
        Func<uint, uint, float> zoneToZoneCost,
        Func<uint, (float X, float Z)?> arrivalPointOf,
        ZoneOrderPolicy policy = ZoneOrderPolicy.CheapestFirst,
        Func<T, int>? levelOf = null)
    {
        var byZone = new Dictionary<uint, List<T>>();
        var zones = new List<uint>(); // first-appearance order, so ties break deterministically
        foreach (var leg in legs)
        {
            var zone = territoryOf(leg);
            if (!byZone.TryGetValue(zone, out var members))
            {
                byZone[zone] = members = [];
                zones.Add(zone);
            }

            members.Add(leg);
        }

        var result = new List<T>(legs.Count);

        // The zone the player is already standing in always comes first, chained from where they
        // actually are: there is nothing cheaper than not travelling.
        if (byZone.Remove(currentTerritory, out var here))
        {
            zones.Remove(currentTerritory);
            ChainNearest(here, playerX, playerZ, positionOf, result);
        }

        foreach (var zone in OrderZones(zones, byZone, currentTerritory, zoneToZoneCost, policy, levelOf))
        {
            var members = byZone[zone];
            var (startX, startZ) = StartPoint(zone, members, positionOf, arrivalPointOf, policy, levelOf);
            ChainNearest(members, startX, startZ, positionOf, result);
        }

        return result;
    }

    private static List<uint> OrderZones<T>(
        List<uint> zones,
        Dictionary<uint, List<T>> byZone,
        uint currentTerritory,
        Func<uint, uint, float> zoneToZoneCost,
        ZoneOrderPolicy policy,
        Func<T, int>? levelOf)
    {
        if (policy == ZoneOrderPolicy.LowestLevelFirst && levelOf is not null)
        {
            // OrderBy is stable, so zones sharing a lowest level keep first-appearance order.
            return [.. zones.OrderBy(z => byZone[z].Min(levelOf))];
        }

        // Greedy nearest-zone tour from where the player stands: pick the cheapest next zone, then
        // continue from there, so zones on one network are visited together instead of ping-ponging.
        var remaining = new List<uint>(zones);
        var ordered = new List<uint>(zones.Count);
        var from = currentTerritory;
        while (remaining.Count > 0)
        {
            var best = remaining[0];
            var bestCost = zoneToZoneCost(from, best);
            foreach (var zone in remaining)
            {
                var cost = zoneToZoneCost(from, zone);
                if (cost < bestCost)
                {
                    best = zone;
                    bestCost = cost;
                }
            }

            ordered.Add(best);
            remaining.Remove(best);
            from = best;
        }

        return ordered;
    }

    private static (float X, float Z) StartPoint<T>(
        uint zone,
        List<T> members,
        Func<T, (float X, float Z)> positionOf,
        Func<uint, (float X, float Z)?> arrivalPointOf,
        ZoneOrderPolicy policy,
        Func<T, int>? levelOf)
    {
        // The lowest-level policy walks from the lowest-level member outward, matching the unlock
        // route's existing behaviour; the cheapest policy walks from where you land.
        if (policy == ZoneOrderPolicy.LowestLevelFirst && levelOf is not null)
        {
            var lowest = members[0];
            foreach (var member in members)
            {
                if (levelOf(member) < levelOf(lowest))
                {
                    lowest = member;
                }
            }

            return positionOf(lowest);
        }

        return arrivalPointOf(zone) ?? positionOf(members[0]);
    }

    /// <summary>Greedy nearest-neighbour walk from a starting point — the same chaining the unlock
    /// route and the hunting "hunt here" ordering already use, in one place.</summary>
    private static void ChainNearest<T>(
        List<T> pool, float x, float z, Func<T, (float X, float Z)> positionOf, List<T> result)
    {
        var remaining = new List<T>(pool);
        while (remaining.Count > 0)
        {
            var best = remaining[0];
            var bestSq = float.MaxValue;
            foreach (var candidate in remaining)
            {
                var (cx, cz) = positionOf(candidate);
                var dx = cx - x;
                var dz = cz - z;
                var sq = (dx * dx) + (dz * dz);
                if (sq < bestSq)
                {
                    best = candidate;
                    bestSq = sq;
                }
            }

            result.Add(best);
            remaining.Remove(best);
            (x, z) = positionOf(best);
        }
    }
}
