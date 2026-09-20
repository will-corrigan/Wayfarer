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

    /// <summary>The strip a row keeps for the icons it may need to show: three slots of 20 at
    /// x 302, 322 and 342, most of them dark on most rows. PROVISIONAL, from the layout file;
    /// the game may place them elsewhere at run time.</summary>
    public const float StripLeft = 302f;

    /// <inheritdoc cref="StripLeft"/>
    public const float StripRight = 362f;

    /// <inheritdoc cref="StripLeft"/>
    public const float StripPitch = 20f;

    /// <inheritdoc cref="StripLeft"/>
    public const float StripTop = 0f;

    /// <summary>How big a mark is: one slot of the row's own strip, so ours read as the game's
    /// own icons do.</summary>
    public const float BadgeSize = StripPitch;
}
