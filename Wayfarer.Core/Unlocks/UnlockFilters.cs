namespace Wayfarer.Core.Unlocks;

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

public sealed class FilterState
{
    public HashSet<string> Categories { get; set; } = [];

    public HashSet<string> Priorities { get; set; } = [];

    public string Search { get; set; } = string.Empty;

    public bool ShowDone { get; set; }
}
