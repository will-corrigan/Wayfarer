using Wayfarer.Surfaces.ScenarioTree;

namespace Wayfarer.Tests;

/// <summary>The above/below mark: that it is drawn, points up, and leaves its edges clear.</summary>
public class ChevronBitmapTests
{
    [Fact]
    public void The_mark_is_ink_on_its_centre_line_and_clear_at_the_corners()
    {
        var chevron = ChevronBitmap.Render();
        var centre = GlyphCanvas.Size / 2;

        Assert.Equal(GlyphCanvas.ByteCount, chevron.Length);
        Assert.True(Enumerable.Range(0, GlyphCanvas.Size).Any(y => GlyphCanvas.AlphaAt(chevron, centre, y) > 0), "an apex sits on the centre line");
        Assert.Equal(0, GlyphCanvas.AlphaAt(chevron, 0, 0));
        Assert.Equal(0, GlyphCanvas.AlphaAt(chevron, GlyphCanvas.Size - 1, GlyphCanvas.Size - 1));
    }
}
