namespace Wayfarer.Core.Ui;

/// <summary>The one rule by which a stack of sections is placed down a column: the cursor starts at
/// the box's top and advances by whatever section it just placed says it is.
///
/// <para><b>Why this exists as its own class.</b> The readout and the journal window each rebuilt
/// this walk independently — <c>ReadoutBodyLayout.Flow</c> and <c>JournalWindowLayout.Flow</c> — while
/// solving the same problem: a block's position must be a consequence of the blocks before it
/// <i>and of nothing else</i>, so that no measurement taken on one section's behalf can ever land on
/// a different one. Both callers still expose their own <c>Flow</c>/<c>FlowHeight</c> with their own
/// spacing, but the walk itself lives here once.</para>
///
/// <para><b>The rule.</b> A section of no height takes no room and no spacing, which is what
/// <c>VerticalListNode</c> does with an invisible child. Two consequences: no section's position
/// depends on a measurement of any section other than the ones above it, and no two returned
/// rectangles can intersect, whatever heights are handed in — including heights far taller than the
/// box, which run off the bottom rather than into a sibling.</para></summary>
public static class FlowLayout
{
    /// <summary>Places a stack of sections, taking each section's height from the section itself and
    /// separating placed sections by <paramref name="spacing"/>. The gap is charged only between two
    /// sections that both actually take room, never before the first one and never after the last.
    /// </summary>
    public static IReadOnlyList<ScreenRect> Flow(IReadOnlyList<float> heights, float spacing, ScreenRect box)
    {
        ArgumentNullException.ThrowIfNull(heights);

        var placed = new ScreenRect[heights.Count];
        var y = box.Y;
        var first = true;

        for (var i = 0; i < heights.Count; i++)
        {
            if (heights[i] <= 0f || box.Width <= 0f)
            {
                continue;
            }

            if (!first)
            {
                y += spacing;
            }

            placed[i] = new ScreenRect(box.X, y, box.Width, heights[i]);
            y += heights[i];
            first = false;
        }

        return placed;
    }

    /// <summary>How tall that stack comes out — the height a container with <c>FitContents</c> takes
    /// on. The same walk as <see cref="Flow"/>, and it has to be, so a caller's height and its contents
    /// cannot disagree.</summary>
    public static float FlowHeight(IReadOnlyList<float> heights, float spacing)
    {
        ArgumentNullException.ThrowIfNull(heights);

        var total = 0f;
        var first = true;

        foreach (var height in heights)
        {
            if (height <= 0f)
            {
                continue;
            }

            total += first ? height : spacing + height;
            first = false;
        }

        return total;
    }
}
