using System.Globalization;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

public class UnlockFilterTests
{
    /// <summary>The chip an entry answers to is read off its <c>channel</c> now, not its
    /// <c>type</c> — which is what lets a title be a Title instead of a cosmetic and a feature be a
    /// Capability instead of whatever was left. The <c>type</c> is deliberately set to something
    /// unhelpful in every case below, so a mapping that still consulted it would fail.</summary>
    [Theory]
    [InlineData("duty", UnlockDomains.Duties)]
    [InlineData("system", UnlockDomains.Capabilities)]
    [InlineData("title", UnlockDomains.Titles)]
    [InlineData("orchestrion", UnlockDomains.Collection)]
    [InlineData("emote", UnlockDomains.Collection)]
    [InlineData("gathering-folklore", UnlockDomains.Logs)]
    [InlineData("job", UnlockDomains.Jobs)]
    [InlineData("zone", UnlockDomains.Travel)]
    public void Domain_Mapping(string channel, string expected)
    {
        var def = new UnlockDefinition { Channel = channel, Type = "system", Cosmetic = true };
        Assert.Equal(expected, UnlockFilters.Domain(def));
    }

    /// <summary>A channel nothing claims has no domain, and says so rather than landing in a
    /// bucket. This is the assertion that the default-bucket behaviour is gone.</summary>
    [Fact]
    public void AnUnclaimedChannelHasNoDomainRatherThanADefaultOne()
    {
        Assert.Null(UnlockFilters.Domain(new UnlockDefinition { Channel = "hats", Type = "system" }));
        Assert.Null(UnlockFilters.Domain(new UnlockDefinition { Channel = string.Empty, Type = "dungeon" }));
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
    public void Matches_DomainPrioritySearch()
    {
        var f = new FilterState
        {
            Domains = [UnlockDomains.Capabilities],
            Priorities = ["essential"],
            Search = "glam",
        };

        Assert.True(UnlockFilters.Matches(U("Glamours", "system", "essential"), f));
        Assert.False(UnlockFilters.Matches(U("Glamours", "system", "nice"), f));      // wrong priority
        Assert.False(UnlockFilters.Matches(U("Glamours", "duty", "essential"), f));   // wrong domain
        Assert.False(UnlockFilters.Matches(U("Materia", "system", "essential"), f));  // search miss
    }

    /// <summary>Glamours is the case the four-bucket mapping got wrong and the reason the chips were
    /// replaced: it is a FEATURE, its <c>type</c> is <c>system</c>, and because the catalogue marks
    /// it cosmetic the old mapping filed it under the Cosmetics chip with 158 titles and every
    /// orchestrion roll. The channel puts it in Capabilities and the <c>cosmetic</c> flag no longer
    /// gets a vote.</summary>
    [Fact]
    public void ACosmeticFlaggedFeatureIsACapabilityRatherThanACosmetic()
    {
        var glamours = new UnlockDefinition
        {
            Unlock = "Glamours",
            Channel = "system",
            Type = "system",
            Cosmetic = true,
        };

        Assert.Equal(UnlockDomains.Capabilities, UnlockFilters.Domain(glamours));
        Assert.NotEqual(UnlockDomains.Collection, UnlockFilters.Domain(glamours), StringComparer.Ordinal);
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

    [Fact]
    public void TopAvailableHere_NearestFirstCappedAndZoneOnly()
    {
        var near = U("Near", "system", territory: 132, x: 10, z: 0);
        var mid = U("Mid", "system", territory: 132, x: 50, z: 0);
        var far = U("Far", "system", territory: 132, x: 200, z: 0);
        var otherZone = U("Other", "system", territory: 130, x: 0, z: 0);
        var notAvailable = U("Locked", "system", territory: 132, x: 5, z: 0, status: UnlockStatus.LevelLocked);
        var top = RoutePlanner.TopAvailableHere([far, mid, near, otherZone, notAvailable], 132, 0f, 0f, max: 2);
        var expected = new[] { "Near", "Mid" };
        Assert.Equal(expected, top.Select(r => r.Def.Unlock).ToArray());
    }

    // The ambient loop is the promise that anything the plugin points at is actually grabbable.
    // An entry whose requirements are unknown, or that is waiting on a set of collectibles, must
    // not raise the info bar's marker, add to its count, or take a line on the readout.
    [Theory]
    [InlineData(UnlockStatus.RequirementsUnknown)]
    [InlineData(UnlockStatus.CollectionLocked)]
    [InlineData(UnlockStatus.UnknownGate)]
    public void TopAvailableHere_ExcludesAnythingNotKnownToBeGrabbable(UnlockStatus status)
    {
        var grabbable = U("Grabbable", "system", territory: 132, x: 50, z: 0);
        var notSure = U("Not sure", "system", territory: 132, x: 1, z: 0, status: status);

        var top = RoutePlanner.TopAvailableHere([notSure, grabbable], 132, 0f, 0f, max: 3);

        var expected = new[] { "Grabbable" };
        Assert.Equal(expected, top.Select(r => r.Def.Unlock).ToArray());
    }

    [Fact]
    public void TopAvailableHere_FewerThanMaxReturnsAll()
    {
        var only = U("Only", "system", territory: 132, x: 10, z: 0);
        var top = RoutePlanner.TopAvailableHere([only], 132, 0f, 0f, max: 3);
        var expected = new[] { "Only" };
        Assert.Equal(expected, top.Select(r => r.Def.Unlock).ToArray());
    }

    /// <summary>An entry whose <c>channel</c> and <c>type</c> are the same string, which is true of
    /// <c>system</c>, <c>emote</c>, <c>mount</c>, <c>minion</c> and <c>zone</c> in the real
    /// catalogue. The filter reads the channel; the type is set alongside it so these fixtures still
    /// describe a shape the catalogue can actually produce.</summary>
    private static ResolvedUnlock U(
        string unlock,
        string channel,
        string prio = "nice",
        bool cosmetic = false,
        uint? territory = null,
        float x = 0,
        float z = 0,
        int level = 1,
        UnlockStatus status = UnlockStatus.Available)
        => new()
        {
            Def = new UnlockDefinition
            {
                Unlock = unlock,
                Type = channel,
                Channel = channel,
                Priority = prio,
                Cosmetic = cosmetic,
            },
            QuestRowId = 1,
            QuestLevel = level,
            GiverTerritory = territory,
            GiverX = x,
            GiverZ = z,
            ZoneName = territory?.ToString(CultureInfo.InvariantCulture),
            Status = status,
        };
}
