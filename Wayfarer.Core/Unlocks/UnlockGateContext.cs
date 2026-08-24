namespace Wayfarer.Core.Unlocks;

/// <summary>Everything <see cref="UnlockStatusCalculator.Compute"/> needs to read live game
/// state, injected so the calculator stays pure and unit-testable. Values that don't vary per
/// unlock entry (the player's own level/GC/rank) are plain scalars, snapshotted once per pass;
/// per-id lookups (a specific job's level, a specific duty's completion, ...) are delegates
/// since they're evaluated per entry.
///
/// <para><see cref="ResolveGameText"/> is the one exception to "game state": it reads game
/// <i>text</i> rather than game <i>progress</i>, resolving a <see cref="GameTextRef"/> against the
/// live client's own sheets in the player's own client language. Optional and defaulted to null so
/// existing callers (chiefly tests) don't have to wire it; a null resolver, or one that misses,
/// just means the curated <see cref="UnlockRequirement.Label"/> fallback is used instead.</para></summary>
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
    Func<GameTextRef, string?>? ResolveGameText = null);
