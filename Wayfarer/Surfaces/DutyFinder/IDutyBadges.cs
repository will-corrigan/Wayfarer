namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>The Duty Finder's rows, which anything may mark with an icon.</summary>
internal interface IDutyBadges
{
    /// <summary>Marks rows for as long as the returned handle is held, and stops when it is
    /// disposed. The surface watches the window only while something is marking it.</summary>
    /// <param name="source">Asked, for each duty on show, what icon its row should carry.</param>
    IDisposable Mark(IDutyBadgeSource source);
}
