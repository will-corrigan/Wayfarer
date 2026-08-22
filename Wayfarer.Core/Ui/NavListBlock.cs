namespace Wayfarer.Core.Ui;

/// <summary>The index arithmetic KamiToolKit's <c>ListNode&lt;T, TU&gt;</c> performs internally,
/// restated here so the surrounding graph can be numbered against it and so the two defects in that
/// implementation can be worked around from the outside without forking the vendored submodule.
///
/// <c>ListNode.RecalculateScroll()</c> reserves <b>four consecutive indices per row node</b> (so a
/// row can grow left/right sub-actions later) plus two sentinels — one above the viewport and one
/// below — whose only job is to consume a held up/down and scroll the list instead of moving the
/// cursor. The layout, verbatim from that method:
/// <code>
/// upwards sentinel : NavIndex
/// row n            : n * 4 + NavIndex + 1
/// downwards sentinel : poolSize * 4 + NavIndex + 1
/// </code>
///
/// Two defects follow from reading the same method, both of which the caller must handle:
/// <list type="number">
/// <item><description><b>A scrolled list cannot be exited downward.</b>
/// <c>OnDownNavReceived()</c> sets the lower sentinel's <c>NavDown</c> to 0 on the first scroll and
/// guards the restore with <c>if (scrollPosition is 0)</c> — which a just-incremented counter can
/// never satisfy. The exit stays dead until something re-runs <c>RecalculateScroll</c>.</description></item>
/// <item><description><b>The "last row" link is computed against the wrong count.</b> The loop
/// iterates <c>nodeList.Count</c> (the recycled node pool) but tests <c>index == nodeCount - 1</c>,
/// so when the list holds fewer items than the pool the real last row's <c>NavDown</c> points at a
/// row node that is currently invisible — pressing down there goes nowhere.</description></item>
/// </list></summary>
public static class NavListBlock
{
    /// <summary>Nav slots a list reserves per row node.</summary>
    public const int SlotsPerRow = 4;

    /// <summary>Absolute index of the row node at <paramref name="rowOrdinal"/> within the pool.</summary>
    public static int RowIndex(int listNavIndex, int rowOrdinal) =>
        (rowOrdinal * SlotsPerRow) + listNavIndex + 1;

    /// <summary>Absolute index of the downward scroll sentinel. Never point another region's "up"
    /// at this — it is a scroll trampoline, not a row.</summary>
    public static int DownwardSentinelIndex(int listNavIndex, int poolSize) =>
        (poolSize * SlotsPerRow) + listNavIndex + 1;

    /// <summary>Total indices a list consumes: its own (the upward sentinel), four per pooled row,
    /// and the downward sentinel.</summary>
    public static int Reserve(int poolSize) => (poolSize * SlotsPerRow) + 2;

    /// <summary>Largest row pool a list starting at <paramref name="listNavIndex"/> can carry
    /// without any index exceeding <see cref="NavGraphPlanner.MaxIndex"/>. Callers should clamp
    /// their viewport to this rather than discovering the truncation in the field, where it
    /// presents as "part of the window is simply unreachable" with nothing in the log.</summary>
    public static int MaxPoolSize(int listNavIndex)
    {
        var available = NavGraphPlanner.MaxIndex - listNavIndex - 1;
        return available <= 0 ? 0 : available / SlotsPerRow;
    }

    /// <summary>Whether a list of <paramref name="poolSize"/> rows starting at
    /// <paramref name="listNavIndex"/> fits the addressable index space.</summary>
    public static bool Fits(int listNavIndex, int poolSize) =>
        listNavIndex >= 1 && DownwardSentinelIndex(listNavIndex, poolSize) <= NavGraphPlanner.MaxIndex;
}
