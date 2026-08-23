namespace Wayfarer.Core.Unlocks;

public static class RoutePlanner
{
    /// <summary>Orders pickups: current-territory entries greedy-nearest from the
    /// player, then remaining territories (ordered by their lowest quest level),
    /// each chained greedy-nearest from its lowest-level entry.</summary>
    public static List<ResolvedUnlock> Order(
        List<ResolvedUnlock> available, uint currentTerritory, float px, float pz)
    {
        var result = new List<ResolvedUnlock>();
        var here = available.Where(u => u.GiverTerritory == currentTerritory).ToList();
        Chain(here, px, pz, result);

        var rest = available
            .Where(u => u.GiverTerritory is { } t && t != currentTerritory)
            .GroupBy(u => u.GiverTerritory!.Value)
            .OrderBy(g => g.Min(u => u.QuestLevel));
        foreach (var group in rest)
        {
            var members = group.ToList();
            var start = members.OrderBy(u => u.QuestLevel).First();
            Chain(members, start.GiverX, start.GiverZ, result);
        }

        return result;
    }

    /// <summary>Top <paramref name="max"/> Available unlocks in <paramref name="currentTerritory"/>,
    /// nearest-first from the player position — the pure selection behind the widget's glanceable
    /// lines and the info bar's alert marker. Same Available + GiverTerritory==territory criterion as
    /// <see cref="UnlockStatusCalculator.Compute"/>; reuses the same greedy-nearest chain
    /// as <see cref="Order"/> restricted to the current zone.</summary>
    public static List<ResolvedUnlock> TopAvailableHere(
        IEnumerable<ResolvedUnlock> all, uint currentTerritory, float px, float pz, int max)
    {
        var here = all
            .Where(u => u.Status == UnlockStatus.Available && u.GiverTerritory == currentTerritory)
            .ToList();
        var ordered = new List<ResolvedUnlock>();
        Chain(here, px, pz, ordered);
        return ordered.Count > max ? ordered.GetRange(0, max) : ordered;
    }

    private static void Chain(List<ResolvedUnlock> pool, float x, float z, List<ResolvedUnlock> result)
    {
        while (pool.Count > 0)
        {
            ResolvedUnlock? best = null;
            var bestSq = float.MaxValue;
            foreach (var u in pool)
            {
                var dx = u.GiverX - x;
                var dz = u.GiverZ - z;
                var sq = (dx * dx) + (dz * dz);
                if (sq < bestSq)
                {
                    best = u;
                    bestSq = sq;
                }
            }

            result.Add(best!);
            pool.Remove(best!);
            x = best!.GiverX;
            z = best.GiverZ;
        }
    }
}
