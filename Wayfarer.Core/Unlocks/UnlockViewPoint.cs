namespace Wayfarer.Core.Unlocks;

/// <summary>Where the player is, for the two decisions that depend on it: which zone group floats to
/// the top, and what "nearest first" means in the Available-now view.
///
/// <para>A record rather than four parameters because it is one fact — the player's position — and
/// because the alternative was a <c>Build</c> call whose last four arguments were a string and three
/// floats in an order nothing enforced.</para>
///
/// <para><see cref="Unknown"/> is the honest value before the client has a player: zone grouping
/// falls back to alphabetical and the Available-now view falls back to level order, both of which are
/// answers rather than guesses at a position.</para></summary>
/// <param name="ZoneName">The zone the player is in, or null when it is not known.</param>
/// <param name="Territory">The territory id, matched against an entry's <c>GiverTerritory</c>.</param>
/// <param name="X">The player's world X.</param>
/// <param name="Z">The player's world Z.</param>
public sealed record UnlockViewPoint(string? ZoneName, uint Territory, float X, float Z)
{
    /// <summary>No position known.</summary>
    public static UnlockViewPoint Unknown { get; } = new(null, 0, 0f, 0f);
}
