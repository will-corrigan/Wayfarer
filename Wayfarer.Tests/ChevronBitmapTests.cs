using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>What the elevation mark has to be, pinned as pixels.
///
/// <para>The defect these come from: the elevation mark was the direction arrow's own art, drawn
/// small. Two meanings, one shape — the player read the "above you" mark as a second direction to
/// travel in. The property that fixes that is not "it is a nicer glyph", it is that the two
/// silhouettes are genuinely different, so the last test here is the one that matters.</para></summary>
public class ChevronBitmapTests
{
    [Fact]
    public void The_image_is_the_size_it_claims_to_be()
    {
        Assert.Equal(ChevronBitmap.ByteCount, ChevronBitmap.Render(ArrowIconVariant.Amber).Length);
        Assert.Equal(ChevronBitmap.Size * ChevronBitmap.Size * 4, ChevronBitmap.ByteCount);
    }

    [Fact]
    public void It_points_up_so_below_is_exactly_half_a_turn()
    {
        // Same contract the arrow keeps: the node rotates by pi for "below" and by nothing for
        // "above", so the art's rest orientation is load-bearing.
        var alpha = AlphaGrid(ArrowIconVariant.Amber);

        // The apex of a chevron is narrow and its arms are wide, so a row near an apex covers less
        // than the row below it.
        Assert.True(RowCoverage(alpha, ChevronBitmap.Size / 6) < RowCoverage(alpha, ChevronBitmap.Size / 3));
    }

    [Fact]
    public void It_is_horizontally_symmetric_about_the_centre()
    {
        var alpha = AlphaGrid(ArrowIconVariant.Amber);

        for (var y = 0; y < ChevronBitmap.Size; y++)
        {
            for (var x = 0; x < ChevronBitmap.Size / 2; x++)
            {
                var mirrored = ChevronBitmap.Size - 1 - x;
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
        var last = ChevronBitmap.Size - 1;

        Assert.Equal(0, alpha[0, 0]);
        Assert.Equal(0, alpha[0, last]);
        Assert.Equal(0, alpha[last, 0]);
        Assert.Equal(0, alpha[last, last]);
    }

    /// <summary>Two strokes with a gap between them, not one mass — that gap is what the eye reads
    /// as "double chevron" instead of "arrowhead", and it has to survive being drawn small.</summary>
    [Fact]
    public void The_double_form_really_has_two_separate_strokes()
    {
        var alpha = AlphaGrid(ArrowIconVariant.Amber);
        var centre = ChevronBitmap.Size / 2;

        var runs = 0;
        var inRun = false;
        for (var y = 0; y < ChevronBitmap.Size; y++)
        {
            var opaque = alpha[y, centre] > 128;
            if (opaque && !inRun)
            {
                runs++;
            }

            inRun = opaque;
        }

        Assert.Equal(2, runs);
    }

    [Fact]
    public void A_single_stroke_form_is_available_for_the_follow_caret()
    {
        var alpha = AlphaGrid(ArrowIconVariant.White, strokes: 1);
        var centre = ChevronBitmap.Size / 2;

        var runs = 0;
        var inRun = false;
        for (var y = 0; y < ChevronBitmap.Size; y++)
        {
            var opaque = alpha[y, centre] > 128;
            if (opaque && !inRun)
            {
                runs++;
            }

            inRun = opaque;
        }

        Assert.Equal(1, runs);
    }

    /// <summary>The whole point of the change. If the elevation mark and the direction arrow ever
    /// converge on the same silhouette again, this fails — and it fails without anyone having to
    /// look at a screen, which is what went wrong the first time.</summary>
    [Fact]
    public void It_is_not_the_same_silhouette_as_the_direction_arrow()
    {
        Assert.Equal(ArrowBitmap.Size, ChevronBitmap.Size);

        var chevron = AlphaGrid(ArrowIconVariant.Amber);
        var arrow = ArrowAlphaGrid(ArrowIconVariant.Amber);

        // The distinction is hollow-versus-solid, which is what survives being drawn at a dozen
        // pixels on a television. A cut straight down the middle crosses the chevron's two strokes
        // with air between them, and the arrow's single continuous body once. That difference
        // cannot be flattened by scale or by colour, which is why it is the assertion.
        Assert.Equal(2, ColumnRuns(chevron, ChevronBitmap.Size / 2));
        Assert.Equal(1, ColumnRuns(arrow, ArrowBitmap.Size / 2));

        // Total ink is deliberately NOT the claim: the chevron is the wider mark, so it can cover a
        // comparable number of pixels while being nothing like the same shape. It still has to be
        // the lighter of the two, and the runs above are what actually carry "hollow, not solid".
        var chevronCovered = TotalCoverage(chevron);
        var arrowCovered = TotalCoverage(arrow);
        Assert.True(
            chevronCovered < arrowCovered,
            $"the elevation mark covers {chevronCovered} pixels to the arrow's {arrowCovered} — it should be the lighter mark.");
    }

    [Fact]
    public void Every_variant_draws_the_same_shape_in_a_different_colour()
    {
        var amber = AlphaGrid(ArrowIconVariant.Amber);

        foreach (var variant in Enum.GetValues<ArrowIconVariant>())
        {
            var other = AlphaGrid(variant);
            for (var y = 0; y < ChevronBitmap.Size; y++)
            {
                for (var x = 0; x < ChevronBitmap.Size; x++)
                {
                    Assert.Equal(amber[y, x], other[y, x]);
                }
            }
        }
    }

    [Fact]
    public void A_zero_or_negative_stroke_count_still_draws_something()
    {
        var alpha = AlphaGrid(ArrowIconVariant.Amber, strokes: 0);
        var covered = 0;
        for (var y = 0; y < ChevronBitmap.Size; y++)
        {
            covered += RowCoverage(alpha, y);
        }

        Assert.True(covered > 0);
    }

    private static byte[,] AlphaGrid(ArrowIconVariant variant, int strokes = 2)
    {
        var pixels = ChevronBitmap.Render(variant, strokes);
        var grid = new byte[ChevronBitmap.Size, ChevronBitmap.Size];
        for (var y = 0; y < ChevronBitmap.Size; y++)
        {
            for (var x = 0; x < ChevronBitmap.Size; x++)
            {
                grid[y, x] = pixels[(((y * ChevronBitmap.Size) + x) * 4) + 3];
            }
        }

        return grid;
    }

    private static byte[,] ArrowAlphaGrid(ArrowIconVariant variant)
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

    /// <summary>How many separate opaque runs a vertical cut crosses. One means a solid body; two
    /// means two stacked strokes with air between them.</summary>
    private static int ColumnRuns(byte[,] alpha, int x)
    {
        var runs = 0;
        var inRun = false;
        for (var y = 0; y < ChevronBitmap.Size; y++)
        {
            var opaque = alpha[y, x] > 128;
            if (opaque && !inRun)
            {
                runs++;
            }

            inRun = opaque;
        }

        return runs;
    }

    private static int TotalCoverage(byte[,] alpha)
    {
        var covered = 0;
        for (var y = 0; y < ChevronBitmap.Size; y++)
        {
            covered += RowCoverage(alpha, y);
        }

        return covered;
    }

    private static int RowCoverage(byte[,] alpha, int y)
    {
        var count = 0;
        for (var x = 0; x < ChevronBitmap.Size; x++)
        {
            if (alpha[y, x] > 128)
            {
                count++;
            }
        }

        return count;
    }
}
