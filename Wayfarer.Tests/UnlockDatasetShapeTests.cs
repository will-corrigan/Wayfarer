using Wayfarer.Core.Ui;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>Loads the real shipped <c>data/unlocks-by-level.json</c> (copied into the test output
/// directory — see Wayfarer.Tests.csproj) through <see cref="UnlockDataset.Parse"/> and asserts
/// the invariants <c>data/validate-catalogue-identity.mjs</c> enforces in CI, so a regeneration
/// that broke one of them would fail <c>dotnet test</c> too and not only the Node validator.
///
/// <para>These are the properties the catalogue's history says are worth a test. Every one of
/// them corresponds to a defect that actually shipped: entries whose identity was a string that
/// matched no quest row, entries reported as available on the strength of a gate nobody had
/// checked for, and levels the import invented for guide sections that state none.</para></summary>
public class UnlockDatasetShapeTests
{
    /// <summary>An entry with no game row behind it must say so. This is the guard that keeps
    /// "I found no gate" from being reported as "go and get it": the status calculator refuses to
    /// grade an entry carrying <c>requires.unverifiable</c>, and nothing else in the file would
    /// stop it falling through to Available.
    ///
    /// <para>Identity and gradeability are separate. A Quest row is a gate the client records, so
    /// an entry citing one can be graded and must not also claim to be unverifiable. A
    /// ContentFinderCondition or Item row identifies the entry without being a gate: clearing
    /// Sigmascape opens the Ultimate, but whether the player then took the unlock is written
    /// nowhere a plugin can read. Those entries carry rows AND the marker, and that is
    /// correct.</para></summary>
    [Fact]
    public void EveryEntryEitherRestsOnAGameRowOrDeclaresItRestsOnNothing()
    {
        foreach (var d in Load())
        {
            var hasRow = d.Sources.Exists(s => s.StartsWith("game-data:", StringComparison.Ordinal));
            var hasQuestRow = d.Sources.Exists(s => s.StartsWith("game-data:Quest#", StringComparison.Ordinal));
            Assert.True(
                hasRow || d.Requires?.Unverifiable == true,
                $"'{d.Unlock}' cites no game row and is not marked unverifiable");
            Assert.False(
                hasQuestRow && d.Requires?.Unverifiable == true,
                $"'{d.Unlock}' is marked unverifiable but cites a Quest row: {string.Join(", ", d.Sources)}");
            Assert.False(
                hasRow && !hasQuestRow && d.Requires?.Unverifiable != true,
                $"'{d.Unlock}' cites only non-quest rows, so nothing says whether the unlock was taken: {string.Join(", ", d.Sources)}");
        }
    }

    /// <summary>No invented levels. Five sections of the source guide state no level at all and
    /// the original import filled them with the previous expansion's cap, putting 13 entries at a
    /// number no source had ever stated. A level now has to name what grounds it.</summary>
    [Fact]
    public void ALevelIsPresentOnlyWhenSomethingGroundsIt()
    {
        foreach (var d in Load())
        {
            if (d.Level is { } level)
            {
                Assert.True(level >= 1, $"'{d.Unlock}' has level {level}; level 0 is not a level");
                Assert.False(string.IsNullOrEmpty(d.LevelSource), $"'{d.Unlock}' has a level but records nothing that grounds it");
                Assert.Null(d.Category);
            }
            else
            {
                Assert.False(string.IsNullOrEmpty(d.Category), $"'{d.Unlock}' has no level, so it needs a category");
                Assert.Null(d.LevelSource);
            }
        }
    }

    /// <summary>The trophy mounts are the case that cannot be fixed by looking harder: the guide
    /// gives no level and the quest that grants them is a hidden level-1 reward row, so any number
    /// printed against them would be a fabrication. They are categorised instead. Six now, not
    /// five: "Wings of Legacy (Mount)" (Dawntrail's "The Wing Spirit Cometh", Quest#71005) was
    /// missing from the catalogue entirely until the trophy-mount reconciliation added it — see
    /// data/README.md and TrophyMountRequirementTests.</summary>
    [Fact]
    public void TheTrophyMountsHaveNoLevelAndAreCategorised()
    {
        var levelless = Load().FindAll(d => d.Level is null);
        Assert.NotEmpty(levelless);
        foreach (var name in new[]
        {
            "Firebird (Mount)", "Kamuy of the Nine Tails (Mount)", "Landerwaffe (Mount)",
            "Apocryphal Bahamut (Mount)", "Wings of Legacy (Mount)",
        })
        {
            var d = levelless.Find(x => string.Equals(x.Unlock, name, StringComparison.Ordinal));
            Assert.True(d is not null, $"expected '{name}' to carry no level");
            Assert.False(string.IsNullOrEmpty(d!.Category));
        }
    }

    /// <summary>Level order, then level-less entries last. The UI groups on this, and a
    /// regeneration that reordered entries silently would make the next diff unreadable.</summary>
    [Fact]
    public void EntriesAreInNonDecreasingLevelOrderWithTheLevellessOnesLast()
    {
        var previous = 0;
        var levellessSeen = false;
        foreach (var d in Load())
        {
            if (d.Level is not { } level)
            {
                levellessSeen = true;
                continue;
            }

            Assert.False(levellessSeen, $"'{d.Unlock}' has a level but follows one that does not");
            Assert.True(level >= previous, $"'{d.Unlock}' is level {level} after level {previous}");
            previous = level;
        }
    }

    /// <summary>'verified' is a claim that two independent sources agree, so it needs two of
    /// them recorded. An entry that cannot show its working does not get to make the claim.</summary>
    [Fact]
    public void VerifiedEntriesCiteAtLeastTwoSources()
    {
        foreach (var d in Load())
        {
            Assert.NotEmpty(d.Sources);
            if (string.Equals(d.Confidence, "verified", StringComparison.Ordinal))
            {
                Assert.True(d.Sources.Count >= 2, $"'{d.Unlock}' claims 'verified' from one source");
            }
        }
    }

    /// <summary>The Reward section's own promise, checked against every shipped entry rather than
    /// a handful of examples: 272 of 587 carry no sheet-backed <c>Reward</c> at all, and every one
    /// of them still has to produce a real, non-repeating reward line through
    /// <see cref="UnlockRowText.GrantedCapability"/> — the mechanical proof that "the unlock IS the
    /// reward when there is no item" holds for the whole catalogue and not only the cases this
    /// change was written against.</summary>
    [Fact]
    public void EveryRewardLessEntryStillStatesTheCapabilityItGrants()
    {
        var rewardLess = Load().FindAll(d => d.Reward is null);

        // The fallback exists because this group is not empty. A regeneration that closed the gap
        // entirely would not be a failure, but it would mean this test is no longer exercising
        // anything, which is worth knowing about explicitly rather than silently.
        Assert.NotEmpty(rewardLess);

        foreach (var d in rewardLess)
        {
            var unlock = new ResolvedUnlock { Def = d };
            var line = UnlockRowText.GrantedCapability(unlock);

            Assert.False(string.IsNullOrWhiteSpace(line), $"'{d.Unlock}' produced no reward line at all");

            // Always a cut of the description, never a paraphrase invented on top of it — a
            // clause with no dash and no interior sentence end is handed back whole, which is
            // correct and not a bug, so equality is allowed; anything that is not a whole-string
            // prefix is not.
            Assert.StartsWith(line, d.Description, StringComparison.Ordinal);
        }
    }

    private static List<UnlockDefinition> Load() =>
        UnlockDataset.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "unlocks-by-level.json")));
}
