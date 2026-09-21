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

// One rectangle of a sheet, written out on its own and scaled up, for looking at closely.
if (args.Contains("--cut", StringComparer.Ordinal))
{
    var sheet = Picture.From(game, loose[0]);
    var (cx, cy, cw, ch) = (int.Parse(loose[1]), int.Parse(loose[2]), int.Parse(loose[3]), int.Parse(loose[4]));
    var times = loose.Length > 6 ? int.Parse(loose[6]) : 3;

    var cut = new Canvas(cw * times, ch * times);
    cut.Clear(new System.Numerics.Vector4(0.10f, 0.11f, 0.13f, 1f));
    for (var y = 0; y < ch * times; y++)
    {
        for (var x = 0; x < cw * times; x++)
        {
            var (sx, sy) = (cx + (x / times), cy + (y / times));
            if (sx >= sheet.Width || sy >= sheet.Height)
            {
                continue;
            }

            var at = ((sy * sheet.Width) + sx) * 4;
            cut.Ink(x, y, sheet.Pixels[at + 3] / 255f, new System.Numerics.Vector4(
                sheet.Pixels[at] / 255f, sheet.Pixels[at + 1] / 255f, sheet.Pixels[at + 2] / 255f, 1f));
        }
    }

    cut.Save(loose[5]);
    Console.WriteLine($"{cw}x{ch} at ({cx},{cy}) -> {loose[5]}");
    return;
}

// Every separate thing drawn on a sheet, found by joining up the pixels that touch each other. A
// window that ships a list of its parts says where its pieces are; one that does not still has them
// laid out with gaps between, and this is what a part list would have said.
if (args.Contains("--pieces", StringComparer.Ordinal))
{
    var sheet = Picture.From(game, loose[0]);
    var seen = new bool[sheet.Width * sheet.Height];
    bool Drawn(int x, int y) => sheet.Pixels[(((y * sheet.Width) + x) * 4) + 3] > 8;

    var found = new List<(int X, int Y, int W, int H, int Lit)>();
    for (var y = 0; y < sheet.Height; y++)
    {
        for (var x = 0; x < sheet.Width; x++)
        {
            if (seen[(y * sheet.Width) + x] || !Drawn(x, y))
            {
                continue;
            }

            var (left, top, right, bottom, lit) = (x, y, x, y, 0);
            var walk = new Stack<(int X, int Y)>();
            walk.Push((x, y));
            seen[(y * sheet.Width) + x] = true;

            while (walk.Count > 0)
            {
                var (px, py) = walk.Pop();
                lit++;
                left = Math.Min(left, px);
                top = Math.Min(top, py);
                right = Math.Max(right, px);
                bottom = Math.Max(bottom, py);

                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var (nx, ny) = (px + dx, py + dy);
                        if (nx < 0 || ny < 0 || nx >= sheet.Width || ny >= sheet.Height || seen[(ny * sheet.Width) + nx] || !Drawn(nx, ny))
                        {
                            continue;
                        }

                        seen[(ny * sheet.Width) + nx] = true;
                        walk.Push((nx, ny));
                    }
                }
            }

            if (lit > 24)
            {
                found.Add((left, top, right - left + 1, bottom - top + 1, lit));
            }
        }
    }

    Console.WriteLine($"{loose[0]}  {sheet.Width}x{sheet.Height}  {found.Count} pieces");
    foreach (var piece in found.OrderBy(f => f.Y).ThenBy(f => f.X))
    {
        Console.WriteLine($"  x {piece.X,3} y {piece.Y,3}  w {piece.W,3} h {piece.H,3}");
    }

    return;
}

// The rows of a sheet that have anything drawn on them, and how far across each run reaches. A
// sheet with no part list still has its pieces laid out in bands, and this is where they are.
if (args.Contains("--bands", StringComparer.Ordinal))
{
    var sheet = Picture.From(game, loose[0]);
    var (runFrom, runLeft, runRight) = (-1, int.MaxValue, -1);
    for (var y = 0; y <= sheet.Height; y++)
    {
        var (left, right) = (int.MaxValue, -1);
        for (var x = 0; y < sheet.Height && x < sheet.Width; x++)
        {
            if (sheet.Pixels[(((y * sheet.Width) + x) * 4) + 3] > 8)
            {
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
        }

        if (right >= 0)
        {
            if (runFrom < 0)
            {
                (runFrom, runLeft, runRight) = (y, left, right);
            }

            runLeft = Math.Min(runLeft, left);
            runRight = Math.Max(runRight, right);
            continue;
        }

        if (runFrom >= 0)
        {
            Console.WriteLine($"  x {runLeft,3} y {runFrom,3}  w {runRight - runLeft + 1,3} h {y - runFrom,3}");
            (runFrom, runLeft, runRight) = (-1, int.MaxValue, -1);
        }
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
