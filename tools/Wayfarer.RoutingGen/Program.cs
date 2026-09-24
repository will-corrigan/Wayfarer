// Writes data/routing-graph.json from the game's own sheets: every aetheryte and aethernet shard
// with its position and network, and every door between two maps.
//
// Local, developer-only: CI has no game installation, so it validates the committed file
// (RoutingGraphFileTests) and never regenerates it.
using Lumina;
using Wayfarer.Routing;
using Wayfarer.RoutingGen;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: Wayfarer.RoutingGen <sqpack directory> <output file>");
    return 2;
}

var (sqpack, output) = (args[0], args[1]);
if (!Directory.Exists(sqpack))
{
    Console.Error.WriteLine($"sqpack directory not found: '{sqpack}'");
    return 3;
}

var game = new GameData(sqpack);
var maps = new MapSpace(game);
var nodes = AetheryteNodes.Read(game, maps);
var doors = DoorLinks.Read(game, maps);

// The layouts of every zone the graph reaches give its stops and doors their real heights, and
// hold the doors taken by asking someone.
var routable = nodes.Select(node => node.At.Territory).Concat(doors.SelectMany(door => new[] { door.From.Territory, door.To.Territory })).Where(territory => territory != 0).ToHashSet();
var layouts = new ZoneLayouts(game);
nodes = Heights.Nodes(game, layouts, nodes);
doors = Heights.Mirrored(game, layouts, maps, doors);
doors = Heights.Doors(layouts, doors);
doors.AddRange(WarpDoors.Read(game, maps, layouts, routable));
Console.Error.WriteLine($"  read the layouts of {layouts.Count} zones");

File.WriteAllText(output, new RoutingGraphFile(nodes, doors).ToJson());
Console.Error.WriteLine($"{nodes.Count} nodes ({nodes.Count(n => n.Kind == RouteNodeKind.Aetheryte)} aetherytes), {doors.Count} doors -> {output}");
return 0;
