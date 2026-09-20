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
    /// <summary>What the guide's own heading says while the plate carries a followed quest rather
    /// than the main scenario the guide is about.</summary>
    private const string FollowedHeader = "Followed Quest";

    private Signature? last;
    private Objective? cached;

    /// <summary>The quest the plate is naming right now, which is what pressing it leads to.</summary>
    private ushort? guided;

    /// <inheritdoc/>
    public string Name => QuestsModule.ModuleName;

    /// <inheritdoc/>
    public Objective? Current => Refresh();

    /// <inheritdoc/>
    /// <remarks>The plate names whichever quest is being guided, followed or the main scenario, so
    /// pressing it opens that quest's page in the journal.</remarks>
    public void PressHeadline()
    {
        if (guided is { } questId)
        {
            journal.Open(questId);
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
            guided = null;
            return cached = null;
        }

        guided = questId;

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

        // The plate leads to the journal page of whichever quest it names, followed or not. Only a
        // quest the player chose to follow retitles the heading above it: the guide already says
        // what it is about when the main scenario is the one being guided.
        var followed = following.Followed is not null;
        var duty = reader.Duty(questId);
        return cached = QuestObjectiveBuilder.Build(
            reader.Name(questId),
            sequence,
            todos,
            progress,
            markers,
            reader.Emotes(),
            true,
            duty,
            reader.Items(questId),
            reader.Marks(questId),
            QuestIds.Event(questId),
            reader.Lairs(questId),
            followed ? FollowedHeader : null);
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
