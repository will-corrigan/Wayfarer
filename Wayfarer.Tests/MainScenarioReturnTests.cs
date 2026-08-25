using Wayfarer.Core.Guidance;

namespace Wayfarer.Tests;

/// <summary>The two things the follow list must never get wrong: that there is a way back to the
/// Main Scenario from wherever the player is, and that the list does not tell her she is already
/// there.
///
/// <para><b>Why these are decision tests and not presence tests.</b> The defect these cover shipped
/// past a surface that <i>had</i> a Main Scenario entry in every mode. The entry was there; it was
/// disabled, because each surface decided "am I already on the main scenario?" from the followed-quest
/// override alone, and that override is null during a hunt. So the entry existed, said "Following",
/// and could not be pressed. Asserting that an entry exists would have passed. What is asserted here
/// is what the entry would DO — which operations the reset performs in that mode, and therefore
/// whether it changes anything at all.</para></summary>
public class MainScenarioReturnTests
{
    /// <summary>Every follow mode, as the two facts that produce it: what is engaged, and whether a
    /// quest has been chosen. "Nothing" is the main scenario — this plugin has no null follow
    /// state.</summary>
    public static TheoryData<string, FollowMode?, bool, FollowMode> Modes =>
        new()
        {
            { "nothing", null, false, FollowMode.MainScenario },
            { "a quest", null, true, FollowMode.Quest },
            { "a hunting target", FollowMode.Hunting, false, FollowMode.Hunting },
            { "an unlock route", FollowMode.UnlockRoute, false, FollowMode.UnlockRoute },
        };

    /// <summary>Exactly one mode is reported for any state, and it is the one the player would
    /// name. This is what the "Following" caption is derived from, so one entry wears it and it is
    /// the right one — the Hunting Log and Unlock Route entries used to pass <c>false</c> literally
    /// and could never report themselves at all.</summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public void The_reported_mode_is_the_one_being_followed(
        string described, FollowMode? engaged, bool hasFollowedQuest, FollowMode expected)
    {
        Assert.Equal(expected, MainScenarioReturn.ModeOf(engaged, hasFollowedQuest));
        Assert.False(string.IsNullOrEmpty(described));
    }

    /// <summary>An engaged mode is what is being followed even when a quest has been chosen
    /// underneath it. Two entries claiming to be followed is a list nobody can read, and the quest is
    /// not what the arrow is on.</summary>
    [Theory]
    [InlineData(FollowMode.Hunting)]
    [InlineData(FollowMode.UnlockRoute)]
    public void An_engaged_mode_outranks_a_chosen_quest(FollowMode engaged)
    {
        Assert.Equal(engaged, MainScenarioReturn.ModeOf(engaged, hasFollowedQuest: true));
    }

    /// <summary><b>The route back exists from every follow mode.</b> Not that a menu entry exists —
    /// that it acts: the reset names at least one operation to perform in every mode but the one the
    /// player is already in.</summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public void There_is_a_way_back_to_the_main_scenario_from_every_mode(
        string described, FollowMode? engaged, bool hasFollowedQuest, FollowMode expected)
    {
        var reset = MainScenarioReturn.From(engaged is not null, hasFollowedQuest);

        if (expected == FollowMode.MainScenario)
        {
            // Already there, so there is nothing for it to do — and the entry that says "Following"
            // and the entry that is disabled must be the same entry.
            Assert.False(reset.Acts);
            Assert.True(MainScenarioReturn.AlreadyThere(engaged is not null, hasFollowedQuest));
            return;
        }

        Assert.True(reset.Acts, $"There is no way back to the Main Scenario while following {described}.");
        Assert.False(MainScenarioReturn.AlreadyThere(engaged is not null, hasFollowedQuest));
    }

    /// <summary>And it names the RIGHT operations. Releasing the engaged source and clearing the
    /// followed quest are independent, so a reset that did only one of them would leave a hunt running
    /// or drop the player back onto a side quest — which is why <see cref="FollowReset"/> carries two
    /// flags rather than one bool.</summary>
    [Fact]
    public void The_reset_releases_what_is_engaged_and_drops_the_chosen_quest()
    {
        var hunting = MainScenarioReturn.From(engaged: true, hasFollowedQuest: false);
        Assert.True(hunting.ReleaseEngagedSource);
        Assert.False(hunting.ClearFollowedQuest);

        var quest = MainScenarioReturn.From(engaged: false, hasFollowedQuest: true);
        Assert.False(quest.ReleaseEngagedSource);
        Assert.True(quest.ClearFollowedQuest);

        // A quest chosen underneath a running hunt needs both, and this is the case a single bool
        // could not express.
        var both = MainScenarioReturn.From(engaged: true, hasFollowedQuest: true);
        Assert.True(both.ReleaseEngagedSource);
        Assert.True(both.ClearFollowedQuest);
        Assert.True(both.Acts);
    }
}
