using Wayfarer.Core.Ui;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>Pins the fix for the "Ceremony of Eternal Bonding" report: the checklist sent a player
/// to it as something to go and get, when the game's own accept message, corroborated by the
/// wiki, says it needs a partner — same Home World, party of two, both in East Shroud, both
/// wearing a Promise Wristlet. None of that is a fact about one character's client state.
///
/// <para>Loads the real, generated <c>data/unlocks-by-level.json</c> (see
/// <see cref="UnlockDatasetShapeTests"/>), not a hand-built fixture — so this fails if the
/// <c>SOCIAL_REQUIREMENT_OVERRIDES</c> correction in <c>scripts/build-unlock-catalogue.mjs</c> is
/// ever lost on a future regeneration, not only if this test file itself goes stale.</para></summary>
public class SocialRequirementTests
{
    /// <summary>The red-proof fixture: a player who has done everything this plugin CAN check for
    /// this entry — the prerequisite quest complete, the level met, no other gate in the way —
    /// must still not be told the ceremony is <see cref="UnlockStatus.Available"/>. Before the fix
    /// this entry carried no <c>requires</c> block at all, so the calculator fell straight through
    /// to Available the moment "The Scions of the Seventh Dawn" was done.</summary>
    [Fact]
    public void EternalBonding_EverythingCheckableMet_IsStillNotAvailable()
    {
        var def = Single("Ceremony of Eternal Bonding");
        var unlocks = new List<ResolvedUnlock> { QualifyingResolvedUnlock(def, 67114) };

        UnlockStatusCalculator.Compute(unlocks, Gates.Ctx(playerLevel: 90, isQuestComplete: id => id == 66045));

        Assert.NotEqual(UnlockStatus.Available, unlocks[0].Status);
        Assert.Equal(UnlockStatus.PartnerRequired, unlocks[0].Status);
    }

    [Fact]
    public void EternalBonding_RequiresBlockNamesThePartner_NotJustUnverifiable()
    {
        var def = Single("Ceremony of Eternal Bonding");

        Assert.NotNull(def.Requires);
        Assert.True(def.Requires!.RequiresAnotherPlayer);
        Assert.False(def.Requires.Unverifiable, "a partner requirement is a known fact, not the generic 'we don't know' escape hatch");

        // The label itself doesn't repeat "partner" — the calculator's PartnerSentence already
        // prepends "Needs a partner — ", so the label carries the specifics instead.
        Assert.Contains("East Shroud", def.Requires.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Home World", def.Requires.Label, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The sentence a player actually reads must say "partner", not the vaguer
    /// "requirements unknown" — the entire reason this status exists rather than reusing
    /// <see cref="UnlockStatus.RequirementsUnknown"/>.</summary>
    [Fact]
    public void EternalBonding_Sentence_SaysPartner_NotRequirementsUnknown()
    {
        var def = Single("Ceremony of Eternal Bonding");
        var u = QualifyingResolvedUnlock(def, 67114);
        UnlockStatusCalculator.Compute([u], Gates.Ctx(playerLevel: 90, isQuestComplete: id => id == 66045));

        var sentence = UnlockStatusDisplay.Sentence(u);
        Assert.Contains("partner", sentence, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requirements unknown", sentence, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The prerequisite quest gate still works normally ahead of the partner gate — a
    /// player who has not even finished "The Scions of the Seventh Dawn" is QuestLocked on that,
    /// not PartnerRequired, because that half of the requirement genuinely is checkable and the
    /// more specific answer is the more useful one.</summary>
    [Fact]
    public void EternalBonding_PrerequisiteQuestIncomplete_IsQuestLocked()
    {
        var def = Single("Ceremony of Eternal Bonding");
        var u = QualifyingResolvedUnlock(def, 67114);
        u.PrereqRowIds = [66045];
        u.PrereqNames = ["The Scions of the Seventh Dawn"];

        UnlockStatusCalculator.Compute([u], Gates.Ctx(playerLevel: 90));

        Assert.Equal(UnlockStatus.QuestLocked, u.Status);
    }

    /// <summary>Never plainly Available, whatever the player has done — the property the report
    /// exists to fix, stated as its own assertion so a future status rename can't quietly make
    /// this pass for the wrong reason.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EternalBonding_NeverAvailable_RegardlessOfPrerequisiteQuest(bool prereqComplete)
    {
        var def = Single("Ceremony of Eternal Bonding");
        var u = QualifyingResolvedUnlock(def, 67114);

        UnlockStatusCalculator.Compute([u], Gates.Ctx(playerLevel: 90, isQuestComplete: id => prereqComplete && id == 66045));

        Assert.NotEqual(UnlockStatus.Available, u.Status);
    }

    /// <summary>Corroboration count: the fix rests on the game's own data (the bound Quest row and
    /// its PreviousQuest column), the wiki, and the live report — never fewer than the project's
    /// multi-source rule requires, and 'verified' confidence needs at least two independent
    /// sources per data/validate-unlocks.mjs.</summary>
    [Fact]
    public void EternalBonding_HasAtLeastThreeIndependentSources()
    {
        var def = Single("Ceremony of Eternal Bonding");
        Assert.True(def.Sources.Count >= 3, $"expected >= 3 sources, got {def.Sources.Count}: {string.Join(", ", def.Sources)}");
        Assert.Contains(def.Sources, s => s.StartsWith("game-data:", StringComparison.Ordinal));
        Assert.Contains(def.Sources, s => s.StartsWith("consolegameswiki:", StringComparison.Ordinal));
        Assert.Equal("verified", def.Confidence);
    }

    /// <summary>The stale, wrong prerequisite ("Sanctum Acolyte" — not a quest that exists in the
    /// game's data) must not survive in the curated notes.</summary>
    [Fact]
    public void EternalBonding_NotesNoLongerNameTheNonexistentQuest()
    {
        var def = Single("Ceremony of Eternal Bonding");
        Assert.DoesNotContain("Sanctum Acolyte", def.Notes ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>The description used to claim a solo/NPC option exists. It does not — see the
    /// override's comment in scripts/build-unlock-catalogue.mjs — and a description that
    /// contradicts a "needs a partner" status would be worse than no description at all.</summary>
    [Fact]
    public void EternalBonding_DescriptionNoLongerClaimsASoloOrNpcOption()
    {
        var def = Single("Ceremony of Eternal Bonding");
        Assert.DoesNotContain("solo", def.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A <see cref="ResolvedUnlock"/> with every gate ahead of the curated-requirement
    /// check left at its default, unrestricted value, so <see cref="UnlockStatusCalculator.Compute"/>
    /// reaches <c>CuratedRequirementBlocking</c> and the partner gate is the only thing tested —
    /// same shape as <c>TrophyMountRequirementTests.QualifyingResolvedUnlock</c>.</summary>
    private static ResolvedUnlock QualifyingResolvedUnlock(UnlockDefinition def, uint questRowId) => new()
    {
        Def = def,
        QuestRowId = questRowId,
        QuestLevel = 1,
    };

    private static UnlockDefinition Single(string unlock) =>
        Assert.Single(Load(), e => string.Equals(e.Unlock, unlock, StringComparison.Ordinal));

    private static List<UnlockDefinition> Load() =>
        UnlockDataset.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "unlocks-by-level.json")));
}
