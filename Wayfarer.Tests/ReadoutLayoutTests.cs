using System.Numerics;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>Where the readout is allowed to be. These exist because the shipped default put it
/// underneath the minimap on a 16:9 television and clipped the objective line clean off.</summary>
public class ReadoutLayoutTests
{
    private static readonly Vector2 Screen = new(1920f, 1080f);
    private static readonly Vector2 Size = new(320f, 140f);

    [Fact]
    public void Top_centre_is_horizontally_centred_and_inside_the_safe_margin()
    {
        var position = ReadoutLayout.Anchor(ReadoutPosition.TopCentre, Size, Screen);

        Assert.Equal((Screen.X - Size.X) / 2f, position.X, 0.01f);
        Assert.Equal(ReadoutLayout.SafeMarginY, position.Y, 0.01f);
    }

    [Fact]
    public void Bottom_centre_is_centred_and_clear_of_the_bottom_margin()
    {
        var position = ReadoutLayout.Anchor(ReadoutPosition.BottomCentre, Size, Screen);

        Assert.Equal((Screen.X - Size.X) / 2f, position.X, 0.01f);
        Assert.Equal(Screen.Y - Size.Y - ReadoutLayout.SafeMarginY, position.Y, 0.01f);
    }

    [Fact]
    public void Every_preset_stays_inside_the_ten_foot_safe_area()
    {
        foreach (var preset in Enum.GetValues<ReadoutPosition>())
        {
            var position = ReadoutLayout.Anchor(preset, Size, Screen);

            Assert.True(position.X >= ReadoutLayout.SafeMarginX, $"{preset} left");
            Assert.True(position.Y >= ReadoutLayout.SafeMarginY, $"{preset} top");
            Assert.True(position.X + Size.X <= Screen.X - ReadoutLayout.SafeMarginX, $"{preset} right");
            Assert.True(position.Y + Size.Y <= Screen.Y - ReadoutLayout.SafeMarginY, $"{preset} bottom");
        }
    }

    [Fact]
    public void A_position_off_the_edge_is_pulled_back_inside()
    {
        var clamped = ReadoutLayout.Clamp(new Vector2(5000f, -400f), Size, Screen);

        Assert.Equal(Screen.X - Size.X - ReadoutLayout.SafeMarginX, clamped.X, 0.01f);
        Assert.Equal(ReadoutLayout.SafeMarginY, clamped.Y, 0.01f);
    }

    [Fact]
    public void A_fraction_survives_a_resolution_change()
    {
        // Half way across a 4K screen is half way across a 720p one — not 2000 pixels off the edge
        // of it, which is what a stored pixel position would have been.
        var big = ReadoutLayout.FromFraction(new Vector2(0.5f, 0f), Size, new Vector2(3840f, 2160f));
        var small = ReadoutLayout.FromFraction(new Vector2(0.5f, 0f), Size, new Vector2(1280f, 720f));

        Assert.Equal(0.5f, ReadoutLayout.ToFraction(big, Size, new Vector2(3840f, 2160f)).X, 0.001f);
        Assert.Equal(0.5f, ReadoutLayout.ToFraction(small, Size, new Vector2(1280f, 720f)).X, 0.001f);
        Assert.True(small.X + Size.X <= 1280f - ReadoutLayout.SafeMarginX);
    }

    [Fact]
    public void A_fraction_round_trips_through_pixels()
    {
        var fraction = new Vector2(0.37f, 0.82f);

        var position = ReadoutLayout.FromFraction(fraction, Size, Screen);
        var back = ReadoutLayout.ToFraction(position, Size, Screen);

        Assert.Equal(fraction.X, back.X, 0.001f);
        Assert.Equal(fraction.Y, back.Y, 0.001f);
    }

    [Fact]
    public void A_fraction_outside_the_range_is_clamped_rather_than_extrapolated()
    {
        var position = ReadoutLayout.FromFraction(new Vector2(4f, -3f), Size, Screen);

        Assert.Equal(ReadoutLayout.Clamp(position, Size, Screen), position);
    }

    [Fact]
    public void A_readout_larger_than_the_screen_collapses_rather_than_inverting()
    {
        var huge = new Vector2(4000f, 4000f);

        var position = ReadoutLayout.Clamp(new Vector2(-500f, -500f), huge, Screen);

        Assert.Equal(ReadoutLayout.SafeMarginX, position.X, 0.01f);
        Assert.Equal(ReadoutLayout.SafeMarginY, position.Y, 0.01f);
    }

    [Fact]
    public void The_readout_is_pushed_clear_of_the_minimap_rather_than_drawn_behind_it()
    {
        // The reported defect: the tracker-following default put the readout under the minimap and
        // the second line was unreadable.
        var minimap = new ScreenRect(1620f, 20f, 260f, 260f);
        var overlapping = new Vector2(1560f, 40f);

        var position = ReadoutLayout.Avoid(overlapping, Size, Screen, [minimap]);

        Assert.False(new ScreenRect(position, Size).Overlaps(minimap));
        Assert.True(position.Y >= minimap.Bottom);
    }

    [Fact]
    public void A_position_clear_of_everything_is_left_exactly_where_it_is()
    {
        var minimap = new ScreenRect(1620f, 20f, 260f, 260f);
        var clear = new Vector2(ReadoutLayout.SafeMarginX, ReadoutLayout.SafeMarginY);

        Assert.Equal(clear, ReadoutLayout.Avoid(clear, Size, Screen, [minimap]));
    }

    [Fact]
    public void Dodging_never_pushes_the_readout_off_screen()
    {
        // A HUD element covering the whole screen cannot be dodged; the readout stays somewhere
        // legal rather than being flung past the bottom edge.
        var everything = new ScreenRect(0f, 0f, Screen.X, Screen.Y);

        var position = ReadoutLayout.Avoid(new Vector2(400f, 400f), Size, Screen, [everything]);

        Assert.Equal(position, ReadoutLayout.Clamp(position, Size, Screen));
    }

    [Fact]
    public void Following_the_tracker_hangs_below_it_on_the_left_and_aligns_right_edges_on_the_right()
    {
        var left = new ScreenRect(100f, 200f, 300f, 180f);
        var right = new ScreenRect(1500f, 200f, 300f, 180f);

        var belowLeft = ReadoutLayout.FollowTracker(left, Size, Screen);
        var belowRight = ReadoutLayout.FollowTracker(right, Size, Screen);

        Assert.Equal(left.X, belowLeft.X, 0.01f);
        Assert.Equal(right.Right - Size.X, belowRight.X, 0.01f);
        Assert.True(belowLeft.Y > left.Bottom - 1f);
    }
}
