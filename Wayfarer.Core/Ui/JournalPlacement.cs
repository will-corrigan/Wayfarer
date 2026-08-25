using System.Numerics;

namespace Wayfarer.Core.Ui;

/// <summary>Where the journal page goes relative to the list it belongs beside.
///
/// <para><b>Why it is a follow and not an attachment.</b> The game has no parent-child relationship
/// between two addons: an <c>AtkUnitBase</c> owns its own position and has no owner field, and the
/// toolkit offers nothing above it. The game's own journal solves this the same way — <c>Journal</c>
/// positions <c>JournalDetail</c> — so the page is repositioned every tick against wherever the hub
/// now is. That covers every case a one-shot placement misses: a drag, a resize, a preset that moves
/// the hub, and a resolution or interface-scale change under an open page.</para>
///
/// <para><b>Two units, and mixing them was a bug.</b> An addon's position is in screen pixels; every
/// number authored in a uld is in addon units, and the interface scale is what converts. The overlap
/// offsets below are authored numbers — <c>Journal.uld</c>'s node <c>#9</c> is at (450,-40) relative
/// to a 462-wide list panel, so the page starts twelve pixels inside the list's right edge and forty
/// above its top, a deliberate overlap that lets the border's ornament cross the seam. Adding those
/// unscaled to a screen-pixel position put the page half an ornament out of place at every scale but
/// 100%.</para>
///
/// <para><b>Clamped, and not as a nicety.</b> This window is chromeless on purpose and has no title
/// bar, so a page whose top-left has gone off screen cannot be dragged back.</para></summary>
public static class JournalPlacement
{
    /// <summary>Where the page's top-left belongs, in screen pixels.</summary>
    /// <param name="hostPosition">The list window's position, in screen pixels.</param>
    /// <param name="hostSize">The list window's size, in screen pixels.</param>
    /// <param name="pageSize">The page's own size, in screen pixels.</param>
    /// <param name="screen">The viewport, in screen pixels. Zero or negative disables the clamp,
    /// which is the state during a resolution change rather than one a player is ever in.</param>
    /// <param name="scale">The interface scale, which is what converts the authored overlap offsets
    /// into screen pixels.</param>
    public static Vector2 Beside(
        Vector2 hostPosition, Vector2 hostSize, Vector2 pageSize, Vector2 screen, float scale)
    {
        var s = scale <= 0f ? 1f : scale;
        var wanted = new Vector2(
            hostPosition.X + hostSize.X - (GameMetrics.JournalFrame.BesideOverlapX * s),
            hostPosition.Y - (GameMetrics.JournalFrame.BesideOverlapY * s));

        if (screen.X <= 0f || screen.Y <= 0f)
        {
            return wanted;
        }

        return new Vector2(
            Math.Clamp(wanted.X, 0f, Math.Max(screen.X - pageSize.X, 0f)),
            Math.Clamp(wanted.Y, 0f, Math.Max(screen.Y - pageSize.Y, 0f)));
    }
}
