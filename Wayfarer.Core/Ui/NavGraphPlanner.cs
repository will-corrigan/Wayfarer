namespace Wayfarer.Core.Ui;

/// <summary>Pure index arithmetic for the game's cursor-navigation graph, kept here (away from any
/// Dalamud/KamiToolKit type) purely so the two things that can silently break it are unit-testable:
/// the 255 ceiling and index collisions between regions.
///
/// Two facts govern everything below, both verified against FFXIVClientStructs' own
/// <c>AtkCursorNavigationInfo</c> (five <see langword="byte"/> fields at 0x00–0x04):
/// <list type="number">
/// <item><description>Every index is a <b>byte</b>. KamiToolKit's <c>NavIndex</c> property is typed
/// <see langword="int"/> and its setter does an <b>unchecked</b> <c>(byte)value</c> cast, so index
/// 256 silently becomes 0 and the whole region falls out of the graph with no error anywhere. This
/// is why <see cref="MaxIndex"/> exists and why callers are expected to assert against it.</description></item>
/// <item><description>Index <b>0 is reserved</b> and means "not in the graph" — KamiToolKit gates
/// all of its own nav wiring behind <c>if (NavIndex is not 0)</c> and uses 0 as a deliberate
/// dead-end while scrolling. Never allocate it to a real element.</description></item>
/// </list></summary>
public static class NavGraphPlanner
{
    /// <summary>The reserved "no navigation" index. Never assign it to a real element.</summary>
    public const int NoNavigation = 0;

    /// <summary>Highest addressable index. The struct's fields are bytes, so 255 is a hard
    /// ceiling and exceeding it truncates silently rather than throwing.</summary>
    public const int MaxIndex = 255;

    /// <summary>Numbers a vertical stack of horizontal rows into one dense, absolute index block
    /// starting at <paramref name="startIndex"/>.
    ///
    /// Within a row, items chain left/right and <b>wrap</b> at both ends (the game's own tab bars
    /// behave this way). Between rows, an item's up/down neighbour is the item in the same column
    /// of the adjacent row, clamped to that row's width — so a 5-chip filter row above a 1-button
    /// action row all lands on the button, and every column keeps its place when the row above
    /// happens to be wider. The first row's up and the last row's down leave the block via
    /// <paramref name="navUp"/> / <paramref name="navDown"/>.
    ///
    /// A row of size 0 is skipped entirely rather than consuming an index: an element that is not
    /// present must not leave a hole in the graph for the cursor to fall into.</summary>
    /// <param name="rowSizes">Navigable item count per row, top to bottom.</param>
    /// <param name="startIndex">First index to allocate. Must be at least 1.</param>
    /// <param name="navUp">Where "up" from the first row leaves the block.</param>
    /// <param name="navDown">Where "down" from the last row leaves the block.</param>
    /// <returns>One list of links per input row, in the same order (empty for empty rows).</returns>
    public static IReadOnlyList<IReadOnlyList<NavLink>> Plan(
        IReadOnlyList<int> rowSizes, int startIndex, int navUp, int navDown)
    {
        ArgumentNullException.ThrowIfNull(rowSizes);
        ArgumentOutOfRangeException.ThrowIfLessThan(startIndex, 1);

        var starts = new int[rowSizes.Count];
        var occupied = new List<int>();
        var next = startIndex;
        for (var row = 0; row < rowSizes.Count; row++)
        {
            starts[row] = next;
            if (rowSizes[row] > 0)
            {
                occupied.Add(row);
                next += rowSizes[row];
            }
        }

        var result = new List<IReadOnlyList<NavLink>>(rowSizes.Count);
        for (var row = 0; row < rowSizes.Count; row++)
        {
            result.Add(rowSizes[row] <= 0
                ? []
                : PlanRow(rowSizes, starts, occupied, row, navUp, navDown));
        }

        return result;
    }

    /// <summary>The highest index <see cref="Plan"/> would allocate, so callers (and tests) can
    /// assert the whole layout fits under <see cref="MaxIndex"/> before anything is built.
    /// Returns <paramref name="startIndex"/> - 1 when nothing is allocated.</summary>
    public static int HighestIndex(IReadOnlyList<int> rowSizes, int startIndex)
    {
        ArgumentNullException.ThrowIfNull(rowSizes);

        var total = 0;
        foreach (var size in rowSizes)
        {
            if (size > 0)
            {
                total += size;
            }
        }

        return startIndex + total - 1;
    }

    /// <summary>Whether a layout fits the indices available to it. Callers should treat a
    /// <see langword="false"/> here as "do not wire this region at all" rather than wiring it and
    /// letting the overflow silently orphan or collide with something.
    ///
    /// <para><paramref name="ceiling"/> is the highest index this region may occupy. It defaults to
    /// the hard <see cref="MaxIndex"/> byte ceiling, but a region that shares the space with others
    /// has a lower one — the hub's control region must stop before the list block, and running past
    /// it does not truncate, it collides, which teleports the cursor rather than losing it.</para></summary>
    public static bool Fits(IReadOnlyList<int> rowSizes, int startIndex, int ceiling = MaxIndex) =>
        startIndex >= 1 && HighestIndex(rowSizes, startIndex) <= Math.Min(ceiling, MaxIndex);

    private static List<NavLink> PlanRow(
        IReadOnlyList<int> rowSizes, int[] starts, List<int> occupied, int row, int navUp, int navDown)
    {
        var position = occupied.IndexOf(row);
        var previous = position > 0 ? occupied[position - 1] : -1;
        var following = position < occupied.Count - 1 ? occupied[position + 1] : -1;

        var size = rowSizes[row];
        var links = new List<NavLink>(size);
        for (var column = 0; column < size; column++)
        {
            var index = starts[row] + column;
            links.Add(new NavLink(
                index,
                Neighbour(rowSizes, starts, previous, column, navUp),
                Neighbour(rowSizes, starts, following, column, navDown),
                size > 1 ? starts[row] + ((column - 1 + size) % size) : NoNavigation,
                size > 1 ? starts[row] + ((column + 1) % size) : NoNavigation));
        }

        return links;
    }

    // Same column in the adjacent row, clamped to that row's width; the block's own exit when
    // there is no adjacent row.
    private static int Neighbour(IReadOnlyList<int> rowSizes, int[] starts, int row, int column, int fallback) =>
        row < 0 ? fallback : starts[row] + Math.Min(column, rowSizes[row] - 1);
}
