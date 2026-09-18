using Wayfarer.Core.Guidance;
using Wayfarer.Core.Routing;

namespace Wayfarer.Core.Quests;

/// <summary>Turns a quest's current step into the objective the app guides to.
///
/// <para>The game never says which ToDos of a step are done; it only removes their quest markers.
/// So a ToDo is kept while a quest marker sits on one of its positions and dropped once none does,
/// unless the quest has no markers at all, when its positions from the data stand in. A ToDo whose
/// words still hold a runtime placeholder takes its marker's label, which the game has already
/// filled in.</para></summary>
public static class QuestObjectiveBuilder
{
    /// <summary>How close a quest marker has to be to a ToDo's position to count as its marker:
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
        var markersAtThisTodo = markers.Where(marker => todo.Positions.Any(position => Near(position, marker.At))).ToList();

        var todoHasMarker = markersAtThisTodo.Count > 0;
        var questHasMarkers = markers.Count > 0;
        var todoHasPosition = todo.Positions.Count > 0;

        return (todoHasMarker, questHasMarkers, todoHasPosition) switch
        {
            (true, _, _) => new ObjectiveEntry(Words(todo, markersAtThisTodo), null, Reachable(markersAtThisTodo)),
            (false, true, _) => null,
            (false, false, true) => new ObjectiveEntry(todo.Text, null, new Destination.Reachable(todo.Positions)),
            (false, false, false) => new ObjectiveEntry(todo.Text, null, new Destination.Blocked(NoLocation)),
        };
    }

    private static List<ObjectiveEntry> DescribedByMarkers(string questName, IReadOnlyList<QuestMarker> markers) =>
        markers.Count == 0
            ? []
            : [new ObjectiveEntry(FirstLabel(markers) ?? questName, null, Reachable(markers))];

    private static string Words(QuestTodo todo, IEnumerable<QuestMarker> markersAtThisTodo) =>
        todo.HasUnresolvedPlaceholder ? FirstLabel(markersAtThisTodo) ?? todo.Text : todo.Text;

    private static string? FirstLabel(IEnumerable<QuestMarker> markers) =>
        markers.Select(marker => marker.Label).FirstOrDefault(label => !string.IsNullOrEmpty(label));

    private static Destination.Reachable Reachable(IEnumerable<QuestMarker> markers) =>
        new([.. markers.Select(marker => marker.At)]);

    private static bool Near(Place position, Place marker) =>
        position.Territory == marker.Territory
        && MathF.Abs(position.X - marker.X) <= MatchYalms
        && MathF.Abs(position.Z - marker.Z) <= MatchYalms;
}
