namespace Wayfarer.Core.Unlocks;

/// <summary>How the checklist is broken into groups: the axis the player browses along. Bands
/// (<see cref="UnlockBand"/>) then order the rows inside each group, whichever axis is chosen —
/// "what can I act on" is the same question in every view.</summary>
public enum UnlockGrouping
{
    /// <summary>Not a grouping at all: one flat list of everything whose gates are satisfied, across
    /// every domain, nearest first.
    ///
    /// <para><b>The default, and deliberately not a domain.</b> The player's question is "what should
    /// I do next", not "show me a taxonomy" — domains are how you browse, this is how you play. It is
    /// in this enum rather than beside it because it occupies the same control: it is one of the
    /// things the list can be, and a separate switch for it would let the two disagree about which
    /// view is showing.</para></summary>
    AvailableNow,

    /// <summary>By <see cref="UnlockDomains"/> — each domain a window the game already has.</summary>
    Domain,

    /// <summary>By the zone the unlocking quest is in, current zone first.</summary>
    Zone,

    /// <summary>By ten-level band.</summary>
    Level,
}
