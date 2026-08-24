using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The journal page's arithmetic, asserted without a game attached — the same contract
/// <see cref="LayoutContainmentTests"/> holds the detail strip to, for the surface that replaced it.
///
/// <para>The page is bigger than the strip in every direction, which makes it easier to believe it
/// cannot overflow and harder to be sure. It carries a 376x120 piece of fixed-size art, a 52-pixel
/// tray of more fixed-size art, and a second column whose existence depends on the window's width —
/// three ways for a block to end up somewhere it was not measured to be.</para></summary>
public class JournalPageLayoutTests
{
    /// <summary>Every window width the plugin can produce, plus the pathological ones a resize can
    /// pass through on its way somewhere.</summary>
    public static TheoryData<float> Widths =>
    [
        1f, 40f, 120f, 240f, 320f, 460f, 507f, 606f, 640f, 730f, 760f, 1200f,
    ];

    /// <summary>Page heights: nothing, a squeezed window, the natural one, and a tall screen.
    /// </summary>
    private static float[] Heights =>
    [
        0f, 24f, 80f, 140f, 200f, JournalPageLayout.NaturalHeight, 400f, 620f, 900f,
    ];

    [Theory]
    [MemberData(nameof(Widths))]
    public void Every_page_block_stays_inside_the_pages_content_box(float width)
    {
        foreach (var height in Heights)
        {
            var box = JournalPageLayout.ContentBox(width, height);
            foreach (var blocks in EveryComposition(width, height))
            {
                foreach (var block in blocks.Blocks)
                {
                    Assert.True(block.ContainedBy(box), $"w={width} h={height}: {block} escapes {box}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Widths))]
    public void The_title_badge_and_kind_stay_inside_the_title_band(float width)
    {
        foreach (var height in Heights)
        {
            var band = JournalPageLayout.TitleBand(width, height);
            foreach (var blocks in EveryComposition(width, height))
            {
                foreach (var block in blocks.HeaderBlocks)
                {
                    Assert.True(block.ContainedBy(band), $"w={width} h={height}: {block} escapes {band}");
                }
            }
        }
    }

    [Fact]
    public void Nothing_is_drawn_over_the_action_row()
    {
        foreach (var height in Heights)
        {
            foreach (var blocks in EveryComposition(730f, height))
            {
                foreach (var block in blocks.Blocks.Where(block => !block.IsEmpty))
                {
                    Assert.False(
                        block.Overlaps(blocks.Actions),
                        $"h={height}: {block} is drawn over the buttons at {blocks.Actions}");
                }
            }
        }
    }

    /// <summary>The two columns never touch. This is the one geometric claim the strip never had to
    /// make, and the one the banner's fixed 376 could break: a column computed as "the rest of the
    /// box" is only correct if the first column really stopped at 376.</summary>
    [Fact]
    public void The_two_columns_never_overlap()
    {
        var blocks = Full(760f, 620f);
        Assert.True(JournalPageLayout.IsTwoColumn(760f, 620f), "the default window should be two columns");

        var art = new[] { blocks.Banner, blocks.RewardTray, blocks.RewardIcon, blocks.RewardName };
        var text = new[]
        {
            blocks.Status, blocks.Requirements, blocks.Description, blocks.Information,
            blocks.RequirementsLabel, blocks.DescriptionLabel, blocks.InformationLabel,
        };

        foreach (var left in art.Where(rect => !rect.IsEmpty))
        {
            foreach (var right in text.Where(rect => !rect.IsEmpty))
            {
                Assert.False(left.Overlaps(right), $"{left} overlaps {right}");
            }
        }
    }

    /// <summary>At the narrow end the page stacks rather than drawing a gutter and calling it a
    /// column — and the stack still has to say the same things in the same order.</summary>
    [Fact]
    public void A_narrow_window_stacks_into_one_column_with_the_status_line_leading()
    {
        Assert.False(JournalPageLayout.IsTwoColumn(460f, 900f));

        var blocks = Full(460f, 900f);
        Assert.False(blocks.Status.IsEmpty);
        Assert.False(blocks.Banner.IsEmpty);
        Assert.True(blocks.Status.Y < blocks.Banner.Y, "the picture was put above the sentence");

        // The banner and the tray are the section's two pieces of art and the game puts both at the
        // same x (JournalCanvas #4 and #31 are both at 18). Stacked or not, they line up.
        Assert.Equal(blocks.Banner.X, blocks.RewardTray.X, 1);
    }

    /// <summary>The banner is the block the design nominates to go first, because it is the only one
    /// that says nothing a player could not read elsewhere. A window too short for everything must
    /// therefore lose the picture before it loses a requirement.</summary>
    [Fact]
    public void A_short_page_loses_the_banner_before_it_loses_a_requirement()
    {
        // 242 is the height at which the content box is 120 tall: enough for the status line, the
        // requirements heading and a bullet under it, and nowhere near the banner's 120 plus its gap.
        var squeezed = Full(760f, 242f);
        Assert.True(squeezed.Banner.IsEmpty, "the banner survived a page that could not hold it");
        Assert.True(squeezed.RequirementLines > 0, "the requirements went before the banner did");

        // And the general form of the same claim, across every height the window can take: the
        // banner is never on screen at a height that could not also hold the requirements.
        foreach (var height in Heights)
        {
            var blocks = Full(760f, height);
            if (!blocks.Banner.IsEmpty)
            {
                Assert.True(blocks.RequirementLines > 0, $"h={height}: a banner with no requirements");
            }
        }
    }

    /// <summary>The page's whole reason for existing over the strip: it shows what is in the way and
    /// what you get at the same time, because they are in different columns.</summary>
    [Fact]
    public void A_locked_entry_shows_its_requirements_and_its_reward_together()
    {
        var blocks = Full(760f, 620f);

        Assert.True(blocks.RequirementLines > 0);
        Assert.False(blocks.RewardTray.IsEmpty);
        Assert.False(blocks.Banner.IsEmpty);
        Assert.False(blocks.Provenance.IsEmpty);
    }

    /// <summary>The banner is drawn at the size the game authors it — 376x120, measured across all
    /// 2,519 <c>Quest.Icon</c> rows and all 773 <c>ContentFinderCondition.Image</c> rows. An image
    /// node whose part rectangle does not match its art draws a band of nothing.</summary>
    [Fact]
    public void The_banner_is_drawn_at_its_authored_size()
    {
        var blocks = Full(760f, 620f);

        Assert.Equal(GameMetrics.Journal.BannerWidth, blocks.Banner.Width);
        Assert.Equal(GameMetrics.Journal.BannerHeight, blocks.Banner.Height);
    }

    /// <summary>The tray is the strip's tray, at the page's scale — same art, same 15-pixel inset,
    /// same 36x36 slot. Two copies of that arithmetic would be two places to fix.</summary>
    [Fact]
    public void The_reward_tray_is_the_same_object_the_strip_draws()
    {
        var page = Full(760f, 620f);
        var strip = DetailPaneLayout.Compose(
            width: 760f,
            height: DetailPaneLayout.NaturalHeight,
            hasStatusIcon: true,
            hasLevel: true,
            bodyLines: DetailPaneLayout.MaxBodyLines,
            requirementLines: 0,
            hasReward: true,
            hasFrom: true,
            hasProvenance: true);

        Assert.Equal(strip.RewardTray.Width, page.RewardTray.Width);
        Assert.Equal(strip.RewardTray.Height, page.RewardTray.Height);
        Assert.Equal(strip.RewardIcon.Width, page.RewardIcon.Width);
        Assert.Equal(page.RewardTray.X + GameMetrics.Journal.TrayInset, page.RewardIcon.X);
    }

    /// <summary>A reward with no picture still gets its tray and its name, and the name starts where
    /// the icon would have — so the absence cannot leave a hole. Half the reward kinds the game
    /// ships have no artwork anywhere.</summary>
    [Fact]
    public void A_reward_with_no_icon_keeps_its_tray_and_its_name()
    {
        var blocks = Full(760f, 620f);

        Assert.False(blocks.RewardTray.IsEmpty);
        Assert.False(blocks.RewardName.IsEmpty);
        Assert.Equal(
            blocks.RewardTray.X + GameMetrics.Journal.TrayInset,
            JournalTrayLayout.Name(blocks.RewardTray, default).X);
    }

    /// <summary>The page's natural height holds everything a fully populated entry has to say.
    /// </summary>
    [Fact]
    public void The_pages_natural_height_holds_a_full_entry()
    {
        var blocks = Full(760f, JournalPageLayout.NaturalHeight);

        Assert.False(blocks.Banner.IsEmpty);
        Assert.False(blocks.RewardTray.IsEmpty);
        Assert.False(blocks.Status.IsEmpty);
        Assert.False(blocks.Provenance.IsEmpty);
    }

    private static JournalPageBlocks Full(float width, float height) =>
        JournalPageLayout.Compose(
            width,
            height,
            hasLevel: true,
            hasBanner: true,
            hasStatusIcon: true,
            requirementLines: JournalPageLayout.MaxRequirementLines,
            hasReward: true,
            descriptionLines: JournalPageLayout.MaxDescriptionLines,
            informationLines: JournalPageLayout.MaxInformationLines,
            hasProvenance: true);

    /// <summary>Every shape an entry can take, because each optional block is a fixed-size rectangle
    /// in a box that may be smaller than it.</summary>
    private static IEnumerable<JournalPageBlocks> EveryComposition(float width, float height)
    {
        foreach (var hasLevel in new[] { true, false })
        {
            foreach (var hasBanner in new[] { true, false })
            {
                foreach (var hasReward in new[] { true, false })
                {
                    foreach (var requirements in new[] { 0, 1, JournalPageLayout.MaxRequirementLines })
                    {
                        foreach (var description in new[] { 0, 1, JournalPageLayout.MaxDescriptionLines })
                        {
                            yield return JournalPageLayout.Compose(
                                width,
                                height,
                                hasLevel,
                                hasBanner,
                                hasStatusIcon: true,
                                requirements,
                                hasReward,
                                description,
                                informationLines: JournalPageLayout.MaxInformationLines,
                                hasProvenance: true);
                        }
                    }
                }
            }
        }
    }
}
