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
/// which is what 200% on a 720p screen reduces to, up through an ultrawide.</para>
///
/// <para><b>The readout is the exception to that, and the scales are swept for real.</b> Its width
/// is the banner's own and never resizes, so the thing that varies is the scale itself — and unlike
/// the window, the readout's own text-size preference multiplies on top of the HUD's, so the two do
/// not cancel. Every readout proof below therefore runs at 100%, 150% and 200% against the worst
/// content the composer can actually emit.</para></summary>
public class LayoutContainmentTests
{
    /// <summary>The scales the readout is proved at: the game's authored size, and the two interface
    /// sizes the player is most likely to be at on a television.</summary>
    public static TheoryData<float> Scales =>
    [
        1f, 1.5f, 2f,
    ];

    /// <summary>Every window width the plugin can produce, in addon units. 460 is the enforced
    /// minimum; 760 the maximum; the two in between are the sizes 150% and 200% HUD scale leave of a
    /// 1920-wide viewport. The pathological ones below the minimum are there because a resize is not
    /// atomic and a layout pass can run against a width that is still on its way somewhere.</summary>
    public static TheoryData<float> Widths =>
    [
        1f, 40f, 120f, 240f, 320f, 460f, 507f, 640f, 760f, 1200f,
    ];

    /// <summary>Every row count the engine can hand back for a line: one, and the wrapped cases up to
    /// the cap. The wrapped ones are the whole point — an unwrapped readout never had this defect.
    /// </summary>
    private static float[] HostileRows => [1f, 2f, ReadoutBodyLayout.MaxWrappedLines];

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
            hasLevel: true,
            bodyLines: DetailPaneLayout.MaxBodyLines,
            requirementLines: DetailPaneLayout.MaxRequirementLines,
            hasReward: true,
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
                hasLevel: true,
                bodyLines: DetailPaneLayout.MaxBodyLines,
                requirementLines: DetailPaneLayout.MaxRequirementLines,
                hasReward: true,
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
            hasLevel: true,
            bodyLines: DetailPaneLayout.MaxBodyLines,
            requirementLines: DetailPaneLayout.MaxRequirementLines,
            hasReward: true,
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
                hasLevel: true,
                bodyLines: DetailPaneLayout.MaxBodyLines,
                requirementLines: DetailPaneLayout.MaxRequirementLines,
                hasReward: true,
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

    [Theory]
    [MemberData(nameof(Scales))]
    public void No_two_of_the_readouts_sections_ever_intersect(float scale)
    {
        foreach (var rows in HostileRows)
        {
            var blocks = ReadoutBodyLayout.Compose(Maximal(scale, rows));
            var sections = blocks.Sections.Where(section => !section.IsEmpty).ToList();

            for (var i = 0; i < sections.Count; i++)
            {
                for (var j = i + 1; j < sections.Count; j++)
                {
                    Assert.False(
                        sections[i].Overlaps(sections[j]),
                        $"scale={scale} rows={rows}: {sections[i]} overlaps {sections[j]}");
                }
            }
        }
    }

    /// <summary>The defect this whole conversion is about: two lines of the readout drawn on top of
    /// each other. Swept over every row count the engine can come back with, because a wrapped line
    /// is exactly the case the old cursor got wrong.</summary>
    [Theory]
    [MemberData(nameof(Scales))]
    public void No_two_of_the_readouts_lines_are_ever_drawn_over_each_other(float scale)
    {
        foreach (var rows in HostileRows)
        {
            var texts = ReadoutBodyLayout.Compose(Maximal(scale, rows)).Texts
                .Where(text => !text.IsEmpty)
                .ToList();

            for (var i = 0; i < texts.Count; i++)
            {
                for (var j = i + 1; j < texts.Count; j++)
                {
                    Assert.False(
                        texts[i].Overlaps(texts[j]),
                        $"scale={scale} rows={rows}: {texts[i]} is drawn over {texts[j]}");
                }
            }
        }
    }

    /// <summary>Each line's words and its rule live wholly inside the line's own section. This is the
    /// property that makes the flow safe: a section is worth its whole cost, so the section after it
    /// starts clear of everything in it.</summary>
    [Theory]
    [MemberData(nameof(Scales))]
    public void Every_part_of_a_line_stays_inside_that_lines_own_section(float scale)
    {
        foreach (var rows in HostileRows)
        {
            var blocks = ReadoutBodyLayout.Compose(Maximal(scale, rows));

            for (var i = 0; i < blocks.Sections.Count; i++)
            {
                var where = $"scale={scale} rows={rows} line={i}";
                Assert.True(
                    blocks.Texts[i].ContainedBy(blocks.Sections[i]),
                    $"{where}: {blocks.Texts[i]} escapes {blocks.Sections[i]}");
                Assert.True(
                    blocks.Rules[i].ContainedBy(blocks.Sections[i]),
                    $"{where}: the rule at {blocks.Rules[i]} escapes {blocks.Sections[i]}");
            }
        }
    }

    /// <summary>The gutter's whole job: a medallion or the arrow beside the words, never on them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Scales))]
    public void Nothing_in_the_gutter_ever_reaches_the_words_beside_it(float scale)
    {
        foreach (var rows in HostileRows)
        {
            foreach (var arrowScale in new[] { 0.5f, 1f, 2f })
            {
                var blocks = ReadoutBodyLayout.Compose(Maximal(scale, rows) with { ArrowScale = arrowScale });

                foreach (var text in blocks.Texts.Where(text => !text.IsEmpty))
                {
                    Assert.False(
                        blocks.Arrow.Overlaps(text),
                        $"scale={scale} arrow={arrowScale}: the arrow at {blocks.Arrow} is over {text}");

                    foreach (var marker in blocks.Markers.Where(marker => !marker.IsEmpty))
                    {
                        Assert.False(
                            Ink(marker, scale).Overlaps(text),
                            $"scale={scale}: the medallion at {marker} is over {text}");
                    }
                }
            }
        }
    }

    /// <summary>The reserved gutter, as a proof rather than as a promise: the arrow is in a column of
    /// its own and takes no vertical room, so taking it away moves not one pixel of anything else.
    /// This is the "everything looks shifted when the arrow is absent" complaint, and it is now
    /// structurally impossible rather than carefully avoided.</summary>
    [Theory]
    [MemberData(nameof(Scales))]
    public void Taking_the_arrow_away_moves_nothing_else_on_the_readout(float scale)
    {
        foreach (var rows in HostileRows)
        {
            var with = ReadoutBodyLayout.Compose(Maximal(scale, rows));
            var without = ReadoutBodyLayout.Compose(Maximal(scale, rows) with { Arrow = false });

            Assert.Equal(with.Height, without.Height, 0.01f);
            Assert.Equal(with.Banner, without.Banner);
            Assert.Equal(with.Headline, without.Headline);
            Assert.Equal(with.Sections, without.Sections);
            Assert.Equal(with.Texts, without.Texts);
            Assert.Equal(with.Rules, without.Rules);
        }
    }

    /// <summary>The readout is exactly as tall as its sections, at every scale and every row count.
    /// There is no arithmetic left that could disagree with the container.</summary>
    [Theory]
    [MemberData(nameof(Scales))]
    public void The_readout_is_exactly_as_tall_as_the_sections_it_holds(float scale)
    {
        foreach (var rows in HostileRows)
        {
            var blocks = ReadoutBodyLayout.Compose(Maximal(scale, rows));
            var last = blocks.Sections[^1];

            // The foot's own section is the only thing after the last line, and it is one gap.
            Assert.Equal(last.Bottom + ReadoutBodyLayout.Gap(scale), blocks.Height, 0.01f);
            Assert.All(blocks.All, rect => Assert.True(rect.Y >= 0f, $"{rect} is above the readout"));
        }
    }

    /// <summary>The banner's own three click targets and its two pieces of art keep off each other and
    /// off the name. The name is the one line on the readout that is cut short rather than wrapped, so
    /// the banner's height cannot depend on it — asserted here, because it is what lets the banner be
    /// one fixed-height section in the stack.</summary>
    [Theory]
    [MemberData(nameof(Scales))]
    public void The_banner_holds_its_name_its_cog_and_its_switcher_without_collision(float scale)
    {
        var blocks = ReadoutBodyLayout.Compose(Maximal(scale, rows: 3f));

        Assert.False(blocks.Cog.Overlaps(blocks.Headline), $"scale={scale}: the cog is over the name");
        Assert.False(blocks.Switcher.Overlaps(blocks.Headline), $"scale={scale}: the switcher is over the name");

        // The cog is on the PILL, and the pill deliberately sits on the plate's top edge — that
        // overlap is the banner's own construction, so what is asserted is that the cog stays on the
        // pill rather than that it keeps off the plate.
        Assert.True(blocks.Cog.ContainedBy(blocks.Banner), $"scale={scale}: the cog leaves the banner");
        Assert.True(blocks.Headline.ContainedBy(blocks.Plate), $"scale={scale}: the name leaves the plate");
        Assert.True(blocks.Plate.ContainedBy(blocks.Banner), $"scale={scale}: the plate leaves the banner");
        Assert.True(blocks.Strip.ContainedBy(blocks.Banner), $"scale={scale}: the pill leaves the banner");
        Assert.Equal(ReadoutBodyLayout.BannerHeight(scale), blocks.Banner.Height, 0.01f);
    }

    /// <summary>The smallest readout there is — a quest and how far away it is — and the largest, at
    /// the same scale, to pin the two ends of what the surface actually measures.</summary>
    [Theory]
    [MemberData(nameof(Scales))]
    public void A_minimal_readout_is_shorter_than_a_maximal_one_and_both_hold_together(float scale)
    {
        var minimal = ReadoutBodyLayout.Compose(Minimal(scale));
        var maximal = ReadoutBodyLayout.Compose(Maximal(scale, rows: 3f));

        Assert.True(minimal.Height < maximal.Height, $"scale={scale}: {minimal.Height} !< {maximal.Height}");
        Assert.Single(minimal.Sections);
        Assert.All(minimal.All, rect => Assert.True(rect.Bottom <= minimal.Height + 0.01f, $"{rect} runs long"));
    }

    /// <summary>The worst readout the composer can actually emit: a quest in another zone reached
    /// through an aethernet shard, four digits of distance with an elevation suffix on it, travel
    /// advice, a hunting summary, the zone's own name and the full three nearby unlocks — with every
    /// line wrapped to <paramref name="rows"/> rows.</summary>
    private static ReadoutBodyRequest Maximal(float scale, float rows) => new()
    {
        Factor = scale,
        Lines = Blocks(ReadoutComposer.Compose(HostileReadout.Inputs), rows),
        Arrow = true,
        Banner = true,
        Cog = true,
        Switcher = true,
    };

    /// <summary>A quest and a distance, which is what the readout looks like almost all of the time.
    /// </summary>
    private static ReadoutBodyRequest Minimal(float scale) => new()
    {
        Factor = scale,
        Lines = Blocks(ReadoutComposer.Compose(HostileReadout.PlainInputs), rows: 1f),
        Arrow = true,
        Banner = true,
        Cog = true,
        Switcher = true,
    };

    /// <summary>The part of a medallion's block that actually has ink in it. The game authors four
    /// transparent pixels on its right — see <see cref="GameMetrics.Banner.MarkerArtMargin"/> — and a
    /// proof about what the player can see has to be about the ink.</summary>
    private static ScreenRect Ink(ScreenRect marker, float scale) =>
        marker with { Width = marker.Width - (GameMetrics.Banner.MarkerArtMargin * scale) };

    /// <summary>Turns composed content into the blocks the readout stacks — which is to say, drops the
    /// two lines that are not subordinate lines. The heading became the header pill and is not drawn;
    /// the first subject goes on the plate and cannot wrap.</summary>
    private static List<ReadoutBlock> Blocks(ReadoutContent content, float rows)
    {
        var blocks = new List<ReadoutBlock>();
        var subjectPlaced = false;

        foreach (var line in content.Lines)
        {
            if (line.Emphasis == ReadoutEmphasis.Heading)
            {
                continue;
            }

            if (line.Subject && !subjectPlaced)
            {
                subjectPlaced = true;
                continue;
            }

            blocks.Add(new ReadoutBlock(line.Marked, line.Separated, rows));
        }

        return blocks;
    }

    /// <summary>The level badge and the reward tray are swept as their own dimensions rather than
    /// pinned on, because both are pieces of ART with a fixed size — a 40-pixel disc and a 52-pixel
    /// tray — inside a box that can be smaller than either. A block that is 40 tall in a pane with
    /// 30 left is exactly the failure this file exists to make impossible, and it cannot be an
    /// optional argument nothing ever passes false to.</summary>
    private static void AssertPaneContained(float width, float height)
    {
        foreach (var bodyLines in new[] { 0, 1, DetailPaneLayout.MaxBodyLines })
        {
            foreach (var requirements in new[] { 0, 1, DetailPaneLayout.MaxRequirementLines })
            {
                foreach (var hasLevel in new[] { true, false })
                {
                    foreach (var hasReward in new[] { true, false })
                    {
                        AssertOneComposition(width, height, bodyLines, requirements, hasLevel, hasReward);
                    }
                }
            }
        }
    }

    private static void AssertOneComposition(
        float width, float height, int bodyLines, int requirements, bool hasLevel, bool hasReward)
    {
        var box = DetailPaneLayout.ContentBox(width, height);
        var blocks = DetailPaneLayout.Compose(
            width,
            height,
            hasStatusIcon: true,
            hasLevel,
            bodyLines,
            requirements,
            hasReward,
            hasFrom: true,
            hasProvenance: true);

        var where = $"w={width} h={height} body={bodyLines} req={requirements} level={hasLevel} reward={hasReward}";
        foreach (var block in blocks.Blocks)
        {
            Assert.True(block.ContainedBy(box), $"{where}: {block} escapes {box}");
        }

        var pane = new ScreenRect(0f, 0f, width, height);
        Assert.True(blocks.Rule.ContainedBy(pane), $"the rule escapes the pane at {width}x{height}");
    }
}
