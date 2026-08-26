namespace Wayfarer.Core.Unlocks;

/// <summary>One group — a domain, a zone, a level band — and the bands inside it.</summary>
/// <param name="Heading">What the group's heading row says.</param>
/// <param name="Bands">Non-empty bands only, in <see cref="UnlockBands.All"/> order.</param>
/// <param name="ShowBandHeadings">Whether the bands inside this group get headings of their own.
///
/// False only in the Available-now view, where the group heading already says which band this is and
/// a second line saying "Available" under it would be the same word twice. True everywhere else,
/// including for a group with a single band: a domain whose entries are all blocked, or all not
/// known, is exactly where the label earns its line.</param>
public sealed record UnlockGroupSection(
    string Heading, IReadOnlyList<UnlockBandSection> Bands, bool ShowBandHeadings = true)
{
    /// <summary>Rows in this group, across all its bands — what the heading's count says. Counted
    /// from the bands rather than carried alongside them, so the number on the heading cannot
    /// disagree with the number of rows drawn under it.</summary>
    public int Count => Bands.Sum(b => b.Entries.Count);
}
