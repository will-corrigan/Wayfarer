using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Wayfarer.Guidance;
using Wayfarer.Routing;

namespace Wayfarer.Modules.Quests;

/// <summary>Turns a quest's current step into the objective the app guides to: every ToDo of the
/// step the game has not ticked, with its count when it has one, placed at its quest markers when
/// the game is showing them and at its positions from the data otherwise. A ToDo that has the
/// player use an item, emote or say something carries that as its action.
///
/// <para>A step is inside the quest's duty when the data puts it in the duty's own territory,
/// which is the game saying the step happens in there rather than anywhere a player can walk to.
/// A step with nowhere on a map to go at all is treated the same way. There is nothing to walk to
/// in either case, so the guidance is to queue for it.</para></summary>
internal static class QuestObjectiveBuilder
{
    /// <summary>How close a quest marker has to be to a ToDo's position to count as its marker when
    /// that position is a point: slack for float error and for markers the game nudges. A position
    /// that is a circle uses its own radius instead.</summary>
    private const float MatchYalms = 5f;

    private const string NoLocation = "no map location for this step";

    private static readonly Dictionary<string, EmoteCommand> NoEmotes = new(StringComparer.Ordinal);

    /// <summary>The objective for one step, or null when nothing in it is left to do.</summary>
    public static Objective? Build(
        string questName,
        byte sequence,
        IReadOnlyList<QuestTodo> todos,
        IReadOnlyList<QuestTodoProgress> progress,
        IReadOnlyList<QuestMarker> markers,
        IReadOnlyDictionary<string, EmoteCommand>? emotes = null,
        bool headlinePressable = false,
        QuestDuty? duty = null,
        IReadOnlyList<QuestItem>? items = null,
        IReadOnlyList<Mark>? marks = null,
        EventId? owner = null,
        IReadOnlyList<Place>? lairs = null,
        string? kind = null)
    {
        ArgumentNullException.ThrowIfNull(questName);
        ArgumentNullException.ThrowIfNull(todos);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(markers);

        var step = todos.Where(todo => todo.Sequence == sequence).ToList();
        var entries = step.Count > 0
            ? [.. step.Where(todo => !IsDone(todo, progress)).Select(todo => Entry(todo, progress, markers, emotes ?? NoEmotes, duty, items, marks, owner, lairs))]
            : DescribedByMarkers(questName, markers, duty);

        return entries.Count > 0 ? new Objective(questName, entries, headlinePressable, kind) : null;
    }

    private static bool IsDone(QuestTodo todo, IReadOnlyList<QuestTodoProgress> progress) =>
        progress.Any(p => p.Index == todo.Index && p.Done);

    private static ObjectiveEntry Entry(
        QuestTodo todo,
        IReadOnlyList<QuestTodoProgress> progress,
        IReadOnlyList<QuestMarker> markers,
        IReadOnlyDictionary<string, EmoteCommand> emotes,
        QuestDuty? duty,
        IReadOnlyList<QuestItem>? items,
        IReadOnlyList<Mark>? marks,
        EventId? owner,
        IReadOnlyList<Place>? lairs)
    {
        var markersAtThisTodo = markers.Where(marker => todo.Positions.Any(position => Near(position, marker.At))).ToList();
        Destination where = Enters(todo, duty) || todo.Positions.Count == 0 ? Nowhere(duty)
            : markersAtThisTodo.Count > 0 ? Reachable(markersAtThisTodo, marks, owner, lairs)
            : new Destination.Reachable(todo.Positions, marks, owner, lairs);

        var reported = progress.FirstOrDefault(p => p.Index == todo.Index);
        var words = todo.Text;
        return new ObjectiveEntry(words, Count(todo, reported), where, QuestTodoActions.From(words, reported?.Item, emotes, items));
    }

    /// <summary>The count for a ToDo that wants more than one of something; null otherwise. The
    /// game's own needed figure wins over the data's when it reports one.</summary>
    private static Progress? Count(QuestTodo todo, QuestTodoProgress? reported)
    {
        var needed = reported?.Needed > 0 ? reported.Needed : todo.Needed;
        return needed > 1 ? new Progress(reported?.Have ?? 0, needed) : null;
    }

    /// <summary>Whether this step happens inside the quest's duty: the data puts it in the duty's
    /// own territory, which is not a place the player can be walked to. A step named anywhere else
    /// is an ordinary place, even for a quest that has a duty, because most of a duty quest happens
    /// outside it.</summary>
    private static bool Enters(QuestTodo todo, QuestDuty? duty) =>
        duty?.Territory is { } territory && todo.Positions.Any(position => position.Territory == territory);

    /// <summary>Where a step with nothing on a map to go to is: inside the quest's duty, or
    /// nowhere the app can help with.</summary>
    private static Destination Nowhere(QuestDuty? duty) =>
        duty is { } instance ? new Destination.InDuty(instance.Finder) : new Destination.Blocked(NoLocation);

    /// <summary>What a quest with no to-do list for this step is about: wherever its markers are,
    /// or its duty when it has neither.</summary>
    private static List<ObjectiveEntry> DescribedByMarkers(string questName, IReadOnlyList<QuestMarker> markers, QuestDuty? duty) =>
        markers.Count > 0 ? [new ObjectiveEntry(FirstLabel(markers) ?? questName, null, Reachable(markers))]
            : duty is { } instance ? [new ObjectiveEntry(questName, null, new Destination.InDuty(instance.Finder))]
            : [];

    private static string? FirstLabel(IEnumerable<QuestMarker> markers) =>
        markers.Select(marker => marker.Label).FirstOrDefault(label => !string.IsNullOrEmpty(label));

    /// <summary>What the game's own markers say, carrying where the quest's creatures are known to
    /// stand along as the answer for a circle that is still empty when the player gets there.</summary>
    private static Destination.Reachable Reachable(IEnumerable<QuestMarker> markers, IReadOnlyList<Mark>? marks = null, EventId? owner = null, IReadOnlyList<Place>? lairs = null) =>
        new([.. markers.Select(marker => marker.At)], marks, owner, lairs);

    /// <summary>Whether a marker belongs to a step's place: standing on it, or standing inside it
    /// when the place is a circle to search. The game draws its own marker where it really wants
    /// the player, and inside a wide circle that is a far better answer than the circle's middle.</summary>
    private static bool Near(Place position, Place marker) =>
        position.Territory == marker.Territory
        && Apart(position, marker) <= MathF.Max(MatchYalms, position.Radius);

    /// <summary>How far apart two places are across the ground.</summary>
    private static float Apart(Place from, Place to) =>
        MathF.Sqrt(((from.X - to.X) * (from.X - to.X)) + ((from.Z - to.Z) * (from.Z - to.Z)));
}
