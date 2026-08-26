namespace Wayfarer.Core.Unlocks;

/// <summary>The chip filters over the checklist — domain, priority, search, whether finished entries
/// are listed. Kept out of the windows so the native window and the ImGui fallback cannot filter
/// differently.</summary>
public static class UnlockFilters
{
    /// <summary>The domain chip an entry answers to.
    ///
    /// <para><b>This used to be four buckets read off <c>type</c>, and it is now
    /// <see cref="UnlockDomains"/>.</b> The old mapping asked <c>type</c>, which has nine values
    /// chosen when the catalogue was 587 duties and systems, and every value it had no word for fell
    /// through <c>d.Cosmetic ? "cosmetic" : "system"</c>. At 1,208 entries that put 158 titles, 53
    /// orchestrion rolls and every emote on one chip, and buried the 235 entries that open a game
    /// feature inside it — the single most useful category in the catalogue, invisible.</para>
    ///
    /// <para>Reads <c>channel</c> rather than <c>type</c>: the channel is the enumeration's own
    /// vocabulary, generated per entry, and it has a word for every kind of thing the catalogue
    /// holds. Null when no domain claims the channel, which is a state
    /// <c>UnlockDomainTests</c> asserts the shipped catalogue is never in.</para></summary>
    public static string? Domain(UnlockDefinition d) => UnlockDomains.Of(d);

    public static bool Matches(ResolvedUnlock u, FilterState f)
    {
        if (!f.ShowDone && u.Status == UnlockStatus.Done)
        {
            return false;
        }

        // An entry with no domain is never hidden by a domain chip. It is already the one row the
        // taxonomy failed to place, and silently filtering it out is how it would stop being
        // noticed — see UnlockDomains for why there is no bucket for it to land in instead.
        if (f.Domains.Count > 0 && Domain(u.Def) is { } domain && !f.Domains.Contains(domain))
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
/// search text, and whether finished entries are shown. Held by whichever window is drawing so the
/// two presentations cannot drift apart on what "filtered" means.</summary>
public sealed class FilterState
{
    /// <summary>Selected domain chips, by <see cref="UnlockDomains"/> key. Empty means all of them,
    /// which is what the tab opens on.</summary>
    public HashSet<string> Domains { get; set; } = [];

    public HashSet<string> Priorities { get; set; } = [];

    public string Search { get; set; } = string.Empty;

    public bool ShowDone { get; set; }
}
