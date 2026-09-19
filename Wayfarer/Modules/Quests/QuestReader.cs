using System.Globalization;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
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
    /// <summary>Every to-do line in a quest's own text sheet is keyed with this in front of it.</summary>
    private const string TodoKeyPrefix = "TEXT_";

    /// <summary>The script parameter a quest names its duty in. A quest may name several, one per
    /// difficulty of the same fight, and the first the Duty Finder knows is the one it is about.</summary>
    private const string DutyParameter = "INSTANCEDUNGEON";
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
    private Dictionary<string, EmoteCommand>? emotesByCommand;
    private Dictionary<uint, uint>? dutiesByContent;

    public static byte Sequence(ushort questId) => QuestManager.GetQuestSequence(questId);

    /// <summary>Whether the player has the quest accepted and not yet complete. No quest is accepted
    /// while the game has no quest manager, which is the case until the player is in the world.</summary>
    public static bool IsAccepted(ushort questId)
    {
        var quests = QuestManager.Instance();
        return quests != null && quests->IsQuestAccepted(questId);
    }

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

    /// <summary>What the game says about these ToDos of the quest right now, from the quest's own
    /// event handler. Empty when the handler is not loaded or there is no player.</summary>
    public List<QuestTodoProgress> Progress(ushort questId, IEnumerable<int> todoIndexes)
    {
        var events = EventFramework.Instance();
        var control = Control.Instance();
        if (events == null || control == null)
        {
            return [];
        }

        var handler = (QuestEventHandler*)events->GetEventHandlerById(QuestIds.RowId(questId));
        var player = control->LocalPlayer;
        if (handler == null || player == null)
        {
            return [];
        }

        var progress = new List<QuestTodoProgress>();
        foreach (var index in todoIndexes)
        {
            uint have, needed, itemId;
            handler->GetTodoArgs(player, (byte)index, &have, &needed, &itemId);
            progress.Add(new QuestTodoProgress(index, handler->IsTodoChecked(player, (byte)index), (int)have, (int)needed, KeyItem(itemId)));
        }

        return progress;
    }

    /// <summary>The main scenario quest the banner names, or null while it shows "???": the branch
    /// the player picked, and only once that quest is accepted. The agent has no data before login.</summary>
    public ushort? CurrentMainScenarioQuest()
    {
        var agent = AgentScenarioTree.Instance();
        var data = agent == null ? null : agent->Data;
        if (data == null)
        {
            return null;
        }

        var questId = data->MainScenarioQuestIds[data->MSQPathIndex];
        return questId != 0 && IsAccepted(questId) ? questId : null;
    }

    /// <summary>Every emote by each of its chat commands, "/bow".</summary>
    public IReadOnlyDictionary<string, EmoteCommand> Emotes() => emotesByCommand ??= ReadEmotes();

    /// <summary>The Duty Finder entry for the duty a quest sends the player into, or null when it
    /// sends them nowhere instanced. A quest names the duty among its own script parameters, the
    /// same list that names its actors and its items, under <c>INSTANCEDUNGEON</c>.</summary>
    public uint? Duty(ushort questId)
    {
        if (QuestRow(questId) is not { } quest)
        {
            return null;
        }

        dutiesByContent ??= ReadDuties();
        foreach (var parameter in quest.QuestParams)
        {
            if (parameter.ScriptInstruction.ExtractText().StartsWith(DutyParameter, StringComparison.Ordinal)
                && Finder(parameter.ScriptArg) is { } duty)
            {
                return duty;
            }
        }

        return null;
    }

    /// <summary>The quest's name as the sheet writes it, or an empty string when the sheet has no
    /// such quest.</summary>
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

    private static List<Place> Positions(Quest.TodoParamsStruct param) =>
        [.. param.ToDoLocation
            .Where(reference => reference.RowId != 0)
            .Select(reference => reference.ValueNullable)
            .OfType<Level>()
            .Select(level => new Place(level.Territory.RowId, level.Map.RowId, level.X, level.Y, level.Z, level.Radius))];

    private QuestItem? KeyItem(uint itemId) =>
        QuestItem.IsKeyItem(itemId) && dataManager.GetExcelSheet<EventItem>().GetRowOrDefault(itemId) is { } item
            ? new QuestItem(itemId, item.Name.ExtractText(), item.Icon)
            : null;

    /// <summary>The Duty Finder entry that runs a piece of instanced content, or null when the
    /// Finder does not queue for it.</summary>
    private uint? Finder(uint contentId) =>
        contentId != 0 && dutiesByContent is { } duties && duties.TryGetValue(contentId, out var duty) ? duty : null;

    /// <summary>Every duty the Duty Finder can queue for, by the instanced content it runs.</summary>
    private Dictionary<uint, uint> ReadDuties()
    {
        var duties = new Dictionary<uint, uint>();
        foreach (var condition in dataManager.GetExcelSheet<ContentFinderCondition>())
        {
            if (condition.Content.Is<InstanceContent>() && condition.Content.RowId != 0)
            {
                duties.TryAdd(condition.Content.RowId, condition.RowId);
            }
        }

        return duties;
    }

    private Dictionary<string, EmoteCommand> ReadEmotes()
    {
        var emotes = new Dictionary<string, EmoteCommand>(StringComparer.Ordinal);
        foreach (var emote in dataManager.GetExcelSheet<Emote>())
        {
            if (emote.TextCommand.ValueNullable is not { } command)
            {
                continue;
            }

            foreach (var text in new[] { command.Command, command.ShortCommand, command.Alias, command.ShortAlias })
            {
                var key = text.ExtractText();
                if (key.Length > 0)
                {
                    emotes.TryAdd(key, new EmoteCommand((ushort)emote.RowId, command.Command.ExtractText(), emote.Icon));
                }
            }
        }

        return emotes;
    }

    private Quest? QuestRow(ushort questId) => dataManager.GetExcelSheet<Quest>().GetRowOrDefault(QuestIds.RowId(questId));

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
            todos.Add(new QuestTodo(i, param.ToDoCompleteSeq, text, hasPlaceholder, param.ToDoQty, Positions(param)));
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
