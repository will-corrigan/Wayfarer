namespace Wayfarer.Core.Navigation;

/// <summary>Decides whether an objective that landed in another territory (the
/// OtherZone path) is actually inside instanced duty content, and if so builds the
/// dedicated guidance state instead of letting route costing run and fail uselessly
/// (instanced territories have no aetherytes/entrances to route to). Pure and
/// core-testable: callers supply the territory→duty lookup and the unlocked check as
/// delegates, so this class has no Dalamud/Lumina/ClientStructs dependency.</summary>
public static class DutyObjectiveGuidance
{
    /// <summary>Reason-text marker for the "duty can be queued now" case, shared with
    /// ArrowWindow so it can pull the duty name back out of <see
    /// cref="NavigationState.Reason"/> to render it as a clickable link without a
    /// separate duty-name field on the wire — the row id in <see
    /// cref="NavigationState.DutyContentFinderConditionId"/> is the only additive
    /// state this feature needs. No trailing suffix: the link's own tooltip ("Open in
    /// Duty Finder") is the affordance, so the reason text ends at the duty name.</summary>
    public const string CompleteDutyPrefix = "Complete the duty: ";

    /// <summary>The same marker for the case the player cannot queue: the duty exists, the objective
    /// is inside it, and the content has not been unlocked yet, so the only thing to say is that it
    /// has to be. Named for the same reason <see cref="CompleteDutyPrefix"/> is — the readout lifts
    /// the duty's own name back out of the reason to draw it, and doing that against a named constant
    /// is not the same thing as recognising a line by its English.</summary>
    public const string UnlockDutyPrefix = "Unlock and complete the duty: ";

    /// <summary>The duty's own name, out of a reason this class wrote, or null when the reason is not
    /// one of them. One place knows both spellings and it is the place that produced them.</summary>
    public static string? DutyName(string? reason) => reason switch
    {
        null => null,
        _ when reason.StartsWith(CompleteDutyPrefix, StringComparison.Ordinal) =>
            reason[CompleteDutyPrefix.Length..],
        _ when reason.StartsWith(UnlockDutyPrefix, StringComparison.Ordinal) =>
            reason[UnlockDutyPrefix.Length..],
        _ => null,
    };

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
            ? $"{CompleteDutyPrefix}{duty.Name}"
            : $"{UnlockDutyPrefix}{duty.Name}";

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
            DutyContentFinderConditionId = unlocked ? duty.ContentFinderConditionId : null,
        };
    }
}
