namespace Wayfarer.Core.Navigation;

/// <summary>Territory and Group default to 0 (unknown/no-aethernet-network) so existing
/// same-zone call sites that only care about Id/Name/X/Z keep compiling unchanged;
/// cross-zone route costing (<see cref="RouteCosting"/>) requires both to be populated
/// from the source Aetheryte sheet row (Territory.RowId, AethernetGroup).</summary>
public sealed record AetherytePoint(uint Id, string Name, float X, float Z, uint Territory = 0, uint Group = 0);

public static class AetherytePicker
{
    // The combined walking legs must beat the direct run by at least this (covers the
    // travel-menu interaction and loading hop). Also reused by RouteCosting as the
    // cross-zone aethernet candidate's menu-overhead slack.
    public const float RouteSlack = 60f;

    // Route via the aethernet only for real detours: objective further than this…
    private const float MinPlayerDistance = 120f;

    public static AetherytePoint? Nearest(IReadOnlyList<AetherytePoint> candidates, float x, float z)
    {
        AetherytePoint? best = null;
        var bestSq = float.MaxValue;
        foreach (var p in candidates)
        {
            var dx = p.X - x;
            var dz = p.Z - z;
            var sq = (dx * dx) + (dz * dz);
            if (sq < bestSq)
            {
                best = p;
                bestSq = sq;
            }
        }

        return best;
    }

    public static bool ShouldRouteViaAethernet(float playerToObjective, float playerToEntry, float exitToObjective) =>
        playerToObjective > MinPlayerDistance
        && playerToEntry + exitToObjective + RouteSlack < playerToObjective;
}
