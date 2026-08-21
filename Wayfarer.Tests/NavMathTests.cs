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
    public void ArrowAngle_FacingNorth_TargetNorth_IsZero()
    {
        // bearing 0 (north), camera facing north → arrow straight up (0).
        Assert.Equal(0f, NavMath.ArrowAngle(0f, 0f), 3);
    }

    // The following cases pin down NavMath.ArrowAngle = Normalize(bearing + cameraYaw),
    // per the verified convention: Camera->DirH is counter-clockwise-positive from north
    // (0 = north, +pi/2 = west). Evidence: live four-direction test 2026-08-21 +
    // shipping Compass-plugin convention. CameraYawSign = -1 turns the subtraction in
    // ArrowAngle's definition into an addition of yaw.
    [Fact]
    public void ArrowAngle_FacingWest_TargetNorth_PointsUpRight()
    {
        // Camera facing west (DirH = +pi/2, CCW-positive). Target is due north (bearing 0).
        // North is to your right when you're facing west, so the arrow should point up-right (+pi/2).
        Assert.Equal(MathF.PI / 2, NavMath.ArrowAngle(0f, MathF.PI / 2), 3);
    }

    [Fact]
    public void ArrowAngle_FacingEast_TargetNorth_PointsUpLeft()
    {
        // Camera facing east (DirH = -pi/2, CCW-positive). Target is due north (bearing 0).
        // North is to your left when you're facing east, so the arrow should point up-left (-pi/2).
        Assert.Equal(-MathF.PI / 2, NavMath.ArrowAngle(0f, -MathF.PI / 2), 3);
    }

    [Fact]
    public void ArrowAngle_FacingSouth_TargetNorth_PointsDown()
    {
        // Camera facing south (DirH = pi, CCW-positive). Target is due north (bearing 0).
        // North is behind you when you're facing south, so the arrow should point straight down (+-pi).
        Assert.Equal(MathF.PI, MathF.Abs(NavMath.ArrowAngle(0f, MathF.PI)), 3);
    }

    [Fact]
    public void ArrowAngle_WrapsAroundPi()
    {
        // bearing 3, camera yaw 3 → raw sum 6 rad, which exceeds pi and must wrap
        // into (-pi, pi] as 6 - 2*pi.
        var a = NavMath.ArrowAngle(3f, 3f);
        Assert.InRange(a, -MathF.PI, MathF.PI);
        Assert.Equal(6f - (2 * MathF.PI), a, 3);
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
