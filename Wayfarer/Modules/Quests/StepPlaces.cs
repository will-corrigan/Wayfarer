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
        var company = Company(steps);
        return [.. steps.Select(step => Chosen(step, furniture, company))];
    }

    /// <summary>Where a line sends a player already on the quest's ride, or null when riding changes
    /// nothing.
    ///
    /// <para>A line that has the player ride or pilot something, or act while transformed, names
    /// the ground the errand is on and also whoever hands out the ride, so a player who gets off can
    /// find their way back on: "Ride the amaro to the sentry at Radisca's Round" names the circle
    /// round the sentry and the amaro's keeper beside the player. Once on the ride the keeper is
    /// only in the way, and being nearest they would win the route. So the people the line does not
    /// name are left out; one it does name, as in "Pilot the magitek armor back to Wedge", is where
    /// the ride is going and stays.</para></summary>
    /// <param name="step">The line, with every place it names.</param>
    /// <param name="chosen">Where the line sends a player on foot, from <see cref="Choose"/>: riding only
    /// ever takes places away from that, so whatever it already left out stays out.</param>
    public static IReadOnlyList<Place>? WhileRiding(StepShape step, IReadOnlyList<Place> chosen)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(chosen);

        if (!step.Places.Any(place => place.ObjectId == 0))
        {
            return null;
        }

        // Only the quest's own people who hand a ride out, never a thing to act on or a monster to fight.
        var keepers = step.Places
            .Where(place => place.IsPerson && !NamedIn(place.ObjectName, step.Words))
            .Select(place => place.At)
            .ToHashSet();
        var riding = chosen.Where(place => !keepers.Contains(place)).ToList();
        return riding.Count == chosen.Count || riding.Count == 0 ? null : riding;
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

    /// <summary>The things a quest carries through most of its own steps. Never a person: a quest
    /// sends the player back to the same one over and over on purpose, and taking those away would
    /// send them to the wrong one.</summary>
    private static HashSet<uint> Furniture(IReadOnlyList<StepShape> steps)
    {
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

    /// <summary>The people a quest lists on most of its own lines: whoever sent the player, kept on
    /// every line so they can go back and ask again. "Put to the Proof" lists Y'shtola on every line
    /// while the player searches three ruins for statues, and being beside the player she won each
    /// search. Unlike furniture they are only left out of lines with ground to search that do not
    /// name them; "Deliver the seal to Y'shtola" still goes to her.</summary>
    private static HashSet<uint> Company(IReadOnlyList<StepShape> steps)
    {
        var located = steps.Where(step => step.Places.Count > 0).ToList();
        var stepsPerRow = new Dictionary<uint, int>();
        foreach (var step in located)
        {
            foreach (var row in step.Places.Where(place => place.IsPerson).Select(place => place.Row).Distinct())
            {
                stepsPerRow[row] = stepsPerRow.GetValueOrDefault(row) + 1;
            }
        }

        var most = located.Count / 2d;
        return [.. stepsPerRow.Where(pair => pair.Value >= LeastStepsToBeFurniture && pair.Value > most).Select(pair => pair.Key)];
    }

    /// <summary>Where one line sends the player: its own places, less the quest's furniture and
    /// less whoever is only standing in the ground it says to search.</summary>
    private static IReadOnlyList<Place> Chosen(StepShape step, HashSet<uint> furniture, HashSet<uint> company)
    {
        // A line that names the thing wants the thing, whatever the rest of the quest does with
        // it: "pass through the portal" is about the portal even on a quest that pins that portal
        // from beginning to end. The quest's company is left out only of a line that has ground of
        // its own to search and does not name them.
        var ground = step.Places.Any(place => place.ObjectId == 0);
        var wanted = step.Places
            .Where(place => !furniture.Contains(place.Row) || NamedIn(place.ObjectName, step.Words))
            .Where(place => !(ground && company.Contains(place.Row) && !NamedIn(place.ObjectName, step.Words)))
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
