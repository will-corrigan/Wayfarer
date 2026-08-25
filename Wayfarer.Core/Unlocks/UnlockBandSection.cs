namespace Wayfarer.Core.Unlocks;

/// <summary>One band inside a group: which band, and the rows under its heading.</summary>
/// <param name="Band">Which band, so the caller can label and mark it.</param>
/// <param name="Entries">The rows, sorted by level and then name.</param>
public sealed record UnlockBandSection(UnlockBand Band, IReadOnlyList<ResolvedUnlock> Entries);
