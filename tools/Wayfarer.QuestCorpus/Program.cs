using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;

// Writes a corpus of real quest step shapes for the test suite. Each quest here was picked for a
// shape the guidance has to get right; the comment says which.
string[] wanted =
[
    "Heavens Weep",                 // a sealed door hung on five steps of six
    "Hunger in the Garden",         // a passage hung on every step
    "Forge Ahead",                  // a portal hung on four steps of five
    "The End of a World",           // a lift hung on three steps of four
    "Travelers at the Crossroads",  // a passage, and a step that searches
    "Perilous Pumpkins",            // a step that names the very thing the quest carries
    "Boots Made for Walking",       // a step that names the vase it carries
    "A Plague of Pumpkins",         // a person on five steps of seven, areas beside them
    "Yes We Cant",                  // one person among three parts of a city
    "Milkroot in Moderation",       // one person among seven areas
    "Brain Buster",                 // speak with and slay, across six areas
    "The Crushing Tide",            // an escort named beside the areas
    "Saintly Inspiration",          // an escort named beside six areas
    "Sleepless in the Stable",      // a person on every step, never scenery
    "The Scarlet Bloodletter",      // the same person spoken to twice
    "No-good Zo Ga's Ambition",     // the same person spoken to three times
    "The Primary Agreement",        // one bare place beside one person
    "In the Middle of Nowhere",     // one bare place beside one person
    "Up Sheet Creek",               // single-place steps naming an object
    "Strange Stew",                 // a cookpot used again and again
    "Signs of the Past",            // gathering, in a territory away from the giver
    "A Peach by Any Other Name",    // the giver in one city, the errand in another
    "Close to Home",                // an ordinary quest nothing should change
    "The Diabolical Bismarck",      // a quest with a duty
];

var game = new GameData(Sqpack());
var quests = game.Excel.GetSheet<Quest>();
var eobjNames = game.Excel.GetSheet<EObjName>();
var npcNames = game.Excel.GetSheet<ENpcResident>();

var corpus = new List<CorpusQuest>();
foreach (var name in wanted)
{
    var quest = quests.FirstOrDefault(q => q.Id.ExtractText().Length > 0 && q.Name.ExtractText().Equals(name, StringComparison.Ordinal));
    if (quest.RowId == 0)
    {
        quest = quests.FirstOrDefault(q => q.Id.ExtractText().Length > 0 && q.Name.ExtractText().Contains(name, StringComparison.OrdinalIgnoreCase));
    }
    if (quest.RowId == 0) { Console.Error.WriteLine($"not found: {name}"); continue; }

    var internalName = quest.Id.ExtractText();
    Dictionary<int, string> text = [];
    try
    {
        var raw = game.Excel.GetSheet<RawRow>(name: $"quest/{internalName[^5..][..3]}/{internalName}");
        foreach (var r in raw)
        {
            var key = r.ReadStringColumn(0).ExtractText();
            if (key.Contains("_TODO_", StringComparison.Ordinal) && int.TryParse(key[(key.LastIndexOf('_') + 1)..], out var idx))
                text[idx] = r.ReadStringColumn(1).ExtractText().Replace("\n", " ");
        }
    }
    catch { }

    var steps = new List<CorpusStep>();
    for (var i = 0; i < quest.TodoParams.Count; i++)
    {
        var p = quest.TodoParams[i];
        if (p.ToDoCompleteSeq == 0) continue;

        var places = new List<CorpusPlace>();
        foreach (var lref in p.ToDoLocation)
        {
            if (lref.RowId == 0 || lref.ValueNullable is not { } lv) continue;
            var isObject = lv.Object.Is<EObj>();
            var objName = lv.Object.RowId == 0 ? "" :
                isObject ? eobjNames.GetRowOrDefault(lv.Object.RowId)?.Singular.ExtractText() ?? ""
                         : npcNames.GetRowOrDefault(lv.Object.RowId)?.Singular.ExtractText() ?? "";
            places.Add(new CorpusPlace(lv.RowId, lv.Object.RowId, isObject, objName, lv.Territory.RowId, lv.Map.RowId,
                MathF.Round(lv.X, 1), MathF.Round(lv.Y, 1), MathF.Round(lv.Z, 1), MathF.Round(lv.Radius, 1)));
        }
        if (places.Count == 0) continue;

        steps.Add(new CorpusStep(i, p.ToDoCompleteSeq, p.ToDoQty, text.GetValueOrDefault(i, ""), places));
    }

    if (steps.Count > 0) corpus.Add(new CorpusQuest(quest.RowId, quest.Name.ExtractText(), internalName, steps));
}

var json = JsonSerializer.Serialize(corpus, new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
});

var outPath = args.Length > 0 ? args[0] : "quest-corpus.json";
File.WriteAllText(outPath, json);
Console.WriteLine($"{corpus.Count} quests, {corpus.Sum(q => q.Steps.Count)} steps -> {outPath}");

internal sealed record CorpusPlace(uint Row, uint ObjectId, bool IsObject, string ObjectName,
    uint Territory, uint Map, float X, float Y, float Z, float Radius);
internal sealed record CorpusStep(int Index, byte Sequence, byte Qty, string Words, List<CorpusPlace> Places);
internal sealed record CorpusQuest(uint QuestRow, string Name, string InternalName, List<CorpusStep> Steps);

// Point SQPACK at a sqpack folder. Copying just ffxiv/0a0000.win32.{dat0,index,index2} to a
// local disk makes a query take under a second instead of minutes when the game lives on a
// mounted Windows drive.
static string Sqpack() =>
    Environment.GetEnvironmentVariable("SQPACK")
    ?? throw new InvalidOperationException("set SQPACK to the game sqpack folder (or a copy of ffxiv/0a0000.win32.*)");
