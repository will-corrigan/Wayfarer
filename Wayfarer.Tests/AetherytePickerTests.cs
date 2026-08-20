using Wayfarer.Core.Navigation;

namespace Wayfarer.Tests;

public class AetherytePickerTests
{
    [Fact]
    public void Nearest_PicksClosest2D()
    {
        var pts = new List<AetherytePoint>
        {
            new(1, "Far", 100f, 100f),
            new(2, "Near", 10f, 5f),
            new(3, "Mid", 50f, 50f),
        };
        Assert.Equal(2u, AetherytePicker.Nearest(pts, 0f, 0f)!.Id);
    }

    [Fact]
    public void Nearest_EmptyReturnsNull()
    {
        Assert.Null(AetherytePicker.Nearest([], 0f, 0f));
    }

    [Fact]
    public void ShouldRouteViaAethernet_OnlyWhenHopBeatsRunning()
    {
        // Direct run 300y; entry 20y away, exit 40y from objective → 60y of walking beats 300y.
        Assert.True(AetherytePicker.ShouldRouteViaAethernet(300f, 20f, 40f));

        // Objective close by → never route (even if shards are right there).
        Assert.False(AetherytePicker.ShouldRouteViaAethernet(80f, 5f, 5f));

        // Walking legs + menu overhead barely better than the direct run → don't route.
        Assert.False(AetherytePicker.ShouldRouteViaAethernet(150f, 60f, 60f));
    }

    [Fact]
    public void MarkerPixelToWorld_RoundTrips()
    {
        // world→pixel: pixel = (world + offset) * (sizeFactor/100) + 1024
        // world 100, offset -50, sizeFactor 200 → pixel (100-50)*2+1024 = 1124
        var (x, z) = MapCoords.MarkerPixelToWorld(1124, 1124, 200, -50, -50);
        Assert.Equal(100f, x, 2);
        Assert.Equal(100f, z, 2);
    }

    [Fact]
    public void MarkerPixelToWorld_CenterIsNegatedOffset()
    {
        var (x, z) = MapCoords.MarkerPixelToWorld(1024, 1024, 100, 30, -70);
        Assert.Equal(-30f, x, 2);
        Assert.Equal(70f, z, 2);
    }
}
