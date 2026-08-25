using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>What the readout's compass has to be, pinned as pixels.
///
/// <para>The readout's direction indicator is a compass: a dial that <b>does not move</b> and a
/// needle that turns inside it. That is two textures, and the properties worth asserting are the ones
/// that would be silently wrong if they broke — the needle centred on its own hub (or it orbits
/// instead of spinning), the needle pointing straight up at rest (or every bearing is out by a fixed
/// turn while the words fallback stays right), neither texture carrying any of the other's ink (or
/// the dial turns with the needle), and the needle no smaller on screen than the arrow it replaced
/// (or the change is a legibility regression for anyone who turned the arrow-size setting up to read
/// the readout across a room).</para></summary>
public class CompassBitmapTests
{
    /// <summary>The arrow-size setting at its maximum, which is where the player who asked for a
    /// bigger pointer actually has it.</summary>
    private const float MaxArrowScale = 2f;

    [Fact]
    public void Both_images_are_the_size_they_claim_to_be()
    {
        Assert.Equal(CompassBitmap.ByteCount, CompassBitmap.RenderRing(ArrowIconVariant.Amber).Length);
        Assert.Equal(CompassBitmap.ByteCount, CompassBitmap.RenderNeedle(ArrowIconVariant.Amber).Length);
        Assert.Equal(CompassBitmap.Size * CompassBitmap.Size * 4, CompassBitmap.ByteCount);
    }

    [Fact]
    public void The_needle_points_up()
    {
        // The load-bearing property: NavMath.ArrowAngle is defined as "0 = straight ahead", handed
        // straight to the node's rotation. The needle is not symmetric top to bottom — its pointing
        // end is the long one — so "points up" is that the long end is the one nearer the top edge.
        var alpha = Needle(ArrowIconVariant.Amber);
        var centre = CompassBitmap.Size / 2;
        var (top, bottom) = Rows(alpha);
        var fromBottom = CompassBitmap.Size - 1 - bottom;
        var where = $"the needle reaches {top} from the top and {fromBottom} from the bottom";

        Assert.True(Opaque(alpha, centre, top + 1), "nothing on the centre line at the top");
        Assert.True(top < fromBottom, $"{where}, so its long end is down");
    }

    [Fact]
    public void The_needle_is_centred_on_its_own_hub_so_it_spins_in_place()
    {
        // Rotation happens about the image's centre, so a needle whose pivot is not that centre
        // orbits instead of spinning. Two halves of the same claim: the ink is symmetric about the
        // centre column, and the hub — the dark dot the needle turns on — is centred in the image.
        var alpha = Needle(ArrowIconVariant.Amber);

        for (var y = 0; y < CompassBitmap.Size; y++)
        {
            for (var x = 0; x < CompassBitmap.Size / 2; x++)
            {
                var mirrored = CompassBitmap.Size - 1 - x;
                Assert.True(
                    Math.Abs(alpha[y, x] - alpha[y, mirrored]) <= 2,
                    $"asymmetric at ({x},{y}): {alpha[y, x]} vs {alpha[y, mirrored]}");
            }
        }

        var hub = HubBounds(ArrowIconVariant.Amber);
        Assert.True(hub.Left < hub.Right, "there is no hub in the needle's image at all");
        Assert.True(
            Math.Abs(hub.Left + hub.Right + 1 - CompassBitmap.Size) <= 1,
            $"the hub spans x {hub.Left}..{hub.Right} of {CompassBitmap.Size}, so it is off centre");
        Assert.True(
            Math.Abs(hub.Top + hub.Bottom + 1 - CompassBitmap.Size) <= 1,
            $"the hub spans y {hub.Top}..{hub.Bottom} of {CompassBitmap.Size}, so it is off centre");
    }

    [Fact]
    public void The_ring_holds_no_part_of_the_needle()
    {
        // Why the compass is two images: the node holding this one is never given a rotation, so
        // anything of the needle's baked into it would be a needle that never turns — and anything of
        // the dial's baked into the needle's image would be a dial that does.
        var alpha = Ring(ArrowIconVariant.Amber);

        for (var y = 0; y < CompassBitmap.Size; y++)
        {
            for (var x = 0; x < CompassBitmap.Size; x++)
            {
                if (alpha[y, x] <= 8)
                {
                    continue;
                }

                var where = $"the ring's image has ink at ({x},{y}), {RingRadiusAt(x, y):F2} out";
                Assert.True(RingRadiusAt(x, y) > 0.6f, $"{where} — inside the dial, where only the needle goes");
            }
        }
    }

    [Fact]
    public void The_needle_holds_no_part_of_the_ring()
    {
        var alpha = Needle(ArrowIconVariant.Amber);

        // Generous: the needle's shoulders plus room for its outline. The ring sits at 0.80 and its
        // ticks reach 0.90, so nothing about this tolerance could hide one.
        const float Widest = CompassBitmap.NeedleHalfWidth + 0.12f;

        for (var y = 0; y < CompassBitmap.Size; y++)
        {
            for (var x = 0; x < CompassBitmap.Size; x++)
            {
                if (alpha[y, x] <= 8)
                {
                    continue;
                }

                var where = $"the needle's image has ink at ({x},{y}), {NeedleUnitsAt(x):F2} off its centre line";
                Assert.True(Math.Abs(NeedleUnitsAt(x)) <= Widest, $"{where} — wider than the needle itself");
            }
        }
    }

    [Fact]
    public void The_ring_is_a_ring_centred_in_its_own_image()
    {
        var alpha = Ring(ArrowIconVariant.Amber);
        var last = CompassBitmap.Size - 1;

        for (var y = 0; y < CompassBitmap.Size; y++)
        {
            for (var x = 0; x < CompassBitmap.Size; x++)
            {
                Assert.True(
                    Math.Abs(alpha[y, x] - alpha[y, last - x]) <= 2, $"not symmetric left to right at ({x},{y})");
                Assert.True(
                    Math.Abs(alpha[y, x] - alpha[last - y, x]) <= 2, $"not symmetric top to bottom at ({x},{y})");
            }
        }

        // The ring's stroke where no tick is, so this is the circle itself and not a cardinal mark.
        Assert.True(RingInkAt(alpha, 45f), "the ring is not continuous between its ticks");

        // And a tick at each cardinal, out past where the circle alone reaches.
        foreach (var (dx, dy) in new[] { (0f, -1f), (1f, 0f), (0f, 1f), (-1f, 0f) })
        {
            Assert.True(
                RingInkAlong(alpha, dx, dy, CompassBitmap.TickOuter - 0.01f),
                $"no cardinal tick along ({dx},{dy})");
        }
    }

    [Fact]
    public void The_needle_is_not_smaller_on_screen_than_the_arrow_it_replaces()
    {
        // The measurement, not the intention: both glyphs' own ink, measured in their own textures,
        // scaled by the box each is actually drawn in at the player's maxed arrow-size setting.
        var arrow = InkHeight(ArrowAlpha(ArrowIconVariant.Amber), ArrowBitmap.Size)
            * ReadoutBodyLayout.ArrowBox(1f, MaxArrowScale) / ArrowBitmap.Size;
        var needle = InkHeight(Needle(ArrowIconVariant.Amber), CompassBitmap.Size)
            * ReadoutBodyLayout.CompassNeedleBox(1f, MaxArrowScale) / CompassBitmap.Size;

        Assert.True(needle >= arrow, $"the needle draws {needle:F1}px tall where the arrow drew {arrow:F1}px");
    }

    [Fact]
    public void The_whole_element_still_clears_the_words_beside_it_at_the_largest_setting()
    {
        // The compass is wider than the arrow was, and the gutter's hard budget is the words: the
        // element may overhang the empty margin to its left and must never reach the text on its
        // right. LayoutContainmentTests proves this against the placed rectangles; this is the same
        // constraint on the size itself, so a change to the geometry fails here first.
        var ring = ReadoutBodyLayout.CompassRingBox(1f, MaxArrowScale);
        var room = ReadoutBodyLayout.SubLineLeft(1f);

        Assert.True(ring <= room, $"the ring's box is {ring:F1} against {room:F1} of room");
    }

    [Fact]
    public void Neither_image_ever_reads_as_a_box()
    {
        var last = CompassBitmap.Size - 1;

        foreach (var alpha in new[] { Ring(ArrowIconVariant.Amber), Needle(ArrowIconVariant.Amber) })
        {
            Assert.Equal(0, alpha[0, 0]);
            Assert.Equal(0, alpha[0, last]);
            Assert.Equal(0, alpha[last, 0]);
            Assert.Equal(0, alpha[last, last]);
        }
    }

    [Fact]
    public void Every_variant_draws_the_same_shapes_in_a_different_colour()
    {
        var amberRing = Ring(ArrowIconVariant.Amber);
        var amberNeedle = Needle(ArrowIconVariant.Amber);

        foreach (var variant in Enum.GetValues<ArrowIconVariant>())
        {
            var ring = Ring(variant);
            var needle = Needle(variant);
            for (var y = 0; y < CompassBitmap.Size; y++)
            {
                for (var x = 0; x < CompassBitmap.Size; x++)
                {
                    Assert.Equal(amberRing[y, x], ring[y, x]);
                    Assert.Equal(amberNeedle[y, x], needle[y, x]);
                }
            }
        }

        // The colour setting still reaches both pieces: a gold dial around a blue needle would be a
        // setting that only half works.
        Assert.NotEqual(
            NeedleColour(ArrowIconVariant.Amber, CompassBitmap.Size / 2, CompassBitmap.Size / 4),
            NeedleColour(ArrowIconVariant.Blue, CompassBitmap.Size / 2, CompassBitmap.Size / 4));
        Assert.NotEqual(
            RingColour(ArrowIconVariant.Amber, CompassBitmap.Size / 2, RingTop()),
            RingColour(ArrowIconVariant.Blue, CompassBitmap.Size / 2, RingTop()));
    }

    [Fact]
    public void The_needles_two_halves_are_two_different_golds()
    {
        // The icon's needle is bright in front of its shoulders and deeper behind them, which is what
        // says which end is the pointing one when the readout is across a room and the shape is a
        // dozen pixels tall.
        var fore = NeedleColour(ArrowIconVariant.Amber, CompassBitmap.Size / 2, CompassBitmap.Size / 4);
        var aft = NeedleColour(ArrowIconVariant.Amber, CompassBitmap.Size / 2, CompassBitmap.Size * 3 / 4);

        Assert.NotEqual(fore, aft);
        Assert.True(
            fore.R + fore.G + fore.B > aft.R + aft.G + aft.B,
            $"the pointing half ({fore}) is not the brighter one ({aft})");
    }

    [Fact]
    public void An_unknown_variant_still_draws_a_compass_rather_than_an_empty_image()
    {
        Assert.True(InkHeight(Needle((ArrowIconVariant)99), CompassBitmap.Size) > 0);
        Assert.True(InkHeight(Ring((ArrowIconVariant)99), CompassBitmap.Size) > 0);
    }

    private static byte[,] Ring(ArrowIconVariant variant) =>
        Grid(CompassBitmap.RenderRing(variant), CompassBitmap.Size);

    private static byte[,] Needle(ArrowIconVariant variant) =>
        Grid(CompassBitmap.RenderNeedle(variant), CompassBitmap.Size);

    private static byte[,] ArrowAlpha(ArrowIconVariant variant) =>
        Grid(ArrowBitmap.Render(variant), ArrowBitmap.Size);

    private static byte[,] Grid(byte[] pixels, int size)
    {
        var grid = new byte[size, size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                grid[y, x] = pixels[(((y * size) + x) * 4) + 3];
            }
        }

        return grid;
    }

    private static (byte R, byte G, byte B) NeedleColour(ArrowIconVariant variant, int x, int y) =>
        Colour(CompassBitmap.RenderNeedle(variant), x, y);

    private static (byte R, byte G, byte B) RingColour(ArrowIconVariant variant, int x, int y) =>
        Colour(CompassBitmap.RenderRing(variant), x, y);

    private static (byte R, byte G, byte B) Colour(byte[] pixels, int x, int y)
    {
        var offset = ((y * CompassBitmap.Size) + x) * 4;
        return (pixels[offset], pixels[offset + 1], pixels[offset + 2]);
    }

    private static bool Opaque(byte[,] alpha, int x, int y) => alpha[y, x] > 128;

    /// <summary>The first and last rows with any ink in them.</summary>
    private static (int Top, int Bottom) Rows(byte[,] alpha)
    {
        var top = CompassBitmap.Size;
        var bottom = -1;

        for (var y = 0; y < CompassBitmap.Size; y++)
        {
            for (var x = 0; x < CompassBitmap.Size; x++)
            {
                if (alpha[y, x] <= 128)
                {
                    continue;
                }

                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return (top, bottom);
    }

    /// <summary>How many rows of its own texture a glyph's ink fills.</summary>
    private static float InkHeight(byte[,] alpha, int size)
    {
        var top = size;
        var bottom = -1;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (alpha[y, x] <= 128)
                {
                    continue;
                }

                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return bottom < top ? 0f : bottom - top + 1;
    }

    /// <summary>Where the hub — the one part of the needle's image drawn in the icon's background dark
    /// rather than in a gold — actually sits.</summary>
    private static (int Left, int Right, int Top, int Bottom) HubBounds(ArrowIconVariant variant)
    {
        var pixels = CompassBitmap.RenderNeedle(variant);
        var (left, right, top, bottom) = (CompassBitmap.Size, -1, CompassBitmap.Size, -1);

        for (var y = 0; y < CompassBitmap.Size; y++)
        {
            for (var x = 0; x < CompassBitmap.Size; x++)
            {
                var offset = ((y * CompassBitmap.Size) + x) * 4;
                var dark = pixels[offset] < 40 && pixels[offset + 1] < 40 && pixels[offset + 2] > 20;
                if (pixels[offset + 3] <= 128 || !dark)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return (left, right, top, bottom);
    }

    /// <summary>How far out a pixel of the ring's image is, in the dial's own units.</summary>
    private static float RingRadiusAt(int x, int y)
    {
        const float Half = CompassBitmap.Size / 2f;
        const float Unit = CompassBitmap.TickOuter / CompassBitmap.GlyphMargin;
        var dx = (((x + 0.5f) / Half) - 1f) * Unit;
        var dy = (((y + 0.5f) / Half) - 1f) * Unit;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>How far off the needle's centre line a column of its image is, in dial units.</summary>
    private static float NeedleUnitsAt(int x)
    {
        const float Half = CompassBitmap.Size / 2f;
        const float Unit = CompassBitmap.NeedleFore / CompassBitmap.GlyphMargin;
        return (((x + 0.5f) / Half) - 1f) * Unit;
    }

    /// <summary>Whether the ring's stroke is present at <paramref name="degrees"/> around the dial.
    /// </summary>
    private static bool RingInkAt(byte[,] alpha, float degrees)
    {
        var radians = degrees * MathF.PI / 180f;
        return RingInkAlong(alpha, MathF.Cos(radians), MathF.Sin(radians), CompassBitmap.RingRadius);
    }

    /// <summary>Whether there is ink at <paramref name="radius"/> dial units along a direction,
    /// allowing a pixel either side of the exact spot for the stroke's own thickness.</summary>
    private static bool RingInkAlong(byte[,] alpha, float dx, float dy, float radius)
    {
        const float Half = CompassBitmap.Size / 2f;
        const float Unit = CompassBitmap.TickOuter / CompassBitmap.GlyphMargin;
        var x = (int)((((dx * radius / Unit) + 1f) * Half) - 0.5f);
        var y = (int)((((dy * radius / Unit) + 1f) * Half) - 0.5f);

        for (var oy = -1; oy <= 1; oy++)
        {
            for (var ox = -1; ox <= 1; ox++)
            {
                var px = Math.Clamp(x + ox, 0, CompassBitmap.Size - 1);
                var py = Math.Clamp(y + oy, 0, CompassBitmap.Size - 1);
                if (Opaque(alpha, px, py))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The row the ring's own stroke crosses at the top of the dial.</summary>
    private static int RingTop()
    {
        const float Half = CompassBitmap.Size / 2f;
        const float Unit = CompassBitmap.TickOuter / CompassBitmap.GlyphMargin;
        return (int)(((1f - (CompassBitmap.RingRadius / Unit)) * Half) - 0.5f);
    }
}
