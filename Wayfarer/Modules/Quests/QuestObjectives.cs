using Wayfarer.Core.Guidance;
using Wayfarer.Core.Quests;

namespace Wayfarer.Modules.Quests;

/// <summary>The quests module's guidance half. Follows the main scenario: whichever quest the
/// banner names, and nothing while it shows "???". Reads the game each frame, but only rebuilds
/// the objective when something it depends on changed: the quest, the step, the ToDos' progress
/// or the markers. Focus is claimed and released by <see cref="QuestsModule"/> as the module
/// goes up and down.</summary>
internal sealed class QuestObjectives(QuestReader reader) : IObjectiveSource
{
    private Signature? last;
    private Objective? cached;

    /// <inheritdoc/>
    public string Name => "Quests";

    /// <inheritdoc/>
    public Objective? Current => Refresh();

    /// <inheritdoc/>
    public void Displaced()
    {
    }

    private static int Fingerprint<T>(IEnumerable<T> items)
    {
        var hash = default(HashCode);
        foreach (var item in items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    private Objective? Refresh()
    {
        if (reader.CurrentMainScenarioQuest() is not { } questId)
        {
            last = null;
            return cached = null;
        }

        var sequence = QuestReader.Sequence(questId);
        var todos = reader.Todos(questId);
        var progress = reader.Progress(questId, todos.Where(todo => todo.Sequence == sequence).Select(todo => todo.Index));
        var markers = QuestReader.Markers(questId);

        var signature = new Signature(questId, sequence, Fingerprint(progress), Fingerprint(markers));
        if (signature == last)
        {
            return cached;
        }

        last = signature;
        return cached = QuestObjectiveBuilder.Build(reader.Name(questId), sequence, todos, progress, markers, reader.Emotes());
    }

    private sealed record Signature(ushort QuestId, byte Sequence, int Progress, int Markers);
}
