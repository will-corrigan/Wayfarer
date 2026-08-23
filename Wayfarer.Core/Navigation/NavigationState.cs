namespace Wayfarer.Core.Navigation;

/// <summary>Immutable-by-convention snapshot of quest navigation, published once per
/// framework tick by the plugin and read by the widget and the get_navigation MCP tool.
/// A record purely so <see cref="Guidance.GuidanceProjection"/> can attach an objective's identity
/// to a route with <c>with</c> instead of restating a dozen fields per branch — the wire shape,
/// which is what this type exists for, is unchanged (records serialize by public property exactly
/// as classes do).</summary>
public sealed record NavigationState
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

    /// <summary>SameZone mode only: the game's own search-area radius around
    /// (<see cref="TargetX"/>, <see cref="TargetZ"/>), in yalms, for a quest step drawn as a circle
    /// on the map rather than a precise waypoint. Null for an ordinary point objective — including
    /// every objective this field did not exist for, so an older or unaware consumer sees exactly
    /// the point-objective behaviour it always had.</summary>
    public float? TargetRadiusYalms { get; init; }

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

    /// <summary>true when the arrow is guiding to something the player explicitly chose rather than
    /// a followed quest. Superseded by <see cref="SourceId"/>/<see cref="Engaged"/> and retained
    /// for wire compatibility — its meaning is unchanged, and it is now computed as
    /// <see cref="Engaged"/> (an explicit mode is by definition not the followed quest).</summary>
    public bool IsPickup { get; init; }

    /// <summary>Stable id of the feature that owns the arrow right now: "quest", "unlocks",
    /// "hunting", or null when nothing is being guided to.</summary>
    public string? SourceId { get; init; }

    /// <summary>The mode indicator — "Main Scenario", "Unlock route", "Hunting Log · Gladiator".
    /// Non-null whenever <see cref="Engaged"/> is true: an explicit mode must always name itself,
    /// because this readout is the only mode indicator the player has.</summary>
    public string? SourceLabel { get; init; }

    /// <summary>What the owning MODULE calls itself, in Title Case and in the singular — "Quest",
    /// "Unlock", "Hunting Log" — as against <see cref="SourceLabel"/>, which describes this
    /// particular objective's context. Supplied by the source; see
    /// <see cref="Guidance.ObjectiveCopy.SourceName"/> for why it is never derived from
    /// <see cref="SourceId"/> anywhere downstream. Null when nothing owns the arrow.</summary>
    public string? SourceName { get; init; }

    /// <summary>An explicit mode is active (a route, a hunt) rather than the ambient followed
    /// quest. Presentations MUST offer a reachable exit whenever this is true.</summary>
    public bool Engaged { get; init; }

    /// <summary>"sourceId:value" — the active objective's stable identity. Consumers key their own
    /// per-objective side effects off this and nothing else: it changes exactly when the objective
    /// changes, and NOT when a live-tracked target's position is refreshed.</summary>
    public string? ObjectiveKey { get; init; }

    /// <summary>Progress within the owning feature's own plan, in that feature's words: "2/3
    /// kills", "68%". <see cref="RouteStop"/>/<see cref="RouteTotal"/> carry the numeric form when
    /// the plan is an ordered chain.</summary>
    public string? ProgressText { get; init; }

    /// <summary>The target position came from a live object-table scan this tick rather than a
    /// curated coordinate. Display only — routing treats both alike.</summary>
    public bool IsLiveTarget { get; init; }

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

    /// <summary>The mode strings, as constants rather than an enum: they cross the IPC boundary
    /// verbatim, so an external consumer reads the same values this file names.</summary>
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
