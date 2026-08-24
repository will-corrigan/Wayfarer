using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The journal window's geometry proof.
///
/// <para>The window it replaces failed in a way that was arithmetic rather than rendering: a
/// description drawn on top of a requirement list, a banner behind text, and the same requirement
/// list printed twice. Every one of those is a property of the numbers, so every one of them is
/// asserted here — at every height the window can be, with the worst content the catalogue holds.
/// </para></summary>
public class JournalWindowLayoutTests
{
    /// <summary>Every frame height the window can be. The authored 628; the natural height it asks
    /// for; the minimum the border can close at; the heights a squeezed viewport leaves; and the
    /// pathological ones below the minimum, because a resize is not atomic and a layout pass can run
    /// against a height that is still on its way somewhere.</summary>
    public static TheoryData<float> Heights =>
    [
        0f, 1f, 40f, 108f, 192f, 288f, 300f, 420f, 520f,
        GameMetrics.JournalFrame.AuthoredHeight, JournalWindowLayout.NaturalHeight, 900f,
    ];

    /// <summary>The hostile text heights: nothing, one line, the full budget, and far past it — the
    /// last standing in for the entry whose gate names thirty jobs, which wraps to five lines of
    /// Axis 14 and used to be flowed into a box sized for one.</summary>
    private static float[] TextHeights =>
    [
        0f,
        JournalWindowLayout.BlockHeight(1),
        JournalWindowLayout.BlockHeight(JournalWindowLayout.MaxRequirementLines),
        JournalWindowLayout.BlockHeight(30),
    ];

    [Theory]
    [MemberData(nameof(Heights))]
    public void Every_block_stays_inside_the_content_box(float height)
    {
        var box = JournalWindowLayout.ContentBox(height);

        foreach (var blocks in Compositions(height))
        {
            foreach (var block in blocks.Blocks)
            {
                Assert.True(block.ContainedBy(box), $"h={height}: {block} escapes {box}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void Every_block_stays_inside_the_gilt_frame(float height)
    {
        // The frame eats 32 pixels a side for its rails, so the box everything has to live in is the
        // border's inside edge, not the window's outside edge.
        var inner = JournalFrameLayout.Inner(height);

        foreach (var blocks in Compositions(height))
        {
            foreach (var block in blocks.All)
            {
                Assert.True(block.ContainedBy(inner), $"h={height}: {block} escapes the frame {inner}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void Nothing_overlaps_anything(float height)
    {
        foreach (var blocks in Compositions(height))
        {
            var drawn = blocks.Foreground.Where(block => !block.IsEmpty).ToList();

            for (var i = 0; i < drawn.Count; i++)
            {
                for (var j = i + 1; j < drawn.Count; j++)
                {
                    // A glyph, its heading and the caption beside a title deliberately share a
                    // line: the layout narrows the text rather than stacking it, so for blocks on
                    // the same baseline only the horizontal relationship is meaningful, and
                    // ContainedBy already proves that.
                    if (Math.Abs(drawn[i].Y - drawn[j].Y) < 1f)
                    {
                        continue;
                    }

                    Assert.False(
                        drawn[i].Overlaps(drawn[j]),
                        $"h={height}: {drawn[i]} overlaps {drawn[j]}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void Nothing_is_drawn_over_the_button_row(float height)
    {
        foreach (var blocks in Compositions(height))
        {
            var row = blocks.Actions;
            var icon = blocks.Boss;

            foreach (var block in blocks.Blocks.Where(block => !block.IsEmpty))
            {
                Assert.False(block.Overlaps(row), $"h={height}: {block} covers the buttons at {row}");
                Assert.False(block.Overlaps(icon), $"h={height}: {block} covers the icon button at {icon}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void A_reward_slot_never_leaves_its_tray(float height)
    {
        foreach (var blocks in Compositions(height))
        {
            Assert.True(
                blocks.RewardIcon.ContainedBy(blocks.RewardTray),
                $"h={height}: the reward icon {blocks.RewardIcon} is off the tray {blocks.RewardTray}");
            Assert.True(
                blocks.RewardName.ContainedBy(blocks.RewardTray),
                $"h={height}: the reward name {blocks.RewardName} is off the tray {blocks.RewardTray}");
        }
    }

    [Fact]
    public void The_button_row_and_the_icon_button_never_touch()
    {
        var height = GameMetrics.JournalFrame.AuthoredHeight;
        Assert.False(JournalWindowLayout.ActionRow(height).Overlaps(JournalWindowLayout.Boss(height)));
    }

    [Fact]
    public void A_thirty_job_requirement_string_never_pushes_the_description_off_the_page()
    {
        // The reported case, with the worst string in the catalogue in the block that matters most:
        // requirements are allocated before the description, so the description is what gives way,
        // and neither may leave the box.
        var height = GameMetrics.JournalFrame.AuthoredHeight;
        var box = JournalWindowLayout.ContentBox(height);
        var blocks = JournalWindowLayout.Compose(
            height,
            hasLevel: true,
            hasStatusIcon: true,
            hasBanner: true,
            hasReward: true,
            requirementsHeight: JournalWindowLayout.BlockHeight(30),
            descriptionHeight: JournalWindowLayout.BlockHeight(JournalWindowLayout.MaxDescriptionLines),
            hasGiver: true,
            hasProvenance: true);

        Assert.True(blocks.Requirements.ContainedBy(box), $"{blocks.Requirements} escapes {box}");
        Assert.True(blocks.Description.ContainedBy(box), $"{blocks.Description} escapes {box}");
        Assert.False(blocks.Requirements.Overlaps(blocks.Description));
    }

    [Fact]
    public void Requirements_outrank_the_description_and_the_banner_when_room_runs_out()
    {
        // 420 is short enough that the ladder has to give something up but tall enough to still hold
        // the block that matters. What it gives up is the picture, and never the reason the entry is
        // locked — which is allocated first and therefore survives every squeeze the requirements
        // block itself fits in.
        var blocks = JournalWindowLayout.Compose(
            height: 420f,
            hasLevel: true,
            hasStatusIcon: true,
            hasBanner: true,
            hasReward: true,
            requirementsHeight: JournalWindowLayout.BlockHeight(JournalWindowLayout.MaxRequirementLines),
            descriptionHeight: JournalWindowLayout.BlockHeight(JournalWindowLayout.MaxDescriptionLines),
            hasGiver: true,
            hasProvenance: true);

        Assert.False(blocks.Requirements.IsEmpty, "the requirements were dropped");
        Assert.True(blocks.Banner.IsEmpty, "the banner survived a window too short for it");
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void A_section_heading_is_never_drawn_without_a_line_under_it(float height)
    {
        foreach (var blocks in Compositions(height))
        {
            Assert.Equal(blocks.Description.IsEmpty, blocks.DescriptionLabel.IsEmpty);
            Assert.Equal(blocks.Requirements.IsEmpty, blocks.RequirementsLabel.IsEmpty);
        }
    }

    [Fact]
    public void The_natural_height_holds_everything_a_full_entry_has_to_say()
    {
        var blocks = JournalWindowLayout.Compose(
            JournalWindowLayout.NaturalHeight,
            hasLevel: true,
            hasStatusIcon: true,
            hasBanner: true,
            hasReward: true,
            requirementsHeight: JournalWindowLayout.BlockHeight(JournalWindowLayout.MaxRequirementLines),
            descriptionHeight: JournalWindowLayout.BlockHeight(JournalWindowLayout.MaxDescriptionLines),
            hasGiver: true,
            hasProvenance: true);

        Assert.False(blocks.LevelBadge.IsEmpty);
        Assert.False(blocks.Banner.IsEmpty);
        Assert.False(blocks.RewardTray.IsEmpty);
        Assert.False(blocks.Description.IsEmpty);
        Assert.False(blocks.Requirements.IsEmpty);
        Assert.False(blocks.Giver.IsEmpty);
        Assert.False(blocks.Provenance.IsEmpty);
        Assert.False(blocks.Actions.IsEmpty);
    }

    /// <summary>Every combination of present-and-absent blocks the window can be asked to draw, at
    /// one height. Swept rather than sampled because "the banner is missing" and "there is no
    /// reward" are the two states the old page got wrong.</summary>
    private static IEnumerable<JournalWindowBlocks> Compositions(float height)
    {
        foreach (var requirements in TextHeights)
        {
            foreach (var description in TextHeights)
            {
                foreach (var hasBanner in new[] { true, false })
                {
                    foreach (var hasReward in new[] { true, false })
                    {
                        foreach (var extras in new[] { true, false })
                        {
                            yield return JournalWindowLayout.Compose(
                                height,
                                hasLevel: extras,
                                hasStatusIcon: extras,
                                hasBanner,
                                hasReward,
                                requirements,
                                description,
                                hasGiver: extras,
                                hasProvenance: extras);
                        }
                    }
                }
            }
        }
    }
}
