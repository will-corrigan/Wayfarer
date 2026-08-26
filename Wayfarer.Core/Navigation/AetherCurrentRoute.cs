using Wayfarer.Core.Guidance;

namespace Wayfarer.Core.Navigation;

/// <summary>The order to visit a zone's outstanding aether currents in.
///
/// <para>Pure, with the travel costs INJECTED, so the rule can be tested without a game running and
/// the plugin side supplies real aetheryte facts — the same arrangement the hunting route uses. The
/// chaining itself is <see cref="ChainPlanner.Order"/>'s and deliberately not reimplemented here:
/// there is one nearest-neighbour walk in this plugin and every route shares it.</para></summary>
public static class AetherCurrentRoute
{
    /// <summary>Nearest first from where the player stands.
    ///
    /// <para>That single rule does more than it looks like, because it is applied through the shared
    /// planner: the zone the player is in is walked first from their actual position, and any other
    /// zone is walked from where they would LAND in it. Nine of the game's quest-granted currents are
    /// handed out in a neighbouring city rather than in the zone they unlock, so even one zone's
    /// route can span territories — and grouping them is the difference between one trip to
    /// Idyllshire and four.</para>
    ///
    /// <para>Placed currents and quest givers are deliberately NOT separated. A route through a
    /// zone's currents is a route, and sorting the quest stops to the end would send the player back
    /// across the map for them; the out-of-zone givers already fall to the end on their own, because
    /// they cost a journey and the planner orders zones by cost.</para>
    ///
    /// <para>Currents with no resolvable location go last, unordered. There is nothing to chain them
    /// by, and dropping them would make the plan quietly shorter than the zone.</para></summary>
    /// <param name="points">The outstanding currents, in any order.</param>
    /// <param name="currentTerritory">Where the player is now.</param>
    /// <param name="playerX">Their X within <paramref name="currentTerritory"/>.</param>
    /// <param name="playerZ">Their Z within <paramref name="currentTerritory"/>.</param>
    /// <param name="zoneToZoneCost">What travelling between two territories costs. Only the order it
    /// induces matters, never the units.</param>
    /// <param name="arrivalPointOf">Where the player would arrive in a territory, or null when
    /// nothing is known.</param>
    public static List<AetherCurrentPoint> Order(
        IReadOnlyList<AetherCurrentPoint> points,
        uint currentTerritory,
        float playerX,
        float playerZ,
        Func<uint, uint, float> zoneToZoneCost,
        Func<uint, (float X, float Z)?> arrivalPointOf)
    {
        var placed = new List<AetherCurrentPoint>();
        var unplaced = new List<AetherCurrentPoint>();
        foreach (var point in points)
        {
            (point.HasLocation ? placed : unplaced).Add(point);
        }

        var ordered = ChainPlanner.Order(
            placed,
            p => p.Territory,
            p => (p.X, p.Z),
            currentTerritory,
            playerX,
            playerZ,
            zoneToZoneCost,
            arrivalPointOf);

        ordered.AddRange(unplaced);
        return ordered;
    }
}
