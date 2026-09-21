using Lumina;
using Wayfarer.Mockup;

// Draws a window of Wayfarer's as a picture, out of the game's own art, faces and colours, so how
// it reads can be settled here rather than by loading the game to look at it.
// usage: mockup --bench [port]            the design bench, in a browser
//        mockup [--boxes] [out.png]       the settings window as it stands
//        mockup --icons <first> <count> [out.png]
//        mockup --find                    what the game's windows are made of

var game = new GameData(
    Environment.GetEnvironmentVariable("SQPACK")
    ?? throw new InvalidOperationException("set SQPACK to the game sqpack folder"));

var loose = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();

if (args.Contains("--icons", StringComparer.Ordinal))
{
    var file = loose.Length > 2 ? loose[2] : "icons.png";
    var sheet = IconSheet.Draw(game, uint.Parse(loose[0]), int.Parse(loose[1]));
    sheet.Save(file);
    Console.WriteLine($"{sheet.Width}x{sheet.Height} -> {file}");
    return;
}

// Where a picture's art actually starts, as against where its edge is. A picture with nothing
// drawn near its edge leaves a margin wherever it is put, which is not the window's fault.
if (args.Contains("--colours", StringComparer.Ordinal))
{
    var sheet = game.GetExcelSheet<Lumina.Excel.Sheets.UIColor>()!;
    Console.WriteLine($"{sheet.Count} rows");
    foreach (var row in sheet.Take(12))
    {
        Console.WriteLine($"  {row.RowId,4}  dark {row.Dark:x8}  light {row.Light:x8}  classic {row.ClassicFF:x8}");
    }

    foreach (var id in new uint[] { 8, 7, 1, 53, 500, 501, 502, 503, 504 })
    {
        var row = sheet.GetRow(id);
        Console.WriteLine($"  {id,4}  dark {row.Dark:x8}  light {row.Light:x8}");
    }

    return;
}

if (args.Contains("--edges", StringComparer.Ordinal))
{
    var art = Picture.From(game, loose[0]);
    int left = art.Width, top = art.Height, right = 0, bottom = 0;
    for (var y = 0; y < art.Height; y++)
    {
        for (var x = 0; x < art.Width; x++)
        {
            if (art.Pixels[(((y * art.Width) + x) * 4) + 3] > 8)
            {
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }
    }

    Console.WriteLine($"{loose[0]}  {art.Width}x{art.Height}");
    Console.WriteLine($"  art from ({left},{top}) to ({right},{bottom})");
    Console.WriteLine($"  clear margin: left {left}, top {top}, right {art.Width - 1 - right}, bottom {art.Height - 1 - bottom}");
    Console.WriteLine($"  as shares: left {left / (float)art.Width:0.000}, top {top / (float)art.Height:0.000}, right {(art.Width - 1 - right) / (float)art.Width:0.000}, bottom {(art.Height - 1 - bottom) / (float)art.Height:0.000}");
    return;
}

if (args.Contains("--bench", StringComparer.Ordinal))
{
    Bench.Run(game, Parts.Harvest(game, Parts.Candidates(Naming())), loose.Length > 0 ? int.Parse(loose[0]) : 5174);
    return;
}

if (args.Contains("--find", StringComparer.Ordinal))
{
    var names = Parts.Candidates(Naming());
    var found = Parts.Harvest(game, names);
    Console.WriteLine($"{names.Count} names tried, {found.Windows.Count} windows, {found.Textures.Count} pictures, {found.Pieces.Count} pieces");
    foreach (var window in found.Windows.Take(40))
    {
        Console.WriteLine($"  {window}");
    }

    return;
}

var canvas = SettingsPage.Draw(game, args.Contains("--boxes", StringComparer.Ordinal));
var page = loose.Length > 0 ? loose[0] : "settings.png";
canvas.Save(page);
Console.WriteLine($"{canvas.Width}x{canvas.Height} -> {page}");

// Everything on this machine that writes a window's name down. The structs library has a type per
// window; Dalamud and the toolkit ask the game for windows by name; and Wayfarer itself names the
// ones it works with, which is why the bench knows about the windows we are actually building on.
static string[] Naming()
{
    var home = Environment.GetEnvironmentVariable("DALAMUD_HOME")
        ?? throw new InvalidOperationException("set DALAMUD_HOME to the folder Dalamud's assemblies are in");

    return
    [
        Path.Combine(home, "FFXIVClientStructs.dll"),
        Path.Combine(home, "Dalamud.dll"),
        Path.Combine("Wayfarer", "bin", "Release", "Wayfarer.dll"),
        Path.Combine("Wayfarer", "bin", "Release", "KamiToolKit.dll"),
    ];
}
