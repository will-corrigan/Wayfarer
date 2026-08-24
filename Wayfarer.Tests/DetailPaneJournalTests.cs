using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The detail strip in the Journal's own vocabulary: a level on its disc, a section glyph
/// in each block's gutter, and a reward tray where a locked entry's requirements would be.
///
/// <para>These are the invariants that make the rebuild safe to ship rather than merely to look at.
/// The strip's job is unchanged and its height is unchanged — the reward block takes the slot the
/// requirements block leaves empty, so the list above it did not lose a row to this — and every
/// piece of fixed-size art has to disappear rather than hang out of a squeezed pane.</para>
///
/// <para>Every number asserted here is a <c>.uld</c> measurement; see
/// <see cref="GameMetrics.Journal"/> for the node each one was read from.</para></summary>
public class DetailPaneJournalTests
{
    /// <summary>The rule the whole block allocation turns on: locked entries show what is in the
    /// way, everything else shows what it gives you, and the pane never has to hold both. It is one
    /// <c>if</c>, and it is why the reward cost the pane no height.</summary>
    [Fact]
    public void A_locked_entry_shows_its_requirements_and_never_the_reward_too()
    {
        var locked = Compose(requirementLines: DetailPaneLayout.MaxRequirementLines, hasReward: true);

        Assert.False(locked.RequirementsLabel.IsEmpty);
        Assert.True(locked.RequirementLines > 0);
        Assert.True(locked.RewardTray.IsEmpty, "a locked entry drew its reward over what is blocking it");
        Assert.True(locked.RewardLabel.IsEmpty);
        Assert.True(locked.RewardGlyph.IsEmpty);
    }

    [Fact]
    public void An_unlocked_entry_shows_what_it_grants()
    {
        var open = Compose(requirementLines: 0, hasReward: true);

        Assert.True(open.RequirementsLabel.IsEmpty);
        Assert.False(open.RewardLabel.IsEmpty);
        Assert.False(open.RewardTray.IsEmpty);
        Assert.False(open.RewardIcon.IsEmpty);
        Assert.False(open.RewardName.IsEmpty);
    }

    /// <summary>The reward block is not in <see cref="DetailPaneLayout.NaturalHeight"/> and must
    /// not need to be: it only ever appears where the requirements pair does not, and it is the
    /// shorter of the two. If this ever inverts, the pane would silently need more room and the
    /// list above it would lose rows.</summary>
    [Fact]
    public void The_reward_block_is_never_taller_than_the_requirements_it_replaces()
    {
        var requirementsPair =
            GameMetrics.Detail.HeadingHeight + DetailPaneLayout.BlockHeight(DetailPaneLayout.MaxRequirementLines);

        var why = $"the reward block needs {DetailPaneLayout.RewardBlockHeight} where the requirements pair "
            + $"takes {requirementsPair}, so the pane's natural height no longer covers it";
        Assert.True(DetailPaneLayout.RewardBlockHeight <= requirementsPair, why);
    }

    /// <summary>The badge spans the title and status lines together, the way JournalDetail's does
    /// beside its two-line title — and both lines move right past it rather than one being drawn
    /// over it.</summary>
    [Fact]
    public void The_level_badge_spans_both_header_lines_and_pushes_them_right()
    {
        var withBadge = Compose(requirementLines: 0, hasReward: true, hasLevel: true);
        var without = Compose(requirementLines: 0, hasReward: true, hasLevel: false);

        Assert.False(withBadge.LevelBadge.IsEmpty);
        Assert.Equal(GameMetrics.Journal.BadgeSize, withBadge.LevelBadge.Width);
        Assert.Equal(GameMetrics.Journal.BadgeSize, withBadge.LevelBadge.Height);

        Assert.True(withBadge.LevelBadge.Y >= withBadge.Title.Y, "the badge starts above the title line");
        Assert.True(withBadge.LevelBadge.Bottom <= withBadge.Status.Bottom, "the badge hangs past the status line");

        Assert.False(withBadge.LevelBadge.Overlaps(withBadge.Title));
        Assert.False(withBadge.LevelBadge.Overlaps(withBadge.Status));
        Assert.False(withBadge.LevelBadge.Overlaps(withBadge.StatusIcon));

        Assert.True(withBadge.Title.X > without.Title.X, "the title did not move right for the badge");
        Assert.True(withBadge.Status.X > without.Status.X, "the status line did not move right for the badge");
    }

    /// <summary>An entry with no level gets no disc. Blank art reads as a failure to load; nothing
    /// at all reads as "this has no level requirement", which is the fact for the trophy mounts and
    /// the unique-reward sections.</summary>
    [Fact]
    public void An_entry_with_no_level_gets_no_badge_at_all()
    {
        Assert.True(Compose(requirementLines: 0, hasReward: true, hasLevel: false).LevelBadge.IsEmpty);
    }

    /// <summary>Half the reward kinds the game ships have no artwork anywhere — a title, an Aether
    /// Current, a folklore book. The tray and the name are drawn regardless and the name moves to
    /// the tray's own left inset, so there is not even a gap where the icon would have been. That is
    /// the difference between "here is what you get" and a slot that failed.</summary>
    [Fact]
    public void A_reward_with_no_icon_keeps_its_tray_and_its_name()
    {
        var blocks = Compose(requirementLines: 0, hasReward: true);

        // The layout always yields both rectangles; whether the icon is DRAWN is the node's choice,
        // and the name is placed against the tray rather than against the icon so that choice
        // cannot leave a hole.
        Assert.False(blocks.RewardTray.IsEmpty);
        Assert.False(blocks.RewardName.IsEmpty);
        Assert.True(
            blocks.RewardName.Right <= blocks.RewardTray.Right,
            "the reward name runs past the edge of the tray it is written on");
        Assert.True(blocks.RewardIcon.ContainedBy(blocks.RewardTray), "the reward icon escapes its tray");
        Assert.False(blocks.RewardIcon.Overlaps(blocks.RewardName));
    }

    /// <summary>The tray is drawn at the width the game authors it, and never wider. It is a plain
    /// image in the Journal, not a nine-grid — the game never stretches it — so a wide pane must
    /// not stretch it either.</summary>
    [Theory]
    [InlineData(460f)]
    [InlineData(760f)]
    [InlineData(1200f)]
    public void The_tray_is_never_drawn_wider_than_the_game_authors_it(float width)
    {
        var blocks = DetailPaneLayout.Compose(
            width,
            DetailPaneLayout.NaturalHeight,
            hasStatusIcon: true,
            hasLevel: true,
            bodyLines: DetailPaneLayout.MaxBodyLines,
            requirementLines: 0,
            hasReward: true,
            hasFrom: true,
            hasProvenance: true);

        Assert.True(blocks.RewardTray.Width <= GameMetrics.Journal.ColumnWidth, $"tray is {blocks.RewardTray.Width} wide");
    }

    /// <summary>Each block that has a section glyph gets one, in the gutter, at the size the game
    /// authors it — and the block's own text starts past it.</summary>
    [Fact]
    public void Each_section_carries_its_glyph_in_the_gutter()
    {
        var open = Compose(requirementLines: 0, hasReward: true);
        var locked = Compose(requirementLines: DetailPaneLayout.MaxRequirementLines, hasReward: false);

        foreach (var (glyph, text) in new[]
        {
            (open.BodyGlyph, open.Body),
            (open.RewardGlyph, open.RewardLabel),
            (locked.RequirementsGlyph, locked.RequirementsLabel),
        })
        {
            Assert.False(glyph.IsEmpty);
            Assert.Equal(GameMetrics.Journal.GlyphSize, glyph.Width);
            Assert.Equal(GameMetrics.Journal.GlyphSize, glyph.Height);
            Assert.Equal(GameMetrics.Journal.GlyphTextLeft, text.X - glyph.X);
        }
    }

    /// <summary>The journal metrics, pinned to the ULD values their doc comments cite. Not a range
    /// check: these are measurements, and a changed measurement is either a new reading of the game
    /// or a mistake — both of which should show up as a failing test rather than as a strip that
    /// quietly stopped matching the Journal.</summary>
    [Fact]
    public void The_journal_metrics_are_the_values_read_out_of_the_uld()
    {
        Assert.Equal(376f, GameMetrics.Journal.ColumnWidth);
        Assert.Equal(24f, GameMetrics.Journal.GlyphSize);
        Assert.Equal(22f, GameMetrics.Journal.GlyphTextLeft);
        Assert.Equal(52f, GameMetrics.Journal.TrayHeight);
        Assert.Equal(15f, GameMetrics.Journal.TrayInset);
        Assert.Equal(36f, GameMetrics.Journal.SlotIconSize);
        Assert.Equal(8f, GameMetrics.Journal.SlotIconTop);
        Assert.Equal(40f, GameMetrics.Journal.BadgeSize);
        Assert.Equal(20u, GameMetrics.Journal.BadgeTextSize);
    }

    /// <summary>The eight section glyphs are a row of 24x24 discs at v=0, 24 apart. Their offsets
    /// are what a crop samples, and an offset that is not a multiple of the glyph size would be
    /// drawing half of two of them.</summary>
    [Fact]
    public void The_section_glyph_crops_sit_on_the_texture_rows_own_grid()
    {
        foreach (var at in new[]
        {
            GameMetrics.JournalArt.GlyphDocument,
            GameMetrics.JournalArt.GlyphReward,
            GameMetrics.JournalArt.GlyphDescription,
        })
        {
            Assert.Equal(0f, at.V);
            Assert.Equal(0f, at.U % GameMetrics.Journal.GlyphSize);
        }

        // Three different glyphs, not one used three times.
        Assert.Equal(24f, GameMetrics.JournalArt.GlyphDocument.U);
        Assert.Equal(48f, GameMetrics.JournalArt.GlyphReward.U);
        Assert.Equal(72f, GameMetrics.JournalArt.GlyphDescription.U);
    }

    private static DetailPaneBlocks Compose(int requirementLines, bool hasReward, bool hasLevel = true) =>
        DetailPaneLayout.Compose(
            width: 760f,
            height: DetailPaneLayout.NaturalHeight,
            hasStatusIcon: true,
            hasLevel,
            bodyLines: DetailPaneLayout.MaxBodyLines,
            requirementLines,
            hasReward,
            hasFrom: true,
            hasProvenance: true);
}
