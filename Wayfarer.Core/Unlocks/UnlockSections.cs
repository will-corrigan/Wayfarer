namespace Wayfarer.Core.Unlocks;

/// <summary>Turns the visible entries into the sectioned list both windows draw: groups on the axis
/// the player picked, and inside each group the three bands in the order they can be acted on.
///
/// <para><b>Why this is here and not in the window.</b> The two windows drew the list independently
/// and could therefore order it differently — and neither could be tested, because both link
/// against the game. This is the whole of "what goes in what order", it takes computed statuses and
/// returns sections, and every claim about the band order and the sort inside a band is asserted
/// against it directly.</para>
///
/// <para><b>Why the bands are inside the groups and not the other way round.</b> The player's
/// question is "what can I do", but they arrive at it through a domain — they open Capabilities
/// because they want to know what features are waiting. Banding across the whole list and grouping
/// inside would answer a question nobody asked ("show me everything available, sorted by
/// taxonomy"), and it is the view <see cref="UnlockGrouping"/> deliberately does not have: a list
/// with one Available band at the top is the default view's job, not this one's.</para></summary>
public static class UnlockSections
{
    /// <summary>Heading for entries whose quest is in no known zone. Not "Unknown" alone, which
    /// beside a zone name reads like the name of a zone.</summary>
    public const string NoZoneHeading = "Location not known";

    /// <summary>Sections for <paramref name="visible"/>, in presentation order.</summary>
    /// <param name="visible">Entries the filters left, with statuses already computed.</param>
    /// <param name="grouping">The axis to group along.</param>
    /// <param name="currentZone">The zone the player is in, floated to the top when grouping by
    /// zone. Null when it is not known, which sorts everything alphabetically.</param>
    public static List<UnlockGroupSection> Build(
        IEnumerable<ResolvedUnlock> visible, UnlockGrouping grouping, string? currentZone = null)
    {
        ArgumentNullException.ThrowIfNull(visible);

        var groups = new Dictionary<string, List<ResolvedUnlock>>(StringComparer.Ordinal);
        foreach (var entry in visible)
        {
            var key = Heading(entry, grouping);
            if (!groups.TryGetValue(key, out var members))
            {
                groups[key] = members = [];
            }

            members.Add(entry);
        }

        return
        [
            .. Sort(groups, grouping, currentZone)
                .Select(g => new UnlockGroupSection(g.Key, Band(g.Value)))
                .Where(s => s.Bands.Count > 0),
        ];
    }

    /// <summary>The bands inside one group: non-empty only, in <see cref="UnlockBands.All"/> order,
    /// each sorted by level and then by name.
    ///
    /// <para>An empty band is left out rather than drawn empty. A "Blocked (0)" heading is a claim
    /// about nothing, and on a domain where everything is available it would be three quarters of
    /// what is on screen.</para></summary>
    public static IReadOnlyList<UnlockBandSection> Band(IEnumerable<ResolvedUnlock> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var byBand = new Dictionary<UnlockBand, List<ResolvedUnlock>>();
        foreach (var entry in entries)
        {
            var band = UnlockBands.Of(entry.Status);
            if (!byBand.TryGetValue(band, out var members))
            {
                byBand[band] = members = [];
            }

            members.Add(entry);
        }

        var sections = new List<UnlockBandSection>();
        foreach (var band in UnlockBands.All)
        {
            if (byBand.TryGetValue(band, out var members) && members.Count > 0)
            {
                sections.Add(new UnlockBandSection(band, Ordered(members)));
            }
        }

        return sections;
    }

    /// <summary>Level, then name. The spec's order, and the level comes first because inside a band
    /// every row is equally actionable and the level is the only thing left that says which one to do
    /// next. An entry with no stated level sorts after the levelled ones rather than at level zero:
    /// the trophy mounts have no level at all, and sorting them first would put the hardest content
    /// in the catalogue at the top of a beginner's list.</summary>
    private static List<ResolvedUnlock> Ordered(List<ResolvedUnlock> members) =>
        [
            .. members
                .OrderBy(u => SortLevel(u) is 0 ? int.MaxValue : SortLevel(u))
                .ThenBy(u => u.Def.Unlock, StringComparer.OrdinalIgnoreCase),
        ];

    private static int SortLevel(ResolvedUnlock u) =>
        u.QuestLevel > 0 ? u.QuestLevel : u.Def.Level is { } level and > 0 ? level : 0;

    private static string Heading(ResolvedUnlock entry, UnlockGrouping grouping) => grouping switch
    {
        UnlockGrouping.Zone => entry.ZoneName is { Length: > 0 } zone ? zone : NoZoneHeading,
        UnlockGrouping.Level => LevelHeading(SortLevel(entry)),
        _ => UnlockDomains.Label(UnlockDomains.Of(entry.Def) ?? UnlockDomains.Unmapped),
    };

    /// <summary>"Level 30–39", or the honest heading for an entry no source states a level for. The
    /// old form put those in "Level 0–9", which is a number nobody had ever said about them.</summary>
    private static string LevelHeading(int level) =>
        level <= 0
            ? "No stated level"
            : $"Level {(level / 10) * 10}–{((level / 10) * 10) + 9}";

    private static IEnumerable<KeyValuePair<string, List<ResolvedUnlock>>> Sort(
        Dictionary<string, List<ResolvedUnlock>> groups, UnlockGrouping grouping, string? currentZone) =>
        grouping switch
        {
            // Presentation order from UnlockDomains, so the seven always appear in the same places
            // and a player learns where Capabilities is. Alphabetical would put it second by
            // accident and move it the moment a domain is renamed.
            UnlockGrouping.Domain => groups.OrderBy(g => DomainRankOfHeading(g.Key)),

            UnlockGrouping.Level => groups.OrderBy(g => g.Value.Min(u => SortLevel(u)) is 0 ? int.MaxValue : g.Value.Min(u => SortLevel(u))),

            _ => groups
                .OrderByDescending(g => currentZone is { Length: > 0 } && string.Equals(g.Key, currentZone, StringComparison.Ordinal))
                .ThenBy(g => string.Equals(g.Key, NoZoneHeading, StringComparison.Ordinal))
                .ThenBy(g => g.Key, StringComparer.Ordinal),
        };

    private static int DomainRankOfHeading(string heading)
    {
        for (var i = 0; i < UnlockDomains.All.Count; i++)
        {
            if (string.Equals(UnlockDomains.Label(UnlockDomains.All[i]), heading, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return UnlockDomains.All.Count;
    }
}
