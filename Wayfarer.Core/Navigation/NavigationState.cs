namespace Wayfarer.Core.Navigation;

/// <summary>Immutable-by-convention snapshot of quest navigation, published once per
/// framework tick by the plugin and read by the widget and the get_navigation MCP tool.</summary>
public sealed class NavigationState
{
    public string Mode { get; init; } = Modes.Hidden;

    public uint? QuestId { get; init; }

    public string? QuestName { get; init; }

    public string? StepLabel { get; init; }

    public string? ZoneName { get; init; }

    public float? TargetX { get; init; }

    public float? TargetY { get; init; }

    public float? TargetZ { get; init; }

    public float? DistanceYalms { get; init; }

    public uint? AetheryteId { get; init; }

    public string? AetheryteName { get; init; }

    public bool AetheryteUnlocked { get; init; }

    // Set when the same-zone target has been retargeted to an aethernet entry shard,
    // OR (OtherZone mode) when RouteCosting picked the aethernet candidate as the
    // cheapest way to reach a cross-territory objective: the arrow points at the entry
    // (nearest the player); the exit (nearest the objective, possibly in a different
    // territory's coordinate space) is what the user picks in the shard's travel menu.
    public string? AethernetEntryName { get; init; }

    public string? AethernetExitName { get; init; }

    // OtherZone mode only, set when RouteCosting picked the entrance candidate (walk
    // through a physical map-link door) as the cheapest route: the widget draws an
    // arrow to this door. Null when the aethernet or teleport candidate won instead.
    public string? EntranceName { get; init; }

    public float? EntranceX { get; init; }

    public float? EntranceZ { get; init; }

    // OtherZone mode only: the remaining walk after the aethernet exit shard or the
    // entrance door — i.e. RouteCandidate.RemainingYalms for whichever mode won. Null
    // for teleport mode (no post-arrival distance is tracked).
    public float? RemainingYalms { get; init; }

    /// <summary>true when the arrow is guiding to an unlock-quest pickup rather than a followed quest</summary>
    public bool IsPickup { get; init; }

    /// <summary>1-based position of the current pickup within an active multi-stop route
    /// (SetRoute), null when no route is active — including single pickups via SetPickup.</summary>
    public int? RouteStop { get; init; }

    /// <summary>Total stops in the active route, null when no route is active.</summary>
    public int? RouteTotal { get; init; }

    public string? Reason { get; init; }

    /// <summary>DutyObjective mode only, set when the duty can be queued right now (the
    /// "Complete the duty" case — never set for the "unlock and complete" case, since
    /// there's nothing to queue yet): the ContentFinderCondition row id to pass to
    /// AgentContentsFinder.OpenRegularDuty so the widget's duty-name link can open the
    /// Duty Finder directly instead of leaving the player to find it themselves.</summary>
    public uint? DutyContentFinderConditionId { get; init; }

    public static class Modes
    {
        public const string Hidden = "hidden";

        /// <summary>Logged in and unhidden, but nothing followed — widget shows only its idle face.</summary>
        public const string Idle = "idle";
        public const string SameZone = "sameZone";
        public const string OtherZone = "otherZone";
        public const string NoLocation = "noLocation";

        /// <summary>The objective's territory is instanced duty content (a dungeon,
        /// trial, raid, etc.) rather than an ordinary zone — there is no route to draw
        /// since duty territories have no aetherytes or entrances; <see cref="Reason"/>
        /// carries the "complete the duty" / "unlock and complete the duty" message.</summary>
        public const string DutyObjective = "dutyObjective";
    }
}
