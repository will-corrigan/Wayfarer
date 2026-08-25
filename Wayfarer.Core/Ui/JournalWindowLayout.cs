namespace Wayfarer.Core.Ui;

/// <summary>The journal page's geometry: the box its contents live in, and the one rule by which a
/// stack of blocks is placed inside it.
///
/// <para><b>What this replaced, and why the replacement is a different kind of thing.</b> This class
/// used to allocate the page out of a budget and then flow it down a cursor — <c>Compose</c>,
/// <c>Allocate</c>, <c>Advance</c>, and a <c>JournalWindowBlocks</c> record of twenty-three
/// rectangles. Every block's position was <i>computed</i> from a measurement of the block above it,
/// so one wrong measurement — a wrapped string measured before its node had the right width, a
/// paragraph shortened after the arithmetic had already run — moved every block below it and drew
/// text on top of text. That is not a bug that gets fixed; it is a bug that gets reintroduced, and it
/// was reintroduced three times.</para>
///
/// <para><b>The rule that replaced it.</b> A block's position is a consequence of the blocks before
/// it <i>and of nothing else</i>: <see cref="Flow"/> walks the list once, taking each block's own
/// declared height. Nothing measures anything on another block's behalf, so there is no measurement
/// to get wrong. This is exactly what <c>VerticalListNode</c> with <c>FitContents</c> does at
/// runtime, which is what the window actually uses — the game's own journal works the same way, with
/// a container that grows to fit its description and the sections below pushed down by that growth.
/// <see cref="Flow"/> is the same rule in a form that can be asserted and drawn without a client
/// attached, and <c>JournalWindowLayoutTests</c> asserts that no two blocks it returns can ever
/// intersect, whatever heights it is handed.</para>
///
/// <para><b>Height follows content.</b> The page is as tall as what is on it
/// (<see cref="WindowHeight"/>), which is why there is no longer a band of empty parchment between
/// the requirements and the foot. The game's own page is a fixed 628 with a scroll bar absorbing the
/// difference; this window has no scroll bar — see
/// <see cref="GameMetrics.JournalFrame.ColumnLeft"/> — so the honest equivalent of "the foot sits
/// under the content" is a page that stops where its content stops.</para></summary>
public static class JournalWindowLayout
{
    /// <summary>Most lines of prose the description will ever be given. Six Axis-14 lines at leading
    /// 18 is 108 pixels, which is what JournalCanvas <c>#8</c>'s section grows to on a long quest
    /// summary. A cap on the <i>window's</i> height rather than a budget the text is squeezed into:
    /// the longest description in the shipped catalogue is 239 characters, which sets in five.
    /// </summary>
    public const int MaxDescriptionLines = 6;

    /// <summary>Most lines the requirements block will ever be given.
    ///
    /// <para>Six, and it used to matter a great deal more than it does. The catalogue's worst case
    /// was a job gate that named thirty classes in one sentence and wrapped to five lines; that
    /// sentence is now the category's own name and a level — see <c>JobGateText</c> — so this cap is
    /// headroom rather than a guillotine.</para></summary>
    public const int MaxRequirementLines = 6;

    /// <summary>Most lines the entry's name will ever be given. JournalDetail <c>#38</c> is 340x50 —
    /// two Axis-18 lines at leading 20 — so the game wraps a long title to two lines and lets the
    /// node ellipsise past that. It does not shrink the face and it does not truncate at one line, so
    /// neither does this.</summary>
    public const int MaxTitleLines = 2;

    /// <summary>Most lines the state line will ever take. It says one thing — which state the entry
    /// is in — because the reason it is in that state belongs to the requirements block, and saying
    /// it twice is what the player's screenshot showed.</summary>
    public const int MaxStatusLines = 1;

    /// <summary>Where the page's content box opens, in the frame's own space. JournalDetail's level
    /// badge group <c>#8</c> is at y=62 and its title <c>#38</c> at y=72 in a root whose frame starts
    /// at y=20 — so the band opens 42 pixels down the border.</summary>
    public static float ContentTop => GameMetrics.JournalFrame.TitleTop;

    /// <summary>How far the content box's foot sits above the frame's own foot. The game's button
    /// row (<c>#49</c>) ends 36 above the frame's bottom edge, and the row is the last thing in the
    /// flow, so this is the whole of the bottom margin.</summary>
    public static float ContentBottomInset => GameMetrics.JournalFrame.ButtonBottomInset;

    /// <summary>The column everything is drawn in: the game's own authored 394, centred.</summary>
    public static float ContentWidth => GameMetrics.Journal.SectionWidth;

    /// <inheritdoc cref="ContentWidth"/>
    public static float ContentLeft => GameMetrics.JournalFrame.ColumnLeft;

    /// <summary>The gap between two blocks in the stack. The game's own gap between stacked blocks,
    /// used here for every one of them: a single spacing is what makes the page read as one column
    /// rather than as a set of separately positioned things.</summary>
    public static float Spacing => GameMetrics.Window.BlockGap;

    /// <summary>The inset that centres the page's rule in the column. The game draws that image at
    /// the width its art is authored at and never stretches it, so neither does this.</summary>
    public static float RuleInset =>
        Math.Max((ContentWidth - GameMetrics.JournalArt.DividerWidth) / 2f, 0f);

    /// <summary>The frame height a fully populated entry wants — every block present, both wrapping
    /// blocks at their cap. What the window would ask for at its very largest, used to size the
    /// sweeps in the tests and never as a target.</summary>
    public static float NaturalHeight =>
        WindowHeight(FlowHeight(
        [
            TitleHeight(MaxTitleLines),
            GameMetrics.Window.RuleHeight,
            BlockHeight(MaxStatusLines),
            GameMetrics.Journal.BannerHeight,
            GameMetrics.Journal.SectionHeadingHeight + GameMetrics.Journal.TrayHeight,
            GameMetrics.Journal.SectionHeadingHeight + BlockHeight(MaxDescriptionLines),
            GameMetrics.Journal.SectionHeadingHeight + BlockHeight(MaxRequirementLines),
            GameMetrics.Row.TextHeight,
            GameMetrics.Journal.FootnoteHeight,
            GameMetrics.Window.RuleHeight,
            GameMetrics.Control.ButtonHeight,
        ]));

    /// <summary>The box the stack lives in, for a frame of <paramref name="height"/>. Empty when the
    /// frame is too short to hold anything at all, which is a state a resize passes through rather
    /// than one a player sees.</summary>
    public static ScreenRect ContentBox(float height)
    {
        var available = Math.Max(height, 0f) - ContentTop - ContentBottomInset;
        return available <= 0f
            ? default
            : new ScreenRect(ContentLeft, ContentTop, ContentWidth, available);
    }

    /// <summary>Places a stack of blocks, taking each block's height from the block itself.
    ///
    /// <para><b>This is the whole of the layout.</b> The walk itself — the cursor starting at the
    /// box's top and advancing by whatever the block it just placed says it is — is shared with the
    /// readout's own stack; see <see cref="FlowLayout"/> for the rule and why it lives there once. The
    /// window's own contribution is <paramref name="spacing"/>, its gap between stacked blocks.
    /// </para></summary>
    public static IReadOnlyList<ScreenRect> Flow(
        IReadOnlyList<float> heights, float spacing, ScreenRect box) =>
        FlowLayout.Flow(heights, spacing, box);

    /// <summary>How tall that stack comes out — the height a container with <c>FitContents</c> takes
    /// on. The same walk as <see cref="Flow"/>, and it has to be, so the window's height and its
    /// contents cannot disagree.</summary>
    public static float FlowHeight(IReadOnlyList<float> heights, float spacing = -1f) =>
        FlowLayout.FlowHeight(heights, spacing < 0f ? Spacing : spacing);

    /// <summary>The frame height a content stack of <paramref name="contentHeight"/> needs: the band
    /// above it, the stack, and the margin the game leaves under its button row. Never shorter than
    /// the border can close at — below <see cref="GameMetrics.JournalFrame.MinHeight"/> the gilt
    /// frame's fixed top and foot would overlap, and half a border is worse than none.</summary>
    public static float WindowHeight(float contentHeight) =>
        Math.Max(
            ContentTop + Math.Max(contentHeight, 0f) + ContentBottomInset,
            GameMetrics.JournalFrame.MinHeight);

    /// <summary>The height a block of <paramref name="lines"/> Axis-14 lines needs — the same
    /// arithmetic the strip and the page use, because it is the same face at the same leading.
    /// </summary>
    public static float BlockHeight(int lines) => DetailPaneLayout.BlockHeight(lines);

    /// <summary>The height a title of <paramref name="lines"/> Axis-18 lines needs. JournalDetail
    /// <c>#38</c> reserves 50 for two, so 25 a line.</summary>
    public static float TitleHeight(int lines) =>
        Math.Max(lines, 0) * GameMetrics.Detail.TitleHeight;
}
