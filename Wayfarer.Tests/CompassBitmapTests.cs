using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The compass art's two properties the surface relies on: the needle points up from
/// the centre of its own image, and the ring is a ring.</summary>
public class CompassBitmapTests
{
    private const int Centre = CompassBitmap.Size / 2;

    [Fact]
    public void The_needle_is_ink_from_its_hub_to_the_top_and_clear_at_the_sides()
    {
        var needle = CompassBitmap.RenderNeedle();

        Assert.Equal(CompassBitmap.ByteCount, needle.Length);
        Assert.True(CompassBitmap.AlphaAt(needle, Centre, Centre) > 0, "the hub is drawn");
        Assert.True(CompassBitmap.AlphaAt(needle, Centre, 6) > 0, "the point reaches near the top edge");
        Assert.Equal(0, CompassBitmap.AlphaAt(needle, 2, Centre));
        Assert.Equal(0, CompassBitmap.AlphaAt(needle, CompassBitmap.Size - 3, Centre));
    }

    [Fact]
    public void The_ring_is_clear_at_its_centre_and_ink_on_its_circle()
    {
        var ring = CompassBitmap.RenderRing();
        var onCircle = (int)(Centre + (CompassBitmap.RingRadius / (CompassBitmap.TickOuter / CompassBitmap.GlyphMargin) * Centre));

        Assert.Equal(0, CompassBitmap.AlphaAt(ring, Centre, Centre));
        Assert.True(CompassBitmap.AlphaAt(ring, onCircle, Centre) > 0, "the circle is drawn");
        Assert.Equal(0, CompassBitmap.AlphaAt(ring, 0, 0));
    }
}
