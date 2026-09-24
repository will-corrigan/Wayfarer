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

// Files are not cached: every zone's layouts are read once, and keeping them all runs out of memory.
var game = new GameData(sqpack, new LuminaOptions { CacheFileResources = false });
var maps = new MapSpace(game);
var nodes = AetheryteNodes.Read(game, maps);
var doors = DoorLinks.Read(game, maps);

// Doors taken by asking someone are read from the layouts of every zone the graph already reaches.
var routable = nodes.Select(node => node.At.Territory).Concat(doors.SelectMany(door => new[] { door.From.Territory, door.To.Territory })).Where(territory => territory != 0).ToHashSet();
doors.AddRange(WarpDoors.Read(game, maps, routable));

File.WriteAllText(output, new RoutingGraphFile(nodes, doors).ToJson());
Console.Error.WriteLine($"{nodes.Count} nodes ({nodes.Count(n => n.Kind == RouteNodeKind.Aetheryte)} aetherytes), {doors.Count} doors -> {output}");
return 0;
