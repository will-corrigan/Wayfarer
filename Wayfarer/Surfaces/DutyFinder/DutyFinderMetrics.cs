namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>Where things are in a Duty Finder row.
///
/// <para>The node indexes are into the list's own populator, which is the array of parts the game
/// fills in as it draws a row, and not node ids. The geometry is out of the window's layout file
/// (<c>ui/uld/ContentsFinder.uld</c>, the 448x24 list item), so these are the game's numbers
/// rather than a guess at them.</para></summary>
internal static class DutyFinderMetrics
{
    /// <summary>The node id of the row template the duty list draws its rows from. What the list
    /// is asked for to find the function that fills a row in.</summary>
    public const uint RowTemplateNodeId = 6;

    /// <summary>The row's own name, the text the player reads.</summary>
    public const int RowNameNodeIndex = 3;

    /// <summary>Where a row keeps its place in the Duty Finder agent's list, counted from one.</summary>
    public const int RowContentIndexValue = 1;

    /// <summary>The square at the left of a row holding the game's own icon for what kind of
    /// content it is, 20x20 at (2, 2).</summary>
    public const float TypeIconLeft = 2f;

    /// <inheritdoc cref="TypeIconLeft"/>
    public const float TypeIconTop = 2f;

    /// <inheritdoc cref="TypeIconLeft"/>
    public const float TypeIconSize = 20f;

    /// <summary>How big our own mark is. Small enough to read as a mark on the game's icon rather
    /// than as another icon beside it.</summary>
    public const float BadgeSize = 13f;

    /// <summary>Where the mark sits: the bottom right corner of the game's own icon, hanging a
    /// little off it. Nothing else in the row is drawn there, so no name is ever covered however
    /// long it runs, and the right of the row is left to whatever else marks it.</summary>
    public const float BadgeLeft = TypeIconLeft + TypeIconSize - (BadgeSize * BadgeOverhang);

    /// <inheritdoc cref="BadgeLeft"/>
    public const float BadgeTop = TypeIconTop + TypeIconSize - (BadgeSize * BadgeOverhang);

    /// <summary>How much of the mark hangs off the corner of the game's icon. Enough that it reads
    /// as sitting on the corner rather than inside the square, and not so much that it drifts off
    /// into the row.</summary>
    private const float BadgeOverhang = 0.65f;
}
