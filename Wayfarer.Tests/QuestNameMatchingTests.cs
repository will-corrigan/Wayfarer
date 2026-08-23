using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>The matcher that binds a catalogue entry to a Quest sheet row. Every fixture here is
/// a real string or a real row id taken from the live sheet — the seven bindings the audit proved
/// wrong are the red-proof cases, and the collision fixtures guard the folds the audit proved
/// harmful.</summary>
public class QuestNameMatchingTests
{
    // The private-use journal icon glyph the sheet prefixes to 1,228 of its 5,356 named rows.
    private const string JournalIcon = "\uE0BE ";

    [Fact]
    public void JournalIconPrefix_FoldsOntoThePlainName()
    {
        Assert.Equal(QuestNameKey.For("Fugitive of Fear"), QuestNameKey.For(JournalIcon + "Fugitive of Fear"));
    }

    // Every Aether Current and Allied Society unlock quest from Shadowbringers on carries the icon.
    [Theory]
    [InlineData("The Naming of Vath")]
    [InlineData("The Caf\u00E9 at the End of the Universe")]
    [InlineData("It's Dwarfin' Time")]
    [InlineData("Well-wishing at the Wishing Well")]
    public void IconPrefixedSheetNames_MatchTheCatalogueSpelling(string catalogueName)
    {
        Assert.Equal(QuestNameKey.For(catalogueName), QuestNameKey.For(JournalIcon + catalogueName));
    }

    [Fact]
    public void AuthoringSuffix_IsFoldedAway()
    {
        // Quest #70217 ships as " Must Be Dreaming(way)"; the catalogue calls it "Must Be Dreaming".
        Assert.Equal(QuestNameKey.For("Must Be Dreaming"), QuestNameKey.For(JournalIcon + "Must Be Dreaming(way)"));
    }

    [Fact]
    public void InvisibleCharacters_AreStripped()
    {
        // One catalogue entry shipped with a trailing U+200E LEFT-TO-RIGHT MARK.
        Assert.Equal(
            QuestNameKey.For("The Instruments of Our Deliverance"),
            QuestNameKey.For("The Instruments of Our Deliverance\u200E"));
    }

    [Fact]
    public void CurlyApostrophesAndDashes_FoldOntoAscii()
    {
        Assert.Equal(QuestNameKey.For("It's Probably Pirates"), QuestNameKey.For("It\u2019s Probably Pirates"));
        Assert.Equal(QuestNameKey.For("Best-laid Schemes"), QuestNameKey.For("Best\u2013laid Schemes"));
    }

    [Fact]
    public void WhitespaceIsCollapsedAndTrimmed()
    {
        Assert.Equal(QuestNameKey.For("Where Eagles Nest"), QuestNameKey.For("  Where   Eagles\tNest "));
    }

    // The four folds the audit measured and rejected: each merges genuinely different quests.
    [Theory]
    [InlineData("A Relic Reborn (Bravura)", "A Relic Reborn (Curtana)")]
    [InlineData("A Relic Reborn", "A Relic Reborn (Bravura)")]
    [InlineData("My Little Chocobo (Twin Adder)", "My Little Chocobo (Maelstrom)")]
    [InlineData("Where the Heart Is (Mist)", "Where the Heart Is (The Goblet)")]
    [InlineData("Resistance Is Futile", "Resistance Is (Not) Futile")]
    [InlineData("Dancing King", "The Dancing King")]
    [InlineData("The First Stela: Of Ronkan Might", "The First Stela: Of Ronkan Benevolence")]
    [InlineData("What's in a Name", "What's in a Name?")]
    public void DistinctQuests_KeepDistinctKeys(string a, string b)
    {
        Assert.NotEqual(QuestNameKey.For(a), QuestNameKey.For(b), StringComparer.Ordinal);
    }

    [Fact]
    public void EmptyNameKeysToEmpty()
    {
        Assert.Equal(string.Empty, QuestNameKey.For(null));
        Assert.Equal(string.Empty, QuestNameKey.For("   "));
    }

    // The three retired pre-6.1 rows: no journal genre and (near-)nothing depends on them, while
    // the live row carries both. First-row-wins picked the lower id, which is the dead one, and
    // five catalogue entries then told players who finished A Realm Reborn that they had not.
    [Theory]
    [InlineData(66060u, 0u, 1, 70058u, 3859u, 31)] // The Ultimate Weapon
    [InlineData(66672u, 0u, 0, 69409u, 3859u, 2)] // Rock the Castrum
    [InlineData(66988u, 0u, 0, 69421u, 3858u, 1)] // Levin an Impression
    public void RetiredRow_LosesToTheLiveRow(
        uint retiredRow, uint retiredGenre, int retiredRefs, uint liveRow, uint liveGenre, int liveRefs)
    {
        var match = QuestNameMatch.Resolve(
        [
            new QuestNameCandidate(retiredRow, retiredGenre, retiredRefs),
            new QuestNameCandidate(liveRow, liveGenre, liveRefs),
        ]);

        Assert.Equal(liveRow, match.Best.RowId);
        Assert.NotEqual(retiredRow, match.Best.RowId);
        Assert.False(match.IsAmbiguous);
    }

    [Fact]
    public void LowestRowIdDoesNotWinByItself()
    {
        // "It's Probably Pirates" is the near-miss: first-row-wins happened to pick the live row
        // only because it sorts lower. The tiebreak must be picking it on the evidence, not the id.
        var match = QuestNameMatch.Resolve(
        [
            new QuestNameCandidate(66211, 0, 0),
            new QuestNameCandidate(65781, 3858, 11),
        ]);

        Assert.Equal(65781u, match.Best.RowId);
    }

    [Fact]
    public void StartingCityVariants_AreReportedAsAlternatives_NotPicked()
    {
        // The three "Simply the Hest" rows are all live and all equally referenced: a character
        // completes exactly one, decided by starting city. Any pick is wrong for two thirds of
        // players, so the matcher must surface all three.
        var match = QuestNameMatch.Resolve(
        [
            new QuestNameCandidate(65594, 1, 0),
            new QuestNameCandidate(65595, 2, 0),
            new QuestNameCandidate(65596, 3, 0),
        ]);

        Assert.True(match.IsAmbiguous);
        Assert.Equal([65594u, 65595u, 65596u], match.Alternatives.Order());
    }

    [Fact]
    public void SingleCandidate_IsNeverAmbiguous()
    {
        var match = QuestNameMatch.Resolve([new QuestNameCandidate(67086, 122, 0)]);
        Assert.Equal(67086u, match.Best.RowId);
        Assert.False(match.IsAmbiguous);
        Assert.Empty(match.Alternatives);
    }

    [Fact]
    public void AnyAlternativeComplete_MarksTheUnlockDone()
    {
        // A Gridania-start character holds #65596; the matcher bound #65594.
        var u = new ResolvedUnlock
        {
            Def = new UnlockDefinition { Unlock = "Guildhests", Type = "system", Quest = "Simply the Hest" },
            QuestRowId = 65594,
            AlternativeQuestRowIds = [65594, 65595, 65596],
            QuestLevel = 10,
        };
        var all = new List<ResolvedUnlock> { u };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(playerLevel: 90, isQuestComplete: id => id == 65596));

        Assert.Equal(UnlockStatus.Done, u.Status);
    }
}
