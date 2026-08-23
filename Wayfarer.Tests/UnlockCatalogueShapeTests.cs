using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>Loads the real shipped <c>data/unlocks-by-level.json</c> (copied into the test output
/// directory — see Wayfarer.Tests.csproj) and asserts what the catalogue recovery established, so
/// a regression is caught by <c>dotnet test</c> and not only by the Node validator.
///
/// <para>These are deliberately assertions about specific entries rather than about totals. A
/// total tells you something moved; naming the entry tells you what, and every one named here is
/// a claim the recovery backed with the guide's own link target plus the linked page's
/// infobox.</para></summary>
public class UnlockCatalogueShapeTests
{
    [Fact]
    public void Catalogue_HasTheExpectedSize()
    {
        // Matches EXPECTED in data/validate-unlocks.mjs. Two entries were removed by the recovery:
        // both belonged to the unreleased-expansion guide page and neither described real content.
        Assert.Equal(586, Load().Count);
    }

    [Fact]
    public void UnreleasedPlaceholders_AreGone()
    {
        var all = Load();
        Assert.DoesNotContain(all, e => e.Unlock.Contains("???", StringComparison.Ordinal));
        Assert.DoesNotContain(all, e => e is { Unlock: "Bastion Specialization" });
        Assert.DoesNotContain(all, e => e is { Unlock: "Role Quests Access", Level: 105 });
    }

    /// <summary>Five entries were typed by string-matching their own name, so every duty whose
    /// name begins with the word "Mount" was filed as a mount. The guide's row icon says
    /// otherwise, and so does the game: none of the five resolves to a Mount row.</summary>
    [Theory]
    [InlineData("Mount Ordeals Trial Access", "trial")]
    [InlineData("Mount Ordeals (Extreme) Trial Access", "trial")]
    [InlineData("Mount Rokkon Variant Dungeon Access", "dungeon")]
    [InlineData("Another Mount Rokkon Criterion Dungeon Access", "dungeon")]
    [InlineData("Another Mount Rokkon (Savage) Criterion Dungeon Access", "dungeon")]
    public void MisTypedDuties_AreTypedByWhatTheyAre(string unlock, string expectedType) =>
        Assert.Equal(expectedType, Single(unlock).Type);

    /// <summary>Both of these ship as three identically-named Quest rows, one per starting city.
    /// Binding the lowest reported "not done" to two thirds of characters.</summary>
    [Theory]
    [InlineData("Guildhests", new uint[] { 65594, 65595, 65596 })]
    [InlineData("Retainer Ventures", new uint[] { 66968, 66969, 66970 })]
    public void PerCityQuests_AreRecordedAsAnyOf(string unlock, uint[] expected)
    {
        var e = Single(unlock);
        Assert.Equal(expected, e.QuestAnyOf);
        Assert.Null(e.Requires);
    }

    /// <summary>Every questAnyOf id has to be backed by its own source line — the same rule the
    /// Node validator enforces, asserted here too because this is the field that replaced a
    /// name match, and an id with no evidence behind it would be exactly the old defect in a new
    /// shape.</summary>
    [Fact]
    public void EveryQuestAnyOfId_CitesItsOwnSource()
    {
        foreach (var e in Load().Where(e => e.QuestAnyOf.Count > 0))
        {
            Assert.True(e.QuestAnyOf.Count >= 2, $"{e.Unlock}: questAnyOf needs at least two rows");
            foreach (var id in e.QuestAnyOf)
            {
                Assert.Contains(e.Sources, s => string.Equals(s, $"game-data:Quest#{id}", StringComparison.Ordinal));
            }
        }
    }

    /// <summary>The catalogue recorded a Wandering Minstrel dialogue label as this entry's quest.
    /// It is not a quest; the checkable fact is the guildhest clear behind it.</summary>
    [Fact]
    public void ADutyGatedEntry_CarriesTheDutyInsteadOfANonExistentQuest()
    {
        var e = Single("Duty Roulette: Guildhests");
        Assert.Null(e.Quest);
        var duty = Assert.Single(e.Requires!.Duties);
        Assert.Equal(10001u, duty.Id);
        Assert.Equal("Basic Training: Enemy Parties", duty.Name);
        Assert.True(e.Requires!.Unverifiable, "a duty clear opens the door; it does not prove the player walked through");
    }

    [Fact]
    public void AnItemGatedEntry_CarriesTheTreasureMap()
    {
        var e = Single("The Aquapolis Access");
        Assert.Null(e.Quest);
        Assert.Equal(12243u, Assert.Single(e.Requires!.Items).Id);
    }

    /// <summary>A previously-unverifiable entry that now grades: the catalogue had an NPC's name
    /// where the quest should be, so nothing about it was checkable. It now names the quest the
    /// guide's own link points at.</summary>
    [Fact]
    public void APreviouslyUnverifiableEntry_NowNamesARealQuest()
    {
        var e = Single("Ceremony of Eternal Bonding");
        Assert.Equal("The Ties That Bind", e.Quest);
        Assert.Null(e.Requires);
        Assert.Equal("verified", e.Confidence);
        Assert.Contains(e.Sources, s => string.Equals(s, "game-data:Quest#67114", StringComparison.Ordinal));
    }

    /// <summary>The invariant the whole schema exists for, restated over the shipped file: an
    /// entry with nothing to check must say so, or the calculator will fall through to Available
    /// and send someone after something they cannot get.</summary>
    [Fact]
    public void NoEntryIsSilentlyIdentityLess()
    {
        foreach (var e in Load())
        {
            var identified = e.Quest is not null
                || e.QuestAnyOf.Count > 0
                || e.Requires?.Unverifiable == true
                || e.Requires?.HasCheckableRequirement == true;
            Assert.True(identified, $"{e.Unlock} (lv{e.Level}) has no quest, no questAnyOf and no requires");
        }
    }

    [Fact]
    public void EveryDutyRequirement_UsesAPlausibleInstanceContentId()
    {
        foreach (var e in Load())
        {
            foreach (var duty in e.Requires?.Duties ?? [])
            {
                Assert.True(duty.Id > 0, $"{e.Unlock}: duty id must be positive");
                Assert.False(string.IsNullOrWhiteSpace(duty.Name), $"{e.Unlock}: duty needs a name");
            }
        }
    }

    private static UnlockDefinition Single(string unlock) =>
        Assert.Single(Load(), e => string.Equals(e.Unlock, unlock, StringComparison.Ordinal));

    private static List<UnlockDefinition> Load() =>
        UnlockDataset.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "unlocks-by-level.json")));
}
