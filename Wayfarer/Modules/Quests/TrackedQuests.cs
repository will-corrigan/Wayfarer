using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Wayfarer.Routing;

namespace Wayfarer.Modules.Quests;

/// <summary>The game's own to-do list for the quests it is tracking, which is the same list it
/// draws on screen. Every line comes out of it finished: the runtime counts and names the quest
/// text leaves as macros are already filled in, the check mark is already applied, the counts the
/// quest's event handler would answer are already on the line, and the line carries the places it
/// is about with their search radius.
///
/// <para>Because the words, the check mark and the counts all come off the same line, nothing here
/// has to translate between two of the game's tables, which is the mistake that would show as a
/// line looking finished when it is not.</para>
///
/// <para>Read fresh every frame and never held: the list is a vector the game reallocates, and the
/// words inside it are the game's own strings. Everything taken from it is copied.</para>
///
/// <para>It only covers quests the game is tracking. <see cref="QuestReader.Todos"/> reads the same
/// lines out of the quest's own sheet for anything not in it, at the cost of unresolved macros.</para></summary>
internal static unsafe class TrackedQuests
{
    /// <summary>The quest's step as the game has it right now, or null when the game is not
    /// tracking that quest and the sheet has to answer instead.</summary>
    /// <param name="questId">The quest, as the quest manager numbers it.</param>
    /// <param name="keyItem">How to name a key item the game reports against a line.</param>
    public static TrackedStep? Step(ushort questId, Func<uint, QuestItem?> keyItem)
    {
        ArgumentNullException.ThrowIfNull(keyItem);

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
                return Read(ref quest, keyItem);
            }
        }

        return null;
    }

    /// <summary>One tracked quest's lines, in the order the game holds them.</summary>
    private static TrackedStep Read(ref QuestTodoList.TrackedQuest quest, Func<uint, QuestItem?> keyItem)
    {
        var todos = new List<QuestTodo>();
        var progress = new List<QuestTodoProgress>();
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
                Needed: objective.TodoArg1,
                Places(ref objective)));

            // The three arguments are the same three the quest's event handler answers with, in
            // the same order: how many are done, how many are wanted, and the key item the line is
            // about. The game has already asked, so we read its answer off the line.
            progress.Add(new QuestTodoProgress(
                index,
                objective.IsTodoChecked,
                objective.TodoArg0,
                objective.TodoArg1,
                keyItem((uint)objective.TodoArg2)));
        }

        return new TrackedStep(todos, progress);
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
