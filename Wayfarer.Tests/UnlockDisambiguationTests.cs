using Wayfarer.Core.Ui;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>Twelve names cover thirty-five entries, and they are not duplicates: one Sightseeing Log
/// per expansion, one Levequest unlock per city. These assert the rule that tells them apart and, as
/// importantly, the two cases where it declines to.</summary>
public class UnlockDisambiguationTests
{
    private static readonly string[] TheFiveExpansions =
        ["A Realm Reborn", "Heavensward", "Stormblood", "Shadowbringers", "Endwalker"];

    private static readonly string[] TheThreeCities = ["Limsa Lominsa", "Gridania", "Ul'dah"];

    /// <summary>The per-expansion set: five entries with one name, told apart by their own quests'
    /// expansions, and the qualifier lands on the row's name.</summary>
    [Fact]
    public void EntriesRepeatedPerExpansionAreToldApartByTheirExpansion()
    {
        var entries = TheFiveExpansions
            .Select(x => Entry("Sightseeing Log Expansion", expansion: x))
            .ToList();

        UnlockDisambiguation.Apply(entries);

        Assert.Equal(TheFiveExpansions, entries.Select(e => e.Qualifier), StringComparer.Ordinal);
        Assert.Equal("Sightseeing Log Expansion (Heavensward)", UnlockRowText.Name(entries[1]));
    }

    /// <summary>The per-city set: three entries in one expansion, told apart by the place their quest
    /// is in, because the expansion cannot do it. This is the case a naive "always use the expansion"
    /// rule would have qualified with the same word three times.</summary>
    [Fact]
    public void EntriesRepeatedPerCityAreToldApartByTheirPlace()
    {
        List<ResolvedUnlock> entries =
        [
            Entry("Levequests", expansion: "A Realm Reborn", place: "Limsa Lominsa"),
            Entry("Levequests", expansion: "A Realm Reborn", place: "Gridania"),
            Entry("Levequests", expansion: "A Realm Reborn", place: "Ul'dah"),
        ];

        UnlockDisambiguation.Apply(entries);

        Assert.Equal(TheThreeCities, entries.Select(e => e.Qualifier), StringComparer.Ordinal);
        Assert.Equal("Levequests (Ul'dah)", UnlockRowText.Name(entries[2]));
    }

    /// <summary>Two entries that share a name AND a quest row keep their bare names. Nothing on the
    /// quest distinguishes them — "Tiisol Ja" is both a custom-delivery client and that client's
    /// crafting-log division, granted by the same quest — so there is no expansion and no place that
    /// tells them apart, and a qualifier would be a fact invented to fill a slot.
    ///
    /// <para><b>This is the one case the qualifier does not finish.</b> The other same-quest pair,
    /// "The Promise of Tomorrow", is an orchestrion roll and a title, so its two rows land in
    /// different domains and are separated by the page they are on. These two are both
    /// <see cref="UnlockDomains.Logs"/>, so they sit in one list under one name — told apart only by
    /// their second line, which does differ (a delivery client's blurb against a crafting log's). The
    /// channel would name them, and reading it here would mean qualifying from our own taxonomy
    /// rather than from the game's data, which is a different rule from the one this class
    /// implements. Asserted rather than left implicit, so the gap is on the record.</para></summary>
    [Fact]
    public void EntriesThatShareAQuestKeepTheirBareNames()
    {
        List<ResolvedUnlock> entries =
        [
            Entry("Tiisol Ja", expansion: "Dawntrail", place: "Tuliyollal", channel: "custom-delivery"),
            Entry("Tiisol Ja", expansion: "Dawntrail", place: "Tuliyollal", channel: "crafting-log-division"),
        ];

        UnlockDisambiguation.Apply(entries);

        Assert.All(entries, e => Assert.Null(e.Qualifier));
        Assert.Equal("Tiisol Ja", UnlockRowText.Name(entries[0]));

        // Both in one domain, so the domain does not separate them either — and the only thing that
        // would is the channel, which these two do differ on.
        Assert.Equal(UnlockDomains.Of(entries[0].Def), UnlockDomains.Of(entries[1].Def), StringComparer.Ordinal);
        Assert.NotEqual(entries[0].Def.Channel, entries[1].Def.Channel, StringComparer.Ordinal);
    }

    /// <summary>The other same-quest pair does not need a qualifier, because its two rows are on
    /// different pages: an orchestrion roll is Collection and a title is Titles.</summary>
    [Fact]
    public void TheOtherSameQuestPairIsSeparatedByItsDomain()
    {
        var roll = Entry("The Promise of Tomorrow", expansion: "Dawntrail", channel: "orchestrion");
        var title = Entry("The Promise of Tomorrow", expansion: "Dawntrail", channel: "title");

        UnlockDisambiguation.Apply([roll, title]);

        Assert.Null(roll.Qualifier);
        Assert.Null(title.Qualifier);
        Assert.Equal(UnlockDomains.Collection, UnlockDomains.Of(roll.Def));
        Assert.Equal(UnlockDomains.Titles, UnlockDomains.Of(title.Def));
    }

    /// <summary>A group where only some members can be named is left alone entirely. One qualified
    /// row beside three bare ones reads as the qualified one being the odd one out, rather than as
    /// the set being per-expansion.</summary>
    [Fact]
    public void APartlyNameableGroupIsLeftAlone()
    {
        List<ResolvedUnlock> entries =
        [
            Entry("Role Quests Access", expansion: "Shadowbringers"),
            Entry("Role Quests Access", expansion: null),
        ];

        UnlockDisambiguation.Apply(entries);

        Assert.All(entries, e => Assert.Null(e.Qualifier));
    }

    /// <summary>A name nothing collides with gets no qualifier. "Glamours (A Realm Reborn)" would be
    /// noise on the row that needs it least.</summary>
    [Fact]
    public void AUniqueNameIsNotQualified()
    {
        List<ResolvedUnlock> entries =
        [
            Entry("Glamours", expansion: "A Realm Reborn", place: "Ul'dah"),
            Entry("Retainer Access", expansion: "A Realm Reborn", place: "Limsa Lominsa"),
        ];

        UnlockDisambiguation.Apply(entries);

        Assert.All(entries, e => Assert.Null(e.Qualifier));
        Assert.Equal("Glamours", UnlockRowText.Name(entries[0]));
    }

    /// <summary>Re-running clears a qualifier that no longer applies, so a recompute cannot leave a
    /// row wearing a distinction it no longer has.</summary>
    [Fact]
    public void ApplyingAgainClearsAQualifierThatNoLongerHolds()
    {
        var kept = Entry("Levequests", expansion: "A Realm Reborn", place: "Limsa Lominsa");
        var dropped = Entry("Levequests", expansion: "A Realm Reborn", place: "Gridania");

        UnlockDisambiguation.Apply([kept, dropped]);
        Assert.NotNull(kept.Qualifier);

        UnlockDisambiguation.Apply([kept]);
        Assert.Null(kept.Qualifier);
    }

    /// <summary>Nothing is parsed out of the name. The expansion is only ever the quest's own, so an
    /// entry whose NAME contains an expansion and whose quest says otherwise takes the quest's
    /// answer — the sheet is the source and the name is prose somebody wrote.</summary>
    [Fact]
    public void TheQualifierComesFromTheQuestAndNotFromTheName()
    {
        List<ResolvedUnlock> entries =
        [
            Entry("Stone, Sky, Sea Access", expansion: "Endwalker"),
            Entry("Stone, Sky, Sea Access", expansion: "Dawntrail"),
        ];

        UnlockDisambiguation.Apply(entries);

        Assert.Equal("Endwalker", entries[0].Qualifier);
        Assert.Equal("Dawntrail", entries[1].Qualifier);
    }

    private static ResolvedUnlock Entry(
        string name, string? expansion = null, string? place = null, string channel = "system") => new()
        {
            Def = new UnlockDefinition { Unlock = name, Channel = channel, Type = "system" },
            QuestExpansion = expansion,
            QuestPlaceName = place,
            Status = UnlockStatus.Available,
        };
}
