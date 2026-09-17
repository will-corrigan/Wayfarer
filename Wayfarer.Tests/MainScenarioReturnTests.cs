using Wayfarer.Core.Guidance;

namespace Wayfarer.Tests;

/// <summary>The two things the follow list must never get wrong: that there is a way back to the
/// Main Scenario from wherever the player is, and that the list does not tell her she is already
/// there.
///
/// <para><b>Why these are decision tests and not presence tests.</b> The defect these cover shipped
/// past a surface that <i>had</i> a Main Scenario entry in every mode. The entry was there; it was
/// disabled, because each surface decided "am I already on the main scenario?" from the followed-quest
/// override alone, and that override is null while a source is engaged. So the entry existed, said
/// "Following", and could not be pressed. Asserting that an entry exists would have passed. What is
/// asserted here is what the entry would DO — which operations the reset performs in that mode, and
/// therefore whether it changes anything at all.</para></summary>
public class MainScenarioReturnTests
{
    /// <summary>Every state the reset has to act from, as the two facts that produce it: whether a
    /// source is engaged, and whether a quest has been chosen. Neither is "already there".</summary>
    public static TheoryData<string, bool, bool> States =>
        new()
        {
            { "nothing", false, false },
            { "a quest", false, true },
            { "an engaged source", true, false },
            { "a quest under an engaged source", true, true },
        };

    /// <summary>Exactly one mode is reported for any state, and it is the one the player would
    /// name. Nothing chosen is the main scenario — this plugin has no null follow state.</summary>
    [Theory]
    [InlineData(false, FollowMode.MainScenario)]
    [InlineData(true, FollowMode.Quest)]
    public void The_reported_mode_is_the_one_being_followed(bool hasFollowedQuest, FollowMode expected)
    {
        Assert.Equal(expected, MainScenarioReturn.ModeOf(hasFollowedQuest));
    }

    /// <summary><b>The route back exists from every follow mode.</b> Not that a menu entry exists —
    /// that it acts: the reset names at least one operation to perform in every mode but the one the
    /// player is already in.</summary>
    [Theory]
    [MemberData(nameof(States))]
    public void There_is_a_way_back_to_the_main_scenario_from_every_mode(
        string described, bool engaged, bool hasFollowedQuest)
    {
        var reset = MainScenarioReturn.From(engaged, hasFollowedQuest);

        if (!engaged && !hasFollowedQuest)
        {
            // Already there, so there is nothing for it to do — and the entry that says "Following"
            // and the entry that is disabled must be the same entry.
            Assert.False(reset.Acts);
            Assert.True(MainScenarioReturn.AlreadyThere(engaged, hasFollowedQuest));
            return;
        }

        Assert.True(reset.Acts, $"There is no way back to the Main Scenario while following {described}.");
        Assert.False(MainScenarioReturn.AlreadyThere(engaged, hasFollowedQuest));
    }

    /// <summary>And it names the RIGHT operations. Releasing the engaged source and clearing the
    /// followed quest are independent, so a reset that did only one of them would leave the engaged
    /// source running or drop the player back onto a side quest — which is why
    /// <see cref="FollowReset"/> carries two flags rather than one bool.</summary>
    [Fact]
    public void The_reset_releases_what_is_engaged_and_drops_the_chosen_quest()
    {
        var engaged = MainScenarioReturn.From(engaged: true, hasFollowedQuest: false);
        Assert.True(engaged.ReleaseEngagedSource);
        Assert.False(engaged.ClearFollowedQuest);

        var quest = MainScenarioReturn.From(engaged: false, hasFollowedQuest: true);
        Assert.False(quest.ReleaseEngagedSource);
        Assert.True(quest.ClearFollowedQuest);

        // A quest chosen underneath a running source needs both, and this is the case a single bool
        // could not express.
        var both = MainScenarioReturn.From(engaged: true, hasFollowedQuest: true);
        Assert.True(both.ReleaseEngagedSource);
        Assert.True(both.ClearFollowedQuest);
        Assert.True(both.Acts);
    }
}
