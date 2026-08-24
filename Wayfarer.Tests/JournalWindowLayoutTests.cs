using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The journal page's anti-overlap proof.
///
/// <para><b>What changed, and why the test changed with it.</b> This file used to sweep
/// <c>JournalWindowLayout.Compose</c> — a ladder that allocated the page out of a budget and then
/// flowed it down a y-cursor — and assert that the twenty-three rectangles it produced neither escaped
/// the box nor collided. It passed, and the window still drew a description over the Requirements
/// heading, because the rectangles were only correct for the heights the ladder was <i>told</i>, and
/// the heights it was told came from a measurement taken at the wrong moment. Asserting a wrong
/// answer's self-consistency is not a proof.</para>
///
/// <para>So the layout is now a flow — <see cref="JournalWindowLayout.Flow"/>, the rule
/// <c>VerticalListNode</c> with <c>FitContents</c> implements and the window actually uses — and what
/// is asserted here is the property that makes the whole class of defect impossible: whatever heights
/// are handed in, however wrong, <b>no two blocks intersect</b>. Not "do not intersect for the
/// heights we expect"; cannot intersect. The hostile fixtures (<see cref="HostileContent"/>) are swept
/// anyway, because a property that holds for everything must hold for them.</para></summary>
public class JournalWindowLayoutTests
{
    /// <summary>Every frame height the window can be, and several it cannot. The authored 628; the
    /// natural height a full entry asks for; the minimum the border can close at; the heights a
    /// squeezed viewport leaves; and the pathological ones below the minimum, because a resize is not
    /// atomic and a layout pass can run against a height that is still on its way somewhere.
    /// </summary>
    public static TheoryData<float> Heights =>
    [
        0f, 1f, 40f, 108f, 192f, 288f, 300f, 420f, 520f,
        GameMetrics.JournalFrame.AuthoredHeight, JournalWindowLayout.NaturalHeight, 900f,
    ];

    /// <summary>Block heights, from nothing to absurd. The absurd ones are the point: 30 lines of Axis
    /// 14 is what the thirty-job requirement sentence measured, and 4,000 is a measurement that has
    /// gone wrong outright. Under the old ladder either one moved every block below it; under a flow
    /// neither can move anything.</summary>
    public static TheoryData<float> BlockHeights =>
    [
        0f,
        -50f,
        1f,
        JournalWindowLayout.BlockHeight(1),
        JournalWindowLayout.BlockHeight(JournalWindowLayout.MaxRequirementLines),
        JournalWindowLayout.BlockHeight(30),
        4000f,
    ];

    /// <summary>The hostile page: every block present, the thirty-job requirement string, the longest
    /// description in the catalogue and a two-line title, at the heights each of those measures to.
    /// </summary>
    private static float[] HostilePage =>
    [
        JournalWindowLayout.TitleHeight(JournalWindowLayout.MaxTitleLines),
        GameMetrics.Window.RuleHeight,
        JournalWindowLayout.BlockHeight(1),
        GameMetrics.Journal.BannerHeight,
        GameMetrics.Journal.SectionHeadingHeight + GameMetrics.Journal.TrayHeight,
        GameMetrics.Journal.SectionHeadingHeight + JournalWindowLayout.BlockHeight(5),
        GameMetrics.Journal.SectionHeadingHeight + JournalWindowLayout.BlockHeight(5),
        GameMetrics.Row.TextHeight,
        GameMetrics.Journal.FootnoteHeight,
        GameMetrics.Window.RuleHeight,
        GameMetrics.Control.ButtonHeight,
    ];

    [Theory]
    [MemberData(nameof(BlockHeights))]
    public void No_two_blocks_ever_intersect(float hostile)
    {
        // The hostile height is put in every position in turn, because "the block that measured wrong
        // is the last one" is the easy case and "it is the second of eleven" is the one that used to
        // move nine other blocks.
        for (var position = 0; position < HostilePage.Length; position++)
        {
            var heights = (float[])HostilePage.Clone();
            heights[position] = hostile;

            AssertDisjoint(
                JournalWindowLayout.Flow(
                    heights,
                    JournalWindowLayout.Spacing,
                    JournalWindowLayout.ContentBox(JournalWindowLayout.NaturalHeight)),
                $"hostile {hostile} at {position}");
        }
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void No_two_blocks_ever_intersect_at_any_window_height(float height)
    {
        AssertDisjoint(
            JournalWindowLayout.Flow(
                HostilePage, JournalWindowLayout.Spacing, JournalWindowLayout.ContentBox(height)),
            $"h={height}");
    }

    /// <summary>The same proof at every interface scale. Scale multiplies every addon unit uniformly
    /// on the game's side, so what actually varies is the window height the viewport leaves — which is
    /// what <see cref="Heights"/> sweeps. This asserts the equivalence rather than assuming it: the
    /// flow is scale-free, so a stack that is disjoint in addon units is disjoint at any scale.
    /// </summary>
    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    [InlineData(4f)]
    public void No_two_blocks_ever_intersect_at_any_scale(float scale)
    {
        var scaled = HostilePage.Select(height => height * scale).ToArray();
        var box = JournalWindowLayout.ContentBox(JournalWindowLayout.NaturalHeight);
        var placed = JournalWindowLayout.Flow(
            scaled, JournalWindowLayout.Spacing * scale, box with { Width = box.Width * scale });

        AssertDisjoint(placed, $"scale={scale}");
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void Every_block_stays_inside_the_column(float height)
    {
        var box = JournalWindowLayout.ContentBox(height);

        foreach (var block in Drawn(JournalWindowLayout.Flow(HostilePage, JournalWindowLayout.Spacing, box)))
        {
            // Horizontally only. A stack taller than its box runs off the bottom by design — that is
            // what the window's own clip node is for, and it is the honest failure: the last block is
            // cut off rather than drawn on top of the one above it.
            Assert.Equal(box.X, block.X);
            Assert.Equal(box.Width, block.Width);
        }
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void The_column_stays_inside_the_gilt_frame(float height)
    {
        var box = JournalWindowLayout.ContentBox(height);
        if (box.IsEmpty)
        {
            return;
        }

        // The frame eats 32 pixels a side for its rails, so the box everything lives in has to be
        // inside the border's inside edge, not the window's outside edge.
        var inner = JournalFrameLayout.Inner(height);
        Assert.True(box.ContainedBy(inner), $"h={height}: the column {box} escapes the frame {inner}");
    }

    /// <summary>A block placed after another one starts below it, always — which is the whole of what
    /// "the sections below are pushed down" means, and the thing the old ladder could not promise
    /// because it had already decided where the lower block went.</summary>
    [Theory]
    [MemberData(nameof(BlockHeights))]
    public void A_taller_block_pushes_everything_under_it_down(float hostile)
    {
        var baseline = JournalWindowLayout.Flow(
            HostilePage, JournalWindowLayout.Spacing, JournalWindowLayout.ContentBox(900f));

        var heights = (float[])HostilePage.Clone();
        heights[0] = hostile;
        var grown = JournalWindowLayout.Flow(
            heights, JournalWindowLayout.Spacing, JournalWindowLayout.ContentBox(900f));

        var delta = hostile <= 0f ? -(HostilePage[0] + JournalWindowLayout.Spacing) : hostile - HostilePage[0];

        for (var i = 1; i < baseline.Count; i++)
        {
            if (baseline[i].IsEmpty)
            {
                continue;
            }

            Assert.Equal(baseline[i].Y + delta, grown[i].Y, 3);
        }
    }

    /// <summary>The window is as tall as its contents, so there is no band of empty parchment between
    /// the last block and the foot. This is the arithmetic half of the player's "huge empty gap".
    /// </summary>
    [Fact]
    public void The_window_is_exactly_as_tall_as_what_is_on_it()
    {
        var content = JournalWindowLayout.FlowHeight(HostilePage);
        var height = JournalWindowLayout.WindowHeight(content);

        Assert.Equal(
            JournalWindowLayout.ContentTop + content + JournalWindowLayout.ContentBottomInset,
            height,
            3);

        // And a page with less on it is a shorter window rather than the same window with a hole in
        // it. Four blocks fewer has to come out shorter, not equal.
        var sparse = JournalWindowLayout.FlowHeight(
        [
            JournalWindowLayout.TitleHeight(1),
            GameMetrics.Window.RuleHeight,
            JournalWindowLayout.BlockHeight(1),
            GameMetrics.Window.RuleHeight,
            GameMetrics.Control.ButtonHeight,
        ]);

        Assert.True(
            JournalWindowLayout.WindowHeight(sparse) < height,
            "a page with less on it did not produce a shorter window");
    }

    /// <summary>A block that is not being drawn takes no room and no spacing — which is what stops a
    /// missing banner or an absent reward leaving a gap where it used to be. The old ladder's
    /// equivalent of this was a bug report: "everything looks shifted when the arrow is absent".
    /// </summary>
    [Fact]
    public void An_absent_block_leaves_no_gap()
    {
        var withBanner = JournalWindowLayout.FlowHeight([40f, GameMetrics.Journal.BannerHeight, 40f]);
        var without = JournalWindowLayout.FlowHeight([40f, 0f, 40f]);

        Assert.Equal(
            withBanner - GameMetrics.Journal.BannerHeight - JournalWindowLayout.Spacing, without, 3);
        Assert.Equal(40f + JournalWindowLayout.Spacing + 40f, without, 3);
    }

    [Fact]
    public void The_natural_height_holds_a_full_entry_and_the_border_can_still_close()
    {
        var natural = JournalWindowLayout.NaturalHeight;

        Assert.True(natural >= GameMetrics.JournalFrame.MinHeight);
        Assert.False(JournalWindowLayout.ContentBox(natural).IsEmpty);
        Assert.True(
            JournalWindowLayout.FlowHeight(HostilePage)
            <= JournalWindowLayout.ContentBox(natural).Height,
            "the hostile page does not fit the height a full entry asks for");
    }

    /// <summary>The title's box is bounded to the game's own two Axis-18 lines. JournalDetail
    /// <c>#38</c> is 340x50 and wraps; it does not shrink the face and it does not stop at one line, so
    /// a second line takes a second line's worth of room and the rest of the page moves down by
    /// exactly that.</summary>
    [Fact]
    public void A_two_line_title_takes_a_second_line_and_no_more()
    {
        Assert.Equal(GameMetrics.Journal.PageTitleHeight, JournalWindowLayout.TitleHeight(2), 3);
        Assert.Equal(
            JournalWindowLayout.TitleHeight(1) * 2f, JournalWindowLayout.TitleHeight(2), 3);
    }

    private static IEnumerable<ScreenRect> Drawn(IEnumerable<ScreenRect> blocks) =>
        blocks.Where(block => !block.IsEmpty);

    private static void AssertDisjoint(IReadOnlyList<ScreenRect> placed, string where)
    {
        var drawn = Drawn(placed).ToList();

        for (var i = 0; i < drawn.Count; i++)
        {
            for (var j = i + 1; j < drawn.Count; j++)
            {
                Assert.False(
                    drawn[i].Overlaps(drawn[j]),
                    $"{where}: {drawn[i]} overlaps {drawn[j]}");
            }
        }
    }
}
