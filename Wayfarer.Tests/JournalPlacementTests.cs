using System.Numerics;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The journal page tracks the list it belongs beside — "can it be attached to the page like
/// the actual journal is attached to the quests?".
///
/// <para>There is no attachment to be had: an addon owns its own position and the game's own Journal
/// positions its detail page the same way, every frame. So what is asserted here is that the follow is
/// correct at any interface scale and never leaves the page somewhere it cannot be got back from —
/// this window is chromeless and has no title bar to drag.</para></summary>
public class JournalPlacementTests
{
    private static Vector2 Screen => new(2560f, 1440f);

    private static Vector2 PageSize => new(GameMetrics.JournalFrame.Width, 628f);

    [Fact]
    public void The_page_overlaps_the_lists_right_edge_by_the_game_s_own_offsets()
    {
        var host = new Vector2(400f, 300f);
        var size = new Vector2(462f, 700f);

        var at = JournalPlacement.Beside(host, size, PageSize, Screen, scale: 1f);

        Assert.Equal(host.X + size.X - GameMetrics.JournalFrame.BesideOverlapX, at.X, 3);
        Assert.Equal(host.Y - GameMetrics.JournalFrame.BesideOverlapY, at.Y, 3);
    }

    /// <summary>The defect this class exists to remove: the overlap offsets are authored in addon
    /// units and the position they are added to is in screen pixels, so they have to be scaled. Unscaled,
    /// the ornament that is meant to cross the seam between the two windows lands in the wrong place
    /// at every interface scale but 100%.</summary>
    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void The_overlap_is_in_screen_pixels_at_every_scale(float scale)
    {
        var host = new Vector2(300f, 400f);
        var size = new Vector2(600f, 800f);

        var at = JournalPlacement.Beside(host, size, PageSize, Screen, scale);

        Assert.Equal(
            host.X + size.X - (GameMetrics.JournalFrame.BesideOverlapX * scale), at.X, 3);
        Assert.Equal(host.Y - (GameMetrics.JournalFrame.BesideOverlapY * scale), at.Y, 3);
    }

    [Fact]
    public void The_page_moves_with_the_list()
    {
        var size = new Vector2(462f, 700f);
        var first = JournalPlacement.Beside(new Vector2(100f, 200f), size, PageSize, Screen, 1f);
        var second = JournalPlacement.Beside(new Vector2(340f, 260f), size, PageSize, Screen, 1f);

        Assert.Equal(new Vector2(240f, 60f), second - first);
    }

    [Fact]
    public void The_page_moves_when_the_list_is_only_resized()
    {
        var host = new Vector2(100f, 200f);
        var narrow = JournalPlacement.Beside(host, new Vector2(462f, 700f), PageSize, Screen, 1f);
        var wide = JournalPlacement.Beside(host, new Vector2(760f, 700f), PageSize, Screen, 1f);

        Assert.Equal(298f, wide.X - narrow.X, 3);
    }

    [Fact]
    public void A_page_beside_a_list_at_the_top_of_the_screen_stays_on_screen()
    {
        // The forty-pixel rise is what puts it off the top: a hub docked at y=0 would otherwise place
        // the page at y=-40, and this window has no title bar to drag it back with.
        var at = JournalPlacement.Beside(
            Vector2.Zero, new Vector2(462f, 700f), PageSize, Screen, scale: 1f);

        Assert.Equal(0f, at.Y, 3);
    }

    [Fact]
    public void A_page_beside_a_list_at_the_right_edge_stays_on_screen()
    {
        var at = JournalPlacement.Beside(
            new Vector2(2400f, 200f), new Vector2(462f, 700f), PageSize, Screen, scale: 1f);

        Assert.True(at.X + PageSize.X <= Screen.X, $"the page's right edge is at {at.X + PageSize.X}");
    }

    /// <summary>A page taller than the viewport is pinned to the top rather than pushed off it. The
    /// clamp's own degenerate case, and the one a 720p screen at 200% interface scale actually
    /// produces.</summary>
    [Fact]
    public void A_page_taller_than_the_screen_is_pinned_to_the_top()
    {
        var at = JournalPlacement.Beside(
            new Vector2(10f, 400f),
            new Vector2(462f, 700f),
            new Vector2(GameMetrics.JournalFrame.Width, 2000f),
            new Vector2(1280f, 720f),
            scale: 1f);

        Assert.Equal(0f, at.Y, 3);
    }

    /// <summary>No viewport yet — a resolution change is not atomic — is not a reason to refuse a
    /// position. The unclamped answer is right the moment the screen size arrives, and the follow runs
    /// every tick.</summary>
    [Fact]
    public void An_unknown_viewport_disables_the_clamp_rather_than_the_placement()
    {
        var at = JournalPlacement.Beside(
            new Vector2(10f, 20f), new Vector2(462f, 700f), PageSize, Vector2.Zero, scale: 1f);

        Assert.Equal(10f + 462f - GameMetrics.JournalFrame.BesideOverlapX, at.X, 3);
        Assert.Equal(20f - GameMetrics.JournalFrame.BesideOverlapY, at.Y, 3);
    }

    [Fact]
    public void A_zero_scale_is_treated_as_one_rather_than_collapsing_the_offset()
    {
        var at = JournalPlacement.Beside(
            new Vector2(100f, 200f), new Vector2(462f, 700f), PageSize, Screen, scale: 0f);

        Assert.Equal(200f - GameMetrics.JournalFrame.BesideOverlapY, at.Y, 3);
    }
}
