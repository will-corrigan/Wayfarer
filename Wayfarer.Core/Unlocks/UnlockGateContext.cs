using Wayfarer.Core.Unlocks.Gates;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks;

/// <summary>Everything <see cref="UnlockStatusCalculator.Compute"/> needs to read live game
/// state, injected so the calculator stays pure and unit-testable. Values that don't vary per
/// unlock entry (the player's own level/GC/rank) are plain scalars, snapshotted once per pass;
/// per-id lookups (a specific job's level, a specific duty's completion, ...) are delegates
/// since they're evaluated per entry.
///
/// <para><b>Two shapes of reader, and the difference is load-bearing.</b> A delegate returning a
/// plain value is a promise that the value is authoritative whenever <see cref="LiveStateReady"/>
/// is true. A delegate returning a <i>nullable</i> is one whose backing read can be unavailable —
/// a table the server has not sent, a zone the player is not standing in — and <c>null</c> is
/// "cannot determine", which the gate model turns into
/// <see cref="UnlockStatus.RequirementsUnknown"/> rather than into "no". Every such reader is
/// optional and defaults to null, which means the same thing: not wired, therefore unknown.</para>
///
/// <para><see cref="ResolveGameText"/> is the one exception to "game state": it reads game
/// <i>text</i> rather than game <i>progress</i>, resolving a <see cref="GameTextRef"/> against the
/// live client's own sheets in the player's own client language. Optional and defaulted to null so
/// existing callers (chiefly tests) don't have to wire it; a null resolver, or one that misses,
/// just means the curated <see cref="UnlockRequirement.Label"/> fallback is used instead.</para></summary>
/// <param name="LiveStateReady">False whenever a read could return a plausible-but-wrong value:
/// not logged in, player state not loaded, mid-zone-change. When false the whole pass is skipped
/// and every entry keeps whatever it last said, which is the guard against a title screen making
/// the checklist claim the player owns nothing.</param>
/// <param name="IsPublicContentUnlocked">Public-content duties — the Eureka fields, the Bozjan
/// front, the Diadem. A separate id space from instance content, never a fallback for it.</param>
/// <param name="IsPublicContentCompleted">See <paramref name="IsPublicContentUnlocked"/>.</param>
/// <param name="IsAchievementComplete">Null until the achievement table has been fetched from the
/// server; an unfetched table reads as "you have earned nothing".</param>
/// <param name="IsAetherCurrentZoneComplete">Whether every current in one zone's completion flag
/// set is collected — the "can you fly here" question.</param>
/// <param name="SharedFateRankAtLeast">Whether a zone's Shared FATE rank reaches a threshold. Null
/// until the progress tab has arrived, which matters because rank 0 is itself a legal answer — and
/// threshold-shaped rather than returning the rank because the answer may come from a remembered
/// observation, which can prove a requirement met but never prove one unmet.</param>
/// <param name="ZoneProgressAtLeast">Whether Eureka elemental level or Bozja resistance rank
/// reaches a threshold. Null outside the zone that owns the director.</param>
/// <param name="IsTitleUnlocked">Whether the player has earned one <c>Title</c> row. Null until
/// something has caused the client to hold an answer: the unlocked-titles bitfield is all zeroes
/// before the list is asked for, so an unguarded read tells a character with two hundred titles
/// that they have none.</param>
/// <param name="GetTitleDataState">Which of the two unknowns
/// <paramref name="IsTitleUnlocked"/>'s null is — never asked for, or asked for and still coming.
/// It shapes the sentence shown in place of an answer and decides nothing.</param>
/// <param name="GetSaddlebagItemCount">The chocobo saddlebags, which an ordinary inventory count
/// does not include.</param>
public sealed record UnlockGateContext(
    int PlayerLevel,
    byte PlayerGrandCompany,
    int PlayerGrandCompanyRank,
    Func<uint, bool> IsQuestComplete,
    Func<uint, bool> IsQuestAccepted,
    Func<uint, int> GetClassJobLevel,
    Func<uint, bool> IsInstanceContentCompleted,
    Func<uint, bool> IsInstanceContentUnlocked,
    Func<byte, byte> GetBeastTribeRank,
    Func<uint, bool> IsMountUnlocked,
    Func<uint, bool> IsMinionUnlocked,
    Func<uint, int> GetOwnedItemCount,
    Func<uint, int> GetKeyItemCount,
    Func<GameTextRef, string?>? ResolveGameText = null,
    bool LiveStateReady = true,
    Func<uint, bool>? IsPublicContentUnlocked = null,
    Func<uint, bool>? IsPublicContentCompleted = null,
    Func<uint, bool?>? IsAchievementComplete = null,
    Func<uint, bool?>? IsAetherCurrentZoneComplete = null,
    Func<uint, int, bool?>? SharedFateRankAtLeast = null,
    Func<ZoneProgressKind, int, bool?>? ZoneProgressAtLeast = null,
    Func<uint, bool?>? IsTitleUnlocked = null,
    Func<TitleDataState>? GetTitleDataState = null,
    Func<uint, int>? GetSaddlebagItemCount = null)
{
    /// <summary>The kinds this build can evaluate. Defaults to the shipped registry; a test may
    /// pass its own to prove the unknown-kind fallback without shipping a broken catalogue.</summary>
    public GateEvaluatorRegistry Gates { get; init; } = GateEvaluatorRegistry.Standard;

    /// <summary>This context, seen the way a gate evaluator sees it — a stateless view over the
    /// delegates above, not a snapshot, so it is cheap to take and never goes stale.</summary>
    public ILiveState Live => new GateContextLiveState(this);
}
