using Wayfarer.Routing;

namespace Wayfarer.Tests;

/// <summary>The shipped routing data holds together: it parses, it is big enough to be the real
/// game and not a stub, and nothing in it points nowhere. This is the validator CI runs on the
/// committed file, since CI has no game to regenerate it from.</summary>
public class RoutingGraphFileTests
{
    /// <summary>Well under the real counts, well over what a truncated or stub file would have.</summary>
    private const int FewestAetherytes = 100;

    private const int FewestShards = 80;
    private const int FewestDoors = 100;
    private const uint ThePillarsMap = 219;
    private const uint FortempsManorMap = 222;
    private const uint NewGridania = 132;
    private const uint CentralShroud = 148;
    private const uint OldGridania = 133;
    private const uint Yanxia = 614;
    private const uint DomanEnclave = 759;
    private const uint YanxiaMap = 354;
    private const uint DomanEnclaveMap = 463;

    private static readonly RoutingGraphFile Shipped = RoutingGraphFile.Parse(File.ReadAllText(RoutingGraphFile.FileName));

    [Fact]
    public void The_file_holds_the_whole_game()
    {
        Assert.True(Shipped.Nodes.Count(n => n.Kind == RouteNodeKind.Aetheryte) >= FewestAetherytes);
        Assert.True(Shipped.Nodes.Count(n => n.Kind == RouteNodeKind.Shard) >= FewestShards);
        Assert.True(Shipped.Doors.Count >= FewestDoors);
    }

    [Fact]
    public void Every_node_has_an_id_a_name_and_a_place()
    {
        Assert.Equal(Shipped.Nodes.Count, Shipped.Nodes.Select(n => n.Id).Distinct().Count());
        Assert.All(Shipped.Nodes, n =>
        {
            Assert.NotEqual(0u, n.Id);
            Assert.False(string.IsNullOrWhiteSpace(n.Name));
            Assert.NotEqual(0u, n.At.Territory);
            Assert.NotEqual(0u, n.At.Map);
        });
    }

    [Fact]
    public void Every_shard_is_on_a_network()
    {
        Assert.All(Shipped.Nodes.Where(n => n.Kind == RouteNodeKind.Shard), n => Assert.NotEqual(0u, n.Network));
    }

    [Fact]
    public void Every_landing_is_on_a_network()
    {
        Assert.All(Shipped.Nodes.Where(n => n.Kind == RouteNodeKind.Landing), n => Assert.NotEqual(0u, n.Network));
    }

    [Fact]
    public void The_aethernet_stops_outside_a_citys_gates_are_landings()
    {
        Assert.Equal(RouteNodeKind.Landing, Shipped.Nodes.Single(n => string.Equals(n.Name, "White Wolf Gate (Central Shroud)", StringComparison.Ordinal)).Kind);
        Assert.Equal(RouteNodeKind.Shard, Shipped.Nodes.Single(n => string.Equals(n.Name, "Mih Khetto's Amphitheatre", StringComparison.Ordinal)).Kind);
    }

    /// <summary>Storm on the Horizon: Yugiri, at Yanxia's mercantile docks, sends you to the
    /// skipper for the boat to the Doman Enclave, where Hien waits. The skipper's warp has no name
    /// of its own, only the question it asks, and a door once needed a name to exist.</summary>
    [Fact]
    public void The_skipper_at_yanxias_docks_rows_you_to_the_doman_enclave()
    {
        const uint SkipperWarp = 131293;
        const uint Skipper = 1024794;
        Assert.Contains(Shipped.Doors, door => door is { Warp: SkipperWarp, Person: Skipper, From.Territory: Yanxia, To.Territory: DomanEnclave });

        var byYugiri = new Place(Yanxia, YanxiaMap, -472f, float.NaN, 538f);
        var byHien = new Place(DomanEnclave, DomanEnclaveMap, 40f, float.NaN, 6f);
        var route = Shipped.ToGraph().FindRoute(byYugiri, [byHien], _ => false, _ => true);

        Assert.NotNull(route);
        Assert.Contains(route.Legs, leg => leg is Leg.Door { Npc: "Mercantile docks skipper" });
    }

    /// <summary>Standing at New Gridania's White Wolf Gate, heading for Old Gridania: the route once
    /// went out through the gatekeeper into Central Shroud to hop back in from a shard that is not
    /// there.</summary>
    [Fact]
    public void A_route_across_gridania_never_boards_the_aethernet_outside_its_gate()
    {
        var graph = Shipped.ToGraph();
        var atTheGate = new Place(NewGridania, 2, -114f, -7.4f, 97f);
        var byMihKhetto = new Place(OldGridania, 3, -60f, float.NaN, -130f);

        var route = graph.FindRoute(atTheGate, [byMihKhetto], _ => true, _ => true);

        Assert.NotNull(route);
        Assert.DoesNotContain(route.Legs, leg => leg is Leg.ShardHop { EntryShard: "White Wolf Gate (Central Shroud)" });
        Assert.DoesNotContain(route.Legs, leg => leg is Leg.Door { Npc: "Franchemontiaux" });
    }

    [Fact]
    public void Every_door_joins_two_different_maps()
    {
        Assert.All(Shipped.Doors, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Name));
            Assert.NotEqual(0u, d.From.Map);
            Assert.NotEqual(0u, d.To.Map);

            // A warp, whether someone offers it or a door object hides it, can move you across one
            // map: over a gap, up a ledge. Warps are one way. Only a door walked both ways has to
            // join two maps, since walking already joins one to itself.
            if (!d.OneWay)
            {
                Assert.NotEqual(d.From.Map, d.To.Map);
            }
        });
    }

    [Fact]
    public void The_lift_to_ul_dahs_airship_landing_is_a_door()
    {
        // Reported in game: the route went round through the Hustings Strip, past the lift.
        Assert.Contains(Shipped.Doors, d => string.Equals(d.Npc, "Lolomaya", StringComparison.Ordinal) && d.To.Territory == 130 && d.To.Map == 70);
    }

    [Fact]
    public void The_ruby_bazaar_offices_door_is_at_the_ruby_bazaar()
    {
        // Reported in game: its Kugane side was a copy of the interior's own coordinates, which
        // put it in the middle of town. The offices' exit lands east, at the Ruby Bazaar.
        Assert.Contains(Shipped.Doors, d => d.From.Territory == 639 && d.To.Territory == 628 && d.To.X > 100f);
        Assert.DoesNotContain(Shipped.Doors, d => d.From.Territory == 639 && d.To.Territory == 628 && d.To.X == 0f && d.To.Z == 13f);
    }

    [Fact]
    public void An_interior_named_only_by_a_label_on_the_city_map_has_a_door()
    {
        // Fortemps Manor draws no markers and no map link leads to it; the label on The Pillars is its door.
        var manor = Shipped.Doors.Single(d => d.To.Map == FortempsManorMap);

        Assert.Equal(ThePillarsMap, manor.From.Map);
        Assert.NotNull(Shipped.ToGraph().FindRoute(manor.From with { X = 0f, Z = 0f }, [manor.To], _ => true));
    }

    [Fact]
    public void No_door_between_zones_has_a_far_side_copied_from_its_near_side()
    {
        // A map that draws no marker back gave the far side nothing but the near side's own
        // coordinates on another zone: a spot that is not there, which a route back out walked to.
        Assert.DoesNotContain(Shipped.Doors, d => d.Npc is null && d.From.Territory != d.To.Territory && d.From.X == d.To.X && d.From.Z == d.To.Z);
    }

    [Fact]
    public void A_zone_exit_stands_on_the_ground_not_at_its_boxs_middle()
    {
        // The Central Shroud's exit to New Gridania is a box whose middle is at 63, while the path
        // beside it is at 25: the compass called a level walk "above". Its near side is where
        // coming back from New Gridania lands, which is on the path.
        var exit = Shipped.Doors.Single(d => d.Npc is null && d.From.Territory == CentralShroud && d.To.Territory == NewGridania);
        var comingBack = Shipped.Doors.Single(d => d.Npc is null && d.From.Territory == NewGridania && d.To.Territory == CentralShroud);

        Assert.InRange(exit.From.Y, comingBack.To.Y - 3f, comingBack.To.Y + 3f);
    }

    [Fact]
    public void An_unknown_height_is_read_back_as_unknown()
    {
        var unknown = new Place(CentralShroud, 4, 1f, float.NaN, 2f);
        var file = new RoutingGraphFile([], [new DoorLink("Somewhere", unknown, unknown with { Territory = NewGridania, Map = 2 }, OneWay: true)]);

        var read = RoutingGraphFile.Parse(file.ToJson());

        Assert.True(float.IsNaN(read.Doors[0].From.Y));
    }

    [Fact]
    public void The_file_builds_a_graph_that_can_teleport()
    {
        var graph = Shipped.ToGraph();
        var limsa = Shipped.Nodes.First(n => n.Kind == RouteNodeKind.Aetheryte && n.Name.StartsWith("Limsa", StringComparison.Ordinal));
        var gridania = Shipped.Nodes.First(n => n.Kind == RouteNodeKind.Aetheryte && n.Name.StartsWith("New Gridania", StringComparison.Ordinal));

        var route = graph.FindRoute(gridania.At, [limsa.At], _ => true);

        Assert.NotNull(route);
        Assert.Contains(route.Legs, leg => leg is Leg.Teleport t && t.AetheryteId == limsa.Id);
    }
}
