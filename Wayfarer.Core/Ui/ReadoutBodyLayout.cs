namespace Wayfarer.Core.Ui;

/// <summary>The readout's geometry: what each of its sections is worth in height, and the one rule by
/// which they are stacked.
///
/// <para><b>What this replaced.</b> The readout used to walk a cursor down itself — <c>y += height</c>
/// after each line, <c>y += BaseGap * 2</c> before each rule — with the height coming from a
/// measurement of the text that had just been written. A wrapped string's height depends on the font,
/// the column width and where the words break, so a measurement taken before the node had its real
/// width, or against the string it drew last frame, moved every line below it. That is the whole of
/// "text on top of text", and it is also the whole of "everything looks shifted when the arrow is
/// absent": the arrow's words fallback was the first thing in the cursor, so its presence changed the
/// origin of the entire readout.</para>
///
/// <para><b>The rule that replaced it.</b> <see cref="Flow"/> walks a list of section heights once and
/// places each section after the one before it. Nothing measures anything on another section's
/// behalf, so there is no measurement to get wrong; a section that comes back too tall runs into the
/// space below the readout rather than into its neighbour, and a section of no height takes no room at
/// all. That is exactly what <c>VerticalListNode</c> with <c>FitContents</c> does at runtime, which is
/// what the readout actually uses — <see cref="Flow"/> is the same rule in a form that can be
/// asserted and drawn with no client attached.</para>
///
/// <para><b>Every number here is the game's.</b> <see cref="GameMetrics.Banner"/> read out of
/// <c>ScenarioTree.uld</c>, <see cref="GameMetrics.Hud"/> out of <c>ToDoList</c>. This class arranges
/// them; it does not choose any of them, and the arrangement is the same one the absolute placement
/// produced — the readout's visible metrics did not change when it started flowing.</para></summary>
public static class ReadoutBodyLayout
{
    /// <summary>The most rows one line is allowed to wrap into. A line that wants five rows is a
    /// content problem, and letting it run would trade a clipped line for the rest of the readout.
    /// </summary>
    public const float MaxWrappedLines = 3f;

    /// <summary>The readout's spacing unit — half of what the quest tracker leaves either side of its
    /// icon column, whose gutter is 28 for a 24-wide marker.</summary>
    public static float BaseGap => (GameMetrics.Hud.Gutter - GameMetrics.Hud.IconSize) / 2f;

    /// <summary>The readout box, before scale: the banner's own plate at the size the game draws it,
    /// plus the margin its emblem hangs into.</summary>
    public static float BaseWidth => GameMetrics.Banner.Width;

    /// <summary>The gap between two sections in the stack, and it is nothing.
    ///
    /// <para><b>Deliberately zero, which is the point.</b> Every gap the readout has is <i>inside</i>
    /// a section — the rule's breathing room is part of the rule's own section, the trailing margin is
    /// its own section — so a section's height is the whole of what it costs. A container spacing
    /// would be a second source of vertical distance, and the readout would then have two answers to
    /// where a line ends.</para></summary>
    public static float Spacing => 0f;

    /// <inheritdoc cref="BaseGap"/>
    public static float Gap(float factor) => BaseGap * factor;

    /// <summary>The readout's width at this scale.</summary>
    public static float Width(float factor) => BaseWidth * factor;

    /// <summary>The banner section's height: the pill's rise above the plate plus the plate itself.
    /// It does not depend on its content — the name is cut short rather than wrapped — which is what
    /// makes the lines beneath it the only things on the readout that move.</summary>
    public static float BannerHeight(float factor) => GameMetrics.Banner.Height * factor;

    /// <summary>The height of the section that says the direction in words, which is on screen only
    /// when the arrow could not be generated at all. One line of the headline face at the tracker's
    /// own leading, plus the gap under it.
    ///
    /// <para>Zero when there are no words, and that is the fix for "everything looks shifted when the
    /// arrow is absent": a section of no height takes no room, so the banner below it starts in
    /// exactly the same place either way.</para></summary>
    public static float WordsHeight(bool present, float factor) =>
        present ? WordsLineHeight(factor) + Gap(factor) : 0f;

    /// <summary>The words line itself, inside its section.</summary>
    public static float WordsLineHeight(float factor) =>
        (GameMetrics.Banner.HeadlineSize + GameMetrics.Hud.LineLeading - GameMetrics.Hud.LineSize) * factor;

    /// <summary>The trailing margin under the last line — its own section, so the readout's height is
    /// still nothing but the sum of its sections.</summary>
    public static float FootHeight(float factor) => Gap(factor);

    /// <summary>How much of a line's section is spent on the rule above it: the gap that separates the
    /// rule from the line before, the rule, and the gap under it. Nothing at all when the line is not
    /// separated.</summary>
    public static float RuleAdvance(bool separated, float factor) =>
        separated ? (Gap(factor) * 3f) + GameMetrics.Window.RuleHeight : 0f;

    /// <summary>Where the rule sits inside its line's section.</summary>
    public static float RuleTop(float factor) => Gap(factor) * 2f;

    /// <summary>The subordinate face at this scale, floored where the engine stops rendering.
    /// </summary>
    public static float SubLineFontSize(float factor) =>
        Math.Max(GameMetrics.Banner.SubLineSize * factor, 8f);

    /// <summary>One number for both a wrapped line's row pitch and the advance to the line after it,
    /// so a wrapped line's second row and its neighbour cannot disagree about where they are. The
    /// banner leads its subordinate lines two over the face, exactly as the tracker does.</summary>
    public static float SubLineStep(float factor) =>
        Math.Max(
            SubLineFontSize(factor) + (GameMetrics.Banner.SubLineLeading - GameMetrics.Banner.SubLineSize),
            11f);

    /// <summary>The block one unwrapped line occupies.
    ///
    /// <para><b>Two heights, and which one a line gets depends on its gutter.</b> A line with
    /// something in the gutter beside it — its medallion, or the bearing arrow — is the quest
    /// tracker's own icon-bearing line: <c>ToDoList 1008</c> is h=22 around a 24x24 icon
    /// (<see cref="GameMetrics.Banner.SubLinePitch"/>). A line with an empty gutter is a bare row of
    /// text and gets the tracker's line spacing for its size, 14
    /// (<see cref="GameMetrics.Banner.AnnotationBlock"/>). Giving every line the icon height is what
    /// made the readout read as too spread out; giving every line the text height leaves the arrow
    /// hanging out of the bottom of the readout.</para></summary>
    public static float SubLineBlock(bool gutter, float factor) =>
        (gutter ? GameMetrics.Banner.SubLinePitch : GameMetrics.Banner.AnnotationBlock) * factor;

    /// <summary>The whole of a line's section: the rule above it, then the taller of its block and the
    /// rows its text actually took. A line that wraps grows past its block, because a clipped line is
    /// worse than a loose one.
    ///
    /// <para><paramref name="gutter"/> is whether this line has a mark beside it. The medallion is on
    /// the line's own <see cref="ReadoutBlock.Marked"/>; the arrow is not, because which line carries
    /// the arrow is a property of the whole readout rather than of any line — it always takes the
    /// first — so the caller passes it in. Both callers are
    /// <see cref="Compose"/> and the live node, and they must pass the same thing or the node's
    /// sections and this arithmetic disagree.</para></summary>
    public static float LineHeight(ReadoutBlock block, float factor, bool gutter = false) =>
        RuleAdvance(block.Separated, factor) + TextHeight(block, factor, gutter);

    /// <inheritdoc cref="LineHeight"/>
    public static float TextHeight(ReadoutBlock block, float factor, bool gutter = false) =>
        Math.Max(SubLineBlock(block.Marked || gutter, factor), SubLineStep(factor) * Rows(block.Rows));

    /// <summary>Where a line's words start inside its section: centred in the block for a single row,
    /// flush with the top of the block once it wraps — a wrapped line has to start where its block
    /// does or its extra rows push into the line beneath.</summary>
    public static float TextTop(ReadoutBlock block, float factor, bool gutter = false)
    {
        var top = RuleAdvance(block.Separated, factor);
        if (Rows(block.Rows) > 1f)
        {
            return top;
        }

        var slack = SubLineBlock(block.Marked || gutter, factor) - SubLineStep(factor);
        return top + Math.Max(slack / 2f, 0f);
    }

    /// <summary>Whether line <paramref name="index"/> is the one with the gutter column beside it. The
    /// arrow always takes the first subordinate line — the objective — so that is the line that has to
    /// be tall enough to hold a mark.
    ///
    /// <para><b>It does not ask whether the arrow is actually there.</b> The column is reserved, not
    /// earned: an arrow that added height when it appeared would move every line under it every time
    /// the bearing became unavailable, which is the "everything looks shifted when the arrow is
    /// absent" complaint. The first line is the tracker's icon-bearing height always, and the arrow
    /// costs nothing vertically — which is what
    /// <c>Taking_the_arrow_away_moves_nothing_else_on_the_readout</c> proves.</para></summary>
    public static bool GutterLine(int index) => index == 0;

    /// <summary>The left edge of the plate's own text, and of the name written across it.</summary>
    public static float HeadlineLeft(float factor) => GameMetrics.Banner.HeadlineLeft * factor;

    /// <summary>The left edge of every subordinate line — the plate's text column, indented by the
    /// nine pixels the game indents a job-quest row by.</summary>
    public static float SubLineLeft(float factor) => GameMetrics.Banner.SubLineLeft * factor;

    /// <summary>The left edge of the marker gutter: the column the game hangs its "!" medallions in,
    /// and the column the arrow shares with them so the readout has one left gutter rather than
    /// two.</summary>
    public static float GutterLeft(float factor) => GameMetrics.Banner.MarkerLeft * factor;

    /// <summary>How wide the gutter is — the medallion's own width, which is what the arrow is centred
    /// in whatever the player's arrow-size setting is.</summary>
    public static float GutterWidth(float factor) => GameMetrics.Banner.MarkerSize * factor;

    /// <summary>The room a subordinate line's words have. Never less than a hair, so a readout caught
    /// mid-resize has a column rather than a negative one.</summary>
    public static float SubLineWidth(float factor) =>
        Math.Max(Width(factor) - SubLineLeft(factor) - (GameMetrics.Banner.HeadlineRight * factor), factor);

    /// <inheritdoc cref="SubLineWidth"/>
    public static float HeadlineWidth(float factor) =>
        Math.Max(Width(factor) - HeadlineLeft(factor) - (GameMetrics.Banner.HeadlineRight * factor), factor);

    /// <summary>Places a stack of sections, taking each section's height from the section itself.
    ///
    /// <para><b>This is the whole of the vertical layout.</b> The walk itself — the cursor starting
    /// at the box's top and advancing by whatever the section it just placed says it is — is shared
    /// with the journal window's own stack; see <see cref="FlowLayout"/> for the rule and why it lives
    /// there once. The readout's own contribution is the spacing between sections, which is
    /// <see cref="Spacing"/> and is nothing.</para></summary>
    public static IReadOnlyList<ScreenRect> Flow(IReadOnlyList<float> heights, ScreenRect box) =>
        FlowLayout.Flow(heights, Spacing, box);

    /// <summary>How tall that stack comes out — the height a container with <c>FitContents</c> takes
    /// on. The same walk as <see cref="Flow"/>, and it has to be, so the readout's height and its
    /// contents cannot disagree.</summary>
    public static float FlowHeight(IReadOnlyList<float> heights) => FlowLayout.FlowHeight(heights, Spacing);

    /// <summary>Arranges the whole readout at one scale, for one set of lines — the same arrangement
    /// the live node produces, from the same helpers, so a proof about this is a proof about what is
    /// on screen.</summary>
    public static ReadoutBodyBlocks Compose(ReadoutBodyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var factor = Math.Max(request.Factor, 0f);
        var width = Width(factor);
        var lines = request.Lines;

        var heights = new float[lines.Count + 3];
        heights[0] = WordsHeight(request.DirectionInWords, factor);
        heights[1] = BannerHeight(factor);
        for (var i = 0; i < lines.Count; i++)
        {
            heights[i + 2] = LineHeight(lines[i], factor, GutterLine(i));
        }

        heights[^1] = FootHeight(factor);

        var placed = Flow(heights, new ScreenRect(0f, 0f, width, FlowHeight(heights)));
        var banner = placed[1];
        var lineParts = ComposeLines(request, factor, placed);
        var arrowCentre = lineParts.ArrowCentre;

        // No subordinate lines at all and still something to point at: the arrow parks where the first
        // one would have been, rather than climbing onto the plate and colliding with the emblem
        // already in that column.
        if (request.Arrow && arrowCentre is null)
        {
            arrowCentre = placed[^1].Y + (GameMetrics.Banner.SubLinePitch * factor / 2f);
        }

        return new ReadoutBodyBlocks
        {
            Height = FlowHeight(heights),
            Words = placed[0].IsEmpty
                ? default
                : new ScreenRect(0f, placed[0].Y, width, WordsLineHeight(factor)),
            Banner = banner,
            Plate = request.Banner ? Plate(banner, factor, width) : default,
            Strip = request.Banner ? Strip(banner, factor, width) : default,
            Crest = request.Banner ? Crest(banner, factor) : default,
            Headline = Headline(banner, factor),
            Cog = request.Cog ? Cog(banner, factor, width) : default,
            Switcher = request.Banner && request.Switcher ? Switcher(banner, factor, width) : default,
            Arrow = arrowCentre is { } centre ? Arrow(centre, factor, request.ArrowScale) : default,
            Sections = lineParts.Sections,
            Rules = lineParts.Rules,
            Markers = lineParts.Markers,
            Texts = lineParts.Texts,
        };
    }

    /// <summary>Fills in what sits inside each already-placed line section — its rule, its words and
    /// either its medallion or the arrow — and says where the arrow's line centre came out.
    ///
    /// <para>Every rectangle here is its section's own <see cref="ScreenRect.Y"/> plus an offset
    /// <i>within</i> that section. That is the property the whole conversion turns on: nothing added up
    /// here can reach a different line, because the only running total is the flow's, and the flow
    /// reads no text.</para></summary>
    private static LineParts ComposeLines(
        ReadoutBodyRequest request, float factor, IReadOnlyList<ScreenRect> placed)
    {
        var lines = request.Lines;
        var parts = new LineParts
        {
            Sections = new ScreenRect[lines.Count],
            Rules = new ScreenRect[lines.Count],
            Markers = new ScreenRect[lines.Count],
            Texts = new ScreenRect[lines.Count],
        };

        for (var i = 0; i < lines.Count; i++)
        {
            var block = lines[i];
            var section = placed[i + 2];
            parts.Sections[i] = section;

            if (block.Separated)
            {
                parts.Rules[i] = new ScreenRect(
                    SubLineLeft(factor),
                    section.Y + RuleTop(factor),
                    SubLineWidth(factor),
                    GameMetrics.Window.RuleHeight);
            }

            var textTop = section.Y + TextTop(block, factor, GutterLine(i));
            parts.Texts[i] = new ScreenRect(
                SubLineLeft(factor), textTop, SubLineWidth(factor), SubLineStep(factor) * Rows(block.Rows));

            // The arrow takes the first subordinate line — the objective — and that line gives up its
            // own medallion while it has it: one mark per line, and the arrow is the stronger statement
            // about the same thing.
            if (request.Arrow && parts.ArrowCentre is null)
            {
                parts.ArrowCentre = textTop + (SubLineFontSize(factor) * GameMetrics.Type.CapHeightCentre);
                continue;
            }

            if (block.Marked && request.Banner)
            {
                parts.Markers[i] = Marker(section, block, factor);
            }
        }

        return parts;
    }

    /// <summary>The medallion, centred on its line's own row pitch rather than on a wrapped line's
    /// full height — 32 tall against a 26-tall row, overhanging it either side, which is what the game
    /// does and what makes the mark read as pinned to the line.</summary>
    private static ScreenRect Marker(ScreenRect section, ReadoutBlock block, float factor)
    {
        var size = GutterWidth(factor);
        var pitch = Math.Min(TextHeight(block, factor), GameMetrics.Banner.SubLinePitch * factor);
        return new ScreenRect(
            GutterLeft(factor),
            section.Y + RuleAdvance(block.Separated, factor) + ((pitch - size) / 2f),
            size,
            size);
    }

    /// <summary>The rows a line actually took, floored at one and capped at
    /// <see cref="MaxWrappedLines"/>.</summary>
    private static float Rows(float rows) => Math.Clamp(rows, 1f, MaxWrappedLines);

    private static ScreenRect Plate(ScreenRect banner, float factor, float width)
    {
        var left = GameMetrics.Banner.PlateLeft * factor;
        return new ScreenRect(
            left,
            banner.Y + (GameMetrics.Banner.PlateTop * factor),
            Math.Max(width - left, factor),
            GameMetrics.Banner.PlateHeight * factor);
    }

    private static ScreenRect Strip(ScreenRect banner, float factor, float width)
    {
        var stripWidth = Math.Min(GameMetrics.Banner.StripWidth * factor, width);
        return new ScreenRect(
            (width - stripWidth) / 2f,
            banner.Y + (GameMetrics.Banner.StripTop * factor),
            stripWidth,
            GameMetrics.Banner.StripHeight * factor);
    }

    private static ScreenRect Crest(ScreenRect banner, float factor)
    {
        var size = GameMetrics.Banner.CrestSize * factor;
        return new ScreenRect(
            GameMetrics.Banner.CrestLeft * factor,
            banner.Y + ((GameMetrics.Banner.PlateTop - GameMetrics.Banner.CrestRise) * factor),
            size,
            size);
    }

    private static ScreenRect Headline(ScreenRect banner, float factor)
    {
        var fontSize = Math.Max(GameMetrics.Banner.HeadlineSize * factor, 8f);
        return new ScreenRect(
            HeadlineLeft(factor),
            banner.Y + ((GameMetrics.Banner.PlateTop + GameMetrics.Banner.HeadlineTop) * factor),
            Math.Max(HeadlineWidth(factor), fontSize),
            Math.Max(GameMetrics.Banner.HeadlineHeight * factor, fontSize));
    }

    private static ScreenRect Cog(ScreenRect banner, float factor, float width)
    {
        var size = Math.Max(GameMetrics.Banner.CogSize * factor, 9f);
        var stripWidth = Math.Min(GameMetrics.Banner.StripWidth * factor, width);
        var x = ((width + stripWidth) / 2f) + (Gap(factor) * 2f);
        var y = banner.Y
            + (GameMetrics.Banner.StripTop * factor)
            + (((GameMetrics.Banner.StripHeight * factor) - size) / 2f);

        return new ScreenRect(Math.Clamp(x, 0f, Math.Max(width - size, 0f)), y, size, size);
    }

    private static ScreenRect Switcher(ScreenRect banner, float factor, float width)
    {
        var cap = GameMetrics.Banner.PlateInsetX * factor;
        return new ScreenRect(
            Math.Max(width - cap, 0f),
            banner.Y + (GameMetrics.Banner.PlateTop * factor),
            cap,
            GameMetrics.Banner.PlateHeight * factor);
    }

    /// <summary>The arrow, centred in the medallion's own column so that it and the marks below it
    /// share one left edge — but never allowed to reach the words it belongs to.
    ///
    /// <para><b>Why the second rule exists.</b> Centring alone is only safe up to the arrow's own
    /// authored size: the gutter is 32 wide and the words start 28 past its left edge, so an arrow
    /// larger than 24 grows straight into the objective line. The player's arrow-size setting goes to
    /// double, so at anything above the default the arrow was drawn over the sentence it was pointing
    /// for. Past that size the arrow grows leftward into the margin instead, which is empty. The two
    /// rules agree exactly at the default size and at every size below it, so this is a no-op for the
    /// arrow the readout actually ships with.</para></summary>
    private static ScreenRect Arrow(float centre, float factor, float arrowScale)
    {
        var size = GameMetrics.Hud.IconSize * factor * Math.Clamp(arrowScale, 0.5f, 2f);
        var centred = GutterLeft(factor) + ((GutterWidth(factor) - size) / 2f);
        var clear = SubLineLeft(factor) - size;

        return new ScreenRect(Math.Max(Math.Min(centred, clear), 0f), centre - (size / 2f), size, size);
    }

    /// <summary>The pieces of the line block, filled in as the sections are walked. A mutable holder
    /// rather than a return tuple only because there are five of them and they are written once each.
    /// </summary>
    private sealed class LineParts
    {
        public ScreenRect[] Sections { get; init; } = [];

        public ScreenRect[] Rules { get; init; } = [];

        public ScreenRect[] Markers { get; init; } = [];

        public ScreenRect[] Texts { get; init; } = [];

        public float? ArrowCentre { get; set; }
    }
}
