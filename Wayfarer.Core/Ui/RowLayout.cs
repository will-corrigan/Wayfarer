namespace Wayfarer.Core.Ui;

/// <summary>Where the parts of a list row go, as plain arithmetic.
///
/// <para>Every number comes from <see cref="GameMetrics.Row"/>, which is to say from Journal's and
/// the Duty Finder's own tree-list rows. The row used to derive its two line heights by halving
/// whatever height it happened to be given, which made the leading a function of the row rather than
/// of the font, and put a 14pt name in a 19-pixel box — the gap that produced is the "text spacing
/// is huge" complaint. The lines are now the game's own line boxes and the row is their sum.</para>
///
/// <para>Every rectangle returned is clipped to the row's own box, so no caller can put a node
/// outside it however narrow the window gets.</para></summary>
public static class RowLayout
{
    /// <summary>The height of each row shape.</summary>
    public static float Height(RowShape shape) => shape switch
    {
        RowShape.Section => GameMetrics.Row.SectionHeight,
        _ => GameMetrics.Row.EntryHeight,
    };

    /// <summary>Lays a row out at <paramref name="width"/>. <paramref name="height"/> is what the
    /// list actually gave the row, which may differ from <see cref="Height"/> while a resize is in
    /// flight.</summary>
    public static RowBlocks Compose(RowShape shape, float width, float height, bool hasIcon)
    {
        var box = new ScreenRect(0f, 0f, Math.Max(width, 0f), Math.Max(height, 0f));
        return shape switch
        {
            RowShape.Note => ComposeNote(box),
            RowShape.Section => ComposeSection(box, hasIcon),
            _ => ComposeEntry(box, hasIcon),
        };
    }

    /// <summary>The whole row, one wrapping block, indented to the text column so a note lines up
    /// with the entries it is standing in for.</summary>
    private static RowBlocks ComposeNote(ScreenRect box)
    {
        var text = new ScreenRect(
            GameMetrics.Row.TextLeft,
            GameMetrics.Row.TextTop,
            box.Width - GameMetrics.Row.TextLeft - GameMetrics.Row.Padding,
            box.Height - (GameMetrics.Row.TextTop * 2f));
        return new RowBlocks(default, Clip(text, box), default, default);
    }

    private static RowBlocks ComposeSection(ScreenRect box, bool hasIcon)
    {
        var iconTop = (box.Height - GameMetrics.Row.SectionIconSize) / 2f;
        var icon = hasIcon
            ? Clip(
                new ScreenRect(0f, iconTop, GameMetrics.Row.SectionIconSize, GameMetrics.Row.SectionIconSize),
                box)
            : default;

        var left = hasIcon ? GameMetrics.Row.SectionTextLeft : GameMetrics.Row.Padding;
        var trailing = Trailing(box);
        return new RowBlocks(icon, Label(box, left, trailing), trailing, default);
    }

    private static RowBlocks ComposeEntry(ScreenRect box, bool hasIcon)
    {
        var iconTop = (box.Height - GameMetrics.Row.IconSize) / 2f;
        var icon = hasIcon
            ? Clip(
                new ScreenRect(
                    GameMetrics.Row.Padding, iconTop, GameMetrics.Row.IconSize, GameMetrics.Row.IconSize),
                box)
            : default;

        var left = hasIcon ? GameMetrics.Row.TextLeft : GameMetrics.Row.Padding;
        var trailing = Trailing(box);

        // The second line is the game's own dimmed sub-row, sitting directly on top of the first with
        // no invented gap — that is how the Journal stacks its own two.
        var description = new ScreenRect(
            left,
            GameMetrics.Row.Height + GameMetrics.Row.SecondaryTextTop,
            box.Width - left - GameMetrics.Row.Padding,
            GameMetrics.Row.SecondaryTextHeight);

        return new RowBlocks(icon, Label(box, left, trailing), trailing, Clip(description, box));
    }

    private static ScreenRect Trailing(ScreenRect box)
    {
        var rect = new ScreenRect(
            box.Width - GameMetrics.Row.Padding - GameMetrics.Row.TrailingWidth,
            GameMetrics.Row.TextTop,
            GameMetrics.Row.TrailingWidth,
            GameMetrics.Row.TextHeight);
        return Clip(rect, box);
    }

    private static ScreenRect Label(ScreenRect box, float left, ScreenRect trailing)
    {
        var width = trailing.IsEmpty
            ? box.Width - left - GameMetrics.Row.Padding
            : trailing.X - GameMetrics.Row.TrailingGap - left;
        return Clip(
            new ScreenRect(left, GameMetrics.Row.TextTop, width, GameMetrics.Row.TextHeight), box);
    }

    /// <summary>Trims a rectangle to its parent, and drops it entirely if there is nothing left. A
    /// row narrower than its own trailing column is not a reason to draw outside it.</summary>
    private static ScreenRect Clip(ScreenRect rect, ScreenRect box)
    {
        var x = Math.Max(rect.X, box.X);
        var y = Math.Max(rect.Y, box.Y);
        var right = Math.Min(rect.Right, box.Right);
        var bottom = Math.Min(rect.Bottom, box.Bottom);
        return right <= x || bottom <= y ? default : new ScreenRect(x, y, right - x, bottom - y);
    }
}
