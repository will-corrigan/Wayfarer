using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>What the readout's arrow has to be, pinned as pixels.
///
/// The defect these come from is worth restating: the readout drew a 24x24 crop of the minimap's
/// texture sheet, the crop was ignored because the image node was set to fit its whole texture, and
/// what actually appeared was the entire sheet squashed into 34 pixels — "an ornate scrollwork bar",
/// in the player's words. The arrow is generated now, so the two properties everything downstream
/// assumes are testable rather than assumed: it points up, and it is centred in its own image.</summary>
public class ArrowBitmapTests
{
    [Fact]
    public void The_image_is_the_size_it_claims_to_be()
    {
        Assert.Equal(ArrowBitmap.ByteCount, ArrowBitmap.Render(ArrowIconVariant.Amber).Length);
        Assert.Equal(ArrowBitmap.Size * ArrowBitmap.Size * 4, ArrowBitmap.ByteCount);
    }

    [Fact]
    public void It_points_up()
    {
        // The load-bearing property: NavMath.ArrowAngle is defined as "0 = straight ahead", handed
        // straight to the node's rotation. If the art's rest orientation were not up, every arrow
        // would be wrong by a fixed turn while the words fallback stayed right.
        var alpha = AlphaGrid(ArrowIconVariant.Amber);

        Assert.True(RowCoverage(alpha, ArrowBitmap.Size / 6) < RowCoverage(alpha, ArrowBitmap.Size * 5 / 6));
        Assert.True(Opaque(alpha, ArrowBitmap.Size / 2, ArrowBitmap.Size / 6));
    }

    [Fact]
    public void It_is_horizontally_symmetric_about_the_centre()
    {
        // Rotation happens about the image's centre, so an off-centre arrow wobbles instead of
        // spinning in place. This is what the minimap's carets got wrong.
        var alpha = AlphaGrid(ArrowIconVariant.Amber);

        for (var y = 0; y < ArrowBitmap.Size; y++)
        {
            for (var x = 0; x < ArrowBitmap.Size / 2; x++)
            {
                var mirrored = ArrowBitmap.Size - 1 - x;
                Assert.True(
                    Math.Abs(alpha[y, x] - alpha[y, mirrored]) <= 2,
                    $"asymmetric at ({x},{y}): {alpha[y, x]} vs {alpha[y, mirrored]}");
            }
        }
    }

    [Fact]
    public void The_corners_are_transparent_so_it_never_reads_as_a_box()
    {
        var alpha = AlphaGrid(ArrowIconVariant.Amber);
        var last = ArrowBitmap.Size - 1;

        Assert.Equal(0, alpha[0, 0]);
        Assert.Equal(0, alpha[0, last]);
        Assert.Equal(0, alpha[last, 0]);
        Assert.Equal(0, alpha[last, last]);
    }

    [Fact]
    public void It_has_a_tail_notch_so_the_pointing_end_is_unambiguous()
    {
        // A plain triangle reads as a triangle; the notch is what makes it read as an arrow.
        var alpha = AlphaGrid(ArrowIconVariant.Amber);
        var centre = ArrowBitmap.Size / 2;

        Assert.False(Opaque(alpha, centre, ArrowBitmap.Size - 4));
        Assert.True(Opaque(alpha, 8, ArrowBitmap.Size - 12) || Opaque(alpha, 12, ArrowBitmap.Size - 14));
    }

    [Fact]
    public void Every_variant_draws_the_same_shape_in_a_different_colour()
    {
        var amber = AlphaGrid(ArrowIconVariant.Amber);

        foreach (var variant in Enum.GetValues<ArrowIconVariant>())
        {
            var other = AlphaGrid(variant);
            for (var y = 0; y < ArrowBitmap.Size; y++)
            {
                for (var x = 0; x < ArrowBitmap.Size; x++)
                {
                    Assert.Equal(amber[y, x], other[y, x]);
                }
            }
        }

        Assert.NotEqual(
            Colour(ArrowIconVariant.Amber, ArrowBitmap.Size / 2, ArrowBitmap.Size / 2),
            Colour(ArrowIconVariant.Blue, ArrowBitmap.Size / 2, ArrowBitmap.Size / 2));
    }

    [Fact]
    public void An_unknown_variant_still_produces_an_arrow_rather_than_an_empty_image()
    {
        var alpha = AlphaGrid((ArrowIconVariant)99);

        Assert.True(RowCoverage(alpha, ArrowBitmap.Size / 2) > 0);
    }

    private static byte[,] AlphaGrid(ArrowIconVariant variant)
    {
        var pixels = ArrowBitmap.Render(variant);
        var grid = new byte[ArrowBitmap.Size, ArrowBitmap.Size];
        for (var y = 0; y < ArrowBitmap.Size; y++)
        {
            for (var x = 0; x < ArrowBitmap.Size; x++)
            {
                grid[y, x] = pixels[(((y * ArrowBitmap.Size) + x) * 4) + 3];
            }
        }

        return grid;
    }

    private static (byte R, byte G, byte B) Colour(ArrowIconVariant variant, int x, int y)
    {
        var pixels = ArrowBitmap.Render(variant);
        var offset = ((y * ArrowBitmap.Size) + x) * 4;
        return (pixels[offset], pixels[offset + 1], pixels[offset + 2]);
    }

    private static bool Opaque(byte[,] alpha, int x, int y) => alpha[y, x] > 128;

    private static int RowCoverage(byte[,] alpha, int y)
    {
        var count = 0;
        for (var x = 0; x < ArrowBitmap.Size; x++)
        {
            if (alpha[y, x] > 128)
            {
                count++;
            }
        }

        return count;
    }
}
