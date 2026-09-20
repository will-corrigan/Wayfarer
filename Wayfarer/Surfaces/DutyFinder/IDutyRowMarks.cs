namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>Something that wants duties marked in the Duty Finder: a module, saying which duties
/// it cares about and what to draw on them. It says nothing about where the mark goes, because
/// where it goes depends on what every other module asked for as well.</summary>
internal interface IDutyRowMarks
{
    /// <summary>The icons this wants on a duty's row, in the order it wants them, or nothing when
    /// it has no interest in that duty.</summary>
    /// <param name="duty">The Duty Finder entry the row is for, as the sheet numbers it.</param>
    IReadOnlyList<uint> MarksFor(uint duty);
}
