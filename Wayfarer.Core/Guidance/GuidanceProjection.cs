using Wayfarer.Core.Navigation;

namespace Wayfarer.Core.Guidance;

/// <summary>Turns "what the player is being guided to" (<see cref="GuidanceObjective"/>) plus "how
/// to get there" (<see cref="RouteResult"/>) into the one published
/// <see cref="NavigationState"/>. Single place, so the same-zone, other-zone, duty and
/// no-location shapes cannot drift apart in what identity, engagement or progress metadata they
/// carry — no branch can forget one.</summary>
public static class GuidanceProjection
{
    /// <summary>Also the enforcement point for the mode-visibility invariant: an engaged objective
    /// with no <see cref="ObjectiveCopy.SourceLabel"/> is a mode with nothing naming it, which the
    /// player experiences as the arrow silently doing something they did not ask for. Throwing is
    /// deliberate — this is a programming error in a source, not a runtime condition.</summary>
    public static NavigationState Build(
        GuidanceObjective objective, GuidanceEngagement engagement, RouteResult route)
    {
        var engaged = engagement == GuidanceEngagement.Engaged;
        if (engaged && string.IsNullOrEmpty(objective.Copy.SourceLabel))
        {
            throw new InvalidOperationException(
                $"Engaged objective '{objective.Key}' has no SourceLabel — an explicit mode must name itself.");
        }

        var identity = new NavigationState
        {
            QuestId = objective.QuestId,
            QuestName = objective.Copy.Headline,
            StepLabel = objective.Copy.Detail,
            SourceId = objective.Key.SourceId,
            SourceLabel = objective.Copy.SourceLabel,
            Engaged = engaged,

            // Wire meaning unchanged from when this was "guiding to an unlock-quest pickup": true
            // exactly when the arrow is NOT following a quest. Derived from engagement rather than
            // from a source id, so guidance never learns which feature it is projecting.
            IsPickup = engaged,
            ObjectiveKey = objective.Key.ToString(),
            ProgressText = objective.Progress?.Text,
            RouteStop = objective.Progress?.Index,
            RouteTotal = objective.Progress?.Total,
            IsLiveTarget = objective.Destination is ObjectiveDestination.WorldPoint { IsLive: true },
        };

        return Apply(identity, route);
    }

    private static NavigationState Apply(NavigationState identity, RouteResult route) =>
        route switch
        {
            RouteResult.SameZone s => identity with
            {
                Mode = NavigationState.Modes.SameZone,
                TargetX = s.TargetX,
                TargetY = s.TargetY,
                TargetZ = s.TargetZ,
                DistanceYalms = s.DistanceYalms,
                AethernetEntryName = s.AethernetEntryName,
                AethernetExitName = s.AethernetExitName,
            },
            RouteResult.OtherZone o => identity with
            {
                Mode = NavigationState.Modes.OtherZone,
                ZoneName = o.ZoneName,
                TargetX = o.TargetX,
                TargetZ = o.TargetZ,
                AetheryteId = o.AetheryteId,
                AetheryteName = o.AetheryteName,
                AetheryteUnlocked = o.AetheryteUnlocked,
                EntranceName = o.EntranceName,
                EntranceX = o.EntranceX,
                EntranceZ = o.EntranceZ,
                AethernetEntryName = o.AethernetEntryName,
                AethernetExitName = o.AethernetExitName,
                RemainingYalms = o.RemainingYalms,
                Reason = o.Reason,
            },
            RouteResult.Duty d => identity with
            {
                Mode = NavigationState.Modes.DutyObjective,
                Reason = d.Reason,
                DutyContentFinderConditionId = d.DutyContentFinderConditionId,
            },
            RouteResult.NoLocation n => identity with
            {
                Mode = NavigationState.Modes.NoLocation,
                Reason = n.Reason,
            },
            _ => identity with { Mode = NavigationState.Modes.NoLocation, Reason = "no location data" },
        };
}
