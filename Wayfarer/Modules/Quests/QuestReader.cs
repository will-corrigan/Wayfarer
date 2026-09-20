using System.Globalization;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using Wayfarer.Guidance;
using Wayfarer.Routing;
using GameMap = FFXIVClientStructs.FFXIV.Client.Game.UI.Map;

namespace Wayfarer.Modules.Quests;

/// <summary>Every read the quests module makes of the game, so the rest of the module is pure.
/// Framework thread only.</summary>
internal sealed unsafe class QuestReader(IDataManager dataManager, ISeStringEvaluator evaluator, IPluginLog log)
{
    /// <summary>Every to-do line in a quest's own text sheet is keyed with this in front of it.</summary>
    private const string TodoKeyPrefix = "TEXT_";

    /// <summary>The script parameter a quest names its duty in. A quest may name several, one per
    /// difficulty of the same fight, and the first the Duty Finder knows is the one it is about.</summary>
    private const string DutyParameter = "INSTANCEDUNGEON";

    /// <summary>The script parameter a quest names an object of the world in, one per object it is
    /// about. A search area names nowhere to look and nothing to look for, but the quest that owns
    /// it names its own objects, and one of them spawning inside the circle is the thing.</summary>
    private const string ObjectParameter = "EOBJECT";

    /// <summary>The script parameter a quest names one of its people in, one per person. A step
    /// that sends the player into a circle to find someone names nobody in the circle itself, and
    /// the game does not stamp its own event onto people the way it does onto what it spawns, so
    /// the quest's own list is the only thing that says who is being looked for. Verified in game
    /// on "The Road Home": its step to find three wounded soldiers named none of them anywhere
    /// but here, while all three stood in the circle, targetable, stamped with no event at all.
    /// </summary>
    private const string PersonParameter = "ACTOR";

    /// <summary>The script parameter a quest names a creature in, one per creature it is about.
    /// Each is a place row of its own, and the row names the kind of thing standing there, which is
    /// how a step that sends the player into a circle to fight says what is in it. The world gives
    /// a creature that same kind as its id, so it is looked for exactly as an object is.</summary>
    private const string CreatureParameter = "ENEMY";

    /// <summary>The script parameter a quest names one of its key items in, one per item. A ToDo
    /// that has the player use one does not always say which through the event handler, and the
    /// quest's own list is what says it then.</summary>
    private const string ItemParameter = "ITEM";

    private const string TodoKeyInfix = "_TODO_";
    private const string TextSheetFolder = "quest/";
    private const int TextSheetFolderDigits = 3;
    private const int UnusedStep = 0;
    private const int ObjectiveIdQuestBits = 0xFFFF;

    private readonly Dictionary<ushort, IReadOnlyList<QuestTodoTemplate>> templatesByQuest = [];
    private readonly Dictionary<ushort, string> namesByQuest = [];
    private readonly Dictionary<ushort, IReadOnlyList<Mark>> marksByQuest = [];
    private readonly Dictionary<ushort, IReadOnlyList<Place>> lairsByQuest = [];
    private readonly Dictionary<ushort, IReadOnlyList<QuestItem>> itemsByQuest = [];
    private string lastHandler = string.Empty;
    private Dictionary<string, EmoteCommand>? emotesByCommand;
    private Dictionary<uint, ContentFinderCondition>? dutiesByContent;

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

            var label = info.Label.ExtractText();
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
        var agent = AgentScenarioTree.Instance();
        var data = agent == null ? null : agent->Data;
        if (data == null)
        {
            return null;
        }

        // The branch index comes from the game and nothing promises it is inside the four the
        // guide holds, so it is checked rather than trusted.
        var paths = data->MainScenarioQuestIds;
        if (data->MSQPathIndex >= paths.Length)
        {
            return null;
        }

        var questId = paths[data->MSQPathIndex];
        return questId != 0 && IsAccepted(questId) ? questId : null;
    }

    /// <summary>The objects of the world this quest is about, by the id the game gives them. Read
    /// once per quest; which of them is spawned is asked of the world, not of the sheet.</summary>
    public IReadOnlyList<Mark> Marks(ushort questId) =>
        marksByQuest.TryGetValue(questId, out var marks) ? marks : marksByQuest[questId] = ReadMarks(questId);

    /// <summary>The key items a quest is about, as it names them among its own parameters. Read
    /// once per quest, and what a ToDo's words are matched against when the handler does not say
    /// which item the step is for.</summary>
    public IReadOnlyList<QuestItem> Items(ushort questId) =>
        itemsByQuest.TryGetValue(questId, out var items) ? items : itemsByQuest[questId] = ReadItems(questId);

    /// <summary>Where the creatures a quest is about stand, as the data places them. A step that
    /// sends the player into a circle to fight names them separately from the circle, and the spot
    /// inside it beats the middle of it whether or not anything has spawned yet.</summary>
    public IReadOnlyList<Place> Lairs(ushort questId) =>
        lairsByQuest.TryGetValue(questId, out var lairs) ? lairs : lairsByQuest[questId] = ReadLairs(questId);

    /// <summary>Every emote by each of its chat commands, "/bow".</summary>
    public IReadOnlyDictionary<string, EmoteCommand> Emotes() => emotesByCommand ??= ReadEmotes();

    /// <summary>The duty a quest sends the player into, or null when it sends them nowhere
    /// instanced. A quest names the duty among its own script parameters, the same list that names
    /// its actors and its items, under <c>INSTANCEDUNGEON</c>. The Finder row it names carries the
    /// instance's own territory, so both halves of the answer come off the one row.</summary>
    public QuestDuty? Duty(ushort questId)
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
                var territory = duty.TerritoryType.RowId;
                return new QuestDuty(duty.RowId, territory != 0 ? territory : null);
            }
        }

        return null;
    }

    /// <summary>The quest's name as the sheet writes it, or an empty string when the sheet has no
    /// such quest.</summary>
    public string Name(ushort questId) =>
        namesByQuest.TryGetValue(questId, out var name) ? name : namesByQuest[questId] = ReadName(questId);

    /// <summary>What the quest's own running script says about each line of a step: whether it is
    /// ticked, how far along it is, and the key item it is about. Cheap enough to ask every frame,
    /// which is what tells the module whether anything moved.</summary>
    public List<QuestTodoProgress> Progress(ushort questId, byte sequence)
    {
        Probe(questId, sequence);
        return Progress(questId, Templates(questId).Where(todo => todo.Sequence == sequence).Select(todo => todo.Index));
    }

    /// <summary>The step's lines with their words finished, built from the progress just read.
    /// Finishing the words means resolving macros and allocating strings, so this is asked only
    /// when something about the step has actually moved rather than every frame.</summary>
    public List<QuestTodo> Todos(ushort questId, byte sequence, IReadOnlyList<QuestTodoProgress> progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var templates = Templates(questId).Where(todo => todo.Sequence == sequence);
        var todos = new List<QuestTodo>();
        foreach (var template in templates)
        {
            var reported = progress.FirstOrDefault(entry => entry.Index == template.Index);
            todos.Add(new QuestTodo(
                template.Index,
                template.Sequence,
                Words(template.Words, reported?.Have ?? 0, Needed(reported, template)),
                template.Needed,
                template.Positions));
        }

        return todos;
    }

    /// <summary>How many of the thing a line wants. The script's own figure wins when it reports
    /// one, and it does report zero, which is the same rule the surface counts by.</summary>
    private static int Needed(QuestTodoProgress? reported, QuestTodoTemplate template) =>
        reported?.Needed > 0 ? reported.Needed : template.Needed;

    /// <summary>The to-do text rows by index. They live in a per-quest sheet, keyed
    /// <c>TEXT_&lt;INTERNAL NAME&gt;_TODO_&lt;index&gt;</c>.</summary>
    private static Dictionary<int, ReadOnlySeString> ParseTodoRows(ExcelSheet<RawRow> raw, string internalName)
    {
        var prefix = TodoKeyPrefix + internalName.ToUpperInvariant() + TodoKeyInfix;
        var rows = new Dictionary<int, ReadOnlySeString>();
        foreach (var row in raw)
        {
            var key = row.ReadStringColumn(0).ExtractText();
            if (key.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(key.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                rows[index] = row.ReadStringColumn(1);
            }
        }

        return rows;
    }

    private static Place At(Level level) =>
        new(level.Territory.RowId, level.Map.RowId, level.X, level.Y, level.Z, level.Radius);

    /// <summary>One line of the sheet read into the plain shape the choosing is done on, so that
    /// choosing needs nothing of the game but what is written here.</summary>
    private StepShape Shape(int index, Quest.TodoParamsStruct param, string words)
    {
        var places = new List<StepPlace>();
        foreach (var reference in param.ToDoLocation)
        {
            if (reference.RowId == 0 || reference.ValueNullable is not { } level)
            {
                continue;
            }

            var thing = level.Object.Is<EObj>();
            var name = thing
                ? dataManager.GetExcelSheet<EObjName>().GetRowOrDefault(level.Object.RowId)?.Singular.ExtractText() ?? string.Empty
                : string.Empty;

            places.Add(new StepPlace(level.RowId, level.Object.RowId, thing, name, At(level)));
        }

        return new StepShape(index, param.ToDoCompleteSeq, words, places);
    }

    /// <summary>What the game says about these ToDos of the quest right now, from the quest's own
    /// event handler. Empty when the handler is not loaded or there is no player.</summary>
    private List<QuestTodoProgress> Progress(ushort questId, IEnumerable<int> todoIndexes)
    {
        var events = EventFramework.Instance();
        var control = Control.Instance();
        if (events == null || control == null)
        {
            return [];
        }

        var handler = (QuestEventHandler*)events->GetEventHandlerById(questId);
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

    /// <summary>The kind of creature a quest's <c>ENEMY</c> parameter names, or null when it names
    /// nothing we can recognise.
    ///
    /// <para>A quest says it one of two ways and both are common: the place row where such a
    /// creature stands, which says what kind stands there, or the kind itself. Which it is, is
    /// whichever the number turns out to be — the two sheets do not overlap, so asking settles
    /// it. Reading only the first way silently lost every quest that used the second.</para></summary>
    private uint? Creature(uint named)
    {
        if (dataManager.GetExcelSheet<Level>().GetRowOrDefault(named)?.Object is { RowId: not 0 } kind)
        {
            return kind.Is<BNpcBase>() ? kind.RowId : null;
        }

        return dataManager.GetExcelSheet<BNpcBase>().GetRowOrDefault(named) is not null ? named : null;
    }

    /// <summary>The key items a quest names among its own parameters.</summary>
    private List<QuestItem> ReadItems(ushort questId)
    {
        if (QuestRow(questId) is not { } quest)
        {
            return [];
        }

        var items = new List<QuestItem>();
        foreach (var parameter in quest.QuestParams)
        {
            if (parameter.ScriptInstruction.ExtractText().StartsWith(ItemParameter, StringComparison.Ordinal)
                && KeyItem(parameter.ScriptArg) is { } item)
            {
                items.Add(item);
            }
        }

        return items;
    }

    /// <summary>The key item with this id, or null when the id is not one.</summary>
    private QuestItem? KeyItem(uint itemId) =>
        QuestItem.IsKeyItem(itemId) && dataManager.GetExcelSheet<EventItem>().GetRowOrDefault(itemId) is { } item
            ? QuestItem.Of(itemId, item)
            : null;

    /// <summary>A line's words as the player would read them. The sheet authors them with macros
    /// standing for whatever the game knows and the sheet cannot: a count the quest is keeping, an
    /// object's name, a whole branch of wording chosen by how far along the player is. Dalamud's
    /// evaluator resolves them, and the quest's own two counts are what a branch is chosen by.
    ///
    /// <para>The counts go in as the first two local parameters because that is what the macros
    /// name them: the first is <c>lnum1</c>, the second <c>lnum2</c>. Leaving them out is silent
    /// rather than loud, because an unresolved branch survives evaluation and is then dropped when
    /// the words are flattened, leaving a hole in the sentence.</para></summary>
    private string Words(ReadOnlySeString words, int have, int needed) =>
        evaluator.Evaluate(words, [have, needed]).ExtractText().StripSoftHyphen();

    /// <summary>The Duty Finder entry that runs a piece of instanced content, or null when the
    /// Finder does not queue for it.</summary>
    private ContentFinderCondition? Finder(uint contentId) =>
        contentId != 0 && dutiesByContent is { } duties && duties.TryGetValue(contentId, out var duty) ? duty : null;

    private List<Mark> ReadMarks(ushort questId)
    {
        if (QuestRow(questId) is not { } quest)
        {
            return [];
        }

        // A quest names one object at a time and names its people all together, so the two are
        // not worth the same. An object it names is the step's own; a person it names is one of
        // everyone the errand involves, and only standing in the circle makes them worth guiding
        // to. Kept apart so a passing face can never take the answer from a named thing.
        var marks = new HashSet<Mark>();
        foreach (var parameter in quest.QuestParams)
        {
            var instruction = parameter.ScriptInstruction.ExtractText();
            if (parameter.ScriptArg == 0)
            {
                continue;
            }

            if (instruction.StartsWith(ObjectParameter, StringComparison.Ordinal))
            {
                marks.Add(new Mark(parameter.ScriptArg, MarkKind.Thing));
            }
            else if (instruction.StartsWith(PersonParameter, StringComparison.Ordinal))
            {
                marks.Add(new Mark(parameter.ScriptArg, MarkKind.Person));
            }
        }

        // A creature the quest names is a place row of its own, and the row says what kind of
        // thing stands there. Lumina types that by the row's own kind, so a creature is only taken
        // as one when the row really says creature.
        foreach (var parameter in quest.QuestParams)
        {
            if (!parameter.ScriptInstruction.ExtractText().StartsWith(CreatureParameter, StringComparison.Ordinal) || parameter.ScriptArg == 0)
            {
                continue;
            }

            if (Creature(parameter.ScriptArg) is { } kind)
            {
                marks.Add(new Mark(kind, MarkKind.Creature));
            }
        }

        // The objects themselves say which event owns them, and a handful of them are owned by a
        // quest that never listed them among its parameters. Both halves together is the whole set.
        var owner = QuestIds.RowId(questId);
        foreach (var thing in dataManager.GetExcelSheet<EObj>())
        {
            if (thing.Data.RowId == owner)
            {
                marks.Add(new Mark(thing.RowId, MarkKind.Thing));
            }
        }

        return [.. marks];
    }

    /// <summary>Temporary: everything the live quest handler holds about this step.</summary>
    private void Probe(ushort questId, byte sequence)
    {
        var events = EventFramework.Instance();
        var control = Control.Instance();
        var handler = events == null ? null : (QuestEventHandler*)events->GetEventHandlerById(questId);
        var player = control == null ? null : control->LocalPlayer;
        if (handler == null || player == null)
        {
            return;
        }

        var args = new List<string>();
        for (byte idx = 0; idx < 8; idx++)
        {
            uint a = 0, b = 0, c = 0;
            handler->GetTodoArgs(player, idx, &a, &b, &c);
            if (a != 0 || b != 0 || c != 0)
            {
                args.Add($"[{idx}] {a},{b},{c} checked={handler->IsTodoChecked(player, idx)}");
            }
        }

        var custom = string.Join(",", handler->CustomTodoValues.ToArray().Select(v => v.ToString(CultureInfo.InvariantCulture)));
        var line = $"seq={sequence} customLoaded={handler->CustomTodoValuesLoaded} custom=[{custom}] instances=[{string.Join(",", handler->InstanceContents.ToArray().Select(v => v.ToString(CultureInfo.InvariantCulture)))}] todoArgs={string.Join(" | ", args)}";
        if (!string.Equals(line, lastHandler, StringComparison.Ordinal))
        {
            lastHandler = line;
            log.Debug($"[handler] {line}");
        }
    }

    /// <summary>Every place a creature this quest names stands, from the place rows the quest
    /// points at.</summary>
    private List<Place> ReadLairs(ushort questId)
    {
        if (QuestRow(questId) is not { } quest)
        {
            return [];
        }

        var lairs = new List<Place>();
        foreach (var parameter in quest.QuestParams)
        {
            if (!parameter.ScriptInstruction.ExtractText().StartsWith(CreatureParameter, StringComparison.Ordinal) || parameter.ScriptArg == 0)
            {
                continue;
            }

            if (dataManager.GetExcelSheet<Level>().GetRowOrDefault(parameter.ScriptArg) is { } where && where.Object.Is<BNpcBase>())
            {
                lairs.Add(new Place(where.Territory.RowId, where.Map.RowId, where.X, where.Y, where.Z));
            }
        }

        return lairs;
    }

    /// <summary>Every duty the Duty Finder can queue for, by the instanced content it runs.</summary>
    private Dictionary<uint, ContentFinderCondition> ReadDuties()
    {
        var duties = new Dictionary<uint, ContentFinderCondition>();
        foreach (var condition in dataManager.GetExcelSheet<ContentFinderCondition>())
        {
            if (condition.Content.Is<InstanceContent>() && condition.Content.RowId != 0)
            {
                duties.TryAdd(condition.Content.RowId, condition);
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
                    emotes.TryAdd(key, new EmoteCommand((ushort)emote.RowId, command.Command.ExtractText()));
                }
            }
        }

        return emotes;
    }

    /// <summary>The quest's whole to-do table as authored, read once per quest.</summary>
    private IReadOnlyList<QuestTodoTemplate> Templates(ushort questId) =>
        templatesByQuest.TryGetValue(questId, out var todos) ? todos : templatesByQuest[questId] = ReadTemplates(questId);

    private Quest? QuestRow(ushort questId) => dataManager.GetExcelSheet<Quest>().GetRowOrDefault(QuestIds.RowId(questId));

    private string ReadName(ushort questId) => QuestRow(questId)?.Name.ExtractText() ?? $"Quest {questId}";

    private List<QuestTodoTemplate> ReadTemplates(ushort questId)
    {
        if (QuestRow(questId) is not { } quest)
        {
            return [];
        }

        var internalName = quest.Id.ExtractText();
        var rows = OpenTextSheet(internalName) is { } raw ? ParseTodoRows(raw, internalName) : [];

        var shapes = new List<(byte Qty, StepShape Shape)>();
        for (var i = 0; i < quest.TodoParams.Count; i++)
        {
            var param = quest.TodoParams[i];
            if (param.ToDoCompleteSeq != UnusedStep)
            {
                shapes.Add((param.ToDoQty, Shape(i, param, rows.GetValueOrDefault(i).ExtractText())));
            }
        }

        var chosen = StepPlaces.Choose([.. shapes.Select(entry => entry.Shape)]);
        var todos = new List<QuestTodoTemplate>();
        for (var i = 0; i < shapes.Count; i++)
        {
            var (qty, shape) = shapes[i];
            todos.Add(new QuestTodoTemplate(shape.Index, shape.Sequence, rows.GetValueOrDefault(shape.Index), qty, chosen[i]));
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
