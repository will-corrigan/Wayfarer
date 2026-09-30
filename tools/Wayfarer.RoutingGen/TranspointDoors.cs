using System.Globalization;
using System.Numerics;
using Lumina;
using Lumina.Excel.Sheets;
using Wayfarer.Routing;

namespace Wayfarer.RoutingGen;

/// <summary>Aethernets that are not aethernets: the Firmament's shards, which the aetheryte sheet
/// knows nothing of. Each is a talk script named for a transpoint whose arguments list, shard by
/// shard, the object you touch (<c>EOBJ01</c>…), the pop range you land on (<c>POPRANGE01</c>…) and
/// the place's name (<c>PLACENAME01</c>…). Every shard is a door to every other, one way, landing
/// on the other's pop range; the way back is the shard you land beside.</summary>
internal static class TranspointDoors
{
    private const string Transpoint = "Transpoint";

    public static List<DoorLink> Read(GameData game, MapSpace maps, ZoneLayouts layouts, IReadOnlySet<uint> routable)
    {
        var places = game.Excel.GetSheet<PlaceName>();
        var objectNames = game.Excel.GetSheet<EObjName>();
        var doors = new List<DoorLink>();
        foreach (var talk in game.Excel.GetSheet<CustomTalk>())
        {
            if (!talk.Name.ExtractText().Contains(Transpoint, StringComparison.Ordinal))
            {
                continue;
            }

            var args = talk.Script
                .Where(line => line.ScriptInstruction.ExtractText().Length > 0)
                .ToDictionary(line => line.ScriptInstruction.ExtractText(), line => line.ScriptArg, StringComparer.Ordinal);
            var shards = Enumerable.Range(1, 99)
                .Select(n => n.ToString("00", CultureInfo.InvariantCulture))
                .TakeWhile(n => args.ContainsKey("EOBJ" + n))
                .Where(n => args.ContainsKey("POPRANGE" + n) && args.ContainsKey("PLACENAME" + n))
                .Select(n => (Object: args["EOBJ" + n], Landing: args["POPRANGE" + n], Place: args["PLACENAME" + n]))
                .ToList();
            if (shards.Count < 2)
            {
                continue;
            }

            // The zone the shards stand in: whichever routable zone's layout places the first of them.
            var zone = routable.Order().FirstOrDefault(territory => layouts.Of(territory).Objects.ContainsKey(shards[0].Object));
            if (zone == 0)
            {
                Console.Error.WriteLine($"  {talk.Name.ExtractText()}: no routable zone places its shards");
                continue;
            }

            var layout = layouts.Of(zone);
            var asked = Shown(objectNames.GetRowOrDefault(shards[0].Object)?.Singular.ExtractText() ?? string.Empty);
            foreach (var from in shards)
            {
                foreach (var to in shards.Where(to => to != from))
                {
                    if (!layout.Objects.TryGetValue(from.Object, out var touched) || !layout.Spots.TryGetValue(to.Landing, out var landing))
                    {
                        continue;
                    }

                    doors.Add(new DoorLink(
                        $"Travel to {places.GetRowOrDefault(to.Place)?.Name.ExtractText()}",
                        Place(layout, zone, touched, maps),
                        Place(layout, zone, landing, maps),
                        OneWay: true,
                        Npc: asked.Length > 0 ? asked : null));
                }
            }

            Console.Error.WriteLine($"  {talk.Name.ExtractText()}: {shards.Count} shards in zone {zone}");
        }

        return doors;
    }

    private static Place Place(ZoneLayout layout, uint territory, Vector3 at, MapSpace maps)
    {
        var map = layout.MapAt(at) ?? maps.MapOf(territory);
        return new Place(territory, map, at.X, at.Y, at.Z);
    }

    private static string Shown(string name) => name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
}
