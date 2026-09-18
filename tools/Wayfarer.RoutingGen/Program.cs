// Writes data/routing-graph.json from the game's own sheets: every aetheryte and aethernet shard
// with its position and network, and every door between two maps.
//
// Local, developer-only: CI has no game installation, so it validates the committed file
// (RoutingGraphFileTests) and never regenerates it.
using Lumina;
using Wayfarer.Core.Routing;
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

File.WriteAllText(output, new RoutingGraphFile(nodes, doors).ToJson());
Console.Error.WriteLine($"{nodes.Count} nodes ({nodes.Count(n => n.Kind == RouteNodeKind.Aetheryte)} aetherytes), {doors.Count} doors -> {output}");
return 0;
