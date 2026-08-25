namespace Wayfarer.Core.Ui;

/// <summary>The reward tray, laid out once for both places that draw it — the detail strip and the
/// journal page.
///
/// <para>The tray is the same object at both scales: <c>Journal_Detail.tex</c> (0,28) 376x52, a slot
/// inset 15 from its left edge, a 36x36 icon 8 down inside that slot, and the reward's name beside
/// it. Two copies of that arithmetic would be two places to fix the day the tray moves, and the page
/// exists precisely to show the same reward the strip shows — bigger, not different.</para></summary>
public static class JournalTrayLayout
{
    /// <summary>The tray itself, at the width the game authors it, indented into
    /// <paramref name="block"/> by <paramref name="left"/>.
    ///
    /// <para>The art is a plain image in the game and never stretched, so a block too narrow to hold
    /// 376 gets a narrower crop rather than a distorted panel.</para></summary>
    public static ScreenRect Tray(ScreenRect block, float left)
    {
        if (block.IsEmpty)
        {
            return default;
        }

        var width = Math.Min(GameMetrics.Journal.ColumnWidth, Math.Max(block.Width - left, 0f));
        return width <= 0f ? default : block with { X = block.X + left, Width = width };
    }

    /// <summary>The reward's own icon, in the tray's first slot. Empty when the tray was dropped or
    /// is too narrow to hold a slot — and empty is also what a reward with no icon gets, which is
    /// why <see cref="Name"/> is placed against the tray rather than against the icon.</summary>
    public static ScreenRect Icon(ScreenRect tray)
    {
        var inset = GameMetrics.Journal.TrayInset;
        var size = GameMetrics.Journal.SlotIconSize;
        return tray.IsEmpty || tray.Width < inset + size || tray.Height < size
            ? default
            : new ScreenRect(tray.X + inset, tray.Y + GameMetrics.Journal.SlotIconTop, size, size);
    }

    /// <summary>The reward said in words, beside its icon. Always drawn when the tray is: a
    /// KamiToolKit tooltip fires on mouse events only, so an icon with no text is unreadable on a
    /// controller — and half the reward kinds the game ships have no icon at all.</summary>
    public static ScreenRect Name(ScreenRect tray, ScreenRect icon)
    {
        if (tray.IsEmpty)
        {
            return default;
        }

        var inset = GameMetrics.Journal.TrayInset;
        var x = icon.IsEmpty ? tray.X + inset : icon.Right + GameMetrics.Window.BlockGap;
        var width = Math.Max(tray.Right - inset - x, 0f);
        var height = Math.Min(GameMetrics.Row.TextHeight, tray.Height);
        return width <= 0f
            ? default
            : new ScreenRect(x, tray.Y + ((tray.Height - height) / 2f), width, height);
    }
}
