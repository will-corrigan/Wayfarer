namespace Wayfarer.Core.Guidance;

/// <summary>What a zone's aether currents add up to, with the one number that could be a lie kept
/// separate from the two that cannot.
///
/// <para><see cref="Total"/> is nullable because a plugin that says "3 of 10" when the zone wants
/// nine has invented a fact about the game. Null means the count of what is left is still true but
/// the denominator is not being claimed — see <see cref="AetherCurrentPlan.Tally"/>.</para></summary>
/// <param name="Attuned">How many of the zone's currents this character has. Always trustworthy: it
/// is one locally-read bit per current.</param>
/// <param name="Remaining">How many are left. Always trustworthy for the same reason.</param>
/// <param name="Total">The zone's full requirement, or null when the sheet's list and the client's
/// own verdict disagree about what it is.</param>
public sealed record AetherCurrentTally(int Attuned, int Remaining, int? Total);
