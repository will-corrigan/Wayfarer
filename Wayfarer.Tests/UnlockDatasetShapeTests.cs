using Wayfarer.Core.Ui;
using Wayfarer.Core.Unlocks;
using Wayfarer.Core.Unlocks.Gates;

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
    /// <para>Identity and gradeability are separate <i>in the data file</i>. A Quest row is a gate
    /// the client records, so an entry citing one can be graded and must not also claim to be
    /// unverifiable. A ContentFinderCondition or Item row identifies the entry without the data
    /// file itself carrying a gate for it, and those entries carry rows AND the marker.</para>
    ///
    /// <para>The marker is not the last word at runtime. An entry whose reward identity is a duty
    /// gets a gate derived from that row — the duty's own unlock bit — and where that reads, the
    /// entry is graded on it. The flag records what the CATALOGUE can express, which is a
    /// different thing from what the client knows; see
    /// <c>ResolvedUnlock.IdentityGate</c>.</para></summary>
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

    /// <summary>Every kind the gate language defines has an evaluator behind it. This is the
    /// non-vacuous half: it iterates the eighteen names in <see cref="GateKinds"/>, so emptying the
    /// registry — or adding a kind string and forgetting to register it — goes red.</summary>
    [Fact]
    public void EveryGateKindTheLanguageDefines_HasARegisteredEvaluator()
    {
        var registered = GateEvaluatorRegistry.Standard.Kinds;

        Assert.NotEmpty(GateKinds.All);
        foreach (var kind in GateKinds.All)
        {
            Assert.Contains(kind, registered, StringComparer.Ordinal);
        }
    }

    /// <summary>The data file and the shipped registry cannot drift apart. A catalogue naming a
    /// gate kind this build lacks degrades safely at runtime — to "we can't check this" — but
    /// safely is not the same as visibly, and a kind misspelt in the data would otherwise ship as
    /// an entry that quietly says nothing.
    ///
    /// <para><b>The shipped catalogue uses none of them.</b> Not one of the 587 entries carries a
    /// <c>requires.gates</c> node — the only gate the plugin builds today is the one the identity
    /// gate synthesises at runtime — so the loop below iterates nothing and cannot fail. That is
    /// asserted rather than assumed: without the count, this test reads as a guarantee it is not
    /// currently providing, and it would stay green with the evaluator registry emptied out (which is
    /// why the test above exists). The day the catalogue starts declaring gates, this number changes
    /// and the loop starts guarding for real.</para></summary>
    [Fact]
    public void EveryShippedCatalogueKind_HasARegisteredEvaluator()
    {
        var registered = GateEvaluatorRegistry.Standard.Kinds;
        var shipped = CatalogueGateKinds.Of(Load()).ToList();

        Assert.Empty(shipped);
        foreach (var kind in shipped)
        {
            Assert.Contains(kind, registered, StringComparer.Ordinal);
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

    /// <summary>An entry and its reward must name the same difficulty tier.
    ///
    /// <para>A raid tier and its Savage tier are two duties with two unlock bits and two catalogue
    /// entries — but the Savage entry is bound to the <i>normal</i> tier's final-floor unlock quest,
    /// because that clear is what opens Savage. Every channel that reasons from the bound quest
    /// therefore states the normal tier, correctly, about a quest the Savage entry only borrowed.
    /// Three Savage tiers shipped this way: Sigmascape (Savage) carrying <i>Sigmascape V4.0</i>,
    /// Asphodelos (Savage) carrying <i>Asphodelos: The Fourth Circle</i>, Abyssos (Savage) carrying
    /// <i>Abyssos: The Eighth Circle</i>. The consequence is not only a wrong plate on the page: the
    /// reward is what the identity gate is derived from, so those entries were marked Done off the
    /// NORMAL tier's unlock bit.</para>
    ///
    /// <para>Checked over every difficulty marker rather than only Savage, and in both directions.
    /// The count of Savage entries is asserted too, because a rule about a group is worth nothing
    /// once the group is empty — and <c>reward.name</c> is deliberately outside the coverage
    /// fingerprint, so nothing else in CI would have caught this.</para></summary>
    [Fact]
    public void AnEntryAndItsRewardNameTheSameDifficulty()
    {
        string[] difficulties = ["(Hard)", "(Extreme)", "(Savage)", "(Unreal)", "(Chaotic)"];
        var all = Load();

        foreach (var d in all)
        {
            if (d.Reward is not { } reward)
            {
                continue;
            }

            foreach (var marker in difficulties)
            {
                var onEntry = d.Unlock.Contains(marker, StringComparison.OrdinalIgnoreCase);
                var onReward = reward.Name.Contains(marker, StringComparison.OrdinalIgnoreCase);

                var message = $"'{d.Unlock}' and its reward '{reward.Name}' disagree about {marker}, so one of "
                    + "them is about the other tier. The identity gate is derived from the reward, so this entry "
                    + "would be graded on the wrong duty's unlock bit.";

                Assert.True(onEntry == onReward, message);
            }
        }

        var savage = all.FindAll(d => d.Unlock.Contains("(Savage)", StringComparison.Ordinal));
        Assert.Equal(20, savage.Count);
        Assert.Equal(8, savage.FindAll(d => d.Reward is not null).Count);
    }

    private static List<UnlockDefinition> Load() =>
        UnlockDataset.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "unlocks-by-level.json")));
}
