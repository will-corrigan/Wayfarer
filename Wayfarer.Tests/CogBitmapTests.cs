using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>What the generated settings cog has to be, checked by looking at the pixels. The same
/// guarantees the direction arrow's tests pin, for the same reason: the readout parks this icon by
/// its centre and nothing else knows anything about the art.</summary>
public class CogBitmapTests
{
    [Fact]
    public void It_renders_the_declared_number_of_bytes()
    {
        Assert.Equal(CogBitmap.ByteCount, CogBitmap.Render().Length);
        Assert.Equal(CogBitmap.Size * CogBitmap.Size * 4, CogBitmap.ByteCount);
    }

    [Fact]
    public void The_corners_are_empty_so_the_cog_reads_as_round()
    {
        var pixels = CogBitmap.Render();
        const int Last = CogBitmap.Size - 1;

        Assert.Equal(0, Alpha(pixels, 0, 0));
        Assert.Equal(0, Alpha(pixels, Last, 0));
        Assert.Equal(0, Alpha(pixels, 0, Last));
        Assert.Equal(0, Alpha(pixels, Last, Last));
    }

    [Fact]
    public void It_has_a_hole_in_the_middle()
    {
        // A cog without a hub hole is a flower. The hole is also what makes it legible at 14 pixels.
        var pixels = CogBitmap.Render();
        const int Centre = CogBitmap.Size / 2;

        Assert.Equal(0, Alpha(pixels, Centre, Centre));
    }

    [Fact]
    public void The_body_between_the_hub_and_the_rim_is_solid()
    {
        var pixels = CogBitmap.Render();
        const int Centre = CogBitmap.Size / 2;

        // Half way out from the centre is inside the body whichever way you go from the middle,
        // teeth or gaps, because the body is an unbroken annulus.
        var offset = CogBitmap.Size / 4;
        Assert.Equal(255, Alpha(pixels, Centre + offset, Centre));
        Assert.Equal(255, Alpha(pixels, Centre - offset, Centre));
        Assert.Equal(255, Alpha(pixels, Centre, Centre + offset));
        Assert.Equal(255, Alpha(pixels, Centre, Centre - offset));
    }

    [Fact]
    public void It_is_centred_in_its_own_image()
    {
        // The readout positions it by its centre, so the art has to be centred in the texture.
        var pixels = CogBitmap.Render();
        long weightX = 0;
        long weightY = 0;
        long total = 0;

        for (var y = 0; y < CogBitmap.Size; y++)
        {
            for (var x = 0; x < CogBitmap.Size; x++)
            {
                var alpha = Alpha(pixels, x, y);
                weightX += (long)alpha * x;
                weightY += (long)alpha * y;
                total += alpha;
            }
        }

        Assert.True(total > 0, "the cog rendered nothing at all");
        var centre = (CogBitmap.Size - 1) / 2.0;
        Assert.Equal(centre, weightX / (double)total, 0.5);
        Assert.Equal(centre, weightY / (double)total, 0.5);
    }

    [Fact]
    public void The_rim_alternates_between_teeth_and_gaps()
    {
        // Walking a circle just outside the root radius has to cross solid and empty in turn, or
        // what has been drawn is a disc with a hole rather than a cog.
        var pixels = CogBitmap.Render();
        var radius = CogBitmap.Size * 0.37f;
        var centre = (CogBitmap.Size - 1) / 2f;

        var transitions = 0;
        var previous = SampleSolid(pixels, centre, radius, 0f);
        for (var step = 1; step <= 360; step++)
        {
            var current = SampleSolid(pixels, centre, radius, step * MathF.Tau / 360f);
            if (current != previous)
            {
                transitions++;
                previous = current;
            }
        }

        // Eight teeth means eight solid runs and eight gaps around the circle.
        Assert.Equal(16, transitions);
    }

    private static bool SampleSolid(byte[] pixels, float centre, float radius, float angle)
    {
        var x = (int)MathF.Round(centre + (radius * MathF.Cos(angle)));
        var y = (int)MathF.Round(centre + (radius * MathF.Sin(angle)));
        return Alpha(pixels, x, y) > 128;
    }

    private static byte Alpha(byte[] pixels, int x, int y) => pixels[(((y * CogBitmap.Size) + x) * 4) + 3];
}
