namespace Wayfarer.Core.Ui;

/// <summary>What goes where inside the hub window's detail pane, as plain arithmetic.
///
/// <para><b>Why this is not inline in the node.</b> The pane used to flow its blocks downward and
/// trust that they would fit: title, status, four lines of prose, a requirements heading, three
/// requirement bullets, a source line and a provenance line, each simply advancing a cursor. At the
/// pane's own height that cursor passed the bottom edge before the requirements had started, so the
/// requirement bullets — the one part of the pane that says <i>why</i> something is locked — were
/// drawn underneath the action buttons and off the end of the window. The fix is not a bigger pane;
/// it is a layout that cannot produce a rectangle outside its own content box, and that is only
/// provable if the arithmetic can be run without a game attached.</para>
///
/// <para><b>The rule.</b> There is a fixed content box: below the rule, above the action row. Blocks
/// are allocated into it in priority order — what a locked entry needs first, prose last — and any
/// block that does not fit is dropped whole rather than clipped in half. Blocks are then placed in
/// reading order. Every rectangle this returns is inside <see cref="ContentBox"/>, and
/// <see cref="ScreenRect.IsEmpty"/> marks the ones that were dropped.</para>
///
/// <para><b>The journal's vocabulary.</b> The blocks are the Journal's own: a level on its 40x40
/// disc beside the title, a section glyph in the left gutter of each block — the open book over the
/// description, the document over the requirements, the treasure chest over the reward — and a
/// reward tray drawn at the width the game authors it. None of that changes the arithmetic; the
/// glyphs live in a gutter the text was already indented past, and the reward block occupies the
/// slot the requirements block leaves empty. See <see cref="GameMetrics.Journal"/>.</para></summary>
public static class DetailPaneLayout
{
    /// <summary>Most requirement bullets the pane will ever draw, including the "and N more" tail.
    /// The tail is what keeps a longer list honest, so this is a display budget rather than a silent
    /// truncation.</summary>
    public const int MaxRequirementLines = 3;

    /// <summary>Most lines of prose the body will ever draw.</summary>
    public const int MaxBodyLines = 4;

    /// <summary>Where the pane's content box starts: below the rule and its gap.</summary>
    public static float ContentTop => GameMetrics.Window.RuleHeight + GameMetrics.Window.RuleGap;

    /// <summary>The pane's natural height: everything a fully populated entry needs, at the measured
    /// block heights. The window gives the pane this much when it can and less when it cannot, and
    /// the layout copes with both.
    ///
    /// <para>The reward block is deliberately absent from this sum. A locked entry shows what is in
    /// the way and anything else shows what it gives you — never both — so the pane needs room for
    /// the taller of the two, which is the requirements pair. Adding the reward would have taken
    /// another 72 pixels off a list that already only shows five rows.</para></summary>
    public static float NaturalHeight =>
        GameMetrics.Window.RuleHeight
        + GameMetrics.Window.RuleGap
        + GameMetrics.Detail.TitleHeight
        + GameMetrics.Detail.HeadingHeight
        + BlockHeight(MaxBodyLines)
        + GameMetrics.Detail.HeadingHeight
        + BlockHeight(MaxRequirementLines)
        + GameMetrics.Detail.HeadingHeight
        + GameMetrics.Detail.HeadingHeight
        + GameMetrics.Window.BlockGap
        + GameMetrics.Control.ButtonHeight
        + GameMetrics.Window.BlockGap;

    /// <summary>What the reward block costs when it is drawn: the journal's own glyph-and-heading
    /// line, then the tray at the height the game authors it.</summary>
    public static float RewardBlockHeight =>
        GameMetrics.Detail.HeadingHeight + GameMetrics.Journal.TrayHeight;

    /// <summary>The rule across the pane's top edge — the same 4-pixel separator the game draws
    /// between blocks in Journal and the Duty Finder.</summary>
    public static ScreenRect Rule(float width, float height) =>
        new(0f, 0f, Math.Max(width, 0f), Math.Min(GameMetrics.Window.RuleHeight, Math.Max(height, 0f)));

    /// <summary>The action row, pinned to the pane's bottom edge. Pinned rather than flowed because
    /// a d-pad reaching for a button must not have to look for it: where the buttons are cannot
    /// depend on how much this particular entry had to say.</summary>
    public static ScreenRect ActionRow(float width, float height)
    {
        var inner = Math.Max(width - (GameMetrics.Row.Padding * 2f), 0f);
        var y = Math.Max(
            height - GameMetrics.Control.ButtonHeight - GameMetrics.Window.BlockGap,
            ContentTop);
        return new ScreenRect(GameMetrics.Row.Padding, y, inner, GameMetrics.Control.ButtonHeight);
    }

    /// <summary>The box every block has to live inside: below the rule, above the action row, inset
    /// by the row padding so the pane's text shares a left edge with the list's icon column.
    /// </summary>
    public static ScreenRect ContentBox(float width, float height)
    {
        var bottom = Math.Max(ActionRow(width, height).Y - GameMetrics.Window.BlockGap, ContentTop);
        return new ScreenRect(
            GameMetrics.Row.Padding,
            ContentTop,
            Math.Max(width - (GameMetrics.Row.Padding * 2f), 0f),
            bottom - ContentTop);
    }

    /// <summary>The height a text block of <paramref name="lines"/> lines needs. The game leads Axis
    /// 14 at 18 inside a window (JournalDetail <c>#34</c>) and reserves a little past the last line
    /// for its descenders — MonsterNoteBook <c>#43</c> is h=32 for two lines at 14.</summary>
    public static float BlockHeight(int lines) =>
        lines <= 0 ? 0f : (lines * GameMetrics.Type.BodyLine) + GameMetrics.Window.RuleGap;

    /// <summary>Lays the pane out. <paramref name="requirementLines"/> and
    /// <paramref name="bodyLines"/> are how many lines the caller actually has to draw, already
    /// capped by <see cref="MaxRequirementLines"/> and <see cref="MaxBodyLines"/>.
    ///
    /// <para><paramref name="hasLevel"/> is whether there is a level to put on the badge — hidden
    /// rather than drawn empty when there is none, because the level-less entries are a real class
    /// and not a gap. <paramref name="hasReward"/> is whether the entry knows what it grants; a
    /// locked entry's requirements outrank it either way.</para></summary>
    public static DetailPaneBlocks Compose(
        float width,
        float height,
        bool hasStatusIcon,
        bool hasLevel,
        int bodyLines,
        int requirementLines,
        bool hasReward,
        bool hasFrom,
        bool hasProvenance)
    {
        var box = ContentBox(width, height);
        var budget = Allocate(box.Height, bodyLines, requirementLines, hasReward, hasFrom, hasProvenance);
        return Place(box, width, height, hasStatusIcon, hasLevel, budget);
    }

    /// <summary>Decides what gets drawn, in priority order. A locked entry's requirements outrank its
    /// description: the description says what the thing is, the requirements say why you cannot have
    /// it, and only one of those is actionable. Prose is last because it is the only block whose
    /// absence costs the player nothing they cannot get elsewhere.
    ///
    /// <para>The reward takes the requirements' place rather than sitting beside them. When an entry
    /// is locked, "what is in the way" outranks "what you get"; when it is not, there is nothing in
    /// the way to say. One <c>if</c>, and the pane never has to hold both.</para></summary>
    private static Budget Allocate(
        float available, int bodyLines, int requirementLines, bool hasReward, bool hasFrom, bool hasProvenance)
    {
        var line = GameMetrics.Detail.HeadingHeight;
        var remaining = available;

        var title = Take(ref remaining, GameMetrics.Detail.TitleHeight);
        var status = Take(ref remaining, line);

        var wanted = Math.Clamp(requirementLines, 0, MaxRequirementLines);
        var requirementsLabel = wanted > 0 && Take(ref remaining, line);
        var requirements = 0;
        if (requirementsLabel)
        {
            requirements = Fit(remaining, wanted);
            remaining -= BlockHeight(requirements);
        }

        var reward = !requirementsLabel && hasReward && Take(ref remaining, RewardBlockHeight);

        var from = hasFrom && Take(ref remaining, line);
        var body = Fit(remaining, Math.Clamp(bodyLines, 0, MaxBodyLines));
        remaining -= BlockHeight(body);
        var provenance = hasProvenance && Take(ref remaining, line);

        return new Budget(title, status, body, requirementsLabel, requirements, reward, from, provenance);
    }

    /// <summary>Places what <see cref="Allocate"/> granted, in reading order — which is not
    /// allocation order: the description reads under the status and above the requirements even
    /// though it is the first thing to be given up.</summary>
    private static DetailPaneBlocks Place(
        ScreenRect box, float width, float height, bool hasStatusIcon, bool hasLevel, Budget budget)
    {
        var y = box.Y;
        var header = Header(ref y, box, hasStatusIcon, hasLevel, budget);
        var (badge, title, kind, statusIcon, status) = header;

        var body = Advance(ref y, box, budget.Body > 0, BlockHeight(budget.Body));
        var bodyGlyph = Glyph(box, body);
        body = Indent(body, GameMetrics.Journal.GlyphTextLeft);

        var requirementsLabel =
            Advance(ref y, box, budget.RequirementsLabel, GameMetrics.Detail.HeadingHeight);
        var requirementsGlyph = Glyph(box, requirementsLabel);
        requirementsLabel = Indent(requirementsLabel, GameMetrics.Journal.GlyphTextLeft);
        var requirements =
            Indent(
                Advance(ref y, box, budget.Requirements > 0, BlockHeight(budget.Requirements)),
                GameMetrics.Row.TextLeft);

        var rewardLabel = Advance(ref y, box, budget.Reward, GameMetrics.Detail.HeadingHeight);
        var rewardGlyph = Glyph(box, rewardLabel);
        rewardLabel = Indent(rewardLabel, GameMetrics.Journal.GlyphTextLeft);
        var tray = RewardTray(ref y, box, budget.Reward);

        var rewardIcon = JournalTrayLayout.Icon(tray);

        var from = Advance(ref y, box, budget.From, GameMetrics.Detail.HeadingHeight);
        var provenance = Advance(ref y, box, budget.Provenance, GameMetrics.Detail.HeadingHeight);

        return new DetailPaneBlocks(
            Rule(width, height),
            badge,
            title,
            kind,
            statusIcon,
            status,
            bodyGlyph,
            body,
            requirementsGlyph,
            requirementsLabel,
            requirements,
            rewardGlyph,
            rewardLabel,
            tray,
            rewardIcon,
            JournalTrayLayout.Name(tray, rewardIcon),
            from,
            provenance,
            ActionRow(width, height),
            budget.Body,
            budget.Requirements);
    }

    /// <summary>The two lines at the top of the pane and the marks beside them: the level on its
    /// disc, the title, the kind word pinned right, and the status marker with its sentence.
    ///
    /// <para>The badge spans both lines, exactly as JournalDetail's does beside its two-line title
    /// (<c>#8</c> is 40 tall against a 50-tall title block). Both lines move right past it rather
    /// than one of them being drawn over it.</para></summary>
    private static HeaderBlocks Header(
        ref float y, ScreenRect box, bool hasStatusIcon, bool hasLevel, Budget budget)
    {
        var title = Advance(ref y, box, budget.Title, GameMetrics.Detail.TitleHeight);
        var status = Advance(ref y, box, budget.Status, GameMetrics.Detail.HeadingHeight);

        var badge = LevelBadge(box, title, status, hasLevel);
        var badgeIndent = badge.IsEmpty ? 0f : GameMetrics.Journal.BadgeSize + GameMetrics.Window.RuleGap;
        title = Indent(title, badgeIndent);
        status = Indent(status, badgeIndent);

        var kind = Kind(box, title);
        title = Narrow(title, kind);

        var statusIcon = StatusIcon(status, hasStatusIcon);
        var statusIndent = statusIcon.IsEmpty
            ? 0f
            : GameMetrics.Detail.HeadingIconSize + GameMetrics.Window.RuleGap;
        status = Indent(status, statusIndent);

        return new HeaderBlocks(badge, title, kind, statusIcon, status);
    }

    /// <summary>The level's black disc, centred over the title and status lines together. Dropped
    /// whole when either line was dropped or the pane is too narrow to give up 40 pixels — a badge
    /// hanging past the bottom of a squeezed pane is the exact class of defect this file exists to
    /// make impossible.</summary>
    private static ScreenRect LevelBadge(ScreenRect box, ScreenRect title, ScreenRect status, bool present)
    {
        var size = GameMetrics.Journal.BadgeSize;
        if (!present || title.IsEmpty || status.IsEmpty || box.Width < size * 2f)
        {
            return default;
        }

        var span = status.Bottom - title.Y;
        return span < size
            ? default
            : new ScreenRect(box.X, title.Y + ((span - size) / 2f), size, size);
    }

    /// <summary>A section glyph in the block's left gutter. The game draws these 24x24 at x=0 with
    /// the heading two pixels under their right edge; the two-pixel tuck is the glyph art's own
    /// transparent margin, not an overlap.</summary>
    private static ScreenRect Glyph(ScreenRect box, ScreenRect block)
    {
        var size = GameMetrics.Journal.GlyphSize;
        return block.IsEmpty || block.Y + size > box.Bottom || box.Width < size
            ? default
            : new ScreenRect(block.X, block.Y, size, size);
    }

    /// <summary>The reward tray, indented past the glyph gutter. The tray's own arithmetic is
    /// <see cref="JournalTrayLayout"/>'s, shared with the journal page — it is the same object at a
    /// different scale, not a second one.</summary>
    private static ScreenRect RewardTray(ref float y, ScreenRect box, bool present) =>
        JournalTrayLayout.Tray(
            Advance(ref y, box, present, GameMetrics.Journal.TrayHeight),
            GameMetrics.Journal.GlyphTextLeft);

    private static ScreenRect Kind(ScreenRect box, ScreenRect title)
    {
        if (title.IsEmpty)
        {
            return default;
        }

        var kindWidth = Math.Min(GameMetrics.Detail.KindWidth, box.Width);
        return new ScreenRect(
            box.Right - kindWidth, title.Y, kindWidth, GameMetrics.Detail.HeadingHeight);
    }

    /// <summary>The title is narrowed by the caption beside it, never overlapped by it.</summary>
    private static ScreenRect Narrow(ScreenRect title, ScreenRect kind) =>
        title.IsEmpty
            ? title
            : title with
            {
                Width = Math.Max(title.Width - kind.Width - GameMetrics.Row.TrailingGap, 0f),
            };

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

    private static ScreenRect Advance(ref float y, ScreenRect box, bool present, float height)
    {
        if (!present || height <= 0f)
        {
            return default;
        }

        var rect = new ScreenRect(box.X, y, box.Width, height);
        y += height;
        return rect;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct HeaderBlocks(
        ScreenRect Badge,
        ScreenRect Title,
        ScreenRect Kind,
        ScreenRect StatusIcon,
        ScreenRect Status);

    private readonly record struct Budget(
        bool Title,
        bool Status,
        int Body,
        bool RequirementsLabel,
        int Requirements,
        bool Reward,
        bool From,
        bool Provenance);
}
