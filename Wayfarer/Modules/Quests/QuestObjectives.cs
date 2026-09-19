using Wayfarer.Guidance;

namespace Wayfarer.Modules.Quests;

/// <summary>The quests module's guidance half. Follows the quest the player chose in the journal
/// while it is accepted, and otherwise the main scenario: whichever quest the banner names, and
/// nothing while it shows "???". Reads the game each frame, but only rebuilds
/// the objective when something it depends on changed: the quest, the step, the ToDos' progress
/// or the markers. Focus is claimed and released by
/// <see cref="QuestsModule"/> as the module goes up and down.</summary>
internal sealed class QuestObjectives(QuestReader reader, QuestFollowing following, QuestJournal journal) : IObjectiveSource
{
    private Signature? last;
    private Objective? cached;

    /// <inheritdoc/>
    public string Name => QuestsModule.ModuleName;

    /// <inheritdoc/>
    public Objective? Current => Refresh();

    /// <inheritdoc/>
    /// <remarks>The plate names the followed quest, so pressing it opens that quest's page in the
    /// journal. Nothing is followed while the main scenario is guiding, and the objective says the
    /// headline is not pressable, so this is not called then.</remarks>
    public void PressHeadline()
    {
        if (following.Followed is { } followed)
        {
            journal.Open(followed);
        }
    }

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
        var progress = reader.Progress(questId, sequence);
        var markers = QuestReader.Markers(questId);
        var signature = new Signature(questId, sequence, Fingerprint(progress), Fingerprint(markers));
        if (signature == last)
        {
            return cached;
        }

        last = signature;

        var todos = reader.Todos(questId, sequence, progress);

        // The guide names the main scenario quest itself, so only a quest the player chose to follow
        // instead gives the headline anywhere to lead.
        var headline = following.Followed is not null;
        return cached = QuestObjectiveBuilder.Build(reader.Name(questId), sequence, todos, progress, markers, reader.Emotes(), headline, reader.Duty(questId), reader.Marks(questId));
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

    private sealed record Signature(ushort QuestId, byte Sequence, int Progress, int Markers);
}
