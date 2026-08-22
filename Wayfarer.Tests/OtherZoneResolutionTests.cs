using Wayfarer.Core.Navigation;

namespace Wayfarer.Tests;

/// <summary>Pure fallback-message decision extracted from QuestNavigator.OtherZone /
/// ArrowWindow (task-fix-interior-report.md fix round 3, re-review finding 2).</summary>
public class OtherZoneResolutionTests
{
    private static readonly RouteCandidate Candidate = new(RouteMode.Entrance, 10f, 1f, 1f);

    private static readonly NavigationState Fallback = new()
    {
        Mode = NavigationState.Modes.SameZone,
        TargetX = 5f,
        TargetY = 0f,
        TargetZ = 5f,
    };

    [Fact]
    public void Resolve_ReturnsRoute_WhenChosenIsNotNull()
    {
        Assert.Equal(OtherZoneOutcome.Route, OtherZoneResolution.Resolve(Candidate, Fallback));
        Assert.Equal(OtherZoneOutcome.Route, OtherZoneResolution.Resolve(Candidate, null));
    }

    [Fact]
    public void Resolve_ReturnsMarkerFallback_WhenChosenIsNullAndFallbackExists()
    {
        Assert.Equal(OtherZoneOutcome.MarkerFallback, OtherZoneResolution.Resolve(null, Fallback));
    }

    [Fact]
    public void Resolve_ReturnsInteriorMessage_WhenNeitherChosenNorFallbackExists()
    {
        Assert.Equal(OtherZoneOutcome.InteriorMessage, OtherZoneResolution.Resolve(null, null));
    }

    [Fact]
    public void InteriorMessage_UsesZoneName_WhenPresent()
    {
        Assert.Equal(
            "Objective is inside Foundation — find the entrance nearby.",
            OtherZoneResolution.InteriorMessage("Foundation"));
    }

    [Fact]
    public void InteriorMessage_FallsBackToGenericPhrase_WhenZoneNameIsNull()
    {
        Assert.Equal(
            "Objective is inside another zone — find the entrance nearby.",
            OtherZoneResolution.InteriorMessage(null));
    }
}
