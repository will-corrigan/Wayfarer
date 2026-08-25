using Wayfarer.Core.Guidance;
using Wayfarer.Core.Navigation;

namespace Wayfarer.Tests;

public class GuidanceProjectionTests
{
    [Fact]
    public void SameZoneRoute_ProducesSameZoneStateWithArrowAndDistance()
    {
        var objective = Objective("quest", "196", new ObjectiveDestination.WorldPoint(129, 129, 1f, 2f, 3f));

        var state = GuidanceProjection.Build(
            objective,
            GuidanceEngagement.Ambient,
            new RouteResult.SameZone(1f, 2f, 3f, 42.5f));

        Assert.Equal(NavigationState.Modes.SameZone, state.Mode);
        Assert.Equal(1f, state.TargetX);
        Assert.Equal(2f, state.TargetY);
        Assert.Equal(3f, state.TargetZ);
        Assert.Equal(42.5f, state.DistanceYalms);
        Assert.False(state.IsPickup);
        Assert.False(state.Engaged);
        Assert.Equal("quest", state.SourceId);
        Assert.Equal("quest:196", state.ObjectiveKey);
    }

    [Fact]
    public void SameZoneRoute_WithRadius_CarriesTargetRadiusYalms()
    {
        var objective = Objective("quest", "196", new ObjectiveDestination.WorldPoint(129, 129, 1f, 2f, 3f));

        var state = GuidanceProjection.Build(
            objective,
            GuidanceEngagement.Ambient,
            new RouteResult.SameZone(1f, 2f, 3f, 66f, Radius: 20f));

        Assert.Equal(20f, state.TargetRadiusYalms);
    }

    [Fact]
    public void SameZoneRoute_WithZeroRadius_LeavesTargetRadiusYalmsNull()
    {
        // The default-shape route every existing call site produces: TargetRadiusYalms must come
        // out null, not 0 — a point objective must not start carrying a "search area" signal.
        var objective = Objective("quest", "196", new ObjectiveDestination.WorldPoint(129, 129, 1f, 2f, 3f));

        var state = GuidanceProjection.Build(
            objective,
            GuidanceEngagement.Ambient,
            new RouteResult.SameZone(1f, 2f, 3f, 42.5f));

        Assert.Null(state.TargetRadiusYalms);
    }

    // --- End-to-end wiring for the REAL measured fixtures — see SearchAreaRadiusTests for the
    // predicate itself and the full provenance of these numbers.
    // 203 = "Heroes of the Hour" (67782), "talk to Aymeric to complete the quest" — the step that
    //       fooled the original revert into concluding radius did not work; genuinely an area.
    // 406, 102 = "The Full Report, Warts and All" (69901), frog search step, its two locations.
    // 50 = "Leap into the Unknown" (70602) — the one row where Level.Radius and Level.Type
    //      disagree; resolved in favour of radius.
    [Theory]
    [InlineData(203f)]
    [InlineData(406f)]
    [InlineData(102f)]
    [InlineData(50f)]
    public void SameZoneRoute_WithARealAreaRadius_CarriesTargetRadiusYalms(float radius)
    {
        var objective = Objective("quest", "196", new ObjectiveDestination.WorldPoint(129, 129, 1f, 2f, 3f));

        var state = GuidanceProjection.Build(
            objective, GuidanceEngagement.Ambient, new RouteResult.SameZone(1f, 2f, 3f, 66f, Radius: radius));

        Assert.Equal(radius, state.TargetRadiusYalms);
    }

    // 1 = "The Hazy Professor" (65780), "Search for the students near Camp Drybone." — wording says
    //     search, location is exact: the trap fixture for a text-based heuristic, and proof this
    //     classifier does not use one.
    // 2 = "Heroes of the Hour" (67782), "Enter Fortemps Manor." — a small Type-51 "pop range" (a
    //     doorway), not a search area.
    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    public void SameZoneRoute_WithARealPointRadius_LeavesTargetRadiusYalmsNull(float radius)
    {
        var objective = Objective("quest", "196", new ObjectiveDestination.WorldPoint(129, 129, 1f, 2f, 3f));

        var state = GuidanceProjection.Build(
            objective, GuidanceEngagement.Ambient, new RouteResult.SameZone(1f, 2f, 3f, 66f, Radius: radius));

        Assert.Null(state.TargetRadiusYalms);
    }

    [Fact]
    public void SameZoneRoute_ViaAethernet_CarriesEntryAndExitShards()
    {
        var objective = Objective("quest", "196", new ObjectiveDestination.WorldPoint(129, 129, 1f, 2f, 3f));

        var state = GuidanceProjection.Build(
            objective,
            GuidanceEngagement.Ambient,
            new RouteResult.SameZone(9f, null, 8f, 30f, "Arcanists' Guild", "Fishermen's Guild"));

        Assert.Equal(NavigationState.Modes.SameZone, state.Mode);
        Assert.Equal(9f, state.TargetX);
        Assert.Null(state.TargetY);
        Assert.Equal("Arcanists' Guild", state.AethernetEntryName);
        Assert.Equal("Fishermen's Guild", state.AethernetExitName);
    }

    [Fact]
    public void OtherZoneRoute_CarriesTeleportAdvice()
    {
        var objective = Objective("unlocks", "65821", new ObjectiveDestination.WorldPoint(140, 5, 1f, 2f, 3f));

        var state = GuidanceProjection.Build(
            objective,
            GuidanceEngagement.Engaged,
            new RouteResult.OtherZone(
                "Western Thanalan",
                1f,
                3f,
                AetheryteId: 9,
                AetheryteName: "Horizon",
                AetheryteUnlocked: true,
                RemainingYalms: 120f));

        Assert.Equal(NavigationState.Modes.OtherZone, state.Mode);
        Assert.Equal("Western Thanalan", state.ZoneName);
        Assert.Equal(9u, state.AetheryteId);
        Assert.Equal("Horizon", state.AetheryteName);
        Assert.True(state.AetheryteUnlocked);
        Assert.Equal(120f, state.RemainingYalms);
        Assert.True(state.Engaged);
        Assert.True(state.IsPickup);
        Assert.Equal("Unlock route", state.SourceLabel);
    }

    /// <summary>Duty handling is not quest-specific: the same destination kind must project the
    /// same way whichever feature produced it.</summary>
    [Theory]
    [InlineData("quest")]
    [InlineData("unlocks")]
    [InlineData("hunting")]
    public void DutyRoute_ProducesDutyObjective_ForEverySourceId(string sourceId)
    {
        var objective = Objective(sourceId, "1", new ObjectiveDestination.InstancedDuty(1036));

        var state = GuidanceProjection.Build(
            objective,
            GuidanceEngagement.Ambient,
            new RouteResult.Duty("Complete the duty: Sastasha", 4));

        Assert.Equal(NavigationState.Modes.DutyObjective, state.Mode);
        Assert.Equal("Complete the duty: Sastasha", state.Reason);
        Assert.Equal(4u, state.DutyContentFinderConditionId);
        Assert.Equal(sourceId, state.SourceId);
    }

    [Fact]
    public void UnresolvedRoute_ProducesNoLocationWithTheSourcesOwnReason()
    {
        var objective = Objective("quest", "196", new ObjectiveDestination.Unresolved("no map location"));

        var state = GuidanceProjection.Build(
            objective, GuidanceEngagement.Ambient, new RouteResult.NoLocation("no map location"));

        Assert.Equal(NavigationState.Modes.NoLocation, state.Mode);
        Assert.Equal("no map location", state.Reason);
        Assert.Null(state.TargetX);
    }

    [Fact]
    public void Progress_ProjectsToRouteStopTotalAndText()
    {
        var objective = Objective("hunting", "77", new ObjectiveDestination.WorldPoint(148, 4, 0f, 0f, 0f, IsLive: true))
            with
        { Progress = new ObjectiveProgress(3, 11, "2/3 kills") };

        var state = GuidanceProjection.Build(
            objective, GuidanceEngagement.Engaged, new RouteResult.SameZone(0f, 0f, 0f, 5f));

        Assert.Equal(3, state.RouteStop);
        Assert.Equal(11, state.RouteTotal);
        Assert.Equal("2/3 kills", state.ProgressText);
        Assert.True(state.IsLiveTarget);
    }

    [Fact]
    public void EngagedObjectiveWithoutSourceLabel_Throws()
    {
        var objective = new GuidanceObjective(
            new ObjectiveKey("hunting", "77"),
            new ObjectiveDestination.WorldPoint(148, 4, 0f, 0f, 0f),
            new ObjectiveCopy("Ornery Karakul", null, null));

        Assert.Throws<InvalidOperationException>(() => GuidanceProjection.Build(
            objective, GuidanceEngagement.Engaged, new RouteResult.SameZone(0f, 0f, 0f, 5f)));
    }

    private static GuidanceObjective Objective(string sourceId, string value, ObjectiveDestination destination)
    {
        var label = sourceId switch
        {
            "unlocks" => "Unlock route",
            "hunting" => "Hunting Log",
            _ => "Main Scenario",
        };
        return new GuidanceObjective(
            new ObjectiveKey(sourceId, value),
            destination,
            new ObjectiveCopy("An objective", "a step", label),
            QuestId: 65832);
    }
}
