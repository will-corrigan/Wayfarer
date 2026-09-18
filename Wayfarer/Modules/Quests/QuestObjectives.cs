using Wayfarer.App;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Quests;

namespace Wayfarer.Modules.Quests;

/// <summary>The quests module's guidance half. Follows the main scenario: whichever quest the
/// banner names, and nothing while it shows "???". Reads the game each frame, but only rebuilds
/// the objective when something it depends on changed — the quest, the step, or the markers —
/// so the app is handed the same objective until there is a new one.
///
/// <para>Claims focus when it starts, because following the main scenario is what Wayfarer does
/// when nothing else has been asked of it. There is nothing to reset on being displaced: the main
/// scenario is not a selection.</para></summary>
internal sealed class QuestObjectives : IObjectiveSource
{
    private readonly QuestReader reader;
    private Signature? last;
    private Objective? cached;

    public QuestObjectives(QuestReader reader, IGuidance guidance)
    {
        this.reader = reader;
        guidance.Claim(this);
    }

    /// <inheritdoc/>
    public string Name => "Quests";

    /// <inheritdoc/>
    public Objective? Current => Refresh();

    /// <inheritdoc/>
    public void Displaced()
    {
    }

    /// <summary>A cheap summary of the markers, so a frame in which none moved and none went away
    /// costs a comparison and nothing else.</summary>
    private static int Fingerprint(List<QuestMarker> markers)
    {
        var hash = default(HashCode);
        hash.Add(markers.Count);
        foreach (var marker in markers)
        {
            hash.Add(marker.At);
            hash.Add(marker.Label, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    /// <summary>Reads the game and returns the objective it describes: the cached one while
    /// nothing it depends on has changed, a freshly built one otherwise.</summary>
    private Objective? Refresh()
    {
        if (reader.CurrentMainScenarioQuest() is not { } questId)
        {
            last = null;
            cached = null;
            return null;
        }

        var sequence = QuestReader.Sequence(questId);
        var markers = QuestReader.Markers(questId);
        var signature = new Signature(questId, sequence, Fingerprint(markers));
        if (signature == last)
        {
            return cached;
        }

        last = signature;
        cached = QuestObjectiveBuilder.Build(reader.Name(questId), sequence, reader.Todos(questId), markers);
        return cached;
    }

    private sealed record Signature(ushort QuestId, byte Sequence, int Markers);
}
