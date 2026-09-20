namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>Where a row's marks go, given the slots the game is not using.
///
/// <para>A row keeps a strip of slots for the icons it may need to show — level sync, unrestricted
/// party, and the rest — and most rows light only one or two. The dark ones are room already set
/// aside at the right size and spacing, so marks go in those and the game's own icons are left
/// exactly where they are. Nothing of the game's is moved, so nothing has to be put back, and
/// another plugin marking the same row is not disturbed.</para>
///
/// <para>Only when there are more marks than dark slots does anything have to give. Then they
/// carry on leftwards past the strip and the layout says how much room that took, which is what
/// the row's name has to give up. It does not take it: that is the caller's to do, or not.</para></summary>
internal static class StripLayout
{
    /// <summary>Lays out a row's marks in the slots the game left dark.</summary>
    /// <param name="marks">How many marks are to be shown.</param>
    /// <param name="dark">Where the unused slots are, left to right.</param>
    /// <param name="stripLeft">Where the strip begins, which is where marks carry on left from.</param>
    /// <param name="pitch">How far apart slots sit, which is one slot's width.</param>
    /// <returns>Where each mark goes, in order, and how much room was wanted past the strip.</returns>
    public static Strip Place(int marks, IReadOnlyList<float> dark, float stripLeft, float pitch)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(marks);
        ArgumentNullException.ThrowIfNull(dark);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pitch);

        var places = new List<float>(marks);
        for (var index = 0; index < marks; index++)
        {
            places.Add(index < dark.Count
                ? dark[index]
                : stripLeft - ((index - dark.Count + 1) * pitch));
        }

        var beyond = Math.Max(0, marks - dark.Count);
        return new Strip(places, beyond * pitch);
    }

    /// <summary>Where a row's marks sit.</summary>
    /// <param name="Places">Where each mark goes, in the order they were asked for.</param>
    /// <param name="Overflow">How much room was wanted past the strip's left edge, which is what
    /// the row's name has to give up for everything to fit. Zero when it all fits.</param>
    internal sealed record Strip(IReadOnlyList<float> Places, float Overflow);
}
