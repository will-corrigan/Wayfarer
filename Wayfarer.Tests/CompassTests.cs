using Wayfarer.Routing;

namespace Wayfarer.Tests;

/// <summary>The needle maths, pinned by the four cardinal directions. FFXIV's north is -Z.</summary>
public class CompassTests
{
    [Fact]
    public void North_is_zero()
    {
        Assert.Equal(0f, Compass.Bearing(0f, -10f), 0.0001f);
    }

    [Fact]
    public void East_is_a_quarter_turn_clockwise()
    {
        Assert.Equal(MathF.PI / 2f, Compass.Bearing(10f, 0f), 0.0001f);
    }

    [Fact]
    public void South_is_half_a_turn()
    {
        Assert.Equal(MathF.PI, MathF.Abs(Compass.Bearing(0f, 10f)), 0.0001f);
    }

    [Fact]
    public void West_is_a_quarter_turn_anticlockwise()
    {
        Assert.Equal(-MathF.PI / 2f, Compass.Bearing(-10f, 0f), 0.0001f);
    }

    [Fact]
    public void Facing_the_target_puts_the_needle_straight_up()
    {
        // A target due east, camera turned to face east: the negated camera yaw is -π/2, and the
        // needle lands at zero.
        var bearing = Compass.Bearing(10f, 0f);

        Assert.Equal(0f, Compass.NeedleAngle(bearing, -MathF.PI / 2f), 0.0001f);
    }

    [Fact]
    public void Angles_wrap_into_a_single_turn()
    {
        Assert.Equal(-MathF.PI / 2f, Compass.Normalize((3f * MathF.PI) / 2f), 0.0001f);
        Assert.Equal(MathF.PI / 2f, Compass.Normalize((-3f * MathF.PI) / 2f), 0.0001f);
    }
}
