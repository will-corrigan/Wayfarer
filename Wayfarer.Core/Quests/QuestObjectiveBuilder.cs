using Wayfarer.Core.Guidance;
using Wayfarer.Core.Routing;

namespace Wayfarer.Core.Quests;

/// <summary>Turns what the game says about a quest's current step into the objective the app
/// guides to. Pure: the game reads are done elsewhere and handed in.
///
/// <para><b>Which lines are still to do.</b> The data lists every to-do line of a step; the game
/// only says which are finished by taking their markers down. So a line is kept while a live marker
/// stands at one of its authored locations, and dropped once none does — unless the quest has no
/// markers at all this frame, in which case the authored locations themselves are the best guess
/// and every line is kept. A line with no location anywhere is kept as blocked, so its words still
/// show.</para>
///
/// <para><b>Words.</b> A line whose sheet text still has a placeholder the game would fill in at
/// runtime, usually a live count, takes the marker's own label when there is one, which the game
/// has already resolved; otherwise the sheet text is used as it is.</para></summary>
public static class QuestObjectiveBuilder
{
    /// <summary>How close a live marker has to stand to an authored location to be that
    /// location's marker. The game places markers exactly on the location row; this is slack for
    /// float error and for markers the game nudges onto the map, not a search radius.</summary>
    public const float MatchYalms = 5f;

    /// <summary>The objective for one step, or null when nothing in it is left to do.</summary>
    /// <param name="questName">What the banner calls the quest.</param>
    /// <param name="sequence">The step the player is on.</param>
    /// <param name="todos">The quest's whole to-do table.</param>
    /// <param name="markers">The game's live markers for the quest this frame.</param>
    public static Objective? Build(
        string questName,
        byte sequence,
        IReadOnlyList<QuestTodo> todos,
        IReadOnlyList<QuestMarker> markers)
    {
        ArgumentNullException.ThrowIfNull(questName);
        ArgumentNullException.ThrowIfNull(todos);
        ArgumentNullException.ThrowIfNull(markers);

        var entries = new List<ObjectiveEntry>();
        var anyAuthoredForStep = false;
        foreach (var todo in todos)
        {
            if (todo.Sequence != sequence)
            {
                continue;
            }

            anyAuthoredForStep = true;
            if (Entry(todo, markers) is { } entry)
            {
                entries.Add(entry);
            }
        }

        // No authored lines for this step at all: the markers are the only description there is.
        if (!anyAuthoredForStep && markers.Count > 0)
        {
            var label = markers.FirstOrDefault(m => m.Label is { Length: > 0 })?.Label ?? questName;
            entries.Add(new ObjectiveEntry(label, null, new Destination.Reachable([.. markers.Select(m => m.At)])));
        }

        return entries.Count == 0 ? null : new Objective(questName, null, entries);
    }

    private static ObjectiveEntry? Entry(QuestTodo todo, IReadOnlyList<QuestMarker> markers)
    {
        var standing = markers.Where(m => todo.Locations.Any(l => Near(l, m.At))).ToList();
        if (standing.Count > 0)
        {
            return new ObjectiveEntry(
                Words(todo, standing),
                null,
                new Destination.Reachable([.. standing.Select(m => m.At)]));
        }

        // The quest has markers, just none at this line's places: the game took this line's down,
        // which is how it says the line is done.
        if (markers.Count > 0)
        {
            return null;
        }

        return todo.Locations.Count > 0
            ? new ObjectiveEntry(Words(todo, standing), null, new Destination.Reachable(todo.Locations))
            : new ObjectiveEntry(Words(todo, standing), null, new Destination.Blocked("no map location for this step"));
    }

    private static string Words(QuestTodo todo, List<QuestMarker> standing)
    {
        if (!todo.HasUnresolvedPlaceholder)
        {
            return todo.Text;
        }

        return standing.FirstOrDefault(m => m.Label is { Length: > 0 })?.Label ?? todo.Text;
    }

    private static bool Near(Place authored, Place live) =>
        authored.Territory == live.Territory
        && MathF.Abs(authored.X - live.X) <= MatchYalms
        && MathF.Abs(authored.Z - live.Z) <= MatchYalms;
}
