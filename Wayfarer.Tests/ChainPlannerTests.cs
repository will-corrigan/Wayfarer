using Wayfarer.Core.Guidance;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

public class ChainPlannerTests
{
    [Fact]
    public void GroupsByZone_NeverInterleavingThem()
    {
        List<Leg> legs =
        [
            new("a1", 100, 0f, 0f),
            new("b1", 200, 0f, 0f),
            new("a2", 100, 5f, 0f),
            new("c1", 300, 0f, 0f),
            new("b2", 200, 5f, 0f),
        ];

        var ordered = ChainPlanner.Order(
            legs, l => l.Territory, l => (l.X, l.Z), currentTerritory: 100, 0f, 0f, Cost, _ => null);

        var zoneRuns = ordered.Select(l => l.Territory).Distinct().ToList();
        Assert.Equal(zoneRuns.Count, CountZoneRuns(ordered));
        Assert.Equal(100u, ordered[0].Territory); // the zone the player is standing in comes first
    }

    [Fact]
    public void CheapestFirst_PrefersTheSameNetworkBeforeATeleport()
    {
        List<Leg> legs =
        [
            new("far", 300, 0f, 0f),   // needs a teleport from 100
            new("near", 101, 0f, 0f),  // shares the aethernet with 100
        ];

        var ordered = ChainPlanner.Order(
            legs, l => l.Territory, l => (l.X, l.Z), currentTerritory: 100, 0f, 0f, Cost, _ => null);

        Assert.Equal(["near", "far"], [.. ordered.Select(l => l.Name)], StringComparer.Ordinal);
    }

    [Fact]
    public void EachZoneWalkStartsFromItsArrivalPoint()
    {
        List<Leg> legs =
        [
            new("byTheAetheryte", 200, 100f, 0f),
            new("farSide", 200, 0f, 0f),
        ];

        var ordered = ChainPlanner.Order(
            legs,
            l => l.Territory,
            l => (l.X, l.Z),
            currentTerritory: 100,
            0f,
            0f,
            Cost,
            zone => zone == 200 ? (105f, 0f) : null);

        // Dataset order would have started at farSide; arriving at (105, 0) makes the nearby target
        // the first stop, which is what "teleport in, then walk" looks like.
        Assert.Equal(["byTheAetheryte", "farSide"], [.. ordered.Select(l => l.Name)], StringComparer.Ordinal);
    }

    [Fact]
    public void CurrentZoneIsChainedFromThePlayerPosition()
    {
        List<Leg> legs = [new("far", 100, 100f, 0f), new("near", 100, 10f, 0f)];

        var ordered = ChainPlanner.Order(
            legs, l => l.Territory, l => (l.X, l.Z), currentTerritory: 100, 0f, 0f, Cost, _ => null);

        Assert.Equal(["near", "far"], [.. ordered.Select(l => l.Name)], StringComparer.Ordinal);
    }

    /// <summary>Pins that the unlock route can migrate onto this planner without a behaviour
    /// change: the lowest-level policy reproduces RoutePlanner.Order exactly.</summary>
    [Fact]
    public void LowestLevelFirstPolicy_MatchesRoutePlannerOrder()
    {
        List<ResolvedUnlock> unlocks =
        [
            Unlock("here-far", territory: 100, x: 80f, z: 0f, level: 30),
            Unlock("here-near", territory: 100, x: 10f, z: 0f, level: 40),
            Unlock("mid-a", territory: 200, x: 0f, z: 0f, level: 20),
            Unlock("mid-b", territory: 200, x: 50f, z: 0f, level: 25),
            Unlock("low", territory: 300, x: 0f, z: 0f, level: 5),
        ];

        var expected = RoutePlanner.Order([.. unlocks], currentTerritory: 100, px: 0f, pz: 0f);
        var actual = ChainPlanner.Order(
            unlocks,
            u => u.GiverTerritory!.Value,
            u => (u.GiverX, u.GiverZ),
            currentTerritory: 100,
            playerX: 0f,
            playerZ: 0f,
            zoneToZoneCost: Cost,
            arrivalPointOf: _ => null,
            policy: ZoneOrderPolicy.LowestLevelFirst,
            levelOf: u => u.QuestLevel);

        Assert.Equal([.. expected.Select(u => u.Def.Unlock)], [.. actual.Select(u => u.Def.Unlock)], StringComparer.Ordinal);
    }

    private static bool SameNetwork(uint from, uint to) => from / 100 == to / 100;

    /// <summary>Zone-to-zone cost shaped like the real one: free to stay, cheap to hop the shared
    /// aethernet, expensive to teleport.</summary>
    private static float Cost(uint from, uint to) => from == to ? 0f : (SameNetwork(from, to) ? 1f : 2f);

    private static int CountZoneRuns(IReadOnlyList<Leg> ordered)
    {
        var runs = 0;
        uint? previous = null;
        foreach (var leg in ordered)
        {
            if (previous != leg.Territory)
            {
                runs++;
            }

            previous = leg.Territory;
        }

        return runs;
    }

    private static ResolvedUnlock Unlock(string name, uint territory, float x, float z, int level) =>
        new()
        {
            Def = new UnlockDefinition { Unlock = name },
            QuestLevel = level,
            GiverTerritory = territory,
            GiverX = x,
            GiverZ = z,
        };

    private sealed record Leg(string Name, uint Territory, float X, float Z);
}
