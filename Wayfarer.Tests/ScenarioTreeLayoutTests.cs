using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The geometry proofs for the block drawn under the game's own Main Scenario Guide. The
/// same properties <c>LayoutContainmentTests</c> proves for the readout, for a stack that has no
/// banner: the first line starts at the block's top, nothing overlaps, the gutter never reaches the
/// words, and the arrow costs no height.</summary>
public class ScenarioTreeLayoutTests
{
    public static TheoryData<float> Scales =>
    [
        1f, 1.5f, 2f,
    ];

    private static float[] HostileRows => [1f, 2f, ReadoutBodyLayout.MaxWrappedLines];

    [Theory]
    [MemberData(nameof(Scales))]
    public void The_first_line_starts_at_the_top_of_the_block(float scale)
    {
        var blocks = ScenarioTreeLayout.Compose(Maximal(scale, rows: 1f));

        Assert.Equal(0f, blocks.Sections[0].Y, 0.01f);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void No_two_sections_ever_intersect(float scale)
    {
        foreach (var rows in HostileRows)
        {
            var sections = ScenarioTreeLayout.Compose(Maximal(scale, rows)).Sections;
            for (var i = 0; i < sections.Count; i++)
            {
                for (var j = i + 1; j < sections.Count; j++)
                {
                    Assert.False(sections[i].Overlaps(sections[j]), $"scale={scale} rows={rows}: {sections[i]} overlaps {sections[j]}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void No_two_lines_are_ever_drawn_over_each_other(float scale)
    {
        foreach (var rows in HostileRows)
        {
            var texts = ScenarioTreeLayout.Compose(Maximal(scale, rows)).Texts.Where(text => !text.IsEmpty).ToList();
            for (var i = 0; i < texts.Count; i++)
            {
                for (var j = i + 1; j < texts.Count; j++)
                {
                    Assert.False(texts[i].Overlaps(texts[j]), $"scale={scale} rows={rows}: {texts[i]} is drawn over {texts[j]}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void Every_part_of_a_line_stays_inside_its_own_section(float scale)
    {
        foreach (var rows in HostileRows)
        {
            var blocks = ScenarioTreeLayout.Compose(Maximal(scale, rows));
            for (var i = 0; i < blocks.Sections.Count; i++)
            {
                Assert.True(blocks.Texts[i].ContainedBy(blocks.Sections[i]), $"scale={scale} rows={rows} line={i}: words escape");
                Assert.True(blocks.Rules[i].ContainedBy(blocks.Sections[i]), $"scale={scale} rows={rows} line={i}: rule escapes");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void Nothing_in_the_gutter_ever_reaches_the_words_beside_it(float scale)
    {
        foreach (var rows in HostileRows)
        {
            foreach (var arrowScale in new[] { 0.5f, 1f, 2f })
            {
                var blocks = ScenarioTreeLayout.Compose(Maximal(scale, rows) with { ArrowScale = arrowScale });
                foreach (var text in blocks.Texts.Where(text => !text.IsEmpty))
                {
                    Assert.False(blocks.Arrow.Overlaps(text), $"scale={scale} arrow={arrowScale}: the compass {blocks.Arrow} reaches {text}");
                    foreach (var marker in blocks.Markers.Where(marker => !marker.IsEmpty))
                    {
                        var ink = marker with { Width = marker.Width - (GameMetrics.Banner.MarkerArtMargin * scale) };
                        Assert.False(ink.Overlaps(text), $"scale={scale}: the medallion {ink} reaches {text}");
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void Taking_the_arrow_away_moves_nothing_else(float scale)
    {
        foreach (var rows in HostileRows)
        {
            var with = ScenarioTreeLayout.Compose(Maximal(scale, rows));
            var without = ScenarioTreeLayout.Compose(Maximal(scale, rows) with { Arrow = false });

            Assert.Equal(with.Height, without.Height, 0.01f);
            Assert.Equal(with.Sections, without.Sections);
            Assert.Equal(with.Texts, without.Texts);
            Assert.Equal(with.Rules, without.Rules);
            Assert.True(without.Arrow.IsEmpty);
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void The_block_is_exactly_as_tall_as_its_sections_plus_the_foot(float scale)
    {
        foreach (var rows in HostileRows)
        {
            var blocks = ScenarioTreeLayout.Compose(Maximal(scale, rows));

            Assert.Equal(blocks.Sections[^1].Bottom + ReadoutBodyLayout.Gap(scale), blocks.Height, 0.01f);
            Assert.All(
                blocks.All.Where(rect => rect != blocks.Arrow),
                rect => Assert.True(rect.Y >= 0f, $"{rect} is above the block"));

            // The compass is the one mark allowed past the block's top edge, and only by less than
            // half of itself: its centre is on the first line's cap height, which is inside the block,
            // and the ring is authored taller than the row it sits beside — exactly as the game's own
            // medallion is.
            Assert.True(
                blocks.Arrow.Y + (blocks.Arrow.Height / 2f) >= 0f,
                $"scale={scale} rows={rows}: the compass's centre {blocks.Arrow} is above the block");
        }
    }

    [Fact]
    public void With_nothing_to_say_but_something_to_point_at_the_compass_parks_on_the_first_pitch()
    {
        var blocks = ScenarioTreeLayout.Compose(new ScenarioTreeLayoutRequest { Arrow = true });

        Assert.False(blocks.Arrow.IsEmpty);
        Assert.Empty(blocks.Sections);
        Assert.True(blocks.Arrow.Y + (blocks.Arrow.Height / 2f) >= 0f);
    }

    [Fact]
    public void At_the_games_own_size_the_block_fits_the_games_own_root()
    {
        Assert.True(ScenarioTreeLayout.Left + ReadoutBodyLayout.Width(1f) <= GameMetrics.ScenarioTree.RootWidth);
        Assert.Equal(GameMetrics.ScenarioTree.GuidanceTop(2), ScenarioTreeLayout.Top(2));
    }

    private static ScenarioTreeLayoutRequest Maximal(float scale, float rows) => new()
    {
        Factor = scale,
        Lines = Blocks(ScenarioTreeContent.Compose(HostileReadout.Inputs), rows),
        Arrow = true,
    };

    private static List<ReadoutBlock> Blocks(ReadoutContent content, float rows) =>
        [.. content.Lines.Select(line => new ReadoutBlock(line.Marked, line.Separated, rows))];
}
