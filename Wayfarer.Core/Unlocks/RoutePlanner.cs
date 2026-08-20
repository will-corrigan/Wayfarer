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
