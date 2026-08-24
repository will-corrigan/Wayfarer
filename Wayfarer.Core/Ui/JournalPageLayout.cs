namespace Wayfarer.Core.Ui;

/// <summary>What goes where on the journal page — the full-height view of one entry that opens when
/// a row is activated — as plain arithmetic.
///
/// <para><b>Why a page as well as a strip.</b> The strip
/// (<see cref="DetailPaneLayout"/>) has to live under the list and share the window with it, so it
/// costs the list 291 pixels every frame and can only ever show what is in the way <i>or</i> what
/// you get, never both. The page replaces the list in the same rectangle, which buys three things at
/// once: the list gets its height back when the page is closed, the entry gets the banner the game
/// authors at 376x120 for exactly this slot, and requirements and rewards can be on screen together
/// because they are in different columns.</para>
///
/// <para><b>Two columns, and when there are not two.</b> The left column is the game's own authored
/// 376 — the width of the banner, the reward tray and JournalDetail's whole inner column. The right
/// column is whatever is left. When what is left is narrower than
/// <see cref="GameMetrics.Journal.MinTextColumn"/> the page stacks into one column instead of
/// drawing a gutter and calling it a column; that is the same "drop it whole rather than draw it
/// wrong" rule the strip follows.</para>
///
/// <para><b>The ladder.</b> Blocks are allocated in priority order into whichever column they belong
/// to, and any block that does not fit is dropped whole. The order is the design's: the status line
/// always, then what is in the way, then what you get, then what it is, then where to go, then the
/// confidence footnote, and the banner last — the banner is the first thing to go because it is the
/// only block that says nothing a player could not read elsewhere.</para></summary>
public static class JournalPageLayout
{
    /// <summary>Most description lines the page will ever draw. The strip's budget is four; the page
    /// has a column to itself and the catalogue's longest descriptions run to about six lines at the
    /// default width.</summary>
    public const int MaxDescriptionLines = 6;

    /// <summary>Most requirement bullets the page will ever draw, including the "and N more" tail.
    /// </summary>
    public const int MaxRequirementLines = 4;

    /// <summary>Most lines the Information section will ever draw: the giver and where, the
    /// coordinates, and the quest that grants it.</summary>
    public const int MaxInformationLines = 3;

    /// <summary>Where the page's content box starts: below the top rule, the title band and the
    /// rule under it.</summary>
    public static float ContentTop =>
        GameMetrics.Window.RuleHeight
        + GameMetrics.Window.RuleGap
        + GameMetrics.Journal.PageTitleHeight
        + GameMetrics.Window.RuleGap
        + GameMetrics.Window.RuleHeight
        + GameMetrics.Window.RuleGap;

    /// <summary>Everything a fully populated entry needs, at the game's own block heights. The page
    /// is given the whole tab body rather than this, but the number is what says whether a window
    /// the player has shrunk can still hold the whole entry.</summary>
    public static float NaturalHeight =>
        ContentTop
        + Math.Max(FullArtColumn, FullTextColumn)
        + GameMetrics.Journal.FootnoteHeight
        + GameMetrics.Window.RuleGap
        + GameMetrics.Window.RuleHeight
        + GameMetrics.Window.BlockGap
        + GameMetrics.Control.ButtonHeight
        + GameMetrics.Window.BlockGap;

    /// <summary>The art column with everything in it: the banner and its gap, then the reward's
    /// heading and tray.</summary>
    private static float FullArtColumn =>
        GameMetrics.Journal.BannerHeight
        + GameMetrics.Window.BlockGap
        + GameMetrics.Detail.HeadingHeight
        + GameMetrics.Journal.TrayHeight;

    /// <summary>The text column with everything in it: the status line, then three
    /// heading-and-body sections at their full budgets.</summary>
    private static float FullTextColumn =>
        GameMetrics.Detail.HeadingHeight
        + (GameMetrics.Detail.HeadingHeight * 3f)
        + BlockHeight(MaxRequirementLines)
        + BlockHeight(MaxDescriptionLines)
        + BlockHeight(MaxInformationLines);

    /// <summary>The rule across the page's top edge.</summary>
    public static ScreenRect Rule(float width, float height) =>
        new(0f, 0f, Math.Max(width, 0f), Math.Min(GameMetrics.Window.RuleHeight, Math.Max(height, 0f)));

    /// <summary>The band the title, its level badge and its kind caption share. Two Axis-18 lines
    /// tall, which is what JournalDetail <c>#38</c> reserves for a title that may wrap.</summary>
    public static ScreenRect TitleBand(float width, float height)
    {
        var top = GameMetrics.Window.RuleHeight + GameMetrics.Window.RuleGap;
        var available = Math.Max(Math.Max(height, 0f) - top, 0f);
        var band = Math.Min(GameMetrics.Journal.PageTitleHeight, available);
        var inner = Math.Max(width - (GameMetrics.Row.Padding * 2f), 0f);
        return band <= 0f ? default : new ScreenRect(GameMetrics.Row.Padding, top, inner, band);
    }

    /// <summary>The rule under the title — JournalDetail <c>#39</c>'s place in the page.</summary>
    public static ScreenRect TitleRule(float width, float height)
    {
        var y = GameMetrics.Window.RuleHeight
                + GameMetrics.Window.RuleGap
                + GameMetrics.Journal.PageTitleHeight
                + GameMetrics.Window.RuleGap;
        return y + GameMetrics.Window.RuleHeight > height
            ? default
            : new ScreenRect(0f, y, Math.Max(width, 0f), GameMetrics.Window.RuleHeight);
    }

    /// <summary>The action row, pinned to the page's bottom edge. Pinned for the same reason the
    /// strip's is: a d-pad reaching for Back must not have to look for it.</summary>
    public static ScreenRect ActionRow(float width, float height)
    {
        var inner = Math.Max(width - (GameMetrics.Row.Padding * 2f), 0f);
        var y = Math.Max(
            height - GameMetrics.Control.ButtonHeight - GameMetrics.Window.BlockGap,
            ContentTop);
        return new ScreenRect(GameMetrics.Row.Padding, y, inner, GameMetrics.Control.ButtonHeight);
    }

    /// <summary>The rule above the action row.</summary>
    public static ScreenRect FooterRule(float width, float height)
    {
        var y = ActionRow(width, height).Y - GameMetrics.Window.BlockGap - GameMetrics.Window.RuleHeight;
        return y < ContentTop
            ? default
            : new ScreenRect(0f, y, Math.Max(width, 0f), GameMetrics.Window.RuleHeight);
    }

    /// <summary>The box every content block has to live inside: below the title rule, above the
    /// footer rule, inset by the row padding so the page's text shares a left edge with the list's
    /// icon column.</summary>
    public static ScreenRect ContentBox(float width, float height)
    {
        var footer = FooterRule(width, height);
        var bottom = footer.IsEmpty
            ? ActionRow(width, height).Y - GameMetrics.Window.BlockGap
            : footer.Y - GameMetrics.Window.RuleGap;

        return new ScreenRect(
            GameMetrics.Row.Padding,
            ContentTop,
            Math.Max(width - (GameMetrics.Row.Padding * 2f), 0f),
            Math.Max(bottom - ContentTop, 0f));
    }

    /// <summary>Whether the page has room for the game's authored column beside a readable second
    /// one. False stacks everything into one column instead.</summary>
    public static bool IsTwoColumn(float width, float height) =>
        ContentBox(width, height).Width
        >= GameMetrics.Journal.SectionWidth + GameMetrics.Row.TrailingGap + GameMetrics.Journal.MinTextColumn;

    /// <summary>The height a text block of <paramref name="lines"/> lines needs — the same
    /// arithmetic the strip uses, because it is the same Axis 14 at the same leading.</summary>
    public static float BlockHeight(int lines) => DetailPaneLayout.BlockHeight(lines);

    /// <summary>Lays the page out. The line counts are how many lines the caller actually has to
    /// draw, already capped by the three <c>Max…Lines</c> budgets.</summary>
    public static JournalPageBlocks Compose(
        float width,
        float height,
        bool hasLevel,
        bool hasBanner,
        bool hasStatusIcon,
        int requirementLines,
        bool hasReward,
        int descriptionLines,
        int informationLines,
        bool hasProvenance)
    {
        var box = ContentBox(width, height);
        var twoColumn = IsTwoColumn(width, height);
        var budget = Allocate(
            box.Height,
            twoColumn,
            hasBanner,
            requirementLines,
            hasReward,
            descriptionLines,
            informationLines,
            hasProvenance);

        return Place(width, height, box, twoColumn, hasLevel, hasStatusIcon, budget);
    }

    /// <summary>Decides what gets drawn, in priority order, against one budget per column. The
    /// status line is unconditional; the banner is last, so a short window loses the picture before
    /// it loses a word.</summary>
    private static Budget Allocate(
        float available,
        bool twoColumn,
        bool hasBanner,
        int requirementLines,
        bool hasReward,
        int descriptionLines,
        int informationLines,
        bool hasProvenance)
    {
        // One accumulator per column, or one shared when the page has stacked into a single column
        // — in which case every block competes with every other, which is exactly right.
        var text = available;
        var art = twoColumn ? available : 0f;

        var status = Take(ref text, GameMetrics.Detail.HeadingHeight);
        var requirements = Section(ref text, requirementLines, MaxRequirementLines);
        var reward = hasReward
                     && TakeArt(ref text, ref art, twoColumn, GameMetrics.Detail.HeadingHeight + GameMetrics.Journal.TrayHeight);
        var description = Section(ref text, descriptionLines, MaxDescriptionLines);
        var information = Section(ref text, informationLines, MaxInformationLines);
        var provenance = TakeFootnote(ref text, ref art, twoColumn, hasProvenance);
        var banner = hasBanner
                     && TakeArt(ref text, ref art, twoColumn, GameMetrics.Journal.BannerHeight + GameMetrics.Window.BlockGap);

        return new Budget(
            banner,
            reward,
            status,
            requirements.Label,
            requirements.Lines,
            description.Label,
            description.Lines,
            information.Label,
            information.Lines,
            provenance);
    }

    /// <summary>One heading-and-body section, allocated out of the text column: the heading first,
    /// then as many of its lines as are left. A heading with no line under it is refused, because a
    /// section that says only its own name is worse than no section.</summary>
    private static (bool Label, int Lines) Section(ref float remaining, int wanted, int max)
    {
        var want = Math.Clamp(wanted, 0, max);
        if (want <= 0 || !Take(ref remaining, GameMetrics.Detail.HeadingHeight))
        {
            return (false, 0);
        }

        var lines = Fit(remaining, want);
        remaining -= BlockHeight(lines);
        return (true, lines);
    }

    /// <summary>Charges a block to the art column — which is the text column when the page has
    /// stacked.</summary>
    private static bool TakeArt(ref float text, ref float art, bool twoColumn, float height) =>
        twoColumn ? Take(ref art, height) : Take(ref text, height);

    /// <summary>The footnote sits under both columns, so both give up its height — which is what
    /// lets the banner allocated after it be refused once the footnote has been granted.</summary>
    private static bool TakeFootnote(ref float text, ref float art, bool twoColumn, bool wanted)
    {
        var footnote = GameMetrics.Journal.FootnoteHeight;
        if (!wanted || footnote > text || (twoColumn && footnote > art))
        {
            return false;
        }

        text -= footnote;
        if (twoColumn)
        {
            art -= footnote;
        }

        return true;
    }

    /// <summary>Places what <see cref="Allocate"/> granted, in reading order — which is not
    /// allocation order: the banner reads at the top of its column even though it is the first thing
    /// to be given up.</summary>
    private static JournalPageBlocks Place(
        float width,
        float height,
        ScreenRect box,
        bool twoColumn,
        bool hasLevel,
        bool hasStatusIcon,
        Budget budget)
    {
        var (badge, title, kind) = Header(width, height, hasLevel);

        var columnWidth = Math.Min(GameMetrics.Journal.SectionWidth, box.Width);
        var artColumn = twoColumn ? box with { Width = columnWidth } : box;
        var textLeft = box.X + columnWidth + GameMetrics.Row.TrailingGap;
        var textColumn = twoColumn
            ? new ScreenRect(textLeft, box.Y, Math.Max(box.Right - textLeft, 0f), box.Height)
            : box;

        var artY = artColumn.Y;
        var textY = textColumn.Y;

        // Stacked, the status line leads: it is the one sentence that says what state the entry is
        // in, and burying it under a picture would be the wrong answer to "what is this".
        var statusLine = Advance(ref textY, textColumn, budget.Status, GameMetrics.Detail.HeadingHeight);
        var statusIcon = StatusIcon(statusLine, hasStatusIcon);
        var statusIndent = statusIcon.IsEmpty
            ? 0f
            : GameMetrics.Detail.HeadingIconSize + GameMetrics.Window.RuleGap;

        ref var artCursor = ref (twoColumn ? ref artY : ref textY);
        var art = PlaceArt(ref artCursor, artColumn, budget);

        var requirements = PlaceSection(
            ref textY, textColumn, budget.RequirementsLabel, budget.Requirements);
        var description = PlaceSection(
            ref textY, textColumn, budget.DescriptionLabel, budget.Description);
        var information = PlaceSection(
            ref textY, textColumn, budget.InformationLabel, budget.Information);

        return new JournalPageBlocks(
            Rule(width, height),
            badge,
            title,
            kind,
            TitleRule(width, height),
            art.Banner,
            art.Glyph,
            art.Label,
            art.Tray,
            art.Icon,
            art.Name,
            statusIcon,
            Indent(statusLine, statusIndent),
            requirements.Glyph,
            requirements.Label,
            requirements.Body,
            description.Glyph,
            description.Label,
            description.Body,
            information.Glyph,
            information.Label,
            information.Body,
            Provenance(box, Math.Max(artY, textY), budget.Provenance),
            FooterRule(width, height),
            ActionRow(width, height),
            budget.Description,
            budget.Requirements,
            budget.Information);
    }

    /// <summary>The art column: the banner, then the reward's chest glyph, heading and tray.
    /// </summary>
    private static ArtBlocks PlaceArt(ref float y, ScreenRect column, Budget budget)
    {
        var banner = Banner(ref y, column, budget.Banner);

        var label = Advance(ref y, column, budget.Reward, GameMetrics.Detail.HeadingHeight);
        var glyph = Glyph(column, label);
        var tray = JournalTrayLayout.Tray(
            Advance(ref y, column, budget.Reward, GameMetrics.Journal.TrayHeight),
            GameMetrics.Journal.SectionInset);
        var icon = JournalTrayLayout.Icon(tray);

        return new ArtBlocks(
            banner,
            glyph,
            Indent(label, GameMetrics.Journal.GlyphTextLeft),
            tray,
            icon,
            JournalTrayLayout.Name(tray, icon));
    }

    /// <summary>One heading-and-body section: the glyph in the gutter, the heading past it, and the
    /// body pulled back to the list's own text column.</summary>
    private static SectionBlocks PlaceSection(
        ref float y, ScreenRect column, bool hasLabel, int lines)
    {
        var label = Advance(ref y, column, hasLabel, GameMetrics.Detail.HeadingHeight);
        var glyph = Glyph(column, label);
        var body = Indent(
            Advance(ref y, column, lines > 0, BlockHeight(lines)),
            GameMetrics.Row.TextLeft);

        return new SectionBlocks(glyph, Indent(label, GameMetrics.Journal.GlyphTextLeft), body);
    }

    /// <summary>The title band's three parts: the level on its disc at the left edge, the title
    /// beside it, and the kind word pinned right.</summary>
    private static (ScreenRect Badge, ScreenRect Title, ScreenRect Kind) Header(
        float width, float height, bool hasLevel)
    {
        var band = TitleBand(width, height);
        if (band.IsEmpty)
        {
            return default;
        }

        var size = GameMetrics.Journal.BadgeSize;
        var badge = !hasLevel || band.Height < size || band.Width < size * 2f
            ? default
            : new ScreenRect(band.X, band.Y + ((band.Height - size) / 2f), size, size);

        var indent = badge.IsEmpty ? 0f : size + GameMetrics.Window.RuleGap;
        var title = Indent(band, indent);

        var kindWidth = Math.Min(GameMetrics.Journal.KindWidth, band.Width);
        var kind = new ScreenRect(
            band.Right - kindWidth,
            band.Y,
            kindWidth,
            Math.Min(GameMetrics.Detail.HeadingHeight, band.Height));

        title = title with
        {
            Width = Math.Max(title.Width - kindWidth - GameMetrics.Row.TrailingGap, 0f),
        };

        return (badge, title, kind);
    }

    /// <summary>The banner, at the size the game authors it. Like the tray, the art is a plain image
    /// the game never stretches, so a column too narrow to hold 376 shows less of it rather than a
    /// distorted one.</summary>
    private static ScreenRect Banner(ref float y, ScreenRect column, bool present)
    {
        var block = Advance(ref y, column, present, GameMetrics.Journal.BannerHeight);
        if (block.IsEmpty)
        {
            return default;
        }

        // The gap under the banner is charged in Allocate; the cursor pays it here so the heading
        // below does not sit against the artwork.
        y += GameMetrics.Window.BlockGap;

        // Inset like the section's other art. JournalCanvas #4 is at x=18 in a 394-wide section,
        // which is the same x the reward tray sits at — the two line up, and that alignment is what
        // makes the column read as one object rather than two stacked pictures.
        var inset = GameMetrics.Journal.SectionInset;
        var bannerWidth = Math.Min(GameMetrics.Journal.BannerWidth, block.Width - inset);
        return bannerWidth <= 0f
            ? default
            : block with { X = block.X + inset, Width = bannerWidth };
    }

    /// <summary>The confidence footnote, under whichever column ran longer, across the full box.
    /// </summary>
    private static ScreenRect Provenance(ScreenRect box, float bottom, bool present)
    {
        var height = GameMetrics.Journal.FootnoteHeight;
        return !present || bottom + height > box.Bottom
            ? default
            : new ScreenRect(box.X, bottom, box.Width, height);
    }

    private static ScreenRect StatusIcon(ScreenRect status, bool present)
    {
        if (!present || status.IsEmpty)
        {
            return default;
        }

        var inset = (GameMetrics.Detail.HeadingHeight - GameMetrics.Detail.HeadingIconSize) / 2f;
        return new ScreenRect(
            status.X,
            status.Y + inset,
            GameMetrics.Detail.HeadingIconSize,
            GameMetrics.Detail.HeadingIconSize);
    }

    /// <summary>A section glyph in the block's left gutter, 24x24 at x=0 with the heading two pixels
    /// under its right edge — the game's own arrangement, and the two pixels are the glyph art's own
    /// transparent margin rather than an overlap.</summary>
    private static ScreenRect Glyph(ScreenRect column, ScreenRect block)
    {
        var size = GameMetrics.Journal.GlyphSize;
        return block.IsEmpty || block.Y + size > column.Bottom || column.Width < size
            ? default
            : new ScreenRect(block.X, block.Y, size, size);
    }

    private static ScreenRect Indent(ScreenRect rect, float by) =>
        rect.IsEmpty || by <= 0f
            ? rect
            : new ScreenRect(rect.X + by, rect.Y, Math.Max(rect.Width - by, 0f), rect.Height);

    /// <summary>How many of <paramref name="wanted"/> lines fit in <paramref name="remaining"/>
    /// space. Whole lines only — half a line of text is worse than none, because the player cannot
    /// tell it was cut.</summary>
    private static int Fit(float remaining, int wanted)
    {
        for (var lines = wanted; lines > 0; lines--)
        {
            if (BlockHeight(lines) <= remaining)
            {
                return lines;
            }
        }

        return 0;
    }

    private static bool Take(ref float remaining, float height)
    {
        if (height > remaining)
        {
            return false;
        }

        remaining -= height;
        return true;
    }

    private static ScreenRect Advance(ref float y, ScreenRect column, bool present, float height)
    {
        if (!present || height <= 0f || column.Width <= 0f || y + height > column.Bottom)
        {
            return default;
        }

        var rect = new ScreenRect(column.X, y, column.Width, height);
        y += height;
        return rect;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct ArtBlocks(
        ScreenRect Banner,
        ScreenRect Glyph,
        ScreenRect Label,
        ScreenRect Tray,
        ScreenRect Icon,
        ScreenRect Name);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct SectionBlocks(ScreenRect Glyph, ScreenRect Label, ScreenRect Body);

    private readonly record struct Budget(
        bool Banner,
        bool Reward,
        bool Status,
        bool RequirementsLabel,
        int Requirements,
        bool DescriptionLabel,
        int Description,
        bool InformationLabel,
        int Information,
        bool Provenance);
}
