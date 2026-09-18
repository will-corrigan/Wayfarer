using Wayfarer.Core.Guidance;
using Wayfarer.Core.Routing;

namespace Wayfarer.Core.Quests;

/// <summary>Turns a quest's current step into the objective the app guides to: every ToDo of the
/// step the game has not ticked, with its count when it has one, placed at its quest markers when
/// the game is showing them and at its positions from the data otherwise. A ToDo whose words hold
/// a runtime placeholder takes its marker's label, which the game has already filled in.</summary>
public static class QuestObjectiveBuilder
{
    /// <summary>How close a quest marker has to be to a ToDo's position to count as its marker:
    /// slack for float error and for markers the game nudges, not a search radius.</summary>
    public const float MatchYalms = 5f;

    private const string NoLocation = "no map location for this step";

    /// <summary>The objective for one step, or null when nothing in it is left to do.</summary>
    public static Objective? Build(
        string questName,
        byte sequence,
        IReadOnlyList<QuestTodo> todos,
        IReadOnlyList<QuestTodoProgress> progress,
        IReadOnlyList<QuestMarker> markers)
    {
        ArgumentNullException.ThrowIfNull(questName);
        ArgumentNullException.ThrowIfNull(todos);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(markers);

        var step = todos.Where(todo => todo.Sequence == sequence).ToList();
        var entries = step.Count > 0
            ? [.. step.Where(todo => !IsDone(todo, progress)).Select(todo => Entry(todo, progress, markers))]
            : DescribedByMarkers(questName, markers);

        return entries.Count > 0 ? new Objective(questName, null, entries) : null;
    }

    private static bool IsDone(QuestTodo todo, IReadOnlyList<QuestTodoProgress> progress) =>
        progress.Any(p => p.Index == todo.Index && p.Done);

    private static ObjectiveEntry Entry(QuestTodo todo, IReadOnlyList<QuestTodoProgress> progress, IReadOnlyList<QuestMarker> markers)
    {
        var markersAtThisTodo = markers.Where(marker => todo.Positions.Any(position => Near(position, marker.At))).ToList();
        Destination where = markersAtThisTodo.Count > 0 ? Reachable(markersAtThisTodo)
            : todo.Positions.Count > 0 ? new Destination.Reachable(todo.Positions)
            : new Destination.Blocked(NoLocation);

        return new ObjectiveEntry(Words(todo, markersAtThisTodo), Count(todo, progress), where);
    }

    /// <summary>The count for a ToDo that wants more than one of something; null otherwise. The
    /// game's own needed figure wins over the data's when it reports one.</summary>
    private static Progress? Count(QuestTodo todo, IReadOnlyList<QuestTodoProgress> progress)
    {
        var reported = progress.FirstOrDefault(p => p.Index == todo.Index);
        var needed = reported?.Needed > 0 ? reported.Needed : todo.Needed;
        return needed > 1 ? new Progress(reported?.Have ?? 0, needed) : null;
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
