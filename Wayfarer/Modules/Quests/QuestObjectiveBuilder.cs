using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Wayfarer.Guidance;
using Wayfarer.Routing;
using Wayfarer.World;

namespace Wayfarer.Modules.Quests;

/// <summary>Turns a quest's current step into the objective the app guides to: every ToDo of the
/// step the game has not ticked, with its count when it has one, placed at its quest markers when
/// the game is showing them and at its positions from the data otherwise. A ToDo that has the
/// player use an item, emote or say something carries that as its action.
///
/// <para>A step is inside one of the quest's duties when the data puts it in that duty's own
/// territory, which is the game saying the step happens in there rather than anywhere a player can
/// walk to. A step with nowhere on a map to go at all is treated the same way when the quest has
/// one duty. There is nothing to walk to
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
        IReadOnlyList<QuestDuty>? duties = null,
        IReadOnlyList<QuestItem>? items = null,
        IReadOnlyList<Mark>? marks = null,
        EventId? owner = null,
        IReadOnlyList<Place>? lairs = null,
        QuestTarget? target = null,
        string? kind = null)
    {
        ArgumentNullException.ThrowIfNull(questName);
        ArgumentNullException.ThrowIfNull(todos);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(markers);

        var step = todos.Where(todo => todo.Sequence == sequence).ToList();
        var entries = step.Count > 0
            ? [.. step.Where(todo => !IsDone(todo, progress)).Select(todo => Entry(todo, progress, markers, emotes ?? NoEmotes, duties ?? [], items, marks, owner, lairs, target))]
            : DescribedByMarkers(questName, markers, duties ?? []);

        return entries.Count > 0 ? new Objective(questName, entries, headlinePressable, kind) : null;
    }

    private static bool IsDone(QuestTodo todo, IReadOnlyList<QuestTodoProgress> progress) =>
        progress.Any(p => p.Index == todo.Index && p.Done);

    private static ObjectiveEntry Entry(
        QuestTodo todo,
        IReadOnlyList<QuestTodoProgress> progress,
        IReadOnlyList<QuestMarker> markers,
        IReadOnlyDictionary<string, EmoteCommand> emotes,
        IReadOnlyList<QuestDuty> duties,
        IReadOnlyList<QuestItem>? items,
        IReadOnlyList<Mark>? marks,
        EventId? owner,
        IReadOnlyList<Place>? lairs,
        QuestTarget? target)
    {
        var markersAtThisTodo = markers.Where(marker => todo.Positions.Any(position => Near(position, marker.At))).ToList();
        Destination where = todo.Roulette is { } roulette ? new Destination.InRoulette(roulette)
            : Entered(todo, duties) is { } entered ? new Destination.InDuty(entered.Finder)
            : todo.Positions.Count == 0 ? Nowhere(duties)
            : Somewhere(target, markersAtThisTodo.Count > 0 ? Places(markersAtThisTodo) : todo.Positions, marks, owner, lairs);

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

    /// <summary>The duty this step happens inside, or null when it happens outside all of them. The
    /// data puts such a step in the duty's own territory, which is not a place the player can be
    /// walked to, and no two duties a quest names share a territory. A step named anywhere else is
    /// an ordinary place, even for a quest that has duties, because most of a duty quest happens
    /// outside them.</summary>
    private static QuestDuty? Entered(QuestTodo todo, IReadOnlyList<QuestDuty> duties)
    {
        foreach (var duty in duties)
        {
            if (duty.Territory is { } territory && todo.Positions.Any(position => position.Territory == territory))
            {
                return duty;
            }
        }

        return null;
    }

    /// <summary>Where a step with nothing on a map to go to is: inside the quest's one duty, or
    /// nowhere the app can help with. A quest that names several duties and positions a step
    /// nowhere is not saying which -- the one live case lists three dungeons as examples for a
    /// roulette -- so none is guessed at.</summary>
    private static Destination Nowhere(IReadOnlyList<QuestDuty> duties) =>
        duties is [var only] ? new Destination.InDuty(only.Finder) : new Destination.Blocked(NoLocation);

    /// <summary>What a quest with no to-do list for this step is about: wherever its markers are,
    /// or its duty when it has neither.</summary>
    private static List<ObjectiveEntry> DescribedByMarkers(string questName, IReadOnlyList<QuestMarker> markers, IReadOnlyList<QuestDuty> duties) =>
        markers.Count > 0 ? [new ObjectiveEntry(FirstLabel(markers) ?? questName, null, new Destination.Reachable(Places(markers)))]
            : duties is [var only] ? [new ObjectiveEntry(questName, null, new Destination.InDuty(only.Finder))]
            : [];

    private static string? FirstLabel(IEnumerable<QuestMarker> markers) =>
        markers.Select(marker => marker.Label).FirstOrDefault(label => !string.IsNullOrEmpty(label));

    /// <summary>What the game's own markers say, carrying where the quest's creatures are known to
    /// stand along as the answer for a circle that is still empty when the player gets there.</summary>
    private static IReadOnlyList<Place> Places(IEnumerable<QuestMarker> markers) =>
        [.. markers.Select(marker => marker.At)];

    /// <summary>Where a step sends the player, asked of the module's own resolver. Without one —
    /// which is how the objective is built in a test, where there is no world to look at — the
    /// places stand as they are.</summary>
    private static Destination Somewhere(QuestTarget? target, IReadOnlyList<Place> places, IReadOnlyList<Mark>? marks, EventId? owner, IReadOnlyList<Place>? lairs) =>
        target?.Where(places, marks, owner, lairs) ?? new Destination.Reachable(places);

    /// <summary>Whether a marker belongs to a step's place: standing on it, or standing inside it
    /// when the place is a circle to search. The game draws its own marker where it really wants
    /// the player, and inside a wide circle that is a far better answer than the circle's middle.</summary>
    private static bool Near(Place position, Place marker) =>
        position.Territory == marker.Territory
        && position.OnTheGround(marker) <= MathF.Max(MatchYalms, position.Radius);

    /// <summary>How far apart two places are across the ground.</summary>
}
