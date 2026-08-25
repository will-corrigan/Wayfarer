namespace Wayfarer.Core.Ui;

/// <summary>Where the parts of a list row go, as plain arithmetic.
///
/// <para>Every number comes from <see cref="GameMetrics.Row"/>, which is to say from Journal's and
/// the Duty Finder's own tree-list rows. The row used to derive its two line heights by halving
/// whatever height it happened to be given, which made the leading a function of the row rather than
/// of the font, and put a 14pt name in a 19-pixel box — the gap that produced is the "text spacing
/// is huge" complaint. The lines are now the game's own line boxes and the row is their sum.</para>
///
/// <para><b>Two columns, not four things in a line.</b> The game's own row has one right-hand
/// caption pinned to its right edge and a name that ends where that caption begins — Journal
/// <c>1023</c> ends its name at x=336 and starts its caption at x=348. This row does the same on
/// both of its lines: the level on the right of line one, the state on the right of line two, and
/// the name and the description each ending at a rail rather than at whatever width the window
/// happens to be. That rail is why the description no longer truncates at an arbitrary place, and
/// the separate columns are why neither caption can ever be squeezed by the other.</para>
///
/// <para><b>One left edge.</b> Every row's words start at the same x whatever the row is and
/// whatever it carries — an entry at <see cref="GameMetrics.Row.TextLeft"/> (24, the game's icon
/// column plus its gap) and a section header at <see cref="GameMetrics.Row.SectionTextLeft"/> (23,
/// one pixel under it, which is the tuck Journal's own header icon art has). The icon column is
/// reserved rather than earned: a row whose icon did not resolve keeps the column and simply draws
/// nothing in it.</para>
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
    /// flight. <paramref name="hasStatus"/> is whether line two has to carry the state in a word,
    /// which it only does when the state's own icon could not be drawn.
    /// <paramref name="portrait"/> widens the icon column from a status marker to the Hunting Log's
    /// own creature art — see <see cref="GameMetrics.Row.PortraitSize"/>.</summary>
    public static RowBlocks Compose(
        RowShape shape, float width, float height, bool hasIcon, bool hasStatus = false, bool portrait = false)
    {
        var box = new ScreenRect(0f, 0f, Math.Max(width, 0f), Math.Max(height, 0f));
        return shape switch
        {
            RowShape.Note => ComposeNote(box),
            RowShape.Section => ComposeSection(box, hasIcon),
            _ => ComposeEntry(box, hasIcon, hasStatus, portrait),
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
        return new RowBlocks(default, Clip(text, box), default, default, default);
    }

    /// <summary>A section header, centred in whatever height the list gave it.
    ///
    /// <para>The game's own header row is 28 tall and this list's rows are all 48, because a
    /// virtualizing list can only recycle on one height. Anchoring the heading's text at
    /// <see cref="GameMetrics.Row.TextTop"/> — the entry row's inset — therefore parked it against
    /// the top of a row half again as tall and left twenty-six pixels of nothing beneath it, which
    /// is what made a heading read as having no content under it. The icon was already centred; the
    /// words now are too, so the header occupies its row rather than hanging off the top of
    /// it.</para></summary>
    private static RowBlocks ComposeSection(ScreenRect box, bool hasIcon)
    {
        var iconTop = (box.Height - GameMetrics.Row.SectionIconSize) / 2f;
        var icon = hasIcon
            ? Clip(
                new ScreenRect(0f, iconTop, GameMetrics.Row.SectionIconSize, GameMetrics.Row.SectionIconSize),
                box)
            : default;

        var height = Math.Min(GameMetrics.Row.TextHeight, box.Height);
        var top = (box.Height - height) / 2f;
        var trailing = Caption(box, top, height, GameMetrics.Row.TrailingWidth);
        return new RowBlocks(
            icon,
            Label(box, GameMetrics.Row.SectionTextLeft, top, height, trailing),
            trailing,
            default,
            default);
    }

    private static RowBlocks ComposeEntry(ScreenRect box, bool hasIcon, bool hasStatus, bool portrait)
    {
        // A portrait row is the game's Hunting Log row (MonsterNoteBook 1017): a 48x48 piece of
        // creature art filling the row's height at x=6, with the words starting at x=56. A status
        // row is Journal's and the Duty Finder's: a 20x20 marker at x=2 centred over both lines,
        // words at x=24.
        var iconSize = portrait ? GameMetrics.Row.PortraitSize : GameMetrics.Row.IconSize;
        var iconLeft = portrait ? GameMetrics.Row.PortraitPadding : GameMetrics.Row.Padding;
        var iconTop = (box.Height - iconSize) / 2f;
        var icon = hasIcon
            ? Clip(new ScreenRect(iconLeft, iconTop, iconSize, iconSize), box)
            : default;

        // The icon column is reserved whether or not this row's own icon could be drawn. It is a
        // column, not a decoration: letting a row without one start twenty-two pixels to the left
        // of the row above it gives a list two left edges, and on the Unlocks tab — where the
        // locked entries are exactly the ones whose icon does not resolve — that meant most of the
        // list was indented differently from the rest of it.
        var left = portrait ? GameMetrics.Row.PortraitTextLeft : GameMetrics.Row.TextLeft;
        var trailing = Caption(
            box, GameMetrics.Row.TextTop, GameMetrics.Row.TextHeight, GameMetrics.Row.TrailingWidth);
        var status = hasStatus
            ? Caption(
                box,
                GameMetrics.Row.Height + GameMetrics.Row.SecondaryTextTop,
                GameMetrics.Row.SecondaryTextHeight,
                GameMetrics.Row.StatusWidth)
            : default;

        // The second line is the game's own dimmed sub-row, sitting directly on top of the first with
        // no invented gap — that is how the Journal stacks its own two. It ends at the same rail the
        // name above it does when there is a caption beside it, and at the row's own padding when
        // there is not.
        var descriptionRight = status.IsEmpty
            ? box.Width - GameMetrics.Row.Padding
            : status.X - GameMetrics.Row.TrailingGap;
        var description = new ScreenRect(
            left,
            GameMetrics.Row.Height + GameMetrics.Row.SecondaryTextTop,
            descriptionRight - left,
            GameMetrics.Row.SecondaryTextHeight);

        return new RowBlocks(
            icon,
            Label(box, left, GameMetrics.Row.TextTop, GameMetrics.Row.TextHeight, trailing),
            trailing,
            Clip(description, box),
            status);
    }

    /// <summary>A caption pinned to the row's right edge: the level on line one, the state on line
    /// two. Each is a fixed column the game itself uses, so neither can ever be squeezed by the
    /// other and the level — three characters — can never be the thing that ellipsises.</summary>
    private static ScreenRect Caption(ScreenRect box, float top, float height, float width) =>
        Clip(new ScreenRect(box.Width - GameMetrics.Row.Padding - width, top, width, height), box);

    private static ScreenRect Label(
        ScreenRect box, float left, float top, float height, ScreenRect trailing)
    {
        var width = trailing.IsEmpty
            ? box.Width - left - GameMetrics.Row.Padding
            : trailing.X - GameMetrics.Row.TrailingGap - left;
        return Clip(new ScreenRect(left, top, width, height), box);
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
