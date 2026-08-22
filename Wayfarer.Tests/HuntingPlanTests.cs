using Wayfarer.Core.Guidance;

namespace Wayfarer.Tests;

public class HuntingPlanTests
{
    [Theory]
    [InlineData(0, 3, false)]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, true)]
    public void IsComplete_IsAKillCountAndNothingElse(int killed, int required, bool expected) =>
        Assert.Equal(expected, HuntingPlan.IsComplete(killed, required));

    [Fact]
    public void ProgressText_ReadsAsKilledOverRequired() => Assert.Equal("2/3", HuntingPlan.ProgressText(2, 3));

    [Fact]
    public void SourceLabel_NamesTheActiveLog() =>
        Assert.Equal("Hunting Log · Gladiator", HuntingPlan.SourceLabel("Gladiator"));

    [Fact]
    public void SourceLabel_FallsBackWhenTheLogIsNotResolvedYet() =>
        Assert.Equal("Hunting Log", HuntingPlan.SourceLabel(null));

    [Fact]
    public void RoutableTarget_ProducesWorldPoint()
    {
        var destination = HuntingPlan.Destination(
            routable: true, territory: 148, mapId: 4, x: 1f, y: 2f, z: 3f, dutyTerritory: null, isLive: true);

        var point = Assert.IsType<ObjectiveDestination.WorldPoint>(destination);
        Assert.Equal(148u, point.Territory);
        Assert.Equal(1f, point.X);
        Assert.True(point.IsLive);
    }

    /// <summary>The 25 Grand-Company-Elite targets live inside instanced duties and have no
    /// overworld coordinate. They stay in the plan as a duty objective instead of being dropped,
    /// which is what happened while a coordinate was the only expressible destination.</summary>
    [Fact]
    public void DutyGatedTarget_ProducesInstancedDutyDestination()
    {
        var destination = HuntingPlan.Destination(
            routable: false, territory: 0, mapId: 0, x: 0f, y: 0f, z: 0f, dutyTerritory: 1036, isLive: false);

        var duty = Assert.IsType<ObjectiveDestination.InstancedDuty>(destination);
        Assert.Equal(1036u, duty.DutyTerritory);
    }

    [Fact]
    public void DutyGatedTargetWithNoKnownDuty_ProducesUnresolvedWithAReason()
    {
        var destination = HuntingPlan.Destination(
            routable: false, territory: 0, mapId: 0, x: 0f, y: 0f, z: 0f, dutyTerritory: null, isLive: false);

        var unresolved = Assert.IsType<ObjectiveDestination.Unresolved>(destination);
        Assert.False(string.IsNullOrWhiteSpace(unresolved.Reason));
    }
}
