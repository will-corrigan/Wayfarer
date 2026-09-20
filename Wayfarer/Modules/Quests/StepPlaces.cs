using Wayfarer.Routing;

namespace Wayfarer.Modules.Quests;

/// <summary>Which of the places a quest's to-do line names the line is actually about.
///
/// <para>The sheet hangs more on a line than the line asks for. A quest pins the hall it happens
/// in, the door back out, the one who sent you, on step after step; and a line that sends the
/// player to search wide ground will still name whoever happens to be standing in it. Either can
/// be the nearer of what a line offers, which is enough to win a route to it and send the player
/// away from what they were asked to do.</para>
///
/// <para>Nothing here reads the world or the player's progress. It is the sheet's own account of a
/// quest, decided once when the quest is first read, which is why it can be put to a whole
/// quest's worth of real steps and checked.</para></summary>
internal static class StepPlaces
{
    /// <summary>How many of a quest's steps a thing has to stand on before being on most of them
    /// means anything. A quest with one located step names its door on all of its steps, which is
    /// not the same as carrying it through.</summary>
    private const int LeastStepsToBeFurniture = 2;

    /// <summary>How short a word of a thing's name has to be before finding it in a line's words
    /// says nothing: "of", "the", "to".</summary>
    private const int ShortestTellingWord = 3;

    private static readonly char[] NameSeparators = [' ', '\''];

    /// <summary>Where each of a quest's lines sends the player, in the order the lines come.</summary>
    /// <param name="steps">Every located line of one quest. Lines naming nowhere may be left out.</param>
    public static IReadOnlyList<IReadOnlyList<Place>> Choose(IReadOnlyList<StepShape> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var furniture = Furniture(steps);
        return [.. steps.Select(step => Chosen(step, furniture))];
    }

    /// <summary>The things a quest carries through most of its own steps. Never a person: a quest
    /// sends the player back to the same one over and over on purpose, and taking those away would
    /// send them to the wrong one.</summary>
    public static HashSet<uint> Furniture(IReadOnlyList<StepShape> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var located = steps.Where(step => step.Places.Count > 0).ToList();
        var stepsPerRow = new Dictionary<uint, int>();
        foreach (var step in located)
        {
            foreach (var row in step.Places.Where(place => place.IsObject).Select(place => place.Row).Distinct())
            {
                stepsPerRow[row] = stepsPerRow.GetValueOrDefault(row) + 1;
            }
        }

        var most = located.Count / 2d;
        return [.. stepsPerRow.Where(pair => pair.Value >= LeastStepsToBeFurniture && pair.Value > most).Select(pair => pair.Key)];
    }

    /// <summary>Whether a line's own words name what stands at a place.
    ///
    /// <para>Both halves come from the game in the player's own language, so the two are always
    /// written the same way. The whole name is looked for first, which is the only thing that can
    /// be done in a language that puts no spaces between its words; then each word of it in turn,
    /// because a sentence rarely spells a thing out in full: a line says "pass through the portal"
    /// where the thing is called "portal of wisdom".</para></summary>
    public static bool NamedIn(string name, string words)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(words))
        {
            return false;
        }

        return words.Contains(name, StringComparison.CurrentCultureIgnoreCase)
            || name.Split(NameSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(word => word.Length > ShortestTellingWord && words.Contains(word, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>Where one line sends the player: its own places, less the quest's furniture and
    /// less whoever is only standing in the ground it says to search.</summary>
    private static IReadOnlyList<Place> Chosen(StepShape step, HashSet<uint> furniture)
    {
        // A line that names the thing wants the thing, whatever the rest of the quest does with
        // it: "pass through the portal" is about the portal even on a quest that pins that portal
        // from beginning to end.
        var wanted = step.Places
            .Where(place => !furniture.Contains(place.Row) || NamedIn(place.ObjectName, step.Words))
            .ToList();

        // Taking the furniture out must never leave a line with nowhere at all.
        var left = wanted.Count > 0 ? wanted : step.Places;

        // A place either names something standing at it or is bare ground to search. When the
        // ground outnumbers the named, the ground is the errand and the named are standing in it.
        var bare = left.Where(place => place.ObjectId == 0).ToList();
        var named = left.Count - bare.Count;
        var chosen = named > 0 && bare.Count > named ? bare : left;

        return [.. chosen.Select(place => place.At)];
    }
}
