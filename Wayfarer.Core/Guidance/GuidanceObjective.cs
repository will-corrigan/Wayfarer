namespace Wayfarer.Core.Guidance;

/// <summary>Stable identity of an objective across ticks. Same key = SAME objective, even if the
/// position moved (live-tracked mobs) or the progress text changed. A DIFFERENT key is the only
/// signal that a source advanced. Consumers key their per-objective side effects off this and
/// nothing else — see <see cref="GuidanceArbiter.OnObjectiveChanged"/>.</summary>
/// <param name="SourceId">The producing source's stable id ("quest", "unlocks", "hunting", ...) —
/// never a display name.</param>
/// <param name="Value">Source-private identity for the objective: a quest id, a monster's
/// dataset index, an unlock's quest row. Opaque to everything outside the source.</param>
public readonly record struct ObjectiveKey(string SourceId, string Value)
{
    /// <summary>"sourceId:value" — the exact string published on the wire as
    /// <c>NavigationState.ObjectiveKey</c>, so a consumer can compare identities without
    /// parsing.</summary>
    public override string ToString() => $"{SourceId}:{Value}";
}

/// <summary>The player-facing words for an objective, owned by the source that produced it.</summary>
/// <param name="Headline">Quest name, "Pick up: A Self-improving Man from Mahenne", "Ornery
/// Karakul".</param>
/// <param name="Detail">"Speak to Momodi", "Unlocks: Glamours". Null when the headline says it
/// all.</param>
/// <param name="SourceLabel">The MODE indicator: "Main Scenario", "Unlock route", "Hunting Log ·
/// Gladiator". Required whenever the objective is <see cref="GuidanceEngagement.Engaged"/> — the
/// readout IS the mode indicator, so an engaged objective with no label would leave the player in
/// a mode with nothing naming it. <see cref="GuidanceArbiter"/> throws rather than publish one.</param>
public sealed record ObjectiveCopy(string Headline, string? Detail, string? SourceLabel);

/// <summary>Position within the SOURCE's own plan: "Stop 2 of 5", "4 of 10 targets", "2/3 kills",
/// "68%". One shape, source-supplied text, so the readout never learns which feature it
/// renders.</summary>
public sealed record ObjectiveProgress(int? Index, int? Total, string? Text);

/// <summary>What this objective asks the FRAMEWORK to do on its behalf while it is active. These
/// are DECLARATIONS OF INTENT, not actions: shared, singleton or destructive game state (above all
/// the map flag, which is backed by a <c>FixedSizeArray1&lt;FlagMapMarker&gt;</c> and whose setter
/// zeroes <c>FlagMarkerCount</c> first, destroying the player's own flag) is performed by exactly
/// one framework-owned coordinator, never by a source.
///
/// A source that wants "flag every target in my chain" sets one bool and gets the save/restore,
/// the one-writer guarantee and the change-only cadence for free — and so does every future
/// module, with no coordinator edit.</summary>
/// <param name="MapFlag">Ask for the game's real flag/minimap/compass marker while active.</param>
/// <param name="MarkEntity">Ask for a nameplate marker on <paramref name="EntityId"/>.</param>
/// <param name="EntityId">The live game object to mark, when known. Null for a pure
/// coordinate.</param>
/// <param name="AnnounceOnActivate">Play the game's own objective-change sound.</param>
public sealed record ObjectiveAffordances(
    bool MapFlag = false,
    bool MarkEntity = false,
    ulong? EntityId = null,
    bool AnnounceOnActivate = false)
{
    /// <summary>The default: this objective asks the framework for nothing.</summary>
    public static readonly ObjectiveAffordances None = new();
}

/// <summary>The thing the player is currently being guided to accomplish. Deliberately not a
/// "target" (too positional — a duty-gated objective has no position), not a "pickup"
/// (quest-shaped) and not a "waypoint" (implies a path).</summary>
/// <param name="QuestId">DISPLAY/IPC ONLY: what the widget puts on its quest line and what
/// <c>NavigationState.QuestId</c> carries. NEVER read to decide whether an objective is complete —
/// that is the producing source's job alone (see <see cref="IGuidanceSource.Poll"/>). Inferring
/// completion from this field is the exact defect that made a hunting target carrying quest id 0
/// vanish one tick after it was selected.</param>
public sealed record GuidanceObjective(
    ObjectiveKey Key,
    ObjectiveDestination Destination,
    ObjectiveCopy Copy,
    ObjectiveProgress? Progress = null,
    ObjectiveAffordances? Affordances = null,
    uint? QuestId = null);
