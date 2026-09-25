using Wayfarer.Modules.Hunting;

namespace Wayfarer.Tests;

/// <summary>The shipped file of where hunted monsters have been seen.</summary>
public class MonsterPositionsFileTests
{
    private static readonly MonsterPositionsFile Shipped = MonsterPositionsFile.Parse(File.ReadAllText(MonsterPositionsFile.FileName));

    [Fact]
    public void Most_hunted_monsters_have_been_seen_somewhere()
    {
        Assert.True(Shipped.Monsters.Count >= 900);
    }

    /// <summary>Reported in game: flying high toward the vinegaroons in the Dravanian Forelands,
    /// the guide knew nothing of their height and said nothing of down. Their reports put the
    /// western ones on the valley floor, at about -10.</summary>
    [Fact]
    public void A_reported_spot_carries_its_height()
    {
        const uint Vinegaroon = 3581;
        const uint DravanianForelands = 398;
        var west = Shipped.Monsters[Vinegaroon].Where(spot => spot.Territory == DravanianForelands && spot.X < -400f).ToList();

        Assert.NotEmpty(west);
        Assert.All(west, spot => Assert.InRange(spot.Y, -20f, 0f));
    }

    [Fact]
    public void A_spot_written_without_a_height_reads_back_as_unknown_not_as_the_ground()
    {
        var read = MonsterPositionsFile.Parse("""{"Monsters":{"49":[{"Territory":140,"Map":20,"X":276,"Z":126}]}}""");

        Assert.True(float.IsNaN(read.Monsters[49][0].Y));
    }

    [Fact]
    public void Gigas_bhikkhu_is_placed_where_it_stands_not_at_its_label()
    {
        // Reported in game: the mark bill sent the player to North Silvertear's label on the Mor
        // Dhona map, at the map's edge, with none there. The label is at world X 682.
        var inMorDhona = Shipped.Monsters[649].Where(spot => spot.Territory == 156).ToList();

        Assert.NotEmpty(inMorDhona);
        Assert.All(inMorDhona, spot => Assert.True(spot.X < 650f));
    }
}
