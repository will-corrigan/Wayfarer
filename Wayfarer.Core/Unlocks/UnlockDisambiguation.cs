namespace Wayfarer.Core.Unlocks;

/// <summary>Tells apart the entries that share a name, from their own bound quests.
///
/// <para>Twelve names cover thirty-five entries in the shipped catalogue: "Sightseeing Log Expansion"
/// five times, "Stone, Sky, Sea Access" five times, "Levequests" three times. <b>They are not
/// duplicates.</b> They are one unlock per expansion or per city, and merging them would lose real
/// information — the player who has the Heavensward sightseeing log and not the Endwalker one has a
/// thing left to do, and a list showing one row cannot say so.</para>
///
/// <para><b>Where the qualifier comes from.</b> The bound quest's own <c>Expansion</c> and
/// <c>PlaceName</c> — never parsed out of the unlock's name. Parsing would be guessing at a string
/// the catalogue's own writers formatted for a human, and it would be wrong on the two groups whose
/// names carry no hint at all. The whole reason the entries bind to a quest ROW is so that questions
/// like this have a sheet to ask.</para>
///
/// <para><b>Why it only applies to shared names.</b> A qualifier on a name nothing collides with is
/// noise: "Glamours" needs no expansion after it. So this is a property of the GROUP, computed across
/// the catalogue once, and an entry whose name is its own keeps its bare name.</para></summary>
public static class UnlockDisambiguation
{
    /// <summary>Sets <see cref="ResolvedUnlock.Qualifier"/> on every entry that shares its name with
    /// another and can be told apart from its quest. Clears it on everything else, so a stale
    /// qualifier cannot survive a recompute.
    ///
    /// <para><b>The rule, in order.</b> Within a group of same-named entries, the expansion wins if
    /// every member has one and they are all different. Failing that, the place name, on the same
    /// condition. Failing both, no qualifier at all — the two entries that share a name AND a quest
    /// row ("Tiisol Ja", "The Promise of Tomorrow") cannot be told apart by anything on the quest,
    /// and they are already told apart by their domain. A row that cannot be disambiguated keeps its
    /// bare name rather than being given a qualifier that does not distinguish it, which would read
    /// as a fact and be one.</para>
    ///
    /// <para><b>Why "all different" and not "some different".</b> A qualifier that repeats is worse
    /// than none: three rows reading "Levequests (A Realm Reborn)" tell the player the catalogue has
    /// three copies of one thing, which is the exact false conclusion the qualifiers exist to
    /// prevent.</para></summary>
    public static void Apply(IReadOnlyList<ResolvedUnlock> all)
    {
        ArgumentNullException.ThrowIfNull(all);

        var groups = new Dictionary<string, List<ResolvedUnlock>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in all)
        {
            entry.Qualifier = null;

            if (!groups.TryGetValue(entry.Def.Unlock, out var members))
            {
                groups[entry.Def.Unlock] = members = [];
            }

            members.Add(entry);
        }

        foreach (var members in groups.Values)
        {
            if (members.Count < 2)
            {
                continue;
            }

            var by = Distinguishing(members);
            if (by is null)
            {
                continue;
            }

            foreach (var entry in members)
            {
                entry.Qualifier = by(entry);
            }
        }
    }

    /// <summary>Which fact tells this group apart, or null when nothing on the quest does.</summary>
    private static Func<ResolvedUnlock, string?>? Distinguishing(List<ResolvedUnlock> members)
    {
        if (AllDistinct(members, e => e.QuestExpansion))
        {
            return e => e.QuestExpansion;
        }

        return AllDistinct(members, e => e.QuestPlaceName) ? e => e.QuestPlaceName : null;
    }

    /// <summary>Whether every member has a value for <paramref name="of"/> and no two share one. An
    /// absent value fails it outright: a group where one row can be named and the others cannot would
    /// come out as one qualified row beside several bare ones, which reads as the qualified one being
    /// the odd one rather than as the set being per-expansion.</summary>
    private static bool AllDistinct(List<ResolvedUnlock> members, Func<ResolvedUnlock, string?> of)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in members)
        {
            if (of(member) is not { Length: > 0 } value || !seen.Add(value))
            {
                return false;
            }
        }

        return true;
    }
}
