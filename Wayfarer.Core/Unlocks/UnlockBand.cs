namespace Wayfarer.Core.Unlocks;

/// <summary>The three answers a player is actually asking for, plus the one they can turn back on.
/// See <see cref="UnlockBands"/> for what each means and why the third exists.</summary>
public enum UnlockBand
{
    /// <summary>Every checkable gate satisfied. Carries a route.</summary>
    Available,

    /// <summary>Something specific is in the way, and the row says what.</summary>
    Blocked,

    /// <summary>Wayfarer does not know. Listed, labelled, never guessed at.</summary>
    NotKnown,

    /// <summary>Already done. Off by default; last when the player asks for it.</summary>
    Complete,
}
