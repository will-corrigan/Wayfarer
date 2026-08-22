using Wayfarer.Core.Guidance;

namespace Wayfarer.Tests;

public class GuidanceSuppressionTests
{
    /// <summary>A visible baseline: logged in, a player exists, no cutscene, no zoning, and neither
    /// opt-in hide condition applies.</summary>
    private static SuppressionInputs Visible => new(
        LoggedIn: true,
        PlayerPresent: true,
        InCutscene: false,
        BetweenAreas: false,
        InCombat: false,
        HideInCombat: true,
        BoundByDuty: false,
        HideInDuty: true);

    [Fact]
    public void Baseline_IsVisible() => Assert.False(GuidanceSuppression.ShouldHide(Visible));

    [Fact]
    public void LoggedOut_Hides() => Assert.True(GuidanceSuppression.ShouldHide(Visible with { LoggedIn = false }));

    [Fact]
    public void NoPlayer_Hides() => Assert.True(GuidanceSuppression.ShouldHide(Visible with { PlayerPresent = false }));

    [Fact]
    public void Cutscene_Hides() => Assert.True(GuidanceSuppression.ShouldHide(Visible with { InCutscene = true }));

    [Fact]
    public void BetweenAreas_Hides() => Assert.True(GuidanceSuppression.ShouldHide(Visible with { BetweenAreas = true }));

    [Fact]
    public void Combat_Hides_OnlyWhenTheSettingIsOn()
    {
        Assert.True(GuidanceSuppression.ShouldHide(Visible with { InCombat = true, HideInCombat = true }));
        Assert.False(GuidanceSuppression.ShouldHide(Visible with { InCombat = true, HideInCombat = false }));
    }

    [Fact]
    public void Duty_Hides_OnlyWhenTheSettingIsOn()
    {
        Assert.True(GuidanceSuppression.ShouldHide(Visible with { BoundByDuty = true, HideInDuty = true }));
        Assert.False(GuidanceSuppression.ShouldHide(Visible with { BoundByDuty = true, HideInDuty = false }));
    }

    /// <summary>Exhaustive truth table over all eight inputs (256 combinations), pinning the two
    /// properties that matter: nothing hides without at least one gate being active, and every
    /// active gate hides.</summary>
    [Fact]
    public void TruthTable_IsExactlyTheSixGates()
    {
        for (var bits = 0; bits < 256; bits++)
        {
            var i = new SuppressionInputs(
                LoggedIn: (bits & 1) != 0,
                PlayerPresent: (bits & 2) != 0,
                InCutscene: (bits & 4) != 0,
                BetweenAreas: (bits & 8) != 0,
                InCombat: (bits & 16) != 0,
                HideInCombat: (bits & 32) != 0,
                BoundByDuty: (bits & 64) != 0,
                HideInDuty: (bits & 128) != 0);

            var anyGate = !i.LoggedIn
                || !i.PlayerPresent
                || i.InCutscene
                || i.BetweenAreas
                || (i.InCombat && i.HideInCombat)
                || (i.BoundByDuty && i.HideInDuty);

            Assert.Equal(anyGate, GuidanceSuppression.ShouldHide(i));
        }
    }
}
