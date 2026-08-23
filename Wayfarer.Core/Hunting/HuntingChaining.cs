namespace Wayfarer.Core.Hunting;

/// <summary>Route-chaining for hunting-log targets, the counterpart of the checklist's "Route me" — a "hunt
/// here" nearest-first ordering among the current page's remaining targets, reusing
/// <c>Unlocks.RoutePlanner</c>'s exact greedy-nearest-neighbor algorithm. Restricted to the
/// player's current zone: unlike unlock pickups (which have a natural next-lowest-level zone to
/// hop to), hunting-log monsters have no analogous cross-zone ordering, so entries outside
/// <c>currentTerritory</c> are simply dropped rather than chained after — the caller decides what,
/// if anything, to show for zones with no remaining targets nearby.</summary>
public static class HuntingChaining
{
    /// <summary>Greedy-nearest chain of every <paramref name="targets"/> entry in
    /// <paramref name="currentTerritory"/>, starting from the player position
    /// (<paramref name="px"/>, <paramref name="pz"/>) and always hopping to the closest remaining
    /// entry from wherever the chain currently stands — identical algorithm to
    /// <c>Unlocks.RoutePlanner</c>'s private <c>Chain</c> helper.</summary>
    public static List<HuntingChainTarget> OrderNearestFirst(
        IEnumerable<HuntingChainTarget> targets, uint currentTerritory, float px, float pz)
    {
        var pool = targets.Where(t => t.TerritoryTypeId == currentTerritory).ToList();
        var result = new List<HuntingChainTarget>();
        var x = px;
        var z = pz;
        while (pool.Count > 0)
        {
            HuntingChainTarget? best = null;
            var bestSq = float.MaxValue;
            foreach (var t in pool)
            {
                var dx = t.WorldX - x;
                var dz = t.WorldZ - z;
                var sq = (dx * dx) + (dz * dz);
                if (sq < bestSq)
                {
                    best = t;
                    bestSq = sq;
                }
            }

            result.Add(best!);
            pool.Remove(best!);
            x = best!.WorldX;
            z = best.WorldZ;
        }

        return result;
    }
}
