using Lumina;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using Lumina.Excel;

// usage: questdump <quest name or row id>
if (args.Length == 0) { Console.Error.WriteLine("usage: questdump <name|rowId>"); return 1; }

var game = new GameData(Sqpack());
var quests = game.Excel.GetSheet<Quest>();

Quest row;
if (uint.TryParse(args[0], out var id)) row = quests.GetRow(id);
else
{
    var found = quests.FirstOrDefault(q => q.Name.ExtractText().Contains(args[0], StringComparison.OrdinalIgnoreCase) && q.Id.ExtractText().Length > 0);
    if (found.RowId == 0) { Console.Error.WriteLine("not found"); return 1; }
    row = found;
}

var internalName = row.Id.ExtractText();
Console.WriteLine($"Quest {row.RowId}  {row.Name.ExtractText()}  [{internalName}]  lvl {row.ClassJobLevel[0]}  icon {row.Icon}");
Console.WriteLine($"  EventIconType -> {row.EventIconType.RowId}  MapIconAvailable {row.EventIconType.ValueNullable?.MapIconAvailable}");
Console.WriteLine($"  PlaceName {row.PlaceName.ValueNullable?.Name.ExtractText()}");

// the quest's own text sheet
var digits = new string(internalName.Where(char.IsDigit).ToArray());
var folder = internalName.Length > 0 ? internalName[^5..][..3] : "000";
var textSheet = $"quest/{folder}/{internalName}";
Dictionary<int, string> todoText = new();
try
{
    var raw = game.Excel.GetSheet<RawRow>(name: textSheet);
    foreach (var r in raw)
    {
        var key = r.ReadStringColumn(0).ExtractText();
        if (key.Contains("_TODO_", StringComparison.Ordinal) && int.TryParse(key[(key.LastIndexOf('_') + 1)..], out var idx))
            todoText[idx] = r.ReadStringColumn(1).ExtractText();
    }
}
catch (Exception e) { Console.WriteLine($"  (no text sheet {textSheet}: {e.GetType().Name})"); }

Console.WriteLine("\n-- TodoParams --");
for (var i = 0; i < row.TodoParams.Count; i++)
{
    var p = row.TodoParams[i];
    if (p.ToDoCompleteSeq == 0 || p.ToDoCompleteSeq == 255 && p.ToDoQty == 0) { }
    var places = new List<string>();
    for (var j = 0; j < p.ToDoLocation.Count; j++)
    {
        var loc = p.ToDoLocation[j].ValueNullable;
        if (loc is null || loc.Value.RowId == 0) continue;
        var lv = loc.Value;
        places.Add($"Level {lv.RowId}: terr {lv.Territory.RowId} map {lv.Map.RowId} ({lv.X:F1}, {lv.Y:F1}, {lv.Z:F1}) r={lv.Radius} type={lv.Type} obj={lv.Object.RowId} eventId={lv.EventId.RowId}");
    }
    if (p.ToDoCompleteSeq == 0 && places.Count == 0 && !todoText.ContainsKey(i)) continue;
    Console.WriteLine($"  [{i}] seq {p.ToDoCompleteSeq}  qty {p.ToDoQty}  \"{todoText.GetValueOrDefault(i, "")}\"");
    foreach (var pl in places) Console.WriteLine($"        {pl}");
}

Console.WriteLine("\n-- QuestListenerParams --");
for (var i = 0; i < row.QuestListenerParams.Count; i++)
{
    var lp = row.QuestListenerParams[i];
    if (lp.Listener == 0 && lp.ConditionValue == 0 && lp.Behavior == 0) continue;
    Console.WriteLine($"  [{i,2}] listener {lp.Listener,8}  spawn {lp.ActorSpawnSeq,3} despawn {lp.ActorDespawnSeq,3}  behavior {lp.Behavior,5}  condType {lp.ConditionType} condVal {lp.ConditionValue} op {lp.ConditionOperator}  u0 {lp.Unknown0} u1 {lp.Unknown1} u8a {lp.QuestUInt8A}  vis {(lp.VisibleBool ? 1 : 0)} cond {(lp.ConditionBool ? 1 : 0)} item {(lp.ItemBool ? 1 : 0)} announce {(lp.AnnounceBool ? 1 : 0)} behav {(lp.BehaviorBool ? 1 : 0)} accept {(lp.AcceptBool ? 1 : 0)} qual {(lp.QualifiedBool ? 1 : 0)} target {(lp.CanTargetBool ? 1 : 0)}");
}

Console.WriteLine("\n-- QuestParams (ENEMY / ITEM) --");
for (var i = 0; i < row.QuestParams.Count; i++)
{
    var p = row.QuestParams[i];
    var sc = p.ScriptInstruction.ExtractText();
    if (sc.Length == 0) continue;
    Console.WriteLine($"  [{i}] {sc} = {p.ScriptArg}");
}
return 0;

// Reads the game Excel sheets with Lumina. Point SQPACK at a sqpack folder: copying just
// ffxiv/0a0000.win32.{dat0,index,index2} to a local disk makes a query take under a second
// instead of minutes when the game lives on a mounted Windows drive.
static string Sqpack() =>
    Environment.GetEnvironmentVariable("SQPACK")
    ?? throw new InvalidOperationException("set SQPACK to the game sqpack folder (or a copy of ffxiv/0a0000.win32.*)");
