using System.Collections;
using System.Reflection;
using Lumina;
using Lumina.Excel;
using Lumina.Text.ReadOnly;

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: sheetq <Sheet> [--row <id>] [--find <text>] [--cols a,b,c] [--max n]");
    return 1;
}

// Which rows anywhere point at a given one. The joins the sheets do not write down are found by
// looking at every sheet rather than by picking one and hoping.
if (args[0] == "--refs")
{
    Wayfarer.SheetReader.Refs.Find(new GameData(Sqpack()), uint.Parse(args[1]), args.Length > 2 ? args[2] : null);
    return 0;
}

// How much of a kind of thing the sheets can account for.
if (args[0] == "--sources")
{
    var data = new GameData(Sqpack());
    if (args.Length > 1 && args[1] == "minions") Wayfarer.SheetReader.Sources.Minions(data);
    else Wayfarer.SheetReader.Sources.Mounts(data);
    return 0;
}

var sheetName = args[0];
string? find = null, cols = null;
uint? row = null;
var max = 25;
for (var i = 1; i < args.Length - 1; i++)
{
    if (args[i] == "--find") find = args[i + 1];
    if (args[i] == "--cols") cols = args[i + 1];
    if (args[i] == "--row") row = uint.Parse(args[i + 1]);
    if (args[i] == "--max") max = int.Parse(args[i + 1]);
}

var game = new GameData(Sqpack());

var type = typeof(Lumina.Excel.Sheets.Addon).Assembly
    .GetTypes().FirstOrDefault(t => t.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase)
        && t.Namespace == "Lumina.Excel.Sheets");
if (type is null) { Console.Error.WriteLine($"no sheet '{sheetName}'"); return 1; }

var sheet = (IEnumerable)typeof(ExcelModule).GetMethods()
    .First(m => m.Name == "GetSheet" && m.GetParameters().Length == 2)
    .MakeGenericMethod(type).Invoke(game.Excel, [null, null])!;

var wanted = cols?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

static string Show(object? value) => value switch
{
    null => "null",
    ReadOnlySeString s => $"'{s.ExtractText()}'",
    _ when value.GetType().Name.StartsWith("RowRef") =>
        $"->{value.GetType().GetProperty("RowId")!.GetValue(value)}",
    _ => value.ToString() ?? string.Empty,
};

var shown = 0;
foreach (var record in sheet)
{
    var id = (uint)props.First(p => p.Name == "RowId").GetValue(record)!;
    if (row is { } only && id != only) continue;

    var fields = props.Where(p => p.Name is not ("ExcelPage" or "RowOffset" or "RowId")
                                  && (wanted is null || wanted.Contains(p.Name, StringComparer.OrdinalIgnoreCase)))
        .Select(p => (p.Name, Value: SafeGet(p, record)))
        .ToList();

    if (find is not null && !fields.Any(f => Show(f.Value).Contains(find, StringComparison.OrdinalIgnoreCase))) continue;

    Console.WriteLine($"[{sheetName} {id}]");
    foreach (var (name, value) in fields)
    {
        if (value is IEnumerable list and not string and not ReadOnlySeString)
        {
            var items = list.Cast<object?>().Select(Show).Where(s => s is not "0" and not "''" and not "null").ToList();
            if (items.Count > 0) Console.WriteLine($"  {name} = [{string.Join(", ", items.Take(40))}]");
            continue;
        }

        Console.WriteLine($"  {name} = {Show(value)}");
    }

    if (++shown >= max) break;
}

return 0;

static object? SafeGet(PropertyInfo p, object record)
{
    try { return p.GetValue(record); }
    catch (Exception ex) { return $"<{ex.GetType().Name}>"; }
}

// Reads the game Excel sheets with Lumina. Point SQPACK at a sqpack folder: copying just
// ffxiv/0a0000.win32.{dat0,index,index2} to a local disk makes a query take under a second
// instead of minutes when the game lives on a mounted Windows drive.
static string Sqpack() =>
    Environment.GetEnvironmentVariable("SQPACK")
    ?? throw new InvalidOperationException("set SQPACK to the game sqpack folder (or a copy of ffxiv/0a0000.win32.*)");
