using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Wayfarer.Routing;

namespace Wayfarer.Modules.Quests;

/// <summary>The game's own to-do list for the quests it is tracking, which is the same list it
/// draws on screen. Every line comes out of it finished: the runtime counts and names the quest
/// text leaves as macros are already filled in, the check marks are already applied, and each line
/// carries the places it is about with their search radius and the mark the map draws on them.
///
/// <para>Read fresh every frame and never held: the list is a vector the game reallocates, and the
/// words inside it are the game's own strings. Everything taken from it is copied.</para>
///
/// <para>It only covers quests the game is tracking. <see cref="QuestReader.Todos"/> reads the same
/// lines out of the quest's own sheet for anything not in it, at the cost of unresolved macros.</para></summary>
internal static unsafe class TrackedQuests
{
    /// <summary>The quest's to-do lines as the game has them right now, or null when the game is
    /// not tracking that quest and the sheet has to answer instead.</summary>
    public static List<QuestTodo>? Todos(ushort questId)
    {
        var state = UIState.Instance();
        if (state == null)
        {
            return null;
        }

        ref var tracked = ref state->QuestTodoList.Todo.TrackedQuests;
        for (var i = 0L; i < (long)tracked.LongCount; i++)
        {
            ref var quest = ref tracked[i];
            if (quest.QuestId == questId)
            {
                return Lines(ref quest);
            }
        }

        return null;
    }

    /// <summary>One tracked quest's lines, in the order the game holds them. The position in that
    /// table is the line's index, the same index the quest's event handler answers about.</summary>
    private static List<QuestTodo> Lines(ref QuestTodoList.TrackedQuest quest)
    {
        var todos = new List<QuestTodo>();
        var objectives = quest.Objectives;
        var count = Math.Clamp(quest.ObjectivesCount, 0, objectives.Length);
        for (var index = 0; index < count; index++)
        {
            ref var objective = ref objectives[index];
            var words = objective.Objective.ToString();
            if (words.Length == 0)
            {
                continue;
            }

            todos.Add(new QuestTodo(
                index,
                (byte)objective.Sequence,
                words,
                HasUnresolvedPlaceholder: false,
                Needed: objective.ToDoParamCountableNum,
                Places(ref objective)));
        }

        return todos;
    }

    /// <summary>Where a line is: one place per level the game listed for it, each with the radius
    /// that makes a search area an area rather than a point.</summary>
    private static List<Place> Places(ref QuestTodoList.TrackedQuest.QuestObjective objective)
    {
        var places = new List<Place>();
        foreach (ref var level in objective.LevelEntries)
        {
            if (level.LevelId != 0)
            {
                places.Add(new Place(objective.TerritoryTypeId, objective.MapId, level.X, level.Y, level.Z, level.Radius));
            }
        }

        return places;
    }
}
