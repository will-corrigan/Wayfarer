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
/// <param name="Headline">A name the GAME itself would print, never a label of ours: the quest's
/// name, the monster's name. It goes in the readout's bar, which is the game's own Main Scenario
/// Guide plate, and a player reads whatever sits on that plate as a game element — see
/// <see cref="UnlockRoutePlan.Headline"/>, which is where this rule was broken and put back.</param>
/// <param name="Detail">Our own words about the headline, and the only place they belong: "Speak to
/// Momodi", "Speak with Claribel to unlock Ceremony of Eternal Bonding". Null when the headline says
/// it all.</param>
/// <param name="SourceLabel">The MODE indicator: "Main Scenario", "Unlock route", "Hunting Log ·
/// Gladiator". Required whenever the objective is <see cref="GuidanceEngagement.Engaged"/> — the
/// readout IS the mode indicator, so an engaged objective with no label would leave the player in
/// a mode with nothing naming it. <see cref="GuidanceArbiter"/> throws rather than publish one.</param>
/// <param name="SourceName">What the MODULE calls itself, in Title Case and in the singular:
/// "Quest", "Unlock", "Hunting Log". Not the mode label — <paramref name="SourceLabel"/> describes
/// this objective's context ("Main Scenario", "Hunting Log - Warrior"), while this names the feature
/// that produced it and is the same string every time that feature speaks.
///
/// <para>It exists so the readout's banner can print "Current Quest" above whatever is in the plate,
/// the way the game's own banner prints "Current Main Scenario Quest". The SOURCE supplies it
/// because nothing on the guidance path — not the arbiter, not the projection, not the composer, not
/// the renderer — may map a source id to a word. A switch over ids anywhere along that chain is the
/// same coupling this interface exists to prevent, moved one file along.</para>
///
/// <para>Null is legal and means "say nothing about the module": the readout falls back to the
/// plugin's own name, which is what an idle readout wants anyway.</para></param>
public sealed record ObjectiveCopy(
    string Headline, string? Detail, string? SourceLabel, string? SourceName = null);

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
