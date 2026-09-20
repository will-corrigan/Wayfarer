using Wayfarer.Surfaces.DutyFinder;

namespace Wayfarer.Tests;

public class StripLayoutTests
{
    // The strip the Duty Finder keeps for a row's icons: three 20px slots.
    private const float Left = 302f;
    private const float Right = 362f;
    private const float Pitch = 20f;

    [Fact]
    public void The_games_own_icons_stay_against_the_right()
    {
        var strip = StripLayout.Place(1, Left, Right, Pitch);

        Assert.Equal(342f, strip.At(0));
        Assert.Equal(0f, strip.Overflow);
    }

    [Fact]
    public void Ours_takes_the_slot_the_game_left_dark()
    {
        // One game icon showing, one mark of ours: ours on the inside, the game's where it was.
        var strip = StripLayout.Place(2, Left, Right, Pitch);

        Assert.Equal(322f, strip.At(0));
        Assert.Equal(342f, strip.At(1));
        Assert.Equal(0f, strip.Overflow);
    }

    [Fact]
    public void A_full_strip_needs_nothing_from_the_name()
    {
        var strip = StripLayout.Place(3, Left, Right, Pitch);

        Assert.Equal(Left, strip.At(0));
        Assert.Equal(0f, strip.Overflow);
    }

    [Fact]
    public void More_icons_than_the_strip_holds_asks_the_name_for_the_difference()
    {
        var strip = StripLayout.Place(4, Left, Right, Pitch);

        Assert.Equal(282f, strip.At(0));
        Assert.Equal(Pitch, strip.Overflow);
    }

    [Fact]
    public void Two_over_asks_for_two_slots_worth()
    {
        var strip = StripLayout.Place(5, Left, Right, Pitch);

        Assert.Equal(262f, strip.At(0));
        Assert.Equal(2f * Pitch, strip.Overflow);
    }

    [Fact]
    public void Nothing_to_show_wants_nothing_and_asks_for_nothing()
    {
        var strip = StripLayout.Place(0, Left, Right, Pitch);

        Assert.Equal(0f, strip.Overflow);
    }

    [Fact]
    public void Icons_sit_one_pitch_apart()
    {
        var strip = StripLayout.Place(3, Left, Right, Pitch);

        Assert.Equal(strip.At(0) + Pitch, strip.At(1));
        Assert.Equal(strip.At(1) + Pitch, strip.At(2));
    }

    [Fact]
    public void A_row_cannot_be_laid_out_backwards_or_on_top_of_itself()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StripLayout.Place(-1, Left, Right, Pitch));
        Assert.Throws<ArgumentOutOfRangeException>(() => StripLayout.Place(1, Left, Right, 0f));
    }
}
