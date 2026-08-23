using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>Pins the trophy-mount reconciliation fix: the shipped catalogue understated four of
/// the six trophy-mount quests' requirements, so a player who owned exactly the (incomplete) set
/// the catalogue used to list was told <see cref="UnlockStatus.Available"/> when the game would
/// not actually let them accept the quest. Loads the real, generated
/// <c>data/unlocks-by-level.json</c> (see <see cref="UnlockDatasetShapeTests"/>), not a hand-built
/// fixture — so these fail if the correction is ever lost on a future regeneration, not only if
/// this test file itself goes stale.
///
/// <para>Triggered by a friend following this exact checklist to a mount quest he could not take:
/// the shipped set understated what the quest required. See data/README.md and the
/// MOUNT_REQUIREMENT_OVERRIDES / NEW_TROPHY_MOUNT_ENTRIES tables in
/// scripts/build-unlock-catalogue.mjs for the full source method.</para></summary>
public class TrophyMountRequirementTests
{
    public static TheoryData<string, uint[], uint, string> CorrectedSets() => new()
    {
        // unlock, previously-shipped mount ids (still owned), the newly-added id, its display name
        { "Kamuy of the Nine Tails (Mount)", [115, 116, 133, 144, 158, 172], 182, "Hallowed Kamuy" },
        { "Landerwaffe (Mount)", [189, 192, 205, 217, 226, 238], 249, "Diamond Gwiber" },
    };

    /// <summary>The four/five-way split the reconciliation found: Apocryphal Bahamut was missing
    /// two mounts, not one, so it needs its own fixture rather than fitting <see cref="CorrectedSets"/>'s
    /// one-new-id shape.</summary>
    [Fact]
    public void ApocryphalBahamut_PlayerOwningOnlyThePreviouslyShippedFive_IsNotAvailable()
    {
        var def = Single("Apocryphal Bahamut (Mount)");
        uint[] previouslyShipped = [261, 262, 306, 315, 325]; // missing 293 (Bluefeather Lynx), 332 (Lynx of Abyssal Grief)
        var owned = new HashSet<uint>(previouslyShipped);

        var unlocks = new List<ResolvedUnlock> { QualifyingResolvedUnlock(def, 70331) };
        UnlockStatusCalculator.Compute(unlocks, Gates.Ctx(playerLevel: 90, isMountUnlocked: owned.Contains));

        Assert.Equal(UnlockStatus.CollectionLocked, unlocks[0].Status);
        Assert.Contains(unlocks[0].MissingRequirements, m => m.Contains("Bluefeather Lynx", StringComparison.Ordinal));
        Assert.Contains(unlocks[0].MissingRequirements, m => m.Contains("Lynx of Abyssal Grief", StringComparison.Ordinal));
    }

    [Fact]
    public void ApocryphalBahamut_PlayerOwningTheFullCorrectedSeven_IsAvailable()
    {
        var def = Single("Apocryphal Bahamut (Mount)");
        var unlocks = new List<ResolvedUnlock> { QualifyingResolvedUnlock(def, 70331) };
        UnlockStatusCalculator.Compute(unlocks, Gates.Ctx(playerLevel: 90, isMountUnlocked: _ => true));

        Assert.Equal(UnlockStatus.Available, unlocks[0].Status);
        Assert.Empty(unlocks[0].MissingRequirements);
    }

    [Theory]
    [MemberData(nameof(CorrectedSets))]
    public void PlayerOwningOnlyThePreviouslyShippedSet_IsNotReportedAvailable(
        string unlock, uint[] previouslyShipped, uint newMountId, string newMountName)
    {
        var def = Single(unlock);
        var owned = new HashSet<uint>(previouslyShipped);

        // Owns every mount the OLD (understated) catalogue asked for, and nothing else — in
        // particular not the mount the reconciliation added. Before the fix, this player would
        // have every `requires.mounts` entry satisfied and the calculator would report
        // Available: exactly the defect that sent a player to a quest they could not accept.
        Assert.DoesNotContain(newMountId, owned);
        var unlocks = new List<ResolvedUnlock> { QualifyingResolvedUnlock(def, questRowId: 1) };
        UnlockStatusCalculator.Compute(unlocks, Gates.Ctx(playerLevel: 90, isMountUnlocked: owned.Contains));

        Assert.Equal(UnlockStatus.CollectionLocked, unlocks[0].Status);
        Assert.Contains(unlocks[0].MissingRequirements, m => m.Contains(newMountName, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(CorrectedSets))]
    public void PlayerOwningTheFullCorrectedSet_IsAvailable(
        string unlock, uint[] previouslyShipped, uint newMountId, string newMountName)
    {
        var def = Single(unlock);
        var owned = new HashSet<uint>(previouslyShipped) { newMountId };

        Assert.Contains(def.Requires!.Mounts, m => m.Id == newMountId && string.Equals(m.Name, newMountName, StringComparison.Ordinal));
        var unlocks = new List<ResolvedUnlock> { QualifyingResolvedUnlock(def, questRowId: 1) };
        UnlockStatusCalculator.Compute(unlocks, Gates.Ctx(playerLevel: 90, isMountUnlocked: owned.Contains));

        Assert.Equal(UnlockStatus.Available, unlocks[0].Status);
        Assert.Empty(unlocks[0].MissingRequirements);
    }

    /// <summary>The sixth trophy-mount quest, missing from the catalogue entirely before this
    /// fix. Same shape of proof: a player short one of the seven Wings mounts must not be told
    /// Available.</summary>
    [Fact]
    public void WingsOfLegacy_PlayerMissingOneOfSevenWingsMounts_IsNotAvailable()
    {
        var def = Single("Wings of Legacy (Mount)");
        uint[] sixOfSeven = [345, 346, 363, 389, 407, 422]; // missing 444, Wings of Nihility
        var owned = new HashSet<uint>(sixOfSeven);

        var unlocks = new List<ResolvedUnlock> { QualifyingResolvedUnlock(def, questRowId: 71005) };
        UnlockStatusCalculator.Compute(unlocks, Gates.Ctx(playerLevel: 100, isMountUnlocked: owned.Contains));

        Assert.Equal(UnlockStatus.CollectionLocked, unlocks[0].Status);
        Assert.Contains(unlocks[0].MissingRequirements, m => m.Contains("Wings of Nihility", StringComparison.Ordinal));
    }

    [Fact]
    public void WingsOfLegacy_PlayerOwningAllSevenWingsMounts_IsAvailable()
    {
        var def = Single("Wings of Legacy (Mount)");
        var unlocks = new List<ResolvedUnlock> { QualifyingResolvedUnlock(def, questRowId: 71005) };
        UnlockStatusCalculator.Compute(unlocks, Gates.Ctx(playerLevel: 100, isMountUnlocked: _ => true));

        Assert.Equal(UnlockStatus.Available, unlocks[0].Status);
    }

    [Fact]
    public void WingsOfLegacy_EntryExists_HasSevenRequiredMounts_AndNoLevel()
    {
        var def = Single("Wings of Legacy (Mount)");
        Assert.Null(def.Level);
        Assert.Equal("Dawntrail Unique Quest Rewards", def.Category);
        Assert.Equal("The Wing Spirit Cometh", def.Quest);
        Assert.Equal(7, def.Requires?.Mounts.Count);
        Assert.Contains(def.Requires!.Mounts, m => m.Id == 444 && string.Equals(m.Name, "Wings of Nihility", StringComparison.Ordinal));
    }

    /// <summary>Found in passing while re-deriving the corrected sets: the shipped catalogue's
    /// 'from' for Round Lanner (part of the already-correct Firebird set) named a duty that has
    /// no Lanner drop at all.</summary>
    [Fact]
    public void Firebird_RoundLanner_FromIsCorrected()
    {
        var def = Single("Firebird (Mount)");
        var roundLanner = def.Requires!.Mounts.Single(m => m.Id == 77);
        Assert.Equal("Round Lanner", roundLanner.Name);
        Assert.Equal("The Minstrel's Ballad: Thordan's Reign", roundLanner.From);
        Assert.False(
            string.Equals(roundLanner.From, "The Singularity Reactor (Extreme)", StringComparison.Ordinal),
            "Alexander: Midas (The Singularity Reactor) has no Round Lanner drop");
    }

    /// <summary>A <see cref="ResolvedUnlock"/> with every gate ahead of the curated-requirement
    /// check (job/level, prereq, lockout, instance content, Grand Company, beast tribe, hard job,
    /// accept condition) left at its default, unrestricted value, so
    /// <see cref="UnlockStatusCalculator.Compute"/> reaches <c>CuratedRequirementBlocking</c> and
    /// the trophy-mount set is the only thing being tested.</summary>
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
