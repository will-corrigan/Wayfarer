using Wayfarer.Surfaces.DutyFinder;

namespace Wayfarer.Tests;

public class StripLayoutTests
{
    // The strip a Duty Finder row keeps for icons: three slots of 20, from 302 to 362.
    private const float First = 302f;
    private const float Second = 322f;
    private const float Third = 342f;
    private const float Pitch = 20f;
    private const float Smallest = 12f;

    private static readonly StripLayout.StripSpace Space = new(First, 362f, Pitch, Smallest);
    private static readonly float[] AllDark = [First, Second, Third];
    private static readonly float[] None = [];

    [Fact]
    public void A_mark_takes_a_slot_the_game_left_dark()
    {
        var strip = StripLayout.Place(1, AllDark, lit: 0, Space);

        Assert.Equal([First], strip.Marks);
        Assert.Empty(strip.GameIcons);
        Assert.Equal(Pitch, strip.Size);
        Assert.Null(strip.Left);
    }

    [Fact]
    public void While_there_is_room_nothing_of_the_games_is_touched_or_shrunk()
    {
        // The game is showing the middle icon; the outer two are free, and one is all we need.
        var strip = StripLayout.Place(1, [First, Third], lit: 1, Space);

        Assert.Equal([First], strip.Marks);
        Assert.Empty(strip.GameIcons);
        Assert.Equal(Pitch, strip.Size);
        Assert.Null(strip.Left);
    }

    [Fact]
    public void A_full_row_shrinks_every_symbol_rather_than_taking_from_the_name()
    {
        // Three lit and one mark wanted: four into the room three had, so all four shrink.
        var strip = StripLayout.Place(1, None, lit: 3, Space);

        Assert.Equal(15f, strip.Size);
        Assert.Null(strip.Left);
        Assert.Equal([First], strip.Marks);
        Assert.Equal([317f, 332f, 347f], strip.GameIcons);
    }

    [Fact]
    public void The_games_icons_shrink_with_ours_rather_than_ours_alone()
    {
        var strip = StripLayout.Place(2, None, lit: 3, Space);

        // Five into sixty is twelve, which is as small as a symbol may be drawn.
        Assert.Equal(Smallest, strip.Size);
        Assert.Equal(5, strip.Marks.Count + strip.GameIcons.Count);
        Assert.Null(strip.Left);
    }

    [Fact]
    public void Past_the_smallest_a_symbol_may_be_the_name_gives_up_the_room()
    {
        var strip = StripLayout.Place(3, None, lit: 3, Space);

        Assert.Equal(Smallest, strip.Size);
        Assert.Equal(362f - (6f * Smallest), strip.Left);
        Assert.True(strip.Left < First);
    }

    [Fact]
    public void Ours_sit_on_the_inside_and_the_games_keep_the_right()
    {
        var strip = StripLayout.Place(2, None, lit: 3, Space);

        Assert.True(strip.Marks.Max() < strip.GameIcons.Min());
    }

    [Fact]
    public void The_strip_always_keeps_its_right_edge()
    {
        foreach (var lit in new[] { 0, 1, 2, 3 })
        {
            var strip = StripLayout.Place(2, None, lit, Space);
            var all = strip.Marks.Concat(strip.GameIcons).ToList();

            Assert.Equal(362f - strip.Size, all.Max(), 3);
        }
    }

    [Fact]
    public void Every_icon_sits_one_size_from_the_next()
    {
        var strip = StripLayout.Place(2, None, lit: 3, Space);
        var all = strip.Marks.Concat(strip.GameIcons).ToList();

        for (var index = 1; index < all.Count; index++)
        {
            Assert.Equal(all[index - 1] + strip.Size, all[index], 3);
        }
    }

    [Fact]
    public void Nothing_wanted_asks_for_nothing_and_moves_nothing()
    {
        var strip = StripLayout.Place(0, None, lit: 3, Space);

        Assert.Empty(strip.Marks);
        Assert.Empty(strip.GameIcons);
        Assert.Null(strip.Left);
    }

    [Fact]
    public void A_row_cannot_be_laid_out_backwards_or_on_top_of_itself()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StripLayout.Place(-1, AllDark, 0, Space));
        Assert.Throws<ArgumentOutOfRangeException>(() => StripLayout.Place(1, AllDark, -1, Space));
        Assert.Throws<ArgumentNullException>(() => StripLayout.Place(1, null!, 0, Space));
        Assert.Throws<ArgumentNullException>(() => StripLayout.Place(1, AllDark, 0, null!));
    }
}
