namespace Wayfarer.Core.Navigation;

public sealed record AetherytePoint(uint Id, string Name, float X, float Z);

public static class AetherytePicker
{
    // Route via the aethernet only for real detours: objective further than this…
    private const float MinPlayerDistance = 120f;

    // …and the combined walking legs must beat the direct run by at least this
    // (covers the travel-menu interaction and loading hop).
    private const float RouteSlack = 60f;

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
