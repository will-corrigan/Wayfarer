using System.Globalization;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;
using Wayfarer.Core.Quests;
using Wayfarer.Core.Routing;
using GameMap = FFXIVClientStructs.FFXIV.Client.Game.UI.Map;

namespace Wayfarer.Modules.Quests;

/// <summary>Every read the quests module makes of the game, so the rest of the module is pure.
/// Framework thread only.</summary>
internal sealed unsafe class QuestReader(IDataManager dataManager)
{
    /// <summary>Lumina offsets the quest sheet's row ids from the game's quest ids by this.</summary>
    private const uint QuestRowIdOffset = 65536;

    private const string TodoKeyPrefix = "TEXT_";
    private const string TodoKeyInfix = "_TODO_";
    private const string TextSheetFolder = "quest/";
    private const int TextSheetFolderDigits = 3;
    private const int UnusedStep = 0;
    private const int ObjectiveIdQuestBits = 0xFFFF;

    /// <summary>Macros that only style text. Any other macro in a to-do line is a value the game
    /// fills in at runtime and the sheet alone cannot.</summary>
    private static readonly HashSet<MacroCode> PresentationalMacroCodes =
    [
        MacroCode.NewLine, MacroCode.Wait, MacroCode.Icon, MacroCode.Color, MacroCode.EdgeColor,
        MacroCode.ShadowColor, MacroCode.SoftHyphen, MacroCode.Key, MacroCode.Scale, MacroCode.Bold,
        MacroCode.Italic, MacroCode.Edge, MacroCode.Shadow, MacroCode.NonBreakingSpace, MacroCode.Icon2,
        MacroCode.Hyphen, MacroCode.Link, MacroCode.Caps, MacroCode.Head, MacroCode.Split,
        MacroCode.HeadAll, MacroCode.Fixed, MacroCode.Lower, MacroCode.LowerHead, MacroCode.ColorType,
        MacroCode.EdgeColorType, MacroCode.Ruby, MacroCode.Sound, MacroCode.LevelPos,
        MacroCode.SetResetTime, MacroCode.SetTime,
    ];

    private readonly Dictionary<ushort, IReadOnlyList<QuestTodo>> todosByQuest = [];
    private readonly Dictionary<ushort, string> namesByQuest = [];

    public static byte Sequence(ushort questId) => QuestManager.GetQuestSequence(questId);

    /// <summary>The game's live markers for this quest, this frame.</summary>
    public static List<QuestMarker> Markers(ushort questId)
    {
        var markers = new List<QuestMarker>();
        foreach (ref var info in GameMap.Instance()->QuestMarkers)
        {
            if ((info.ObjectiveId & ObjectiveIdQuestBits) != questId)
            {
                continue;
            }

            var label = info.Label.ToString();
            for (var i = 0; i < (int)info.MarkerData.LongCount; i++)
            {
                var data = info.MarkerData[i];
                var at = new Place(data.TerritoryTypeId, data.MapId, data.Position.X, data.Position.Y, data.Position.Z, data.Radius);
                markers.Add(new QuestMarker(at, label.Length > 0 ? label : null));
            }
        }

        return markers;
    }

    /// <summary>The main scenario quest the banner names, or null while it shows "???": the branch
    /// the player picked, and only once that quest is accepted. The agent has no data before login.</summary>
    public ushort? CurrentMainScenarioQuest()
    {
        var data = AgentScenarioTree.Instance()->Data;
        if (data == null)
        {
            return null;
        }

        var questId = data->MainScenarioQuestIds[data->MSQPathIndex];
        return questId != 0 && QuestManager.Instance()->IsQuestAccepted(questId) ? questId : null;
    }

    public string Name(ushort questId) =>
        namesByQuest.TryGetValue(questId, out var name) ? name : namesByQuest[questId] = ReadName(questId);

    /// <summary>The quest's whole to-do table, read once per quest.</summary>
    public IReadOnlyList<QuestTodo> Todos(ushort questId) =>
        todosByQuest.TryGetValue(questId, out var todos) ? todos : todosByQuest[questId] = ReadTodos(questId);

    private static bool HasUnresolvedPlaceholder(ReadOnlySeString text) =>
        text.Any(payload => payload.Type == ReadOnlySePayloadType.Macro && !PresentationalMacroCodes.Contains(payload.MacroCode));

    /// <summary>The to-do text rows by index. They live in a per-quest sheet, keyed
    /// <c>TEXT_&lt;INTERNAL NAME&gt;_TODO_&lt;index&gt;</c>.</summary>
    private static Dictionary<int, (string Text, bool HasPlaceholder)> ParseTodoRows(ExcelSheet<RawRow> raw, string internalName)
    {
        var prefix = TodoKeyPrefix + internalName.ToUpperInvariant() + TodoKeyInfix;
        var rows = new Dictionary<int, (string Text, bool HasPlaceholder)>();
        foreach (var row in raw)
        {
            var key = row.ReadStringColumn(0).ExtractText();
            if (key.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(key.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                var text = row.ReadStringColumn(1);
                rows[index] = (text.ExtractText(), HasUnresolvedPlaceholder(text));
            }
        }

        return rows;
    }

    private static List<Place> Locations(Quest.TodoParamsStruct param) =>
        [.. param.ToDoLocation
            .Where(reference => reference.RowId != 0)
            .Select(reference => reference.ValueNullable)
            .OfType<Level>()
            .Select(level => new Place(level.Territory.RowId, level.Map.RowId, level.X, level.Y, level.Z, level.Radius))];

    private Quest? QuestRow(ushort questId) => dataManager.GetExcelSheet<Quest>().GetRowOrDefault(questId + QuestRowIdOffset);

    private string ReadName(ushort questId) => QuestRow(questId)?.Name.ExtractText() ?? $"Quest {questId}";

    private List<QuestTodo> ReadTodos(ushort questId)
    {
        if (QuestRow(questId) is not { } quest)
        {
            return [];
        }

        var internalName = quest.Id.ExtractText();
        var rows = OpenTextSheet(internalName) is { } raw ? ParseTodoRows(raw, internalName) : [];

        var todos = new List<QuestTodo>();
        for (var i = 0; i < quest.TodoParams.Count; i++)
        {
            var param = quest.TodoParams[i];
            if (param.ToDoCompleteSeq == UnusedStep)
            {
                continue;
            }

            var (text, hasPlaceholder) = rows.GetValueOrDefault(i, (string.Empty, false));
            todos.Add(new QuestTodo(i, param.ToDoCompleteSeq, text, hasPlaceholder, param.ToDoQty, Locations(param)));
        }

        return todos;
    }

    /// <summary>The quest's own text sheet, at <c>quest/&lt;first three digits&gt;/&lt;internal name&gt;</c>,
    /// or null when there is none under that name.</summary>
    private ExcelSheet<RawRow>? OpenTextSheet(string internalName)
    {
        var number = internalName.Split('_')[^1];
        if (number.Length < TextSheetFolderDigits)
        {
            return null;
        }

        try
        {
            return dataManager.Excel.GetSheet<RawRow>(name: TextSheetFolder + number[..TextSheetFolderDigits] + "/" + internalName);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
