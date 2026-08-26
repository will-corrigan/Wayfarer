using Wayfarer.Core.Ui;
using Wayfarer.Core.Unlocks;
using Wayfarer.Core.Unlocks.Gates;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Tests;

/// <summary>The title channel's two claims, and the mutations that prove each fixture is testing
/// something.
///
/// <para>A title is the first entry kind in the catalogue with <b>no place at all</b> and with a
/// <b>request-gated</b> proof of ownership, and each of those is a way to be confidently wrong that
/// nothing in the file could previously go red about. Routability used to be a consequence of a
/// coordinate happening to be null, and an unread title list reads as a character who has earned no
/// titles — 870 of them, all saying "go and get it" about things the player already has.</para></summary>
public class TitleUnlockTests
{
    private const uint TitleRow = 630;
    private const uint OtherTitleRow = 649;

    // ------------------------------------------------------------------ 1. no place, no route

    /// <summary>An entry whose place is stated as "none" is not routable, and stating it is the
    /// point: the territory is populated here deliberately, so the fixture cannot pass by the same
    /// accident the old behaviour relied on.</summary>
    [Fact]
    public void ATitleWithNoPlace_IsNotRoutable_EvenWithACoordinate()
    {
        var title = Title(place: UnlockPlaceKinds.None);
        title.GiverTerritory = 129;
        title.GiverMap = 12;

        Assert.False(title.Routable);
    }

    /// <summary>The mutation. Change nothing but the stated place and the very same entry becomes
    /// routable — so the assertion above is about the place and not about some other absence.</summary>
    [Fact]
    public void TheSameEntryWithAPlace_IsRoutable()
    {
        var title = Title(place: UnlockPlaceKinds.QuestGiver);
        title.GiverTerritory = 129;
        title.GiverMap = 12;

        Assert.True(title.Routable);
    }

    /// <summary>The affordance itself, not just the flag: the route planner is what a "Route me
    /// there" button runs, and a placeless entry must not appear in what it plans.</summary>
    [Fact]
    public void ThePlanner_OffersNoRouteToAPlacelessEntry()
    {
        var placeless = Title(place: UnlockPlaceKinds.None);
        placeless.GiverTerritory = 129;
        placeless.Status = UnlockStatus.Available;

        Assert.Empty(RoutePlanner.Order([placeless], 129, 0, 0));
        Assert.Empty(RoutePlanner.TopAvailableHere([placeless], 129, 0, 0, 8));
    }

    /// <summary>The mutation for the planner. Same entry, same coordinate, place restored: it is
    /// planned. Without this the test above would still pass against a planner that never returned
    /// anything at all.</summary>
    [Fact]
    public void ThePlanner_OffersARouteToTheSameEntryWithAPlace()
    {
        var placed = Title(place: UnlockPlaceKinds.QuestGiver);
        placed.GiverTerritory = 129;
        placed.Status = UnlockStatus.Available;

        Assert.Single(RoutePlanner.Order([placed], 129, 0, 0));
        Assert.Single(RoutePlanner.TopAvailableHere([placed], 129, 0, 0, 8));
    }

    /// <summary>Over the shipped dataset rather than a fixture, so it cannot drift: every entry
    /// that states it has nowhere to go is unroutable even when handed a coordinate.</summary>
    [Fact]
    public void NoShippedPlacelessEntry_IsRoutable()
    {
        var placeless = Load().FindAll(d => d.Place is { Kind: UnlockPlaceKinds.None });
        Assert.NotEmpty(placeless);

        foreach (var d in placeless)
        {
            var u = new ResolvedUnlock { Def = d, GiverTerritory = 129, GiverMap = 12 };
            Assert.False(u.Routable, $"'{d.Unlock}' states no place but reads as routable");
        }
    }

    // ------------------------------------------------------------------ 2. unknown is not "no"

    /// <summary>The claim this whole channel rests on. Before the title list or the achievement
    /// table has arrived, nothing can say whether a title has been earned — and the entry must say
    /// exactly that, rather than the two things it could say instead: Done (it has been) or
    /// Available (it has not, go and get it).</summary>
    [Fact]
    public void AnUnreadableTitle_IsNeitherObtainedNorNotObtained()
    {
        var all = new List<ResolvedUnlock> { Title(place: UnlockPlaceKinds.None) };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(100, isTitleUnlocked: _ => null));

        Assert.Equal(UnlockStatus.RequirementsUnknown, all[0].Status);
        Assert.NotEqual(UnlockStatus.Available, all[0].Status);
        Assert.NotEqual(UnlockStatus.Done, all[0].Status);
    }

    /// <summary>The mutation that makes the fixture above mean something. The one thing that
    /// changes is that the reader can now answer — and the same entry immediately grades, in both
    /// directions. A test that only asserted "unknown" would pass against a build that could never
    /// grade a title at all.</summary>
    [Theory]
    [InlineData(true, UnlockStatus.Done)]
    [InlineData(false, UnlockStatus.Available)]
    public void AReadableTitle_Grades(bool earned, UnlockStatus expected)
    {
        var all = new List<ResolvedUnlock> { Title(place: UnlockPlaceKinds.None) };

        UnlockStatusCalculator.Compute(all, Gates.Ctx(100, isTitleUnlocked: id => id == TitleRow && earned));

        Assert.Equal(expected, all[0].Status);
    }

    /// <summary>And what it says while it cannot tell. "Requirements unknown" is the right status
    /// and the wrong sentence on its own: the reason has to be about the reading, not about the
    /// entry, and it must not read as a verdict on whether the player has the title.</summary>
    [Fact]
    public void AnUnreadableTitle_SaysWhyRatherThanShrugging()
    {
        var pending = new List<ResolvedUnlock> { Title(place: UnlockPlaceKinds.None) };
        UnlockStatusCalculator.Compute(
            pending, Gates.Ctx(100, isTitleUnlocked: _ => null, titleData: TitleDataState.Pending));

        var never = new List<ResolvedUnlock> { Title(place: UnlockPlaceKinds.None) };
        UnlockStatusCalculator.Compute(
            never, Gates.Ctx(100, isTitleUnlocked: _ => null, titleData: TitleDataState.NotRequested));

        Assert.Equal("your titles are still on their way from the server", pending[0].LockReason);
        Assert.Equal("Wayfarer has not read your titles yet", never[0].LockReason);
        Assert.NotEqual(pending[0].LockReason, never[0].LockReason, StringComparer.Ordinal);
        Assert.DoesNotContain("no quest", never[0].LockReason!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What the player actually reads, end to end, for a title whose state has not arrived.
    /// It lands in the honest band — <b>Not known</b>, never Blocked and never Complete — and its
    /// sentence says the reading has not happened, rather than claiming the requirement is a mystery
    /// or that the title is unearned.</summary>
    [Fact]
    public void APendingTitle_LandsInNotKnown_AndSaysSo()
    {
        var all = new List<ResolvedUnlock> { Title(place: UnlockPlaceKinds.None) };
        UnlockStatusCalculator.Compute(
            all, Gates.Ctx(100, isTitleUnlocked: _ => null, titleData: TitleDataState.Pending));

        var band = UnlockBands.Of(all[0].Status);
        Assert.Equal(UnlockBand.NotKnown, band);
        Assert.Equal("Not known", UnlockBands.Label(band));

        var sentence = UnlockStatusDisplay.Sentence(all[0]);
        Assert.Equal("Not known yet — your titles are still on their way from the server.", sentence);
        Assert.DoesNotContain("obtain", sentence, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("locked", sentence, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The mutation for the row above: the same entry, once its state can be read, leaves
    /// the honest band in whichever direction the answer points. A test that only asserted "Not
    /// known" would pass against a build where every title was stuck there for ever.</summary>
    [Theory]
    [InlineData(true, UnlockBand.Complete)]
    [InlineData(false, UnlockBand.Available)]
    public void AReadableTitle_LeavesNotKnown(bool earned, UnlockBand expected)
    {
        var all = new List<ResolvedUnlock> { Title(place: UnlockPlaceKinds.None) };
        UnlockStatusCalculator.Compute(all, Gates.Ctx(100, isTitleUnlocked: _ => earned));

        Assert.Equal(expected, UnlockBands.Of(all[0].Status));
    }

    /// <summary>The evaluator on its own, at the seam the calculator cannot see: an unread list is
    /// Indeterminate, and a read one that says the bit is clear is Blocked. Those are different
    /// outcomes and the whole channel turns on them not being collapsed into one.</summary>
    [Fact]
    public void TheEvaluator_SeparatesUnreadFromUnearned()
    {
        var node = Gates.Node(GateKinds.TitleUnlocked, [TitleRow], display: "Of the Final Conflict");
        var evaluator = new TitleUnlockedEvaluator();

        var unread = evaluator.Evaluate(node, Gates.Ctx(100, isTitleUnlocked: _ => null).Live);
        var unearned = evaluator.Evaluate(node, Gates.Ctx(100, isTitleUnlocked: _ => false).Live);
        var earned = evaluator.Evaluate(node, Gates.Ctx(100, isTitleUnlocked: _ => true).Live);

        Assert.Equal(GateOutcome.Indeterminate, unread.Outcome);
        Assert.Equal(GateOutcome.Blocked, unearned.Outcome);
        Assert.Equal(GateOutcome.Satisfied, earned.Outcome);
    }

    /// <summary>A gate that names no row could only ever be read by guessing, so it is not read at
    /// all. Same rule as every other evaluator in the registry.</summary>
    [Fact]
    public void TheEvaluator_RefusesAGateThatNamesNoRow()
    {
        var evaluator = new TitleUnlockedEvaluator();
        var result = evaluator.Evaluate(
            Gates.Node(GateKinds.TitleUnlocked, [TitleRow, OtherTitleRow]),
            Gates.Ctx(100, isTitleUnlocked: _ => true).Live);

        Assert.Equal(GateOutcome.Indeterminate, result.Outcome);
    }

    // ------------------------------------------------------------------ 3. the game's own words

    /// <summary>A title carries no curated description because the game already wrote one. What the
    /// row shows is that sentence, resolved live — and an entry that has neither shows nothing
    /// rather than its own name repeated back.</summary>
    [Fact]
    public void TheRowShowsTheGamesOwnSentence_WhenTheCatalogueWroteNone()
    {
        var quoted = new ResolvedUnlock
        {
            Def = new UnlockDefinition { Unlock = "Of the Final Conflict", Type = "title" },
            GameDescription = "Reach the rank of Crystal in the Feast.",
        };
        var silent = new ResolvedUnlock
        {
            Def = new UnlockDefinition { Unlock = "Of the Final Conflict", Type = "title" },
        };

        Assert.Equal("Reach the rank of Crystal in the Feast.", UnlockRowText.Description(quoted));
        Assert.Equal(string.Empty, UnlockRowText.Description(silent));
    }

    /// <summary>Over the shipped dataset: every title states how it is obtained in the game's own
    /// words, states whether it has a place, and states what proves it has been earned — and none
    /// of the three is inferred from either of the others.</summary>
    [Fact]
    public void EveryShippedTitle_CarriesAllThreeFactsSeparately()
    {
        var titles = Load().FindAll(d => string.Equals(d.Channel, "title", StringComparison.Ordinal));
        Assert.NotEmpty(titles);

        foreach (var d in titles)
        {
            Assert.True(d.DescriptionSource is { Sheet: "Achievement" }, $"'{d.Unlock}' quotes no requirement sentence");
            Assert.True(d.Place is not null, $"'{d.Unlock}' does not say whether it has a place");
            Assert.True(d.State is { Kind: GateKinds.TitleUnlocked }, $"'{d.Unlock}' says nothing that would grade it");
            Assert.Null(d.Description);

            // The place follows the quest and nothing else. A title awarded by a quest goes to that
            // quest's giver; one awarded by a kill count has nowhere to send anybody.
            var expected = d.Quest is null ? UnlockPlaceKinds.None : UnlockPlaceKinds.QuestGiver;
            Assert.Equal(expected, d.Place!.Kind);
        }
    }

    /// <summary>No level where no source states one. 669 of the 870 have no quest, and a quest's
    /// accept level is the only level anything states about a title — so those entries carry none
    /// rather than a zero, which would sort every one of them above level-1 content while claiming a
    /// requirement the game does not make.</summary>
    [Fact]
    public void ATitleHasALevelOnlyWhereAQuestStatesOne()
    {
        var titles = Load().FindAll(d => string.Equals(d.Channel, "title", StringComparison.Ordinal));

        foreach (var d in titles)
        {
            if (d.Quest is null)
            {
                Assert.Null(d.Level);
                Assert.False(string.IsNullOrEmpty(d.Category), $"'{d.Unlock}' has no level and no category");
            }
            else
            {
                Assert.NotNull(d.Level);
                Assert.True(
                    d.LevelSource?.StartsWith("game-data:Quest#", StringComparison.Ordinal) == true,
                    $"'{d.Unlock}' has a level that no quest row grounds");
                Assert.Contains(d.LevelSource!.Replace("game-data:", "game-data:", StringComparison.Ordinal), d.Sources, StringComparer.Ordinal);
            }
        }
    }

    private static ResolvedUnlock Title(string place) => new()
    {
        Def = new UnlockDefinition
        {
            Unlock = "Of the Final Conflict",
            Type = "title",
            Category = "Ranking",
            Reward = new UnlockReward("Title", TitleRow, "Of the Final Conflict"),
            Place = new UnlockPlace(place),
            DescriptionSource = new GameTextRef("Achievement", 2481, 2),
            State = new GateNode
            {
                Kind = GateKinds.TitleUnlocked,
                Ids = [TitleRow],
                Display = "Of the Final Conflict",
            },
        },
        IdentityGate = new GateNode
        {
            Kind = GateKinds.TitleUnlocked,
            Ids = [TitleRow],
            Display = "Of the Final Conflict",
        },
    };

    private static List<UnlockDefinition> Load() =>
        UnlockDataset.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "unlocks-by-level.json")));
}
