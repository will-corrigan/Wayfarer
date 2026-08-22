using Wayfarer.Core.Guidance;

namespace Wayfarer.Tests;

public class QuestFollowResolutionTests
{
    private static readonly ushort[] Msq = [196, 0, 0];

    [Fact]
    public void NoOverride_FollowsTheMainScenarioHead()
    {
        var outcome = QuestFollowResolution.Resolve(null, questManagerAvailable: true, _ => true, Msq);

        Assert.Equal((ushort)196, outcome.QuestId);
        Assert.False(outcome.ClearOverride);
    }

    [Fact]
    public void AcceptedOverride_IsFollowed()
    {
        var outcome = QuestFollowResolution.Resolve(700, questManagerAvailable: true, id => id == 700, Msq);

        Assert.Equal((ushort)700, outcome.QuestId);
        Assert.False(outcome.ClearOverride);
    }

    [Fact]
    public void OverrideNoLongerAccepted_FallsBackToMsqHead_AndClears()
    {
        var outcome = QuestFollowResolution.Resolve(700, questManagerAvailable: true, _ => false, Msq);

        Assert.Equal((ushort)196, outcome.QuestId);
        Assert.True(outcome.ClearOverride);
    }

    /// <summary>Documented live behaviour: an unreadable quest system is not evidence that the
    /// player's chosen quest is finished, so the override survives the frame rather than snapping
    /// the arrow back to the main scenario.</summary>
    [Fact]
    public void QuestManagerUnavailable_KeepsOverride()
    {
        var outcome = QuestFollowResolution.Resolve(
            700, questManagerAvailable: false, _ => throw new InvalidOperationException("must not be asked"), Msq);

        Assert.Equal((ushort)700, outcome.QuestId);
        Assert.False(outcome.ClearOverride);
    }

    [Fact]
    public void NoOverrideAndNoMainScenario_FollowsNothing()
    {
        var outcome = QuestFollowResolution.Resolve(null, questManagerAvailable: true, _ => true, [0, 0, 0]);

        Assert.Null(outcome.QuestId);
    }

    [Fact]
    public void MainScenarioHead_SkipsLeadingZeroes()
    {
        var outcome = QuestFollowResolution.Resolve(null, questManagerAvailable: true, _ => true, [0, 321, 999]);

        Assert.Equal((ushort)321, outcome.QuestId);
    }
}
