using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

public class ChevronBitmapTests
{
    [Fact]
    public void The_mark_is_ink_on_its_centre_line_and_clear_at_the_corners()
    {
        var chevron = ChevronBitmap.Render();
        var centre = ChevronBitmap.Size / 2;

        Assert.Equal(ChevronBitmap.ByteCount, chevron.Length);
        Assert.True(Enumerable.Range(0, ChevronBitmap.Size).Any(y => ChevronBitmap.AlphaAt(chevron, centre, y) > 0), "an apex sits on the centre line");
        Assert.Equal(0, ChevronBitmap.AlphaAt(chevron, 0, 0));
        Assert.Equal(0, ChevronBitmap.AlphaAt(chevron, ChevronBitmap.Size - 1, ChevronBitmap.Size - 1));
    }
}
