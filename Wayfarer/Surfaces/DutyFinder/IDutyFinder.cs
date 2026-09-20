namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>The Duty Finder's rows, which a module may mark.</summary>
internal interface IDutyFinder
{
    /// <summary>Marks rows for as long as the returned handle is held, and stops when it is
    /// disposed. The window is watched only while something is marking it.</summary>
    /// <param name="marks">Asked, for each duty on show, what it wants drawn on that row.</param>
    IDisposable Mark(IDutyRowMarks marks);
}
