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

/// <summary>Every read the quests module makes of the game, in one place, so the rest of the
/// module is pure. Framework thread only: it reads the game's agents and sheets.</summary>
internal sealed unsafe class QuestReader(IDataManager dataManager)
{
    /// <summary>Lumina offsets the quest sheet's row ids from the game's own quest ids by this.</summary>
    private const uint QuestRowIdOffset = 65536;

    /// <summary>Macros that only style text and never stand in for a value the game fills at
    /// runtime. A to-do line containing any other macro is one whose sheet text is incomplete.</summary>
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

    private readonly Dictionary<ushort, IReadOnlyList<QuestTodo>> todoCache = [];
    private readonly Dictionary<ushort, string> nameCache = [];

    /// <summary>The step the player is on.</summary>
    public static byte Sequence(ushort questId) => QuestManager.GetQuestSequence(questId);

    /// <summary>The game's live markers for this quest, this frame.</summary>
    public static List<QuestMarker> Markers(ushort questId)
    {
        var markers = new List<QuestMarker>();
        var map = GameMap.Instance();
        if (map == null)
        {
            return markers;
        }

        foreach (ref var info in map->QuestMarkers)
        {
            if ((info.ObjectiveId & 0xFFFF) != questId)
            {
                continue;
            }

            var label = info.Label.ToString();
            for (var i = 0; i < (int)info.MarkerData.LongCount; i++)
            {
                var data = info.MarkerData[i];
                markers.Add(new QuestMarker(
                    new Place(data.TerritoryTypeId, data.MapId, data.Position.X, data.Position.Y, data.Position.Z, data.Radius),
                    label.Length > 0 ? label : null));
            }
        }

        return markers;
    }

    /// <summary>The main scenario quest the banner names right now, or null while it shows "???":
    /// the branch the player has picked on the banner's own dropdown, and only while that quest is
    /// accepted. Between quests, and before a branch is chosen, this is null.</summary>
    public ushort? CurrentMainScenarioQuest()
    {
        var tree = AgentScenarioTree.Instance();
        if (tree == null || tree->Data == null)
        {
            return null;
        }

        var ids = tree->Data->MainScenarioQuestIds;
        var path = tree->Data->MSQPathIndex;
        if (path >= 3 || path >= ids.Length)
        {
            return null;
        }

        var id = ids[path];
        if (id == 0)
        {
            return null;
        }

        var manager = QuestManager.Instance();
        return manager != null && manager->IsQuestAccepted(id) ? id : null;
    }

    /// <summary>The quest's name, as the banner shows it.</summary>
    public string Name(ushort questId)
    {
        if (!nameCache.TryGetValue(questId, out var name))
        {
            name = dataManager.GetExcelSheet<Quest>().GetRowOrDefault(questId + QuestRowIdOffset)?.Name.ExtractText()
                ?? $"Quest {questId}";
            nameCache[questId] = name;
        }

        return name;
    }

    /// <summary>The quest's whole to-do table with its text and locations. Static data, read once
    /// per quest and kept.</summary>
    public IReadOnlyList<QuestTodo> Todos(ushort questId)
    {
        if (!todoCache.TryGetValue(questId, out var todos))
        {
            todos = ReadTodos(questId);
            todoCache[questId] = todos;
        }

        return todos;
    }

    private static bool HasUnresolvedPlaceholder(ReadOnlySeString text)
    {
        foreach (var payload in text)
        {
            if (payload.Type == ReadOnlySePayloadType.Macro && !PresentationalMacroCodes.Contains(payload.MacroCode))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The to-do text rows, keyed by index. They live in a per-quest sheet whose name is
    /// derived from the quest's internal name: <c>quest/&lt;first three digits&gt;/&lt;internal name&gt;</c>.
    /// </summary>
    private static Dictionary<int, (string Text, bool HasPlaceholder)> ParseTodoRows(ExcelSheet<RawRow> raw, string internalName)
    {
        var prefix = $"TEXT_{internalName.ToUpperInvariant()}_TODO_";
        var byIndex = new Dictionary<int, (string Text, bool HasPlaceholder)>();
        foreach (var row in raw)
        {
            var key = row.ReadStringColumn(0).ExtractText();
            if (!key.StartsWith(prefix, StringComparison.Ordinal)
                || !int.TryParse(key.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                continue;
            }

            var text = row.ReadStringColumn(1);
            byIndex[index] = (text.ExtractText(), HasUnresolvedPlaceholder(text));
        }

        return byIndex;
    }

    private List<QuestTodo> ReadTodos(ushort questId)
    {
        if (dataManager.GetExcelSheet<Quest>().GetRowOrDefault(questId + QuestRowIdOffset) is not { } quest
            || quest.TodoParams.Count == 0)
        {
            return [];
        }

        var internalName = quest.Id.ExtractText();
        var rows = OpenTextSheet(internalName) is { } raw ? ParseTodoRows(raw, internalName) : [];

        var todos = new List<QuestTodo>();
        for (var i = 0; i < quest.TodoParams.Count; i++)
        {
            var param = quest.TodoParams[i];
            if (param.ToDoCompleteSeq == 0)
            {
                continue;
            }

            var locations = new List<Place>();
            foreach (var reference in param.ToDoLocation)
            {
                if (reference.RowId != 0 && reference.ValueNullable is { } level)
                {
                    locations.Add(new Place(level.Territory.RowId, level.Map.RowId, level.X, level.Y, level.Z, level.Radius));
                }
            }

            var (text, hasPlaceholder) = rows.TryGetValue(i, out var row) ? row : (string.Empty, false);
            todos.Add(new QuestTodo(i, param.ToDoCompleteSeq, text, hasPlaceholder, param.ToDoQty, locations));
        }

        return todos;
    }

    private ExcelSheet<RawRow>? OpenTextSheet(string internalName)
    {
        var number = internalName.Split('_')[^1];
        if (number.Length < 3)
        {
            return null;
        }

        try
        {
            return dataManager.Excel.GetSheet<RawRow>(name: $"quest/{number[..3]}/{internalName}");
        }
        catch (Exception)
        {
            // No text sheet under the usual name: the lines fall back to their markers' labels.
            return null;
        }
    }
}
