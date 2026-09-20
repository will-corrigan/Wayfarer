namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>Something that wants duties marked in the Duty Finder. A module implements this; the
/// surface that draws the marks never learns why a duty is marked, only which icon to put on it.
/// Quests and anything else that cares about duties answer the same question.</summary>
internal interface IDutyBadgeSource
{
    /// <summary>The icon to put on this duty's row, or null to leave the row unmarked.</summary>
    /// <param name="duty">The Duty Finder entry the row is for, as the sheet numbers it.</param>
    uint? IconFor(uint duty);
}
