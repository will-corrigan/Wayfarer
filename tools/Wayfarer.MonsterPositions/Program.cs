// Writes data/monster-positions.json: where every monster a mark bill or hunting log asks for has
// been seen, by name id, as world positions across the ground.
//
// No game file places ordinary monsters, so this is built from two community sources, both MIT:
//   - Hunty's hunting log data (Hunty/monsters.json): each log monster's spots, curated by hand.
//   - FFXIV Teamcraft's monster data (libs/data/src/lib/json/monsters.json): spots players report.
// The game's sheets give the map each spot's coordinates are on, and turn map coordinates into
// world positions.
//
// Local, developer-only: run again when either source changes.

using System.Text.Json;
using Lumina;
using Lumina.Excel.Sheets;
using Wayfarer.Modules.Hunting;

if (args.Length != 4)
{
    Console.Error.WriteLine("usage: Wayfarer.MonsterPositions <sqpack directory> <Hunty monsters.json> <Teamcraft monsters.json> <output file>");
    return 2;
}

var (sqpack, huntyFile, teamcraftFile, output) = (args[0], args[1], args[2], args[3]);
var game = new GameData(sqpack);
var maps = game.Excel.GetSheet<Map>();

// Every monster a hunt asks for, so nothing else is shipped.
var wanted = new HashSet<uint>();
foreach (var target in game.Excel.GetSheet<MobHuntTarget>())
{
    if (target.Name.RowId != 0)
    {
        wanted.Add(target.Name.RowId);
    }
}

foreach (var target in game.Excel.GetSheet<MonsterNoteTarget>())
{
    if (target.BNpcName.RowId != 0)
    {
        wanted.Add(target.BNpcName.RowId);
    }
}

var spots = new Dictionary<uint, List<MonsterSpot>>();

// A spot a few yalms from one already kept says nothing new; how many make a stretch of ground.
const float Apart = 8f;
const int MostPerMap = 24;

void Add(uint name, uint mapId, float mapX, float mapY, float height)
{
    if (!wanted.Contains(name) || maps.GetRowOrDefault(mapId) is not { } map || map.TerritoryType.RowId == 0)
    {
        return;
    }

    // The inverse of the game's own map coordinates: c = offset/50 + 2048/scale + world/50 + 1.
    var x = (50f * (mapX - 1f)) - (102400f / map.SizeFactor) - map.OffsetX;
    var z = (50f * (mapY - 1f)) - (102400f / map.SizeFactor) - map.OffsetY;
    if (!spots.TryGetValue(name, out var list))
    {
        spots[name] = list = [];
    }

    var onThisMap = list.Count(spot => spot.Map == mapId);
    var same = list.FindIndex(spot => spot.Map == mapId && MathF.Abs(spot.X - x) < Apart && MathF.Abs(spot.Z - z) < Apart);
    if (same >= 0)
    {
        // Hunty's spots come first and carry no height; a report of the same spot gives it one.
        if (float.IsNaN(list[same].Y) && !float.IsNaN(height))
        {
            list[same] = list[same] with { Y = height };
        }

        return;
    }

    if (onThisMap >= MostPerMap)
    {
        return;
    }

    list.Add(new MonsterSpot(map.TerritoryType.RowId, mapId, x, z, height));
}

var hunty = 0;
using (var doc = JsonDocument.Parse(File.ReadAllText(huntyFile)))
{
    foreach (var monster in Monsters(doc.RootElement))
    {
        var name = monster.GetProperty("Id").GetUInt32();
        foreach (var at in monster.GetProperty("Locations").EnumerateArray())
        {
            Add(name, at.GetProperty("Map").GetUInt32(), at.GetProperty("xCoord").GetSingle(), at.GetProperty("yCoord").GetSingle(), float.NaN);
            hunty++;
        }
    }
}

var teamcraft = 0;
var placeholders = 0;
using (var doc = JsonDocument.Parse(File.ReadAllText(teamcraftFile)))
{
    foreach (var monster in doc.RootElement.EnumerateObject())
    {
        if (!uint.TryParse(monster.Name, out var name) || !monster.Value.TryGetProperty("positions", out var positions))
        {
            continue;
        }

        foreach (var at in positions.EnumerateArray())
        {
            // A report at level nought is a placeholder, not a sighting: one put a vinegaroon in
            // the middle of the Dravanian Forelands, five hundred yalms underground.
            if (at.TryGetProperty("level", out var level) && level.GetInt32() == 0)
            {
                placeholders++;
                continue;
            }

            // Its height is the world height over a hundred, to one place: within five yalms.
            var height = at.TryGetProperty("z", out var reported) ? reported.GetSingle() * 100f : float.NaN;
            Add(name, at.GetProperty("map").GetUInt32(), at.GetProperty("x").GetSingle(), at.GetProperty("y").GetSingle(), height);
            teamcraft++;
        }
    }
}

var file = new MonsterPositionsFile(spots.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<MonsterSpot>)pair.Value));
File.WriteAllText(output, file.ToJson());
Console.Error.WriteLine($"{spots.Count} of {wanted.Count} hunted monsters placed ({spots.Values.Sum(list => list.Count)} spots, {spots.Values.Sum(list => list.Count(spot => !float.IsNaN(spot.Y)))} with a height, from {hunty} Hunty and {teamcraft} Teamcraft reports; {placeholders} placeholder reports left out) -> {output}");
return 0;

// Every monster in Hunty's file, wherever it nests them: under job ranks and Grand Company ranks.
static IEnumerable<JsonElement> Monsters(JsonElement element)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            if (element.TryGetProperty("Id", out _) && element.TryGetProperty("Locations", out _))
            {
                yield return element;
                yield break;
            }

            foreach (var property in element.EnumerateObject())
            {
                foreach (var found in Monsters(property.Value))
                {
                    yield return found;
                }
            }

            break;
        case JsonValueKind.Array:
            foreach (var item in element.EnumerateArray())
            {
                foreach (var found in Monsters(item))
                {
                    yield return found;
                }
            }

            break;
    }
}
