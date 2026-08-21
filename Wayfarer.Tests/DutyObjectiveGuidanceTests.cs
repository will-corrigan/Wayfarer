using Wayfarer.Core.Navigation;

namespace Wayfarer.Tests;

/// <summary>Live-bug fixture: quest "Disarmed" → objective inside "The Fist of the
/// Father" (Alexander's first duty). Instanced territories have no aetherytes/entrances,
/// so route costing correctly finds nothing — the fix is to recognize the territory as
/// duty content BEFORE route costing runs, and say so instead of "no route found".</summary>
public class DutyObjectiveGuidanceTests
{
    private const uint FistOfTheFatherTerritory = 621;
    private const uint FistOfTheFatherInstanceContentId = 258;
    private const string FistOfTheFatherName = "The Fist of the Father";

    [Fact]
    public void NonDutyTerritory_ReturnsNull_SoRouteCostingProceedsAsBefore()
    {
        var result = DutyObjectiveGuidance.TryBuild(
            targetTerritory: 148, // an ordinary open-world territory, not in the lookup
            territoryToDuty: Lookup,
            isInstanceContentUnlocked: _ => true,
            displayQuestId: 1000,
            questName: "Disarmed",
            stepLabel: "Speak with someone",
            isPickup: false,
            routeStop: null,
            routeTotal: null);

        Assert.Null(result);
    }

    [Fact]
    public void DutyTerritory_Unlocked_ProducesQueueMessage()
    {
        var result = DutyObjectiveGuidance.TryBuild(
            targetTerritory: FistOfTheFatherTerritory,
            territoryToDuty: Lookup,
            isInstanceContentUnlocked: id => id == FistOfTheFatherInstanceContentId,
            displayQuestId: 1000,
            questName: "Disarmed",
            stepLabel: "Defeat the Manipulator",
            isPickup: false,
            routeStop: null,
            routeTotal: null);

        Assert.NotNull(result);
        Assert.Equal(NavigationState.Modes.DutyObjective, result!.Mode);
        Assert.Equal("Complete the duty: The Fist of the Father — queue via Duty Finder", result.Reason);
        Assert.Equal(1000u, result.QuestId);
        Assert.Equal("Disarmed", result.QuestName);
        Assert.Equal("Defeat the Manipulator", result.StepLabel);
    }

    [Fact]
    public void DutyTerritory_NotYetUnlocked_ProducesUnlockFirstMessage()
    {
        var result = DutyObjectiveGuidance.TryBuild(
            targetTerritory: FistOfTheFatherTerritory,
            territoryToDuty: Lookup,
            isInstanceContentUnlocked: _ => false,
            displayQuestId: 1000,
            questName: "Disarmed",
            stepLabel: null,
            isPickup: false,
            routeStop: null,
            routeTotal: null);

        Assert.NotNull(result);
        Assert.Equal("Unlock and complete the duty: The Fist of the Father", result!.Reason);
    }

    [Fact]
    public void PreservesPickupAndRouteFlags()
    {
        var result = DutyObjectiveGuidance.TryBuild(
            targetTerritory: FistOfTheFatherTerritory,
            territoryToDuty: Lookup,
            isInstanceContentUnlocked: _ => true,
            displayQuestId: 2000,
            questName: "Some Unlock Quest",
            stepLabel: null,
            isPickup: true,
            routeStop: 2,
            routeTotal: 5);

        Assert.NotNull(result);
        Assert.True(result!.IsPickup);
        Assert.Equal(2, result.RouteStop);
        Assert.Equal(5, result.RouteTotal);
    }

    private static DutyInfo? Lookup(uint territoryId) =>
        territoryId == FistOfTheFatherTerritory
            ? new DutyInfo(FistOfTheFatherName, FistOfTheFatherInstanceContentId)
            : null;
}
