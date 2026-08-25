using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>What a row count costs, and the two unit-domain mistakes that made every line of the
/// readout pay it twice.
///
/// <para><b>The complaint this closes.</b> "The banner and the quest steps are way too spread out",
/// three times over, against a layout whose own arithmetic kept auditing as correct — a bare line is
/// worth <see cref="GameMetrics.Banner.AnnotationBlock"/>, 14, the quest tracker's own Axis-12
/// leading, and the spacing unit either side of it is two pixels. The looseness was not a metric. It
/// was <c>ReadoutBodyNode.WrappedLines</c> reporting two rows for text that fits on one, and
/// <see cref="ReadoutBodyLayout.TextHeight"/> faithfully doubling 14 to 28 for every line on the
/// readout.</para>
///
/// <para><b>Why it took three passes to find.</b> Both mistakes are invisible at an interface size of
/// exactly 100%: the engine's measurement was being asked for in screen pixels and divided by numbers
/// in addon units, which agree only at 1.0, and the row count was the drawn height over the leading,
/// which is off by one only once a row draws taller than the leading it is set at. The readout leads
/// its lines deliberately tighter than the face's own rows — that is the whole of being a heads-up
/// element rather than a window — so its own tightness is what made the second division
/// wrong.</para></summary>
public class ReadoutMeasurementTests
{
    private const string Node = "Wayfarer/Windows/Native/ReadoutBodyNode.cs";

    /// <summary>The cost of the bug, in the layout's own arithmetic: a row count of two on a line
    /// whose words fit on one row is not a rounding error, it is the line's whole height again.
    /// </summary>
    [Fact]
    public void A_second_row_doubles_a_bare_lines_height()
    {
        var one = ReadoutBodyLayout.TextHeight(new ReadoutBlock(false, false, 1f), 1f);
        var two = ReadoutBodyLayout.TextHeight(new ReadoutBlock(false, false, 2f), 1f);

        Assert.Equal(GameMetrics.Banner.AnnotationBlock, one);
        Assert.Equal(one * 2f, two);
    }

    /// <summary>And what the readout is worth when the row counts are honest: the distance from one
    /// bare line's words to the next line's words is one leading, 14, and nothing else. The banner
    /// above them is the art's own fixed height; the container adds nothing between sections at all.
    /// </summary>
    [Fact]
    public void Two_consecutive_single_row_lines_sit_one_leading_apart()
    {
        var request = new ReadoutBodyRequest
        {
            Factor = 1f,
            Banner = true,
            Lines =
            [
                new ReadoutBlock(false, false, 1f),
                new ReadoutBlock(false, false, 1f),
                new ReadoutBlock(false, false, 1f),
            ],
        };

        var blocks = ReadoutBodyLayout.Compose(request);

        // The first subordinate line is the one that reserves the arrow's gutter, so it takes the
        // tracker's icon-bearing block; every line after it is a bare row of text.
        Assert.Equal(GameMetrics.Banner.SubLinePitch, blocks.Sections[1].Y - blocks.Sections[0].Y);
        Assert.Equal(GameMetrics.Banner.AnnotationBlock, blocks.Sections[2].Y - blocks.Sections[1].Y);
        Assert.Equal(GameMetrics.Hud.MetaLeading, blocks.Texts[2].Y - blocks.Texts[1].Y);
    }

    /// <summary>Mistake one, pinned: the engine is asked for its measurement in the units the node was
    /// given. The readout bakes the interface scale into the sizes it hands its nodes and leaves their
    /// <c>Scale</c> at 1, so a scaled measurement carries that scale a second time — and the divisions
    /// it feeds are ceilings, which turn any excess at all into another row.</summary>
    [Fact]
    public void Every_text_measurement_on_the_readout_is_taken_unscaled()
    {
        var code = SourceGuard.SourceOf(Node);

        var calls = SourceGuard.Occurrences(code, "GetTextDrawSize(");
        var unscaled = SourceGuard.Occurrences(code, "considerScale: false");

        Assert.True(calls > 0, "The readout no longer measures its own text.");
        Assert.Equal(calls, unscaled);
    }

    /// <summary>Mistake two, pinned: a row count is one row plus the steps beyond it, measured against
    /// a row of this node's own face — never the drawn height divided by the leading, which reads a
    /// single row as two whenever the face's rows are taller than the leading they are set at, and on
    /// this readout they always are.</summary>
    [Fact]
    public void The_row_count_measures_a_single_row_rather_than_dividing_by_the_leading()
    {
        var method = SourceGuard.Body(SourceGuard.SourceOf(Node), "private static float WrappedLines(");

        Assert.Contains("RowProbe", method, StringComparison.Ordinal);
        Assert.Contains("1f + MathF.Round(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("MathF.Ceiling(drawn.Y", method, StringComparison.Ordinal);
    }
}
