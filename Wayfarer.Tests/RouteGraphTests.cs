using Wayfarer.Routing;

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

    [Fact]
    public void An_area_already_stood_in_beats_a_point_a_few_yalms_off()
    {
        // The shape every "search here" step really has: one wide circle to be somewhere in, and a
        // precise point beside it that the sheet repeats on every step of the quest.
        Place circle = new(Field, 1, 200f, 0f, 0f, 103f);
        Place point = new(Field, 1, 115f, 0f, 0f);

        var route = World().FindRoute(At(Field, 1, 100f), [circle, point], AllAttuned);

        var walk = Assert.IsType<Leg.Walk>(Assert.Single(route!.Legs));
        Assert.Equal(circle, walk.To);
        Assert.Equal(0f, walk.Yalms, 0.01f);
    }

    [Fact]
    public void An_area_not_yet_reached_costs_the_walk_to_its_edge()
    {
        Place circle = new(Field, 1, 200f, 0f, 0f, 20f);

        var route = World().FindRoute(At(Field, 1, 100f), [circle], AllAttuned);

        var walk = Assert.IsType<Leg.Walk>(Assert.Single(route!.Legs));
        Assert.Equal(80f, walk.Yalms, 0.01f);
    }

    [Fact]
    public void A_shard_on_the_floor_above_beats_walking_straight_up_to_it()
    {
        // The Gold Saucer is one map for every floor. From the ground-floor Nanamo, the one
        // upstairs is 39 yalms across and 21 up: the Wonder Square West shard stands on her floor.
        const uint saucer = 144;
        var entrance = new RouteNode(63, "Entrance & Card Squares", RouteNodeKind.Shard, 5, new Place(saucer, 196, -61.5f, 0f, 50.9f));
        var west = new RouteNode(65, "Wonder Square West", RouteNodeKind.Shard, 5, new Place(saucer, 196, 1.6f, 21f, 57f));
        var graph = new RouteGraph([entrance, west], []);

        var route = graph.FindRoute(new Place(saucer, 196, -51.7f, 0f, 53.9f), [new Place(saucer, 196, -12.8f, 21f, 47.9f)], AllAttuned);

        Assert.Contains(route!.Legs, leg => leg is Leg.ShardHop { ExitShard: "Wonder Square West" });
    }

    [Fact]
    public void A_lift_up_to_a_landing_beats_the_long_way_round()
    {
        // Ul'dah: the lift attendant on the Hustings Strip takes you straight up to the airship
        // landing, which otherwise is a long walk round through the city.
        var lift = new DoorLink("Ride Lift to the Airship Landing", At(Field, 1, 5f), At(Field, 9, 0f), OneWay: true, Npc: "Lolomaya");
        var stairs = new DoorLink("Hustings Strip", At(Field, 1, 400f), At(Field, 9, 400f));
        var graph = new RouteGraph([], [lift, stairs]);

        var route = graph.FindRoute(At(Field, 1, 0f), [At(Field, 9, 10f)], AllAttuned);

        Assert.Contains(route!.Legs, leg => leg is Leg.Door { Npc: "Lolomaya" });
    }

    [Fact]
    public void A_door_kept_until_a_quest_is_done_is_not_taken_before_it()
    {
        var airship = new DoorLink("Purchase Passage to Gridania", At(Field, 1, 5f), At(Field, 9, 0f), OneWay: true, Npc: "Elyenora", Quests: [66000u]);
        var stairs = new DoorLink("The long way", At(Field, 1, 400f), At(Field, 9, 400f));
        var graph = new RouteGraph([], [airship, stairs]);

        var before = graph.FindRoute(At(Field, 1, 0f), [At(Field, 9, 10f)], AllAttuned, questDone: _ => false);
        var after = graph.FindRoute(At(Field, 1, 0f), [At(Field, 9, 10f)], AllAttuned, questDone: quest => quest == 66000u);

        Assert.DoesNotContain(before!.Legs, leg => leg is Leg.Door { Npc: "Elyenora" });
        Assert.Contains(after!.Legs, leg => leg is Leg.Door { Npc: "Elyenora" });
    }

    /// <summary>A city's aethernet drops you just outside its gate, in the field, but there is no
    /// shard there to board: the aethernet goes out through it and never back in. White Wolf Gate
    /// (Central Shroud) once routed a player through the gate to hop back into the city.</summary>
    [Fact]
    public void A_landing_outside_the_gate_is_hopped_to_but_never_from()
    {
        var landing = new RouteNode(23, "Gate (Field)", RouteNodeKind.Landing, Network, new Place(Field, 1, 500f, 0f, 0f));
        var graph = new RouteGraph([FieldAetheryte, CityAetheryte, NearShard, FarShard, landing], []);

        // Standing on the landing, the far shard is only reached by teleporting in and hopping.
        var fromLanding = graph.FindRoute(At(Field, 1, 500f), [At(City, 3, 1020f)], AllAttuned);
        Assert.DoesNotContain(fromLanding!.Legs, leg => leg is Leg.ShardHop { EntryShard: "Gate (Field)" });

        // Beside the near shard, the landing is a hop away.
        var toLanding = graph.FindRoute(At(City, 2, 30f), [At(Field, 1, 510f)], AllAttuned);
        Assert.Contains(toLanding!.Legs, leg => leg is Leg.ShardHop { ExitShard: "Gate (Field)" });
    }

    /// <summary>A seasonal event's door, such as a Moonfire Faire boat, is there only while the
    /// event runs: it is neither left out of the graph nor taken the rest of the year.</summary>
    [Fact]
    public void A_seasonal_door_is_taken_only_while_its_event_runs()
    {
        const ushort Faire = 7;
        var boat = new DoorLink("Close Your Eyes", new Place(Field, 1, 10f, 0f, 0f), new Place(Field, 1, 4000f, 0f, 0f), OneWay: true, Npc: "Faire adventurer", Warp: 1, Person: 2, Festival: Faire);
        var graph = new RouteGraph([], [boat]);
        var from = At(Field, 1, 0f);
        Place[] across = [At(Field, 1, 4010f)];

        var during = graph.FindRoute(from, across, AllAttuned, festivalOn: (festival, _) => festival == Faire);
        var after = graph.FindRoute(from, across, AllAttuned, festivalOn: (_, _) => false);

        Assert.Contains(during!.Legs, leg => leg is Leg.Door { Warp: 1 });
        Assert.DoesNotContain(after!.Legs, leg => leg is Leg.Door);
    }

    /// <summary>Flying high over a monster 150 yalms off, the route once switched to teleporting to
    /// the zone's aetheryte and walking: every yalm of height was charged as stairs.</summary>
    [Fact]
    public void Height_costs_nothing_while_flying()
    {
        var graph = World();
        var high = new Place(Field, 1, 300f, 150f, 0f);
        Place[] monster = [new Place(Field, 1, 150f, 0f, 0f)];

        var onFoot = graph.FindRoute(high, monster, AllAttuned);
        var flying = graph.FindRoute(high, monster, AllAttuned, airborne: true);

        Assert.IsType<Leg.Teleport>(onFoot!.Legs[0]);
        var walk = Assert.IsType<Leg.Walk>(Assert.Single(flying!.Legs));
        Assert.Equal(150f, walk.Yalms, 0.01f);
    }

    /// <summary>Standing in a city where nobody flies, heading for a zone the player can fly in:
    /// the height there is flown, not climbed. Frost grenades from Ishgard once walked the whole
    /// way out through the Pillars, because the sky island's aetheryte stands high above the path
    /// down and the drop was charged as stairs.</summary>
    [Fact]
    public void Height_costs_nothing_in_a_zone_the_player_can_fly_in()
    {
        var graph = World();
        var low = new Place(Field, 1, 150f, -150f, 0f);

        var cannotFly = graph.FindRoute(At(City, 2, 0f), [low], AllAttuned);
        var canFly = graph.FindRoute(At(City, 2, 0f), [low], AllAttuned, flyable: territory => territory == Field);

        Assert.Equal(Leg.TeleportCost + 150f + (3f * 150f), cannotFly!.Cost, 0.01f);
        Assert.Equal(Leg.TeleportCost + 150f, canFly!.Cost, 0.01f);
    }

    [Fact]
    public void A_landing_is_never_teleported_to()
    {
        var landing = new RouteNode(23, "Gate (Field)", RouteNodeKind.Landing, 0, new Place(Field, 1, 5000f, 0f, 0f));
        var graph = new RouteGraph([FieldAetheryte, landing], []);

        var route = graph.FindRoute(At(Field, 1, 0f), [At(Field, 1, 5000f)], AllAttuned);

        Assert.DoesNotContain(route!.Legs, leg => leg is Leg.Teleport);
    }

    private static RouteGraph World() => new([FieldAetheryte, CityAetheryte, NearShard, FarShard], []);

    private static Place At(uint territory, uint map, float x) => new(territory, map, x, 0f, 0f);

    private static (string Entry, string Exit) Pair(Leg.ShardHop hop) => (hop.EntryShard, hop.ExitShard);
}
