using System.Numerics;
using Wayfarer.Modules.Treasure;

namespace Wayfarer.Tests;

/// <summary>Telling a coffer on one of its spots from treasure standing anywhere else.</summary>
public class TreasureMatchTests
{
    private static readonly Vector3 Spot = new(771f, 108f, -144f);

    [Fact]
    public void A_chest_on_a_spot_fills_it()
    {
        Assert.True(TreasureMatch.Holds(Spot, [new Vector3(772f, 108.5f, -143f)]));
    }

    [Fact]
    public void A_chest_a_few_yalms_off_does_not_fill_it()
    {
        Assert.False(TreasureMatch.Holds(Spot, [new Vector3(771f, 108f, -150f)]));
    }

    [Fact]
    public void Height_does_not_matter_only_the_ground()
    {
        Assert.True(TreasureMatch.Holds(Spot, [new Vector3(771f, 90f, -144f)]));
    }

    [Fact]
    public void No_chests_fill_nothing()
    {
        Assert.False(TreasureMatch.Holds(Spot, []));
    }

    [Fact]
    public void Chests_off_every_spot_are_named_by_where_they_are_in_the_list()
    {
        var chests = new[] { new Vector3(0f, 0f, 0f), Spot, new Vector3(500f, 0f, 500f) };

        Assert.Equal([0, 2], TreasureMatch.OffSpots(chests, [Spot]));
    }

    [Fact]
    public void With_no_spots_every_chest_is_off_them()
    {
        var chests = new[] { Spot, new Vector3(1f, 2f, 3f) };

        Assert.Equal([0, 1], TreasureMatch.OffSpots(chests, []));
    }
}
