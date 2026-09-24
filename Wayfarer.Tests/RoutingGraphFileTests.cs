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
    public void Every_door_joins_two_different_maps()
    {
        Assert.All(Shipped.Doors, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Name));
            Assert.NotEqual(0u, d.From.Map);
            Assert.NotEqual(0u, d.To.Map);

            // A warp someone offers can move you across one map: over a gap, up a ledge. Only a
            // door walked through has to join two maps, since walking already joins one to itself.
            if (d.Npc is null)
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
    public void An_interior_named_only_by_a_label_on_the_city_map_has_a_door()
    {
        // Fortemps Manor draws no markers and no map link leads to it; the label on The Pillars is its door.
        var manor = Shipped.Doors.Single(d => d.To.Map == FortempsManorMap);

        Assert.Equal(ThePillarsMap, manor.From.Map);
        Assert.NotNull(Shipped.ToGraph().FindRoute(manor.From with { X = 0f, Z = 0f }, [manor.To], _ => true));
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
