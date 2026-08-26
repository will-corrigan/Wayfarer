namespace Wayfarer.Core.Unlocks;

/// <summary>Which of the game's own logs a catalogue entry belongs to, and the mapping from every
/// <see cref="UnlockDefinition.Channel"/> onto one of them.
///
/// <para><b>Why this replaces the four category chips.</b> They answered from <c>type</c>, whose
/// nine values were chosen when the catalogue was 587 duties,
/// systems and a handful of cosmetics. At 1,208 entries it collapses: 158 titles, 53 orchestrion
/// rolls and every emote land on one <c>cosmetic</c> chip, and the 235 entries that open a game
/// FEATURE — the Aesthetician, retainers, glamours, Stone Sky Sea — sit inside it too. A player
/// looking for "what can I now do" had nowhere to look.</para>
///
/// <para><b>Why seven and why these seven.</b> Each one is a window the game already has, so the
/// player does not have to learn our taxonomy to use it: the Duty Finder, the Collection window's
/// tabs, the Achievements list, the Challenge/Crafting/Gathering logs, the Armoury and Grand
/// Company, the map. <see cref="Capabilities"/> is the exception and it is the important one — the
/// game has no window for "features that have opened up", it announces each one with a single
/// message you never see again, so this is the only domain here with no in-game counterpart and the
/// only one whose contents a player cannot look up anywhere else.</para>
///
/// <para><b>Why the domain key is not the channel key.</b> <c>system</c> presents as
/// <see cref="Capabilities"/> because that is what those entries are, and the channel string stays
/// <c>system</c> because the catalogue is generated and <c>data/validate-catalogue-identity.mjs</c>
/// pins its identities. A rename in the data would be a rename of the enumeration's own vocabulary;
/// this is a rename in the presentation, which is where it belongs.</para>
///
/// <para><b>Why there is no default bucket.</b> <see cref="Of"/> returns null for a channel nothing
/// here claims, and every surface that groups by domain has to decide what to do with that. A
/// <c>_ =&gt;</c> fallback is exactly how 158 titles ended up under "Cosmetics": a channel added to
/// the enumeration would land in whichever bucket was last in the switch and nobody would be told.
/// <see cref="Unmapped"/> and <see cref="Conflicts"/> exist so a test can say so instead.</para>
/// </summary>
public static class UnlockDomains
{
    /// <summary>Content you clear, in the order the Duty Finder lists it.</summary>
    public const string Duties = "duties";

    /// <summary>Things the player can now DO — the game's features opening up.</summary>
    public const string Capabilities = "capabilities";

    /// <summary>Things the player can now OWN, matching the Collection window's tabs.</summary>
    public const string Collection = "collection";

    /// <summary>Achievements' own list.</summary>
    public const string Titles = "titles";

    /// <summary>The log family: Challenge, Crafting, Gathering, Hunts, and the standing
    /// relationships (allied societies, custom deliveries) that read the same way.</summary>
    public const string Logs = "logs";

    /// <summary>The Armoury and the Grand Company: what the character can be.</summary>
    public const string Jobs = "jobs";

    /// <summary>Places, and getting to them.</summary>
    public const string Travel = "travel";

    /// <summary>What a row whose channel no domain claims is filed under. It exists so such a row is
    /// still drawn and still says what it is, rather than being dropped from every page — and it is
    /// asserted to be empty for the shipped catalogue, so seeing it means the data has grown a
    /// channel the code has not.</summary>
    public const string Unmapped = "Unclassified";

    /// <summary>The channels each domain claims. This is the ONE statement of the mapping — the
    /// lookup below is derived from it, so the two cannot disagree.
    ///
    /// <para>Ordered as the domains are presented, largest and most actionable first. Every channel
    /// <c>data/unlock-channels.mjs</c> allows appears exactly once across the whole table, including
    /// the five the enumeration ships a channel for but the catalogue has no entries under yet
    /// (<c>aether-current</c>, <c>chocobo-companion</c>, <c>hunt-board</c>, <c>stone-sky-sea</c>,
    /// <c>variant-dungeon</c>). Those are mapped now rather than when the first entry arrives,
    /// because the alternative is a channel appearing in the data and having nowhere to go.</para>
    /// </summary>
    private static readonly (string Domain, string[] Channels)[] Table =
    [
        (Duties,
        [
            "duty",

            // Variant and criterion dungeons are queued from the Duty Finder like everything else
            // here; the enumeration keeps them apart because their unlock rows are shaped
            // differently, which is a fact about the data rather than about where a player looks.
            "variant-dungeon",
        ]),
        (Capabilities,
        [
            "system",

            // "Stone, Sky, Sea Access" is one of the entries the spec for this domain named: a
            // facility that opens, per expansion. The catalogue currently files all five under
            // `system`; the enumeration has its own channel for them and both belong here.
            "stone-sky-sea",

            // The chocobo companion is a capability, not a collectible — it is the game switching
            // on "you may fight alongside a mount". Its BARDING is in Collection, which is the
            // right split: one is a thing you can do, the other is a thing you can own.
            "chocobo-companion",
        ]),
        (Collection,
        [
            "minion", "emote", "orchestrion", "mount", "barding", "fashion-accessory",
            "framers-kit", "facewear", "hairstyle", "triple-triad-card",
        ]),
        (Titles, ["title"]),
        (Logs,
        [
            "challenge-log", "crafting-log-division", "gathering-folklore",

            // Allied societies and custom deliveries are not logs in the game's menus, and they
            // belong with them anyway: both are a standing relationship with a rank that advances
            // on a schedule, which is what every log in this domain is. Filing them under
            // Capabilities would say "you may now do dailies", which is not what a player is
            // looking for when they open this.
            "allied-society", "custom-delivery",

            // The Hunts board is the Hunting log, which is the one log in this family Wayfarer
            // already draws its own page for.
            "hunt-board",
        ]),
        (Jobs, ["job", "grand-company-rank", "general-action"]),
        (Travel,
        [
            "zone",

            // Routing to an individual current works from the same quest-and-giver data every other
            // entry here uses. The per-zone TOTAL does not — no sheet states it — so nothing in
            // this domain may print a denominator until one is found.
            "aether-current",
        ]),
    ];

    private static readonly (Dictionary<string, string> Map, string[] Clashes) Built = Build();

    /// <summary>The domains in presentation order.</summary>
    public static IReadOnlyList<string> All { get; } = [.. Table.Select(t => t.Domain)];

    /// <summary>Channels claimed by more than one domain — empty, and asserted to be. Collected
    /// rather than thrown on, so the whole table can be reported at once and so a fault here cannot
    /// take the plugin down at type-initialisation time.</summary>
    public static IReadOnlyList<string> Conflicts => Built.Clashes;

    /// <summary>Every channel the table claims. Compared against the closed channel set by
    /// <c>UnlockDomainTests</c>: a channel added to the enumeration and not to this table shows up
    /// as a difference there rather than as rows quietly missing from every page.</summary>
    public static IReadOnlyCollection<string> MappedChannels => Built.Map.Keys;

    /// <summary>The domain a channel belongs to, or <see langword="null"/> when nothing here claims
    /// it. Null is a real answer and callers must handle it — see the class remarks for why there is
    /// deliberately no bucket for it to fall into.</summary>
    public static string? Of(string? channel) =>
        channel is { Length: > 0 } key && Built.Map.TryGetValue(key, out var domain) ? domain : null;

    /// <summary>The domain an entry belongs to, or <see langword="null"/>.</summary>
    public static string? Of(UnlockDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Of(definition.Channel);
    }

    /// <summary>The channels a domain claims, or empty for a name that is not a domain.</summary>
    public static IReadOnlyList<string> ChannelsOf(string domain) =>
        Array.Find(Table, t => string.Equals(t.Domain, domain, StringComparison.Ordinal)).Channels ?? [];

    /// <summary>What the player reads. <c>system</c>'s domain is <b>Capabilities</b> here and
    /// nowhere in the data — see the class remarks.</summary>
    public static string Label(string domain) => domain switch
    {
        Duties => "Duties",
        Capabilities => "Capabilities",
        Collection => "Collection",
        Titles => "Titles",
        Logs => "Logs",
        Jobs => "Jobs",
        Travel => "Travel",
        _ => Unmapped,
    };

    /// <summary>Presentation order of a domain, for sorting groups. Unmapped sorts last.</summary>
    public static int Rank(string? domain)
    {
        if (domain is null)
        {
            return All.Count;
        }

        for (var i = 0; i < All.Count; i++)
        {
            if (string.Equals(All[i], domain, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return All.Count;
    }

    private static (Dictionary<string, string> Map, string[] Clashes) Build()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var clashes = new List<string>();
        foreach (var (domain, channels) in Table)
        {
            foreach (var channel in channels)
            {
                if (!map.TryAdd(channel, domain))
                {
                    clashes.Add(channel);
                }
            }
        }

        return (map, [.. clashes]);
    }
}
