namespace Wayfarer.Core.Ui;

/// <summary>Scroll-follows-focus, as arithmetic.
///
/// <b>Why this is its own tested thing.</b> A controller player reported that the window's Settings
/// tab "would select things clipped off the box as if it were visible in the box". That is exactly
/// what a scrolling container without scroll-follows-focus does: the container clips what is out of
/// view, but the cursor graph does not know about clipping, so the cursor walks happily onto rows
/// nobody can see and the player is pressing Confirm on an invisible control. The game's own
/// scrolling lists never do this — moving the cursor onto a row scrolls the row into view — and this
/// is that rule, in one place, where it can be tested without a game attached.
///
/// The list-backed tabs (Checklist, Hunting Log, Quests) get this for free from KamiToolKit's
/// <c>ListNode</c>. The Settings tab is a <c>ScrollingNode</c>, which has no navigation
/// implementation at all, so it has to be given one from the outside.</summary>
public static class ScrollIntoView
{
    /// <summary>The scroll position that brings the item into view, or the current one when it is
    /// already fully visible.
    ///
    /// <para>Scroll positions here are pixels from the top of the content, which is what the game's
    /// own scrollbar component uses. Nudging up to the item's top when it is above the viewport and
    /// down to its bottom when it is below is the minimum movement that works, and minimum movement
    /// is what stops the list from jumping about as the cursor walks down it.</para></summary>
    /// <param name="itemTop">The item's top edge, in content coordinates.</param>
    /// <param name="itemHeight">The item's height.</param>
    /// <param name="viewportHeight">How much of the content is visible at once.</param>
    /// <param name="currentScroll">Where the container is scrolled to now.</param>
    /// <param name="maxScroll">The container's own scroll ceiling.</param>
    public static float Adjust(
        float itemTop, float itemHeight, float viewportHeight, float currentScroll, float maxScroll)
    {
        if (maxScroll <= 0f || viewportHeight <= 0f)
        {
            return 0f;
        }

        var itemBottom = itemTop + Math.Max(itemHeight, 0f);
        var target = currentScroll;

        if (itemTop < currentScroll)
        {
            target = itemTop;
        }
        else if (itemBottom > currentScroll + viewportHeight)
        {
            // An item taller than the viewport is aligned to its top rather than its bottom: the
            // top is where its label is, and scrolling to the bottom of a tall item shows the
            // player the part that does not say what it is.
            target = itemHeight > viewportHeight ? itemTop : itemBottom - viewportHeight;
        }

        return Math.Clamp(target, 0f, maxScroll);
    }
}
