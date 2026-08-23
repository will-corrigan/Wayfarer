namespace Wayfarer.Core.Unlocks;

/// <summary>The chip filters over the checklist — category, priority, level band, zone — and the
/// mapping from an entry's type to the category chip it answers to. Kept out of the windows so the
/// native window and the ImGui fallback cannot filter differently.</summary>
public static class UnlockFilters
{
    public static string Category(UnlockDefinition d) => d.Type switch
    {
        "dungeon" or "trial" or "raid" or "alliance-raid" => "content",
        "mount" or "minion" or "emote" => "cosmetic",
        "zone" => "zone",
        _ => d.Cosmetic ? "cosmetic" : "system",
    };

    public static bool Matches(ResolvedUnlock u, FilterState f)
    {
        if (!f.ShowDone && u.Status == UnlockStatus.Done)
        {
            return false;
        }

        if (f.Categories.Count > 0 && !f.Categories.Contains(Category(u.Def)))
        {
            return false;
        }

        if (f.Priorities.Count > 0 && !f.Priorities.Contains(u.Def.Priority))
        {
            return false;
        }

        if (f.Search.Length > 0)
        {
            var hit = Contains(u.Def.Unlock, f.Search)
                || (u.Def.Quest is { } q && Contains(q, f.Search))
                || (u.ZoneName is { } z && Contains(z, f.Search));
            if (!hit)
            {
                return false;
            }
        }

        return true;

        static bool Contains(string haystack, string needle) =>
            haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>What the player has narrowed the checklist to right now — the chip selections, the
/// level band, the zone, and whether finished entries are shown. Held by whichever window is
/// drawing so the two presentations cannot drift apart on what "filtered" means.</summary>
public sealed class FilterState
{
    public HashSet<string> Categories { get; set; } = [];

    public HashSet<string> Priorities { get; set; } = [];

    public string Search { get; set; } = string.Empty;

    public bool ShowDone { get; set; }
}
