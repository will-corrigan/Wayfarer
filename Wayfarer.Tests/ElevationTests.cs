using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>When the compass says above or below: a threshold with hysteresis, not a comparison.</summary>
public class ElevationTests
{
    [Fact]
    public void Terrain_roll_is_level()
    {
        Assert.Equal(ElevationHint.Level, Elevation.Classify(3f, ElevationHint.Level));
        Assert.Equal(ElevationHint.Level, Elevation.Classify(-3f, ElevationHint.Level));
    }

    [Fact]
    public void A_storey_is_above_or_below()
    {
        Assert.Equal(ElevationHint.Above, Elevation.Classify(7f, ElevationHint.Level));
        Assert.Equal(ElevationHint.Below, Elevation.Classify(-7f, ElevationHint.Level));
    }

    [Fact]
    public void Once_shown_the_mark_holds_until_the_difference_falls_back_past_the_lower_bound()
    {
        Assert.Equal(ElevationHint.Above, Elevation.Classify(5f, ElevationHint.Above));
        Assert.Equal(ElevationHint.Level, Elevation.Classify(3f, ElevationHint.Above));
    }

    [Fact]
    public void An_unknown_height_is_level()
    {
        Assert.Equal(ElevationHint.Level, Elevation.Classify(null, ElevationHint.Above));
    }
}
