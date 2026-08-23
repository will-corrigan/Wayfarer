using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The geometry proof: no part of a row or a detail pane may be drawn outside the box it
/// belongs to, at any window size the plugin allows and at any HUD scale.
///
/// <para>These exist because the field report was "everything is bleeding out of bounds of boxes …
/// half of the requirements leak out of pages", and that was not a rendering accident — it was
/// arithmetic. The pane flowed a title, a status line, four lines of prose, a requirements heading,
/// three requirement bullets, a source line and a provenance line into a fixed 158-pixel box that
/// could hold about half of it, and never checked. Nothing that can be measured should be
/// discovered by looking at it.</para>
///
/// <para><b>Why scale is a parameter and the answer is that it isn't.</b> The HUD scale multiplies
/// every addon unit uniformly on the game's side, so a layout that fits at 100% fits at 200%. What
/// does change is the <i>window size in addon units</i>, because the window is clamped to the
/// viewport in screen pixels and then divided back — so a big HUD scale is equivalent to a small
/// window. The widths swept below are that equivalence: the narrowest window the plugin allows,
/// which is what 200% on a 720p screen reduces to, up through an ultrawide.</para></summary>
public class LayoutContainmentTests
{
    /// <summary>Every window width the plugin can produce, in addon units. 460 is the enforced
    /// minimum; 760 the maximum; the two in between are the sizes 150% and 200% HUD scale leave of a
    /// 1920-wide viewport. The pathological ones below the minimum are there because a resize is not
    /// atomic and a layout pass can run against a width that is still on its way somewhere.</summary>
    public static TheoryData<float> Widths =>
    [
        1f, 40f, 120f, 240f, 320f, 460f, 507f, 640f, 760f, 1200f,
    ];

    /// <summary>Pane heights: the natural one, the ones a squeezed window would produce, and zero.
    /// </summary>
    private static float[] PaneHeights =>
    [
        0f, 16f, 40f, 80f, 120f, DetailPaneLayout.NaturalHeight, 400f,
    ];

    [Theory]
    [MemberData(nameof(Widths))]
    public void Every_row_part_stays_inside_its_row(float width)
    {
        foreach (var shape in Enum.GetValues<RowShape>())
        {
            foreach (var hasIcon in new[] { true, false })
            {
                var height = RowLayout.Height(shape);
                var row = new ScreenRect(0f, 0f, width, height);
                var blocks = RowLayout.Compose(shape, width, height, hasIcon);

                foreach (var block in blocks.Blocks)
                {
                    Assert.True(
                        block.ContainedBy(row),
                        $"{shape} icon={hasIcon} width={width}: {block} escapes {row}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Widths))]
    public void A_rows_two_lines_never_overlap(float width)
    {
        var height = RowLayout.Height(RowShape.Entry);
        var blocks = RowLayout.Compose(RowShape.Entry, width, height, hasIcon: true);

        if (blocks.Label.IsEmpty || blocks.Description.IsEmpty)
        {
            return;
        }

        Assert.False(blocks.Label.Overlaps(blocks.Description));
        Assert.False(blocks.Label.Overlaps(blocks.Trailing));
    }

    [Theory]
    [MemberData(nameof(Widths))]
    public void A_rows_icon_never_sits_under_its_text(float width)
    {
        // Entry rows only. The game's own section header tucks its text one pixel under the icon's
        // right edge (Journal 1021: a 24-wide icon at x=0, text at x=23) because that icon block is
        // authored with a transparent margin — so the same one pixel here is the game's, not a defect.
        var height = RowLayout.Height(RowShape.Entry);
        var blocks = RowLayout.Compose(RowShape.Entry, width, height, hasIcon: true);
        if (blocks.Icon.IsEmpty || blocks.Label.IsEmpty)
        {
            return;
        }

        Assert.False(blocks.Icon.Overlaps(blocks.Label), $"at {width}");
    }

    [Theory]
    [MemberData(nameof(Widths))]
    public void Every_pane_block_stays_inside_the_panes_content_box(float width)
    {
        foreach (var height in PaneHeights)
        {
            AssertPaneContained(width, height);
        }
    }

    [Fact]
    public void The_requirements_a_locked_entry_needs_survive_a_squeezed_pane()
    {
        // The exact case the field report describes, at the exact height it was reported at: an entry
        // with a description AND a full requirement list AND a source line, in the 158-pixel pane that
        // could not hold them. Requirements say why the thing is locked, so they are the block that
        // must survive and prose is what gives way.
        var blocks = DetailPaneLayout.Compose(
            width: 460f,
            height: 158f,
            hasStatusIcon: true,
            bodyLines: DetailPaneLayout.MaxBodyLines,
            requirementLines: DetailPaneLayout.MaxRequirementLines,
            hasFrom: true,
            hasProvenance: true);

        Assert.True(blocks.RequirementLines > 0, "the requirement block was dropped entirely");
        Assert.True(blocks.RequirementLines >= blocks.BodyLines, "prose outranked the requirements");
    }

    [Fact]
    public void Nothing_is_drawn_over_the_action_buttons()
    {
        foreach (var height in PaneHeights)
        {
            var blocks = DetailPaneLayout.Compose(
                width: 460f,
                height: height,
                hasStatusIcon: true,
                bodyLines: DetailPaneLayout.MaxBodyLines,
                requirementLines: DetailPaneLayout.MaxRequirementLines,
                hasFrom: true,
                hasProvenance: true);

            foreach (var block in blocks.Blocks.Where(block => !block.IsEmpty))
            {
                Assert.False(
                    block.Overlaps(blocks.Actions),
                    $"height={height}: {block} is drawn over the buttons at {blocks.Actions}");
            }
        }
    }

    [Fact]
    public void The_panes_natural_height_holds_everything_a_full_entry_has_to_say()
    {
        var blocks = DetailPaneLayout.Compose(
            width: 460f,
            height: DetailPaneLayout.NaturalHeight,
            hasStatusIcon: true,
            bodyLines: DetailPaneLayout.MaxBodyLines,
            requirementLines: DetailPaneLayout.MaxRequirementLines,
            hasFrom: true,
            hasProvenance: true);

        Assert.Equal(DetailPaneLayout.MaxBodyLines, blocks.BodyLines);
        Assert.Equal(DetailPaneLayout.MaxRequirementLines, blocks.RequirementLines);
        Assert.False(blocks.From.IsEmpty);
        Assert.False(blocks.Provenance.IsEmpty);
    }

    [Fact]
    public void The_panes_blocks_never_overlap_each_other()
    {
        var blocks = DetailPaneLayout
            .Compose(
                width: 460f,
                height: DetailPaneLayout.NaturalHeight,
                hasStatusIcon: true,
                bodyLines: DetailPaneLayout.MaxBodyLines,
                requirementLines: DetailPaneLayout.MaxRequirementLines,
                hasFrom: true,
                hasProvenance: true)
            .Blocks
            .Where(block => !block.IsEmpty)
            .ToList();

        for (var i = 0; i < blocks.Count; i++)
        {
            for (var j = i + 1; j < blocks.Count; j++)
            {
                // The title, its caption and the status icon deliberately share a line with the text
                // beside them; the layout narrows the text rather than stacking, so only the
                // vertical relationship is asserted for those.
                if (Math.Abs(blocks[i].Y - blocks[j].Y) < 1f)
                {
                    continue;
                }

                Assert.False(blocks[i].Overlaps(blocks[j]), $"{blocks[i]} overlaps {blocks[j]}");
            }
        }
    }

    private static void AssertPaneContained(float width, float height)
    {
        foreach (var bodyLines in new[] { 0, 1, DetailPaneLayout.MaxBodyLines })
        {
            foreach (var requirements in new[] { 0, 1, DetailPaneLayout.MaxRequirementLines })
            {
                var box = DetailPaneLayout.ContentBox(width, height);
                var blocks = DetailPaneLayout.Compose(
                    width,
                    height,
                    hasStatusIcon: true,
                    bodyLines,
                    requirements,
                    hasFrom: true,
                    hasProvenance: true);

                foreach (var block in blocks.Blocks)
                {
                    Assert.True(
                        block.ContainedBy(box),
                        $"w={width} h={height} body={bodyLines} req={requirements}: {block} escapes {box}");
                }

                var pane = new ScreenRect(0f, 0f, width, height);
                Assert.True(blocks.Rule.ContainedBy(pane), $"the rule escapes the pane at {width}x{height}");
            }
        }
    }
}
