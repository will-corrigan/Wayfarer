using Wayfarer.Core.Routing;

namespace Wayfarer.Tests;

/// <summary>The routing graph, proved on hand-built worlds. Each test is one shape of journey the
/// old hand-written router either handled as a special case or could not express at all; the graph
/// has to produce the same answer for the first kind and a real answer for the second.</summary>
public class RouteGraphTests
{
    // Two zones. Zone 1 is a field with one aetheryte. Zone 2 is a split city on one aethernet
    // network: its main aetheryte and two shards, one of them across the city, like Ishgard's
    // Foundation and Pillars.
    private const uint Field = 1;
    private const uint City = 2;
    private const uint Network = 7;

    private static readonly RouteNode FieldAetheryte = new(10, "Field Aetheryte", RouteNodeKind.Aetheryte, 0, new Place(Field, 1, 0f, 0f, 0f));
    private static readonly RouteNode CityAetheryte = new(20, "City Aetheryte", RouteNodeKind.Aetheryte, Network, new Place(City, 2, 0f, 0f, 0f));
    private static readonly RouteNode NearShard = new(21, "Near Shard", RouteNodeKind.Shard, Network, new Place(City, 2, 30f, 0f, 0f));
    private static readonly RouteNode FarShard = new(22, "Far Shard", RouteNodeKind.Shard, Network, new Place(City, 3, 1000f, 0f, 0f));

    private static readonly Func<uint, bool> AllAttuned = _ => true;

    [Fact]
    public void Standing_on_the_same_map_is_a_single_walk()
    {
        var graph = World();

        var route = graph.FindRoute(At(Field, 1, 0f), [At(Field, 1, 250f)], AllAttuned);

        var walk = Assert.IsType<Leg.Walk>(Assert.Single(route!.Legs));
        Assert.Equal(250f, walk.Yalms, 0.01f);
        Assert.Equal(250f, route.Cost, 0.01f);
    }

    [Fact]
    public void A_far_walk_loses_to_a_teleport_that_lands_close()
    {
        var graph = World();

        // 5,000 yalms away on foot; the field's aetheryte is 40 from the target.
        var route = graph.FindRoute(At(Field, 1, -5000f), [At(Field, 1, 40f)], AllAttuned);

        Assert.Collection(
            route!.Legs,
            leg => Assert.Equal(10u, Assert.IsType<Leg.Teleport>(leg).AetheryteId),
            leg => Assert.Equal(40f, Assert.IsType<Leg.Walk>(leg).Yalms, 0.01f));
        Assert.Equal(Leg.TeleportCost + 40f, route.Cost, 0.01f);
    }

    [Fact]
    public void An_unattuned_aetheryte_is_not_a_route_at_all()
    {
        var graph = World();

        var route = graph.FindRoute(At(Field, 1, -5000f), [At(Field, 1, 40f)], _ => false);

        var walk = Assert.IsType<Leg.Walk>(Assert.Single(route!.Legs));
        Assert.Equal(5040f, walk.Yalms, 0.01f);
    }

    [Fact]
    public void Another_zone_is_reached_by_teleporting_then_walking()
    {
        var graph = World();

        var route = graph.FindRoute(At(Field, 1, 0f), [At(City, 2, 10f)], AllAttuned);

        Assert.Collection(
            route!.Legs,
            leg => Assert.Equal("City Aetheryte", Assert.IsType<Leg.Teleport>(leg).AetheryteName),
            leg => Assert.Equal(10f, Assert.IsType<Leg.Walk>(leg).Yalms, 0.01f));
    }

    [Fact]
    public void A_shard_hop_beats_a_long_walk_across_the_city()
    {
        var graph = World();

        // Standing beside the near shard; the target is 20 past the far shard, on the far map.
        var route = graph.FindRoute(At(City, 2, 25f), [At(City, 3, 1020f)], AllAttuned);

        Assert.Collection(
            route!.Legs,
            leg => Assert.Equal(5f, Assert.IsType<Leg.Walk>(leg).Yalms, 0.01f),
            leg => Assert.Equal(("Near Shard", "Far Shard"), Pair(Assert.IsType<Leg.ShardHop>(leg))),
            leg => Assert.Equal(20f, Assert.IsType<Leg.Walk>(leg).Yalms, 0.01f));
        Assert.Equal(5f + Leg.ShardHopCost + 20f, route.Cost, 0.01f);
    }

    /// <summary>The journey the old router could not express: teleport into a city, hop its
    /// aethernet to the far half, then walk. Ishgard's shape.</summary>
    [Fact]
    public void Teleport_then_shard_then_walk_composes_on_its_own()
    {
        var graph = World();

        var route = graph.FindRoute(At(Field, 1, 0f), [At(City, 3, 1020f)], AllAttuned);

        Assert.Collection(
            route!.Legs,
            leg => Assert.Equal("City Aetheryte", Assert.IsType<Leg.Teleport>(leg).AetheryteName),
            leg => Assert.Equal(("City Aetheryte", "Far Shard"), Pair(Assert.IsType<Leg.ShardHop>(leg))),
            leg => Assert.Equal(20f, Assert.IsType<Leg.Walk>(leg).Yalms, 0.01f));
        Assert.Equal(Leg.TeleportCost + Leg.ShardHopCost + 20f, route.Cost, 0.01f);
    }

    [Fact]
    public void A_hop_needs_both_shards_attuned()
    {
        var graph = World();

        // Everything attuned except the far shard: the hop is closed, so it is a teleport and a
        // long walk instead — and since the far map has no door, that walk cannot exist either.
        var route = graph.FindRoute(At(Field, 1, 0f), [At(City, 3, 1020f)], id => id != 22);

        Assert.Null(route);
    }

    [Fact]
    public void A_door_joins_two_maps_of_one_zone()
    {
        var door = new DoorLink("Manor Gate", new Place(Field, 1, 100f, 0f, 0f), new Place(Field, 9, 0f, 0f, 0f));
        var graph = new RouteGraph([FieldAetheryte], [door]);

        var route = graph.FindRoute(At(Field, 1, 90f), [At(Field, 9, 15f)], AllAttuned);

        Assert.Collection(
            route!.Legs,
            leg => Assert.Equal(10f, Assert.IsType<Leg.Walk>(leg).Yalms, 0.01f),
            leg => Assert.Equal("Manor Gate", Assert.IsType<Leg.Door>(leg).Name),
            leg => Assert.Equal(15f, Assert.IsType<Leg.Walk>(leg).Yalms, 0.01f));
    }

    [Fact]
    public void A_one_way_door_cannot_be_walked_back_through()
    {
        // A ledge: drop from the upper map to the lower one, never the other way. No aetheryte in
        // this world, so the ledge is the only link and the climb back has no route at all.
        var ledge = new DoorLink("Ledge", new Place(Field, 1, 100f, 0f, 0f), new Place(Field, 9, 0f, 0f, 0f), OneWay: true);
        var graph = new RouteGraph([], [ledge]);

        var down = graph.FindRoute(At(Field, 1, 90f), [At(Field, 9, 15f)], AllAttuned);
        var up = graph.FindRoute(At(Field, 9, 15f), [At(Field, 1, 90f)], AllAttuned);

        Assert.Equal("Ledge", Assert.IsType<Leg.Door>(down!.Legs[1]).Name);
        Assert.Null(up);
    }

    [Fact]
    public void Among_several_places_the_cheapest_to_reach_wins()
    {
        var graph = World();

        // One place 300 away on foot; another in the city 10 from its aetheryte, costing 130.
        var near = At(Field, 1, 300f);
        var acrossTheWorld = At(City, 2, 10f);

        var route = graph.FindRoute(At(Field, 1, 0f), [near, acrossTheWorld], AllAttuned);

        Assert.Equal(acrossTheWorld, route!.End);
        Assert.Equal(Leg.TeleportCost + 10f, route.Cost, 0.01f);
    }

    [Fact]
    public void Somewhere_with_no_way_in_is_null()
    {
        var graph = World();

        var route = graph.FindRoute(At(Field, 1, 0f), [At(99, 1, 0f)], AllAttuned);

        Assert.Null(route);
    }

    [Fact]
    public void No_places_offered_is_null()
    {
        Assert.Null(World().FindRoute(At(Field, 1, 0f), [], AllAttuned));
    }

    [Fact]
    public void Already_there_is_a_walk_of_nothing()
    {
        var route = World().FindRoute(At(Field, 1, 0f), [At(Field, 1, 0f)], AllAttuned);

        var walk = Assert.IsType<Leg.Walk>(Assert.Single(route!.Legs));
        Assert.Equal(0f, walk.Yalms, 0.01f);
    }

    private static RouteGraph World() => new([FieldAetheryte, CityAetheryte, NearShard, FarShard], []);

    private static Place At(uint territory, uint map, float x) => new(territory, map, x, 0f, 0f);

    private static (string Entry, string Exit) Pair(Leg.ShardHop hop) => (hop.EntryShard, hop.ExitShard);
}
