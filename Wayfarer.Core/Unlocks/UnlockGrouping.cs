namespace Wayfarer.Core.Unlocks;

/// <summary>How the checklist is broken into groups: the axis the player browses along. Bands
/// (<see cref="UnlockBand"/>) then order the rows inside each group, whichever axis is chosen —
/// "what can I act on" is the same question in every view.</summary>
public enum UnlockGrouping
{
    /// <summary>By <see cref="UnlockDomains"/> — the default, because each domain is a window the
    /// game already has.</summary>
    Domain,

    /// <summary>By the zone the unlocking quest is in, current zone first.</summary>
    Zone,

    /// <summary>By ten-level band.</summary>
    Level,
}
