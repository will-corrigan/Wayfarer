namespace Wayfarer.Core.Guidance;

/// <summary>What the router worked out about HOW TO GET to an objective — and nothing about WHAT
/// the objective is. Keeping the two apart is what lets every source produce identical routing
/// output: a hunting leg, an unlock pickup and a quest step all reach the router as a destination
/// and all come back with the same teleport / aethernet / entrance / arrow / distance advice, then
/// have their own identity attached by <see cref="GuidanceProjection"/>.</summary>
public abstract record RouteResult
{
    private RouteResult()
    {
    }

    /// <summary>Walkable from where the player stands. When the aethernet beats the direct run,
    /// the arrow has already been retargeted to the entry shard and
    /// <see cref="DistanceYalms"/> is the walk to THAT — the exit shard's name is what the player
    /// picks in the shard's travel menu.</summary>
    /// <param name="TargetY">Null when the arrow points at an aethernet entry shard: shard
    /// positions carry no vertical axis, so the widget uses the player's own Y.</param>
    /// <param name="Radius">The objective's search-area radius in yalms, or 0 for a point. Never set
    /// when the arrow has been retargeted to an aethernet entry shard — that shard is a precise
    /// place to stand, not the objective itself, so it must never inherit the objective's
    /// area-search wording.</param>
    public sealed record SameZone(
        float TargetX,
        float? TargetY,
        float TargetZ,
        float DistanceYalms,
        string? AethernetEntryName = null,
        string? AethernetExitName = null,
        float Radius = 0f) : RouteResult;

    /// <summary>Off the player's current map. At most one of the aethernet / entrance / teleport
    /// candidate sets is populated — whichever route costing chose. <paramref name="Reason"/> is
    /// set only when no candidate existed at all.</summary>
    public sealed record OtherZone(
        string? ZoneName,
        float TargetX,
        float TargetZ,
        uint? AetheryteId = null,
        string? AetheryteName = null,
        bool AetheryteUnlocked = false,
        string? EntranceName = null,
        float? EntranceX = null,
        float? EntranceZ = null,
        string? AethernetEntryName = null,
        string? AethernetExitName = null,
        float? RemainingYalms = null,
        string? Reason = null) : RouteResult;

    /// <summary>Inside instanced duty content: there is nothing to route to, so the answer is the
    /// duty itself — queue it, or unlock it first.</summary>
    public sealed record Duty(string Reason, uint? DutyContentFinderConditionId) : RouteResult;

    /// <summary>No usable location at all.</summary>
    public sealed record NoLocation(string Reason) : RouteResult;
}
