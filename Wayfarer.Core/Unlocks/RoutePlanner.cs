namespace Wayfarer.Core.Unlocks;

/// <summary>Orders a set of unlock pickups into a route worth walking: this zone first, nearest
/// first, then the other zones in the order their content becomes relevant. The whole of what
/// "Route me" means, with no game dependency.</summary>
public static class RoutePlanner
{
    /// <summary>Orders pickups: current-territory entries greedy-nearest from the
    /// player, then remaining territories (ordered by their lowest quest level),
    /// each chained greedy-nearest from its lowest-level entry.</summary>
    public static List<ResolvedUnlock> Order(
        List<ResolvedUnlock> available, uint currentTerritory, float px, float pz)
    {
        var result = new List<ResolvedUnlock>();

        // Routable, not "has a territory". Those are the same answer for every entry a quest gives a
        // giver, and different answers for the channels that have no place at all: those used to be
        // filtered out by their territory happening to be null — a correct outcome nothing could
        // assert, and one that any later code populating a coordinate would silently have undone.
        // See ResolvedUnlock.Routable.
        var here = available.Where(u => u.Routable && u.GiverTerritory == currentTerritory).ToList();
        Chain(here, px, pz, result);

        var rest = available
            .Where(u => u.Routable && u.GiverTerritory is { } t && t != currentTerritory)
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

    /// <summary>The stops a "Route Me" press actually queues: <see cref="Order"/>, cut to
    /// <see cref="UnlockRouteCap.Stops"/>.
    ///
    /// <para>Separate from <see cref="Order"/> rather than a cap inside it, because the two answer
    /// different questions — "in what order are these worth walking" has no cap in it, and a caller
    /// that wants the whole ordering (the chain planner's own comparison against it, for one) must
    /// not have to work around one. This is the only thing the three Route Me buttons call, which is
    /// what stops one of them from being uncapped: the cap is not theirs to apply.</para>
    ///
    /// <para>The count of what was left out is not returned — the caller already knows it, from the
    /// same list it passed in. See <see cref="UnlockRouteCap.ButtonLabel"/> for the half of this
    /// that matters, which is that the number is on screen before the button is pressed.</para>
    /// </summary>
    public static List<ResolvedUnlock> Plan(
        List<ResolvedUnlock> available, uint currentTerritory, float px, float pz)
    {
        var ordered = Order(available, currentTerritory, px, pz);
        var take = UnlockRouteCap.Take(ordered.Count);
        return ordered.Count > take ? ordered.GetRange(0, take) : ordered;
    }

    /// <summary>Top <paramref name="max"/> Available unlocks in <paramref name="currentTerritory"/>,
    /// nearest-first from the player position — the pure selection behind the widget's glanceable
    /// lines and the info bar's alert marker. Same Available + routable-here criterion every other
    /// route affordance uses (see <see cref="ResolvedUnlock.Routable"/>); reuses the same
    /// greedy-nearest chain as <see cref="Order"/> restricted to the current zone.</summary>
    public static List<ResolvedUnlock> TopAvailableHere(
        IEnumerable<ResolvedUnlock> all, uint currentTerritory, float px, float pz, int max)
    {
        var here = all
            .Where(u => u.Status == UnlockStatus.Available
                && u.Routable
                && u.GiverTerritory == currentTerritory)
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
