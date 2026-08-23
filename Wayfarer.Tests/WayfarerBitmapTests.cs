using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>What the generated emblem has to be, checked by looking at the pixels rather than at the
/// picture. Same guarantees the arrow's and the cog's tests pin, plus the ones that make this
/// particular mark recognisably the one in <c>images/icon.png</c>: a ring with four cardinal ticks,
/// a needle inside it pointing up, and empty space between the two.</summary>
public class WayfarerBitmapTests
{
    [Fact]
    public void It_renders_the_declared_number_of_bytes()
    {
        Assert.Equal(WayfarerBitmap.ByteCount, WayfarerBitmap.Render().Length);
        Assert.Equal(WayfarerBitmap.Size * WayfarerBitmap.Size * 4, WayfarerBitmap.ByteCount);
    }

    [Fact]
    public void The_corners_are_empty_so_the_emblem_reads_as_round()
    {
        // Also the check that the installer icon's dark rounded-square tile really is gone: a crest
        // sits ON the banner's plate, and a filled corner would be a sticker pasted onto it.
        var pixels = WayfarerBitmap.Render();
        const int Last = WayfarerBitmap.Size - 1;

        Assert.Equal(0, Alpha(pixels, 0, 0));
        Assert.Equal(0, Alpha(pixels, Last, 0));
        Assert.Equal(0, Alpha(pixels, 0, Last));
        Assert.Equal(0, Alpha(pixels, Last, Last));
    }

    [Fact]
    public void There_is_a_ring_with_nothing_but_air_inside_it()
    {
        // Walking out along a diagonal — which no compass point lies on, and no tick — has to cross
        // empty, then the ring, then empty again. A solid disc would fail the first of those and is
        // exactly what the emblem must not be.
        var pixels = WayfarerBitmap.Render();
        const float Diagonal = MathF.PI / 4f;

        Assert.False(Solid(pixels, 0.45f, Diagonal), "the space between the needle and the ring is filled in");
        Assert.True(Solid(pixels, 0.82f, Diagonal), "there is no ring");
        Assert.False(Solid(pixels, 0.99f, Diagonal), "the ring has no outside");
    }

    [Fact]
    public void The_ring_carries_exactly_four_cardinal_ticks()
    {
        // Just outside the ring's own stroke only the ticks are solid, so a circle walked at that
        // radius crosses four solid runs — north, east, south and west, as the source icon draws
        // them.
        var pixels = WayfarerBitmap.Render();
        var transitions = 0;
        var previous = Solid(pixels, 0.925f, 0f);

        for (var step = 1; step <= 360; step++)
        {
            var current = Solid(pixels, 0.925f, step * MathF.Tau / 360f);
            if (current != previous)
            {
                transitions++;
                previous = current;
            }
        }

        Assert.Equal(8, transitions);
    }

    [Fact]
    public void The_needle_points_up()
    {
        // The long half is above the waist and the short one below, which is what makes it a needle
        // rather than a diamond — and what says which way is north without a letter.
        var pixels = WayfarerBitmap.Render();
        const int Centre = WayfarerBitmap.Size / 2;

        Assert.True(Solid(pixels, -0.50f, MathF.PI / 2f), "the needle has no tip");
        Assert.False(Solid(pixels, 0.50f, MathF.PI / 2f), "the needle's tail is as long as its tip");

        // And the pin through it is dark, not gold: it is the one part of the mark that is a hole in
        // the source icon.
        Assert.True(Luma(pixels, Centre, Centre) < 80, "the hub is not dark");
    }

    [Fact]
    public void It_is_centred_in_its_own_image()
    {
        var pixels = WayfarerBitmap.Render();
        long weightX = 0;
        long total = 0;

        for (var y = 0; y < WayfarerBitmap.Size; y++)
        {
            for (var x = 0; x < WayfarerBitmap.Size; x++)
            {
                var alpha = Alpha(pixels, x, y);
                weightX += (long)alpha * x;
                total += alpha;
            }
        }

        Assert.True(total > 0, "the emblem rendered nothing at all");

        // Horizontally only: the mark is symmetric left to right and deliberately is NOT top to
        // bottom, because the needle's tip is longer than its tail.
        var centre = (WayfarerBitmap.Size - 1) / 2.0;
        Assert.Equal(centre, weightX / (double)total, 0.5);
    }

    /// <summary>Samples the emblem at a radius in units of half the image (negative for "up the
    /// vertical axis") and an angle in radians.</summary>
    private static bool Solid(byte[] pixels, float radius, float angle)
    {
        var half = WayfarerBitmap.Size / 2f;
        var x = (int)MathF.Round(half + (radius * half * MathF.Cos(angle)));
        var y = (int)MathF.Round(half + (radius * half * MathF.Sin(angle)));
        return Alpha(pixels, Math.Clamp(x, 0, WayfarerBitmap.Size - 1), Math.Clamp(y, 0, WayfarerBitmap.Size - 1)) > 128;
    }

    private static byte Alpha(byte[] pixels, int x, int y) =>
        pixels[(((y * WayfarerBitmap.Size) + x) * 4) + 3];

    private static int Luma(byte[] pixels, int x, int y)
    {
        var offset = ((y * WayfarerBitmap.Size) + x) * 4;
        return ((pixels[offset] * 30) + (pixels[offset + 1] * 59) + (pixels[offset + 2] * 11)) / 100;
    }
}
