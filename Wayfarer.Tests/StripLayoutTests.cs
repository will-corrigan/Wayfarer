using Wayfarer.Surfaces.DutyFinder;

namespace Wayfarer.Tests;

public class StripLayoutTests
{
    // The strip a Duty Finder row keeps for icons: three slots of 20, left to right.
    private const float Left = 302f;
    private const float Middle = 322f;
    private const float Right = 342f;
    private const float Pitch = 20f;

    private static readonly float[] AllDark = [Left, Middle, Right];
    private static readonly float[] None = [];

    [Fact]
    public void A_mark_takes_the_first_slot_the_game_left_dark()
    {
        var strip = StripLayout.Place(1, AllDark, Left, Pitch);

        Assert.Equal([Left], strip.Places);
        Assert.Equal(0f, strip.Overflow);
    }

    [Fact]
    public void Marks_fill_the_dark_slots_in_order()
    {
        var strip = StripLayout.Place(3, AllDark, Left, Pitch);

        Assert.Equal([Left, Middle, Right], strip.Places);
        Assert.Equal(0f, strip.Overflow);
    }

    [Fact]
    public void A_lit_slot_is_left_alone_and_marks_go_round_it()
    {
        // The game is showing the middle icon, so only the outer two are free.
        var strip = StripLayout.Place(2, [Left, Right], Left, Pitch);

        Assert.Equal([Left, Right], strip.Places);
        Assert.Equal(0f, strip.Overflow);
    }

    [Fact]
    public void More_marks_than_dark_slots_carry_on_left_of_the_strip()
    {
        var strip = StripLayout.Place(2, [Right], Left, Pitch);

        Assert.Equal([Right, Left - Pitch], strip.Places);
        Assert.Equal(Pitch, strip.Overflow);
    }

    [Fact]
    public void A_full_strip_puts_every_mark_left_of_it()
    {
        var strip = StripLayout.Place(2, None, Left, Pitch);

        Assert.Equal([Left - Pitch, Left - (2f * Pitch)], strip.Places);
        Assert.Equal(2f * Pitch, strip.Overflow);
    }

    [Fact]
    public void What_fits_asks_the_name_for_nothing()
    {
        Assert.Equal(0f, StripLayout.Place(0, AllDark, Left, Pitch).Overflow);
        Assert.Equal(0f, StripLayout.Place(3, AllDark, Left, Pitch).Overflow);
    }

    [Fact]
    public void Nothing_to_show_takes_no_room_at_all()
    {
        var strip = StripLayout.Place(0, AllDark, Left, Pitch);

        Assert.Empty(strip.Places);
        Assert.Equal(0f, strip.Overflow);
    }

    [Fact]
    public void Overflowing_marks_stay_one_pitch_apart()
    {
        var strip = StripLayout.Place(3, None, Left, Pitch);

        Assert.Equal(strip.Places[0] - Pitch, strip.Places[1]);
        Assert.Equal(strip.Places[1] - Pitch, strip.Places[2]);
    }

    [Fact]
    public void A_row_cannot_be_laid_out_backwards_or_on_top_of_itself()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StripLayout.Place(-1, AllDark, Left, Pitch));
        Assert.Throws<ArgumentOutOfRangeException>(() => StripLayout.Place(1, AllDark, Left, 0f));
        Assert.Throws<ArgumentNullException>(() => StripLayout.Place(1, null!, Left, Pitch));
    }
}
