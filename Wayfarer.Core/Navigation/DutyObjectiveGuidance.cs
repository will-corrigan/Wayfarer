namespace Wayfarer.Core.Navigation;

/// <summary>Decides whether an objective that landed in another territory (the
/// OtherZone path) is actually inside instanced duty content, and if so builds the
/// dedicated guidance state instead of letting route costing run and fail uselessly
/// (instanced territories have no aetherytes/entrances to route to). Pure and
/// core-testable: callers supply the territory→duty lookup and the unlocked check as
/// delegates, so this class has no Dalamud/Lumina/ClientStructs dependency.</summary>
public static class DutyObjectiveGuidance
{
    /// <summary>Returns the dedicated duty-guidance state when <paramref
    /// name="targetTerritory"/> is duty content, or null when it isn't (the caller
    /// should fall through to its normal route-costing path unchanged).</summary>
    public static NavigationState? TryBuild(
        uint targetTerritory,
        Func<uint, DutyInfo?> territoryToDuty,
        Func<uint, bool> isInstanceContentUnlocked,
        uint displayQuestId,
        string questName,
        string? stepLabel,
        bool isPickup,
        int? routeStop,
        int? routeTotal)
    {
        if (territoryToDuty(targetTerritory) is not { } duty)
        {
            return null;
        }

        var unlocked = isInstanceContentUnlocked(duty.InstanceContentId);
        var reason = unlocked
            ? $"Complete the duty: {duty.Name} — queue via Duty Finder"
            : $"Unlock and complete the duty: {duty.Name}";

        return new NavigationState
        {
            Mode = NavigationState.Modes.DutyObjective,
            QuestId = displayQuestId,
            QuestName = questName,
            StepLabel = stepLabel,
            Reason = reason,
            IsPickup = isPickup,
            RouteStop = routeStop,
            RouteTotal = routeTotal,
        };
    }
}
