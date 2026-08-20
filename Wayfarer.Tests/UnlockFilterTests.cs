using System.Globalization;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

public class UnlockFilterTests
{
    [Theory]
    [InlineData("dungeon", false, "content")]
    [InlineData("trial", false, "content")]
    [InlineData("alliance-raid", false, "content")]
    [InlineData("mount", false, "cosmetic")]
    [InlineData("emote", false, "cosmetic")]
    [InlineData("system", true, "cosmetic")]
    [InlineData("system", false, "system")]
    [InlineData("zone", false, "zone")]
    public void Category_Mapping(string type, bool cosmetic, string expected)
    {
        Assert.Equal(expected, UnlockFilters.Category(new UnlockDefinition { Type = type, Cosmetic = cosmetic }));
    }

    [Fact]
    public void Matches_EmptyFilterShowsNonDone()
    {
        var f = new FilterState();
        Assert.True(UnlockFilters.Matches(U("A", "system"), f));
        Assert.False(UnlockFilters.Matches(U("A", "system", status: UnlockStatus.Done), f));
        f.ShowDone = true;
        Assert.True(UnlockFilters.Matches(U("A", "system", status: UnlockStatus.Done), f));
    }

    [Fact]
    public void Matches_CategoryPrioritySearch()
    {
        var f = new FilterState { Categories = ["cosmetic"], Priorities = ["essential"], Search = "glam" };
        Assert.True(UnlockFilters.Matches(U("Glamours", "system", "essential", cosmetic: true), f));
        Assert.False(UnlockFilters.Matches(U("Glamours", "system", "nice", cosmetic: true), f));   // wrong priority
        Assert.False(UnlockFilters.Matches(U("Glamours", "dungeon", "essential"), f));             // wrong category
        Assert.False(UnlockFilters.Matches(U("Materia", "system", "essential", cosmetic: true), f)); // search miss
    }

    [Fact]
    public void RouteOrder_CurrentZoneNearestFirst_ThenOtherZonesByLevel()
    {
        var here1 = U("H1", "system", territory: 132, x: 10, z: 0);
        var here2 = U("H2", "system", territory: 132, x: 100, z: 0);
        var far1 = U("F1", "system", territory: 130, x: 0, z: 0, level: 30);
        var far2 = U("F2", "system", territory: 129, x: 0, z: 0, level: 5);
        var noloc = U("N", "system");
        var route = RoutePlanner.Order([far1, here2, noloc, far2, here1], 132, 0f, 0f);
        var expected = new[] { "H1", "H2", "F2", "F1" };
        Assert.Equal(expected, route.Select(r => r.Def.Unlock).ToArray());
    }

    [Fact]
    public void RouteOrder_GreedyWithinZone()
    {
        var a = U("A", "system", territory: 132, x: 0, z: 100);
        var b = U("B", "system", territory: 132, x: 0, z: 10);
        var c = U("C", "system", territory: 132, x: 0, z: 50);
        var route = RoutePlanner.Order([a, b, c], 132, 0f, 0f);
        var expected = new[] { "B", "C", "A" };
        Assert.Equal(expected, route.Select(r => r.Def.Unlock).ToArray());
    }

    private static ResolvedUnlock U(
        string unlock,
        string type,
        string prio = "nice",
        bool cosmetic = false,
        uint? territory = null,
        float x = 0,
        float z = 0,
        int level = 1,
        UnlockStatus status = UnlockStatus.Available)
        => new()
        {
            Def = new UnlockDefinition { Unlock = unlock, Type = type, Priority = prio, Cosmetic = cosmetic },
            QuestRowId = 1,
            QuestLevel = level,
            GiverTerritory = territory,
            GiverX = x,
            GiverZ = z,
            ZoneName = territory?.ToString(CultureInfo.InvariantCulture),
            Status = status,
        };
}
