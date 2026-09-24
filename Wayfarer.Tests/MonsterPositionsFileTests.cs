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
