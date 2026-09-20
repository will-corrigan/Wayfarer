namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>Where a row's icons go and how big they are: the game's own and ours, laid out as one
/// strip.
///
/// <para>A row keeps three slots for the icons it may need to show, and lights one or two on most
/// rows. While the dark ones are room enough, marks go in those at their natural size and nothing
/// of the game's is touched: its icons stay where the player is used to finding them, and a row
/// another plugin has marked is left as it was found.</para>
///
/// <para>When there is not room enough, the strip is worked out again as one list. It keeps its
/// right edge, and the first thing to give is the size of the icons: every symbol in the strip,
/// the game's and ours, is drawn smaller so that more of them fit in the same space. Only when
/// they would be too small to read does the strip grow leftwards, and then the room it takes comes
/// out of the row's name.</para>
///
/// <para>Everything that has to move or change size is said here, so putting it all back is a
/// matter of record rather than of guesswork.</para></summary>
internal static class StripLayout
{
    /// <summary>Lays out a row's icons.</summary>
    /// <param name="marks">How many marks are wanted.</param>
    /// <param name="dark">Where the row's unused slots are, left to right.</param>
    /// <param name="lit">How many icons the game itself is showing.</param>
    /// <param name="space">The room the row keeps for them.</param>
    public static Strip Place(int marks, IReadOnlyList<float> dark, int lit, StripSpace space)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(marks);
        ArgumentNullException.ThrowIfNull(dark);
        ArgumentOutOfRangeException.ThrowIfNegative(lit);
        ArgumentNullException.ThrowIfNull(space);

        // Room enough as things stand: ours go in the gaps, at full size, and the game's stay put.
        // The gaps taken are the rightmost, because the strip is anchored to the right of the row
        // and a mark belongs at that end whether or not the game has put anything there.
        if (marks <= dark.Count)
        {
            return new Strip([.. dark.TakeLast(marks)], [], space.Pitch, null);
        }

        // Not room enough. Every symbol shrinks together until they all fit, and no further than
        // is still legible.
        var icons = lit + marks;
        var size = Math.Clamp(space.Width / icons, space.Smallest, space.Pitch);

        // Still short even at that size, so the strip grows left and the name gives up the room.
        var left = space.Right - (icons * size);
        var places = Enumerable.Range(0, icons).Select(slot => left + (slot * size)).ToList();
        return new Strip(places[..marks], places[marks..], size, left < space.Left ? left : null);
    }

    /// <summary>The room a row keeps for its icons.</summary>
    /// <param name="Left">Where the strip begins while it holds what it was built for.</param>
    /// <param name="Right">Where it ends, which it keeps however many icons there are.</param>
    /// <param name="Pitch">How big an icon is when there is room for it to be.</param>
    /// <param name="Smallest">How small an icon may be drawn before it stops being worth drawing,
    /// past which the strip takes room from the name instead of shrinking further.</param>
    internal sealed record StripSpace(float Left, float Right, float Pitch, float Smallest)
    {
        /// <summary>How wide the strip is before it has to take room from anything.</summary>
        public float Width => Right - Left;
    }

    /// <summary>Where a row's icons sit and how big they are.</summary>
    /// <param name="Marks">Where each mark goes, in the order they were asked for.</param>
    /// <param name="GameIcons">Where the game's own icons go, left to right, when they have had to
    /// move. Empty when they have not, which is when nothing of the game's is to be touched.</param>
    /// <param name="Size">How big every icon in the strip is to be drawn, the game's and ours.</param>
    /// <param name="Left">Where the strip now begins, and so where the row's name must now end.
    /// Null when the strip has not outgrown its own room and the name is to be left alone.</param>
    internal sealed record Strip(IReadOnlyList<float> Marks, IReadOnlyList<float> GameIcons, float Size, float? Left);
}
