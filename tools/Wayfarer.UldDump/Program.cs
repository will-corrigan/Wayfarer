using Lumina;
using Lumina.Data.Files;

// What art one of the game's own windows is built from. usage: ulddump <name> [name...]
if (args.Length == 0)
{
    Console.Error.WriteLine("usage: ulddump <uldName> [more...]");
    return 1;
}

var game = new GameData(Sqpack());
foreach (var name in args)
{
    var uld = game.GetFile<UldFile>($"ui/uld/{name}.uld");
    if (uld is null)
    {
        Console.WriteLine($"{name}: no such window");
        continue;
    }

    Console.WriteLine($"\n=== {name}  ({uld.AssetData.Length} pictures, {uld.Parts.Length} part lists, {uld.Components.Length} parts)");
    foreach (var asset in uld.AssetData)
    {
        var path = new string(asset.Path).TrimEnd('\0', ' ');
        if (path.Length > 0) Console.WriteLine($"  {path}");
    }

    var kinds = uld.Components.GroupBy(c => c.Type).OrderByDescending(g => g.Count());
    Console.WriteLine($"  parts used: {string.Join(", ", kinds.Select(g => $"{g.Key} x{g.Count()}"))}");
}

return 0;

static string Sqpack() =>
    Environment.GetEnvironmentVariable("SQPACK")
    ?? throw new InvalidOperationException("set SQPACK to the game sqpack folder (or a copy of ffxiv/0a0000.win32.*)");
