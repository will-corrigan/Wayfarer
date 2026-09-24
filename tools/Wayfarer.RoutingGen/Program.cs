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

// A map that draws a link into a duty's own map is not a door: nobody walks into a duty, the
// game puts them in when it starts. The link sat at the entrance and its far side was a guess.
var duties = game.Excel.GetSheet<Lumina.Excel.Sheets.TerritoryType>().Where(territory => territory.ContentFinderCondition.RowId != 0).Select(territory => territory.RowId).ToHashSet();
var intoDuties = doors.RemoveAll(door => duties.Contains(door.From.Territory) || duties.Contains(door.To.Territory));
Console.Error.WriteLine($"  {intoDuties} drawn links into duties left out");

// The layouts of every zone the graph reaches give its stops and doors their real heights, and
// hold the doors taken by asking someone.
var routable = nodes.Select(node => node.At.Territory).Concat(doors.SelectMany(door => new[] { door.From.Territory, door.To.Territory })).Where(territory => territory != 0).ToHashSet();
var layouts = new ZoneLayouts(game);

// Walking from zone to zone is read off the zones' exits, and going through a door, a lift or a
// ferry off the warps behind them, both ends placed. The drawn doors are kept only where neither
// goes.
var exits = ExitDoors.Read(game, maps, layouts, routable, doors);
var warped = WarpDoors.Read(game, maps, layouts, routable);
doors = ExitDoors.Unwalked(game, doors, [.. exits, .. warped]);
nodes = Heights.Nodes(game, layouts, nodes);
doors = Heights.Mirrored(game, layouts, maps, doors);
doors = Heights.Doors(layouts, doors);
doors.AddRange(exits);
doors.AddRange(warped);
Console.Error.WriteLine($"  read the layouts of {layouts.Count} zones, {layouts.Mirrored} files from the mirror");

File.WriteAllText(output, new RoutingGraphFile(nodes, doors).ToJson());
Console.Error.WriteLine($"{nodes.Count} nodes ({nodes.Count(n => n.Kind == RouteNodeKind.Aetheryte)} aetherytes), {doors.Count} doors -> {output}");
return 0;
