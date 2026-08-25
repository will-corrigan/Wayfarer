using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>Guards <see cref="GameMetrics.Type.CapHeightCentre"/> — a font-rendering constant tuned
/// once rather than read out of a <c>.uld</c>; see its own doc comment. It is shared by three call
/// sites in <c>ReadoutBodyNode</c> (the direction arrow, the settings cog and the follow switcher),
/// all of which read it rather than carrying their own copy, so a change here changes all three
/// alignments together rather than letting one drift from the others.
///
/// <para>It is not the only value in that file which is not a direct measurement — <c>Row.Spacing</c>,
/// <c>Control.ButtonGap</c>, <c>Journal.MinTextColumn</c>, <c>JournalFrame.ColumnLeft</c> and several
/// of the <c>Banner</c> values each declare their own deviation. <c>Banner.CogSize</c> is the one that
/// cites nothing at all, because there was nothing to cite; it says so, and it has no test here
/// because there is no property to assert about a number that was chosen by
/// looking.</para></summary>
public class GameMetricsTests
{
    [Fact]
    public void CapHeightCentre_is_below_the_line_boxs_own_geometric_centre()
    {
        // Above 0.5 (measured down from the top of the em) means the mark it centres sits lower
        // than the box's own middle — which is the whole point: a cap-height glyph's visual weight
        // is in the upper part of the em, so its optical centre is above the geometric one, and a
        // control aligned to the box alone reads as sitting a couple of pixels low beside it.
        Assert.InRange(GameMetrics.Type.CapHeightCentre, 0.5f, 1f);
    }

    [Fact]
    public void CapHeightCentre_is_the_tuned_value()
    {
        // Pinned rather than merely range-checked: this exact number was tuned once against the
        // readout's own direction indicator (see ReadoutBodyNode.LayoutCompass) and a silent change
        // here would silently misalign the compass, the cog and the follow switcher together.
        Assert.Equal(0.58f, GameMetrics.Type.CapHeightCentre);
    }
}
