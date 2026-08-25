using Wayfarer.Core.Ui;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>Pins two fixes for the "Ceremony of Eternal Bonding" report, made in two passes.
///
/// <para>First pass: the checklist sent a player to it as something to go and get, when the
/// game's own accept message, corroborated by the wiki, says it needs a partner physically
/// present — not a fact about one character's client state. That earned it a curated
/// <c>requires.requiresAnotherPlayer</c> block.</para>
///
/// <para>Second pass, this file's current shape: an entry with every checkable gate met (the
/// prerequisite quest, the level) is not, in fact, unreachable — it is exactly as reachable as any
/// other Available entry, for a couple who both play the game. It reports Available with the
/// condition named alongside it, sourced from the game's own <c>HowToPage</c> checklist
/// (<c>requires.conditionSource</c>) rather than curated prose, per
/// the requirement-text survey.</para>
///
/// <para>Loads the real, generated <c>data/unlocks-by-level.json</c> (see
/// <see cref="UnlockDatasetShapeTests"/>), not a hand-built fixture — so this fails if the
/// <c>SOCIAL_REQUIREMENT_OVERRIDES</c> correction in <c>scripts/build-unlock-catalogue.mjs</c> is
/// ever lost on a future regeneration, not only if this test file itself goes stale.</para></summary>
public class SocialRequirementTests
{
    /// <summary>The red-proof fixture: a player who has done everything this plugin CAN check for
    /// this entry — the prerequisite quest complete, the level met, no other gate in the way —
    /// must be told the ceremony is <see cref="UnlockStatus.Available"/>, with the partner
    /// condition named alongside it. Before the original fix this entry carried no <c>requires</c>
    /// block at all, so the calculator fell straight through to a silent Available the moment "The
    /// Scions of the Seventh Dawn" was done — no condition named at all. After this change it is
    /// Available again, but honestly this time: the condition is stated, not hidden.</summary>
    [Fact]
    public void EternalBonding_EverythingCheckableMet_IsAvailableWithTheConditionNamed()
    {
        var def = Single("Ceremony of Eternal Bonding");
        var unlocks = new List<ResolvedUnlock> { QualifyingResolvedUnlock(def, 67114) };

        UnlockStatusCalculator.Compute(unlocks, Gates.Ctx(playerLevel: 90, isQuestComplete: id => id == 66045));

        Assert.Equal(UnlockStatus.Available, unlocks[0].Status);
        Assert.Equal("needs a partner", unlocks[0].AvailableCondition);
        Assert.NotNull(unlocks[0].AvailableConditionDetail);
    }

    [Fact]
    public void EternalBonding_RequiresBlockNamesThePartner_NotJustUnverifiable()
    {
        var def = Single("Ceremony of Eternal Bonding");

        Assert.NotNull(def.Requires);
        Assert.True(def.Requires!.RequiresAnotherPlayer);
        Assert.False(def.Requires.Unverifiable, "a partner requirement is a known fact, not the generic 'we don't know' escape hatch");

        // The curated label is a short, honestly-ours fallback now, not the source of truth — the
        // requirement's real detail lives in requires.conditionSource, quoting the game itself
        // (HowToPage#1861) rather than paraphrasing it. See
        // the requirement-text survey.
        Assert.NotNull(def.Requires.ConditionSource);
        Assert.Equal("HowToPage", def.Requires.ConditionSource!.Sheet);
        Assert.Equal(1861u, def.Requires.ConditionSource.Row);
        Assert.True(def.Requires.Label is { Length: > 0 } and { Length: <= 40 }, "the fallback label must stay short — the real detail lives in conditionSource");
    }

    /// <summary>The sentence a player actually reads must say "partner" and must say "Available" —
    /// both, in the same breath, because the entry genuinely is something to go and do and the
    /// condition genuinely is still outstanding.</summary>
    [Fact]
    public void EternalBonding_Sentence_SaysAvailableAndPartner()
    {
        var def = Single("Ceremony of Eternal Bonding");
        var u = QualifyingResolvedUnlock(def, 67114);
        UnlockStatusCalculator.Compute([u], Gates.Ctx(playerLevel: 90, isQuestComplete: id => id == 66045));

        var sentence = UnlockStatusDisplay.Sentence(u);
        Assert.Contains("Available", sentence, StringComparison.Ordinal);
        Assert.Contains("partner", sentence, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requirements unknown", sentence, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The prerequisite quest gate still works normally ahead of the partner condition —
    /// a player who has not even finished "The Scions of the Seventh Dawn" is QuestLocked on that,
    /// not Available, because that half of the requirement genuinely is checkable and a real
    /// blocker still wins over the unverifiable one.</summary>
    [Fact]
    public void EternalBonding_PrerequisiteQuestIncomplete_IsQuestLocked()
    {
        var def = Single("Ceremony of Eternal Bonding");
        var u = QualifyingResolvedUnlock(def, 67114);
        u.PrereqRowIds = [66045];
        u.PrereqNames = ["The Scions of the Seventh Dawn"];

        UnlockStatusCalculator.Compute([u], Gates.Ctx(playerLevel: 90));

        Assert.Equal(UnlockStatus.QuestLocked, u.Status);
        Assert.Null(u.AvailableCondition);
    }

    /// <summary>Same fact as <see cref="EternalBonding_PrerequisiteQuestIncomplete_IsQuestLocked"/>
    /// and <see cref="EternalBonding_EverythingCheckableMet_IsAvailableWithTheConditionNamed"/>,
    /// stated together as the red-proof pair the report exists to fix: a missing checkable gate
    /// still blocks (never a silent Available), and a met one is Available (never stuck blocked
    /// forever for a fact this plugin will never be able to confirm).</summary>
    [Fact]
    public void EternalBonding_BlockedWithTheGateMissing_AvailableWithTheGateMet()
    {
        var def = Single("Ceremony of Eternal Bonding");

        var blocked = QualifyingResolvedUnlock(def, 67114);
        blocked.PrereqRowIds = [66045];
        blocked.PrereqNames = ["The Scions of the Seventh Dawn"];
        UnlockStatusCalculator.Compute([blocked], Gates.Ctx(playerLevel: 90));
        Assert.Equal(UnlockStatus.QuestLocked, blocked.Status);

        var met = QualifyingResolvedUnlock(def, 67114);
        met.PrereqRowIds = [66045];
        met.PrereqNames = ["The Scions of the Seventh Dawn"];
        UnlockStatusCalculator.Compute([met], Gates.Ctx(playerLevel: 90, isQuestComplete: id => id == 66045));
        Assert.Equal(UnlockStatus.Available, met.Status);
        Assert.Equal("needs a partner", met.AvailableCondition);
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
