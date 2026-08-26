using Wayfarer.Core.Navigation;

namespace Wayfarer.Tests;

/// <summary>The order a zone's outstanding aether currents are visited in.</summary>
public class AetherCurrentRouteTests
{
    /// <summary>Coerthas Western Highlands, and the territory its quest givers actually sit in.</summary>
    private const uint Home = 397;

    private const uint Neighbour = 478;

    private static readonly AetherCurrentPoint Blank =
        new(2818053, AetherCurrentKind.Attunable, 1, "Coerthas Western Highlands", Home, 211, 0f, 0f, 0f);

    [Fact]
    public void NearestFirstFromWhereThePlayerStands()
    {
        List<AetherCurrentPoint> points =
        [
            Placed("far", Home, 400f, 0f),
            Placed("near", Home, 10f, 0f),
            Placed("middle", Home, 100f, 0f),
        ];

        var ordered = AetherCurrentRoute.Order(points, Home, 0f, 0f, Cost, _ => null);

        Assert.Equal(["near", "middle", "far"], Names(ordered));
    }

    /// <summary>Placed currents and quest givers are not separated: within the zone the route is just
    /// a route, and pushing the quest stops to the end would send the player back across the
    /// map.</summary>
    [Fact]
    public void PlacedCurrentsAndQuestGiversAreInterleavedByDistance()
    {
        List<AetherCurrentPoint> points =
        [
            Placed("current-far", Home, 300f, 0f),
            QuestAt("giver-near", Home, 20f, 0f),
            Placed("current-near", Home, 10f, 0f),
            QuestAt("giver-far", Home, 400f, 0f),
        ];

        var ordered = AetherCurrentRoute.Order(points, Home, 0f, 0f, Cost, _ => null);

        Assert.Equal(["current-near", "giver-near", "current-far", "giver-far"], Names(ordered));
    }

    /// <summary>Nine of the game's quest currents are handed out in a neighbouring city. Those stops
    /// cost a journey, so they come after everything in the zone the player is already standing in —
    /// and they come as one group rather than being visited twice.</summary>
    [Fact]
    public void OutOfZoneGiversComeLastAndAsOneTrip()
    {
        List<AetherCurrentPoint> points =
        [
            QuestAt("elsewhere-a", Neighbour, 0f, 0f),
            Placed("here-a", Home, 200f, 0f),
            QuestAt("elsewhere-b", Neighbour, 10f, 0f),
            Placed("here-b", Home, 10f, 0f),
        ];

        var ordered = AetherCurrentRoute.Order(points, Home, 0f, 0f, Cost, _ => null);

        Assert.Equal(["here-b", "here-a", "elsewhere-a", "elsewhere-b"], Names(ordered));
    }

    /// <summary>A zone the player has to travel to is walked from where they would LAND, not from
    /// wherever the first stop happens to be listed.</summary>
    [Fact]
    public void ATravelledZoneIsWalkedFromItsArrivalPoint()
    {
        List<AetherCurrentPoint> points =
        [
            QuestAt("by-the-gate", Neighbour, 0f, 0f),
            QuestAt("across-town", Neighbour, 500f, 0f),
        ];

        var ordered = AetherCurrentRoute.Order(
            points, Home, 0f, 0f, Cost, zone => zone == Neighbour ? (500f, 0f) : null);

        Assert.Equal(["across-town", "by-the-gate"], Names(ordered));
    }

    /// <summary>A current with nowhere to go stays in the plan, at the end. Dropping it would make the
    /// plan quietly shorter than the zone.</summary>
    [Fact]
    public void CurrentsWithNoLocationGoLastRatherThanBeingDropped()
    {
        List<AetherCurrentPoint> points =
        [
            Placed("nowhere", territory: 0, 0f, 0f),
            Placed("far", Home, 300f, 0f),
            Placed("near", Home, 10f, 0f),
        ];

        var ordered = AetherCurrentRoute.Order(points, Home, 0f, 0f, Cost, _ => null);

        Assert.Equal(["near", "far", "nowhere"], Names(ordered));
    }

    [Fact]
    public void AnEmptyZoneOrdersToAnEmptyRoute() =>
        Assert.Empty(AetherCurrentRoute.Order([], Home, 0f, 0f, Cost, _ => null));

    /// <summary>Staying put is free and going anywhere else costs a trip — the shape the plugin side
    /// supplies, without needing the aetheryte sheets to say so here.</summary>
    private static float Cost(uint from, uint to) => from == to ? 0f : 2f;

    private static string[] Names(List<AetherCurrentPoint> ordered) =>
        [.. ordered.Select(p => p.QuestName ?? string.Empty)];

    /// <summary>The stop's label is smuggled through QuestName purely so the assertions read as
    /// names rather than as row ids.</summary>
    private static AetherCurrentPoint Placed(string name, uint territory, float x, float z) =>
        Blank with { Territory = territory, X = x, Z = z, QuestName = name };

    private static AetherCurrentPoint QuestAt(string name, uint territory, float x, float z) =>
        Blank with
        {
            Kind = AetherCurrentKind.Quest,
            Territory = territory,
            X = x,
            Z = z,
            QuestRowId = 67296,
            QuestName = name,
        };
}
