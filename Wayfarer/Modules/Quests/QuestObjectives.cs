using Wayfarer.Core.Guidance;
using Wayfarer.Core.Quests;

namespace Wayfarer.Modules.Quests;

/// <summary>The quests module's guidance half. Follows the quest the player chose in the journal
/// while it is accepted, and otherwise the main scenario: whichever quest the banner names, and
/// nothing while it shows "???". Reads the game each frame, but only rebuilds
/// the objective when something it depends on changed: the quest, the step, the ToDos' progress
/// the markers, or the key items the player is carrying. Focus is claimed and released by
/// <see cref="QuestsModule"/> as the module goes up and down.</summary>
internal sealed class QuestObjectives(QuestReader reader, QuestFollowing following) : IObjectiveSource
{
    private Signature? last;
    private Objective? cached;

    /// <inheritdoc/>
    public string Name => QuestsModule.ModuleName;

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
        if (QuestToFollow() is not { } questId)
        {
            last = null;
            return cached = null;
        }

        var sequence = QuestReader.Sequence(questId);
        var todos = reader.Todos(questId);
        var progress = reader.Progress(questId, todos.Where(todo => todo.Sequence == sequence).Select(todo => todo.Index));
        var markers = QuestReader.Markers(questId);
        var carried = reader.KeyItems(questId);

        // What the player is carrying is part of what the objective says, so picking a key item up
        // rebuilds the objective even when nothing else about the step moved.
        var signature = new Signature(questId, sequence, Fingerprint(progress), Fingerprint(markers), Fingerprint(carried));
        if (signature == last)
        {
            return cached;
        }

        last = signature;

        // The guide names the main scenario quest itself, so only a quest the player chose to follow
        // instead gives the headline anywhere to lead.
        var headline = following.Followed is { } followed ? new HeadlineAction.OpenQuestJournal(followed) : null;
        return cached = QuestObjectiveBuilder.Build(reader.Name(questId), sequence, todos, progress, markers, reader.Emotes(), headline, reader.Duty(questId), carried);
    }

    /// <summary>The followed quest while it is still accepted; completing or abandoning it hands
    /// guidance back to the main scenario and forgets the choice.</summary>
    private ushort? QuestToFollow()
    {
        if (following.Followed is { } followed)
        {
            if (QuestReader.IsAccepted(followed))
            {
                return followed;
            }

            following.Unfollow();
        }

        return reader.CurrentMainScenarioQuest();
    }

    private sealed record Signature(ushort QuestId, byte Sequence, int Progress, int Markers, int Carried);
}
