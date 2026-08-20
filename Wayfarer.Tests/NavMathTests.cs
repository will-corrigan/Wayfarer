using Wayfarer.Core.Navigation;

namespace Wayfarer.Tests;

public class NavMathTests
{
    [Fact]
    public void Bearing_NorthIsZero()
    {
        // FFXIV world axes: +X east, +Z south → north is -Z.
        Assert.Equal(0f, NavMath.Bearing(0f, -1f), 3);
    }

    [Fact]
    public void Bearing_EastIsHalfPi()
    {
        Assert.Equal(MathF.PI / 2, NavMath.Bearing(1f, 0f), 3);
    }

    [Fact]
    public void Bearing_SouthIsPi()
    {
        Assert.Equal(MathF.PI, MathF.Abs(NavMath.Bearing(0f, 1f)), 3);
    }

    [Fact]
    public void ArrowAngle_SubtractsCameraYawAndNormalizes()
    {
        // bearing 0 (north), camera facing north → arrow straight up (0).
        Assert.Equal(0f, NavMath.ArrowAngle(0f, 0f), 3);

        // wrap-around stays in (-pi, pi]
        var a = NavMath.ArrowAngle(-3f, 3f);
        Assert.InRange(a, -MathF.PI, MathF.PI);
        Assert.Equal((2 * MathF.PI) - 6f, a, 3);
    }

    [Fact]
    public void Normalize_Wraps()
    {
        Assert.Equal(-MathF.PI + 0.5f, NavMath.Normalize(MathF.PI + 0.5f), 3);
        Assert.Equal(0.25f, NavMath.Normalize(0.25f), 3);
    }

    [Fact]
    public void Distance_Euclidean()
    {
        Assert.Equal(5f, NavMath.Distance(3f, 0f, 4f), 3);
    }

    [Theory]
    [InlineData(142.4f, "142 yalms")]
    [InlineData(0.4f, "0 yalms")]
    [InlineData(999.4f, "999 yalms")]
    [InlineData(1250f, "1.3k yalms")]
    public void FormatDistance_Cases(float yalms, string expected)
    {
        Assert.Equal(expected, NavMath.FormatDistance(yalms));
    }

    [Fact]
    public void NavigationState_DefaultsToHidden()
    {
        var s = new NavigationState();
        Assert.Equal(NavigationState.Modes.Hidden, s.Mode);
        Assert.Null(s.QuestName);
    }
}
