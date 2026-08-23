namespace Wayfarer.Core.Unlocks;

/// <summary>Everything <see cref="UnlockStatusCalculator.Compute"/> needs to read live game
/// state, injected so the calculator stays pure and unit-testable. Values that don't vary per
/// unlock entry (the player's own level/GC/rank) are plain scalars, snapshotted once per pass;
/// per-id lookups (a specific job's level, a specific duty's completion, ...) are delegates
/// since they're evaluated per entry.</summary>
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
    Func<uint, int> GetKeyItemCount);
