using Wayfarer.Core.Guidance;
using Wayfarer.Core.Routing;

namespace Wayfarer.Core.Quests;

/// <summary>Turns a quest's current step into the objective the app guides to.
///
/// <para>The game never says which lines of a step are done; it only takes their markers down. So
/// a line is kept while a marker stands at one of its authored locations and dropped once none
/// does, unless the quest has no markers at all, when the authored locations stand in. A line whose
/// words still hold a runtime placeholder takes its marker's label, which the game has already
/// filled in.</para></summary>
public static class QuestObjectiveBuilder
{
    /// <summary>How close a live marker has to stand to an authored location to be its marker:
    /// slack for float error and for markers the game nudges, not a search radius.</summary>
    public const float MatchYalms = 5f;

    private const string NoLocation = "no map location for this step";

    /// <summary>The objective for one step, or null when nothing in it is left to do.</summary>
    public static Objective? Build(string questName, byte sequence, IReadOnlyList<QuestTodo> todos, IReadOnlyList<QuestMarker> markers)
    {
        ArgumentNullException.ThrowIfNull(questName);
        ArgumentNullException.ThrowIfNull(todos);
        ArgumentNullException.ThrowIfNull(markers);

        var step = todos.Where(todo => todo.Sequence == sequence).ToList();
        var entries = step.Count > 0
            ? [.. step.Select(todo => Entry(todo, markers)).OfType<ObjectiveEntry>()]
            : DescribedByMarkers(questName, markers);

        return entries.Count > 0 ? new Objective(questName, null, entries) : null;
    }

    private static ObjectiveEntry? Entry(QuestTodo todo, IReadOnlyList<QuestMarker> markers)
    {
        var standing = markers.Where(marker => todo.Locations.Any(location => Near(location, marker.At))).ToList();

        var lineHasLiveMarker = standing.Count > 0;
        var questHasAnyLiveMarker = markers.Count > 0;
        var lineHasAuthoredLocation = todo.Locations.Count > 0;

        return (lineHasLiveMarker, questHasAnyLiveMarker, lineHasAuthoredLocation) switch
        {
            (true, _, _) => new ObjectiveEntry(Words(todo, standing), null, Reachable(standing)),
            (false, true, _) => null,
            (false, false, true) => new ObjectiveEntry(todo.Text, null, new Destination.Reachable(todo.Locations)),
            (false, false, false) => new ObjectiveEntry(todo.Text, null, new Destination.Blocked(NoLocation)),
        };
    }

    private static List<ObjectiveEntry> DescribedByMarkers(string questName, IReadOnlyList<QuestMarker> markers) =>
        markers.Count == 0
            ? []
            : [new ObjectiveEntry(FirstLabel(markers) ?? questName, null, Reachable(markers))];

    private static string Words(QuestTodo todo, IEnumerable<QuestMarker> standing) =>
        todo.HasUnresolvedPlaceholder ? FirstLabel(standing) ?? todo.Text : todo.Text;

    private static string? FirstLabel(IEnumerable<QuestMarker> markers) =>
        markers.Select(marker => marker.Label).FirstOrDefault(label => !string.IsNullOrEmpty(label));

    private static Destination.Reachable Reachable(IEnumerable<QuestMarker> markers) =>
        new([.. markers.Select(marker => marker.At)]);

    private static bool Near(Place authored, Place live) =>
        authored.Territory == live.Territory
        && MathF.Abs(authored.X - live.X) <= MatchYalms
        && MathF.Abs(authored.Z - live.Z) <= MatchYalms;
}
