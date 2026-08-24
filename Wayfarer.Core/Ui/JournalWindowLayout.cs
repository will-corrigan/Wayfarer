namespace Wayfarer.Core.Ui;

/// <summary>What goes where inside the journal window, as plain arithmetic.
///
/// <para><b>Why this replaced the page drawn inside the hub window.</b> The page had to fit whatever
/// width the player had dragged the hub to, so it could not wear the border, it had to decide between
/// one column and two, and it flowed prose into rectangles sized by a line <i>count</i> rather than
/// by what the text actually measured — which is how a description came to be drawn on top of a
/// requirement list. This window is <see cref="GameMetrics.JournalFrame.Width"/> wide, always, which
/// is the width every number on this surface was authored for. One column, no decisions, and the
/// text heights are measured by the caller and passed in rather than guessed.</para>
///
/// <para><b>The ladder.</b> Blocks are allocated in priority order and any block that does not fit
/// is dropped whole rather than clipped. The order is the design's: the state of the entry always,
/// then what is in the way, then what you get, then what it is — and the banner last, because it is
/// the only block that says nothing a player could not read in words. The giver and the confidence
/// footnote are not flowed at all: they are anchored to the foot of the content box, which is where
/// the game's own journal puts them and where the player's screenshot shows the giver.</para>
///
/// <para><b>Nothing may overlap.</b> Every block below is either flowed down a single cursor or
/// anchored to a box edge, and the two are kept apart by taking the anchored band out of the flow's
/// budget before the flow starts. That is the whole mechanism, and
/// <c>LayoutContainmentTests</c> asserts it at every height with hostile content.</para></summary>
public static class JournalWindowLayout
{
    /// <summary>Most lines of prose the description will ever be given. Six Axis-14 lines at leading
    /// 18 is 108 pixels, which is what JournalCanvas <c>#8</c>'s section grows to on a long quest
    /// summary.</summary>
    public const int MaxDescriptionLines = 6;

    /// <summary>Most lines the requirements block will ever be given, including the "and N more"
    /// tail. Six rather than the strip's four because this is the block the player is reading when
    /// the entry is locked, and the catalogue's worst case — a job gate naming thirty classes — is a
    /// single sentence that wraps to five lines at this column's width.</summary>
    public const int MaxRequirementLines = 6;

    /// <summary>Most lines the state line will ever take. It says one thing now — which state the
    /// entry is in — because the reason it is in that state belongs to the requirements block and
    /// saying it twice is what the player's screenshot showed.</summary>
    public const int MaxStatusLines = 1;

    /// <summary>The frame height a fully populated entry wants. What the window asks for before the
    /// viewport has its say.</summary>
    public static float NaturalHeight =>
        GameMetrics.JournalFrame.BodyTop
        + BlockHeight(MaxStatusLines)
        + GameMetrics.Journal.BannerHeight + GameMetrics.Window.BlockGap
        + GameMetrics.Journal.SectionHeadingHeight + GameMetrics.Journal.TrayHeight
        + GameMetrics.Journal.SectionHeadingHeight + BlockHeight(MaxDescriptionLines)
        + GameMetrics.Journal.SectionHeadingHeight + BlockHeight(MaxRequirementLines)
        + GameMetrics.Row.TextHeight
        + GameMetrics.Journal.FootnoteHeight
        + GameMetrics.Window.RuleGap
        + GameMetrics.JournalFrame.FooterRuleBottomInset;

    /// <summary>The column everything is drawn in: the game's own authored 394, centred.</summary>
    public static ScreenRect Column(float height)
    {
        var top = GameMetrics.JournalFrame.TitleTop;
        var h = Math.Max(height, 0f) - top;
        return h <= 0f
            ? default
            : new ScreenRect(GameMetrics.JournalFrame.ColumnLeft, top, GameMetrics.Journal.SectionWidth, h);
    }

    /// <summary>The band the title, its level badge and its kind caption share — two Axis-18 lines,
    /// which is what JournalDetail <c>#38</c> reserves for a title that may wrap.</summary>
    public static ScreenRect TitleBand(float height)
    {
        var column = Column(height);
        var band = Math.Min(GameMetrics.Journal.PageTitleHeight, column.Height);
        return column.IsEmpty || band <= 0f ? default : column with { Height = band };
    }

    /// <summary>The rule under the title.</summary>
    public static ScreenRect TitleRule(float height)
    {
        var column = Column(height);
        var y = GameMetrics.JournalFrame.TitleRuleTop;
        return column.IsEmpty || y + GameMetrics.Window.RuleHeight > height
            ? default
            : new ScreenRect(column.X, y, column.Width, GameMetrics.Window.RuleHeight);
    }

    /// <summary>The button row along the bottom edge, less the gold rivet at its right end.
    /// </summary>
    public static ScreenRect ActionRow(float height)
    {
        var column = Column(height);
        var y = height - GameMetrics.JournalFrame.ButtonBottomInset - GameMetrics.Control.ButtonHeight;
        var width = column.Width - GameMetrics.JournalFrame.BossSize - GameMetrics.Control.ButtonGap;
        return column.IsEmpty || y < GameMetrics.JournalFrame.BodyTop || width <= 0f
            ? default
            : new ScreenRect(column.X, y, width, GameMetrics.Control.ButtonHeight);
    }

    /// <summary>The gold rivet beside the row — JournalDetail <c>#53</c>'s slot, worn as ornament
    /// rather than as a control. See <see cref="GameMetrics.JournalFrame.BossSize"/>.</summary>
    public static ScreenRect Boss(float height)
    {
        var row = ActionRow(height);
        if (row.IsEmpty)
        {
            return default;
        }

        var size = GameMetrics.JournalFrame.BossSize;
        var column = Column(height);
        return new ScreenRect(
            column.Right - size,
            row.Y + ((row.Height - size) / 2f),
            size,
            size);
    }

    /// <summary>The rule above the button row.</summary>
    public static ScreenRect FooterRule(float height)
    {
        var column = Column(height);
        var y = height - GameMetrics.JournalFrame.FooterRuleBottomInset;
        return column.IsEmpty || y < GameMetrics.JournalFrame.BodyTop
            ? default
            : new ScreenRect(column.X, y, column.Width, GameMetrics.Window.RuleHeight);
    }

    /// <summary>The box every flowed block has to live inside: below the title rule, above the
    /// footer rule.</summary>
    public static ScreenRect ContentBox(float height)
    {
        var column = Column(height);
        if (column.IsEmpty)
        {
            return default;
        }

        var top = GameMetrics.JournalFrame.BodyTop;
        var footer = FooterRule(height);
        var bottom = footer.IsEmpty
            ? Math.Max(height - GameMetrics.JournalFrame.FooterRuleBottomInset, top)
            : footer.Y - GameMetrics.Window.RuleGap;

        return new ScreenRect(column.X, top, column.Width, Math.Max(bottom - top, 0f));
    }

    /// <summary>The height a block of <paramref name="lines"/> Axis-14 lines needs — the same
    /// arithmetic the strip and the page use, because it is the same face at the same leading.
    /// </summary>
    public static float BlockHeight(int lines) => DetailPaneLayout.BlockHeight(lines);

    /// <summary>How much room a wrapping text block may be given, so the caller can fit its string
    /// to a measured height before the layout runs. Deliberately generous — the layout below is what
    /// actually decides, and it never grants more than is left.</summary>
    public static float TextAllowance(float height, int maxLines) =>
        Math.Min(BlockHeight(maxLines), Math.Max(ContentBox(height).Height, 0f));

    /// <summary>Lays the window out. <paramref name="descriptionHeight"/> and
    /// <paramref name="requirementsHeight"/> are the heights the caller's text actually
    /// <b>measured</b> at this column's width — not a line count — which is the whole difference
    /// between this and the page it replaces.</summary>
    public static JournalWindowBlocks Compose(
        float height,
        bool hasLevel,
        bool hasStatusIcon,
        bool hasBanner,
        bool hasReward,
        float requirementsHeight,
        float descriptionHeight,
        bool hasGiver,
        bool hasProvenance)
    {
        var box = ContentBox(height);
        var (giver, provenance, flow) = Foot(box, hasGiver, hasProvenance);
        var (badge, title, kind) = Header(height, hasLevel);

        var y = flow.Y;
        var status = Advance(ref y, flow, true, BlockHeight(MaxStatusLines));
        var statusIcon = StatusIcon(status, hasStatusIcon);
        var statusText = Indent(
            status,
            statusIcon.IsEmpty ? 0f : GameMetrics.Detail.HeadingIconSize + GameMetrics.Window.RuleGap);

        // In allocation order, not reading order: the banner is placed last so that a short window
        // loses the picture rather than a word, but it is drawn at the top of the flow. The cursor
        // therefore reserves its band up front and the sections below start under it.
        var bannerBand = Reserve(ref y, flow, hasBanner, GameMetrics.Journal.BannerHeight + GameMetrics.Window.BlockGap);

        var requirements = Section(ref y, flow, requirementsHeight);
        var reward = Reward(ref y, flow, hasReward);
        var description = Section(ref y, flow, descriptionHeight);

        return new JournalWindowBlocks(
            badge,
            title,
            kind,
            TitleRule(height),
            statusIcon,
            statusText,
            Banner(bannerBand),
            reward.Glyph,
            reward.Label,
            reward.Tray,
            reward.Icon,
            reward.Name,
            description.Glyph,
            description.Label,
            description.Body,
            requirements.Glyph,
            requirements.Label,
            requirements.Body,
            giver,
            provenance,
            FooterRule(height),
            ActionRow(height),
            Boss(height));
    }

    /// <summary>The two blocks anchored to the foot of the content box, and what is left over for
    /// the flow above them. Taking them out of the budget before anything flows is what makes
    /// "nothing overlaps" a property of the arithmetic rather than of the content.</summary>
    private static (ScreenRect Giver, ScreenRect Provenance, ScreenRect Flow) Foot(
        ScreenRect box, bool hasGiver, bool hasProvenance)
    {
        var flow = box;
        var provenance = default(ScreenRect);
        var giver = default(ScreenRect);

        if (hasProvenance && flow.Height >= GameMetrics.Journal.FootnoteHeight)
        {
            var h = GameMetrics.Journal.FootnoteHeight;
            provenance = new ScreenRect(flow.X, flow.Bottom - h, flow.Width, h);
            flow = flow with { Height = flow.Height - h };
        }

        if (hasGiver && flow.Height >= GameMetrics.Row.TextHeight)
        {
            var h = GameMetrics.Row.TextHeight;
            giver = new ScreenRect(flow.X, flow.Bottom - h, flow.Width, h);
            flow = flow with { Height = flow.Height - h };
        }

        return (giver, provenance, flow);
    }

    /// <summary>The title band's three parts: the level on its disc at the column's left edge, the
    /// title beside it, and the kind word pinned right.</summary>
    private static (ScreenRect Badge, ScreenRect Title, ScreenRect Kind) Header(float height, bool hasLevel)
    {
        var band = TitleBand(height);
        if (band.IsEmpty)
        {
            return default;
        }

        var size = GameMetrics.Journal.BadgeSize;
        var badge = !hasLevel || band.Height < size || band.Width < size * 2f
            ? default
            : new ScreenRect(band.X, band.Y + ((band.Height - size) / 2f), size, size);

        var indent = badge.IsEmpty ? 0f : size + GameMetrics.Window.RuleGap;
        var kindWidth = Math.Min(GameMetrics.Journal.KindWidth, band.Width);
        var kind = new ScreenRect(
            band.Right - kindWidth,
            band.Y,
            kindWidth,
            Math.Min(GameMetrics.Detail.HeadingHeight, band.Height));

        var title = Indent(band, indent);
        title = title with
        {
            Width = Math.Max(title.Width - kindWidth - GameMetrics.Row.TrailingGap, 0f),
        };

        return (badge, title, kind);
    }

    /// <summary>One heading-and-body section: the glyph in the column's gutter, the heading past it,
    /// and the body pulled in to the same inset the tray and the banner sit at, so the column reads
    /// as one object.</summary>
    private static (ScreenRect Glyph, ScreenRect Label, ScreenRect Body) Section(
        ref float y, ScreenRect column, float bodyHeight)
    {
        if (bodyHeight <= 0f)
        {
            return default;
        }

        var label = Advance(ref y, column, true, GameMetrics.Journal.SectionHeadingHeight);
        if (label.IsEmpty)
        {
            return default;
        }

        var body = Indent(
            Advance(ref y, column, true, Math.Min(bodyHeight, Math.Max(column.Bottom - y, 0f))),
            GameMetrics.Journal.SectionInset);

        // A heading with no line under it is refused whole: a section that says only its own name is
        // worse than no section.
        if (body.IsEmpty)
        {
            y = label.Y;
            return default;
        }

        return (Glyph(column, label), Indent(label, GameMetrics.Journal.GlyphTextLeft), body);
    }

    /// <summary>The reward section: the chest glyph, the heading, and the tray with one slot's icon
    /// and the reward said in words beside it.</summary>
    private static (ScreenRect Glyph, ScreenRect Label, ScreenRect Tray, ScreenRect Icon, ScreenRect Name) Reward(
        ref float y, ScreenRect column, bool present)
    {
        if (!present)
        {
            return default;
        }

        var needed = GameMetrics.Journal.SectionHeadingHeight + GameMetrics.Journal.TrayHeight;
        if (y + needed > column.Bottom)
        {
            return default;
        }

        var label = Advance(ref y, column, true, GameMetrics.Journal.SectionHeadingHeight);
        var tray = JournalTrayLayout.Tray(
            Advance(ref y, column, true, GameMetrics.Journal.TrayHeight), GameMetrics.Journal.SectionInset);
        var icon = JournalTrayLayout.Icon(tray);

        return (
            Glyph(column, label),
            Indent(label, GameMetrics.Journal.GlyphTextLeft),
            tray,
            icon,
            JournalTrayLayout.Name(tray, icon));
    }

    /// <summary>The banner inside the band reserved for it, at the size the game authors it. The art
    /// is a plain image the game never stretches, so a column too narrow shows less of it rather
    /// than a distorted one — which cannot happen at this window's fixed width, and is kept because
    /// a resize is not atomic.</summary>
    private static ScreenRect Banner(ScreenRect band)
    {
        if (band.IsEmpty)
        {
            return default;
        }

        var inset = GameMetrics.Journal.SectionInset;
        var width = Math.Min(GameMetrics.Journal.BannerWidth, band.Width - inset);
        return width <= 0f
            ? default
            : new ScreenRect(band.X + inset, band.Y, width, GameMetrics.Journal.BannerHeight);
    }

    private static ScreenRect StatusIcon(ScreenRect status, bool present)
    {
        if (!present || status.IsEmpty)
        {
            return default;
        }

        var size = GameMetrics.Detail.HeadingIconSize;
        return new ScreenRect(status.X, status.Y + ((status.Height - size) / 2f), size, size);
    }

    /// <summary>A section glyph in the column's left gutter, 24x24 with the heading two pixels under
    /// its right edge — the game's own arrangement, and the two pixels are the glyph art's own
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

    /// <summary>Reserves a band at the cursor and moves past it, returning the band. Used for the
    /// banner, whose place in the flow is decided here and whose art is placed inside it.</summary>
    private static ScreenRect Reserve(ref float y, ScreenRect column, bool present, float height) =>
        Advance(ref y, column, present, height);

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
}
