namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>Where things are in a Duty Finder row.
///
/// <para>A node index is into the list's own populator, which is the array of parts the game fills
/// in as it draws a row, and is not a node id. A node id is what the window's layout file calls a
/// part, and is how a part the populator does not hand over is reached. The geometry is out of
/// that same file (<c>ui/uld/ContentsFinder.uld</c>, the 448x24 list item), so these are the
/// game's numbers rather than a guess at them.</para></summary>
internal static class DutyFinderMetrics
{
    /// <summary>The node id of the row template the duty list draws its rows from. What the list
    /// is asked for to find the function that fills a row in.</summary>
    public const uint RowTemplateNodeId = 6;

    /// <summary>The row's own name, the text the player reads.</summary>
    public const int RowNameNodeIndex = 3;

    /// <summary>Where a row keeps its place in the Duty Finder agent's list, counted from one.</summary>
    public const int RowContentIndexValue = 1;

    /// <summary>The strip a row keeps for icons: three slots of 20 at x 302, 322 and 342, most of
    /// them dark on most rows. PROVISIONAL, from the layout file; the game may place them
    /// elsewhere at run time.</summary>
    public const float StripLeft = 302f;

    /// <inheritdoc cref="StripLeft"/>
    public const float StripRight = 362f;

    /// <inheritdoc cref="StripLeft"/>
    public const float StripPitch = 20f;

    /// <inheritdoc cref="StripLeft"/>
    public const float StripTop = 0f;

    /// <summary>How small a symbol may be drawn before it stops being worth drawing. Past this the
    /// strip takes room from the row's name rather than shrinking any further.</summary>
    public const float SmallestIcon = 12f;

    /// <summary>The node ids of the slots holding the game's own icons — level sync, unrestricted
    /// party, and the rest — in the order they sit, left to right. Most are dark on most rows,
    /// which is the room our own marks go in. PROVISIONAL, from the layout file.</summary>
    public static readonly uint[] GameIconNodeIds = [7u, 10u, 14u];

    /// <summary>The room a row keeps for its icons, as the layout rule wants it.</summary>
    public static readonly StripLayout.StripSpace Strip = new(StripLeft, StripRight, StripPitch, SmallestIcon);
}
