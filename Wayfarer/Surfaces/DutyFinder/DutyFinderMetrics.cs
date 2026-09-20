namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>Where things are in a Duty Finder row, from the window's own layout file
/// (<c>ui/uld/ContentsFinder.uld</c>, the 448x24 list item). Read from the file rather than
/// measured in game, so these are the game's numbers and not a guess at them.</summary>
internal static class DutyFinderMetrics
{
    /// <summary>The row's own name, the text the player reads. Present only on a duty's row: a
    /// heading in the list is a different kind of row and has no node with this id, which is what
    /// tells the two apart without trusting anything else.</summary>
    public const uint RowNameTextNodeId = 6;

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
    /// little off it. Nothing else in the row is drawn here, so no name is ever covered however
    /// long it runs.</summary>
    public const float BadgeLeft = TypeIconLeft + TypeIconSize - (BadgeSize * BadgeOverhang);

    /// <inheritdoc cref="BadgeLeft"/>
    public const float BadgeTop = TypeIconTop + TypeIconSize - (BadgeSize * BadgeOverhang);

    /// <summary>How much of the mark hangs off the corner of the game's icon. Enough that it reads
    /// as sitting on the corner rather than inside the square, and not so much that it drifts off
    /// into the row.</summary>
    private const float BadgeOverhang = 0.65f;
}
