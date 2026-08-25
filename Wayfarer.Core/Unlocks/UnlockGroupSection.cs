namespace Wayfarer.Core.Unlocks;

/// <summary>One group — a domain, a zone, a level band — and the bands inside it.</summary>
/// <param name="Heading">What the group's heading row says.</param>
/// <param name="Bands">Non-empty bands only, in <see cref="UnlockBands.All"/> order.</param>
public sealed record UnlockGroupSection(string Heading, IReadOnlyList<UnlockBandSection> Bands)
{
    /// <summary>Rows in this group, across all its bands — what the heading's count says. Counted
    /// from the bands rather than carried alongside them, so the number on the heading cannot
    /// disagree with the number of rows drawn under it.</summary>
    public int Count => Bands.Sum(b => b.Entries.Count);
}
