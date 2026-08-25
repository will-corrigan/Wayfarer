using System.Globalization;

namespace Wayfarer.Core.Unlocks;

/// <summary>How a job-and-level gate is said, in the game's own words for the set rather than in a
/// list of its members.
///
/// <para><b>The defect this exists to remove.</b> A quest's job gate is a
/// <c>ClassJobCategory</c> row — a mask with one flag per job — and the plugin used to say it by
/// enumerating every flag that was set. On a "any combat job" gate that is a sentence naming thirty
/// jobs, which at the journal page's column width wraps to five lines and reads as a fault:
/// <i>"needs gladiator or pugilist or marauder or lancer or archer or conjurer or thaumaturge or
/// paladin or …"</i>. The mask is not the name of the requirement; it is the implementation of it.
/// </para>
///
/// <para><b>What the game does instead.</b> Every <c>ClassJobCategory</c> row carries its own
/// <c>Name</c>, and that name is what the game prints: "Disciple of War or Magic", "Disciple of the
/// Land", "Disciple of the Hand", or a single job's own name on a job quest. So the whole of the fix
/// is to print the category's name and the level, and never the members —
/// <see cref="ResolvedUnlock.RequiredJobCategoryName"/> is that string, read straight off the row.
/// </para>
///
/// <para><b>Bounded by construction.</b> Even the fallbacks cannot produce a paragraph. A row whose
/// name is blank falls back to the one job it flags, then to
/// <see cref="MaxNamedJobs"/> names and a count of the rest — never to the whole mask. That
/// guarantee is what lets the journal page stop treating a requirement line as something that might
/// wrap five times.</para></summary>
public static class JobGateText
{
    /// <summary>Most jobs an unnamed category will ever be said by name. Three, because three job
    /// names and a level is the longest such phrase that still sets on one line of Axis 14 in the
    /// journal page's 376-wide column, and the point of this class is that no requirement line is
    /// ever a paragraph.</summary>
    public const int MaxNamedJobs = 3;

    /// <summary>"Disciple of War or Magic Lv. 70". The level's own form is the game's own
    /// abbreviation — the same "Lv." the client prints in its quest and duty lists — and it goes
    /// after the thing it qualifies, which is where the game puts it on a journal entry.</summary>
    public static string Describe(string? categoryName, IReadOnlyList<string> jobNames, int level)
    {
        var who = Who(categoryName, jobNames);
        if (who.Length == 0)
        {
            return Level(level);
        }

        return level > 0 ? $"{who} {Level(level)}" : who;
    }

    /// <summary>Who the gate is about, and nothing about the level: the category's own name when the
    /// sheet gives one, then the single job it flags, then at most
    /// <see cref="MaxNamedJobs"/> of them with the remainder counted. Empty when the gate names
    /// nobody, which is the unrestricted case — the caller says the level alone.</summary>
    public static string Who(string? categoryName, IReadOnlyList<string> jobNames)
    {
        ArgumentNullException.ThrowIfNull(jobNames);

        if (categoryName is { Length: > 0 } name && name.Trim().Length > 0)
        {
            return name.Trim();
        }

        if (jobNames.Count == 0)
        {
            return string.Empty;
        }

        if (jobNames.Count <= MaxNamedJobs)
        {
            return string.Join(" or ", jobNames);
        }

        // The cap, and the only place a generated string survives. Naming the first few and
        // counting the rest is a true statement of a set that has no name, and it is one line.
        var rest = jobNames.Count - MaxNamedJobs;
        var named = string.Join(" or ", jobNames.Take(MaxNamedJobs));
        return $"{named} or {rest.ToString(CultureInfo.InvariantCulture)} more";
    }

    /// <summary>"Lv. 70", or empty at level zero — a printed "Lv. 0" is an invented requirement.
    /// </summary>
    public static string Level(int level) =>
        level > 0 ? $"Lv. {level.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
}
