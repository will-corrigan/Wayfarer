using System.Numerics;

namespace Wayfarer.Core.Ui;

/// <summary>Draws Wayfarer's own emblem — the compass needle and ticked ring from
/// <c>images/icon.png</c> — as pixels, at the size the readout's crest slot draws it.
///
/// <para><b>Why the emblem is here at all.</b> The readout wears the game's Main Scenario Guide
/// plate, and that plate has a crest slot: the game puts the crimson meteor of the Scions in it.
/// Ours must not be the meteor — that mark means "main scenario", and the readout follows unlock
/// routes and hunting logs too — so the slot carries the plugin's own mark instead, which is what
/// stops the plate reading as a mislabelled game element.</para>
///
/// <para><b>It is the plugin's existing mark, not a new one.</b> The same compass needle inside the
/// same ticked gold ring the player has already seen on Wayfarer's entry in Dalamud's plugin
/// installer (<c>images/icon.png</c>, referenced as <c>IconUrl</c> in <c>repo.json</c>). Two
/// deliberate differences, both forced by where it is going:
/// <list type="bullet">
/// <item>the installer icon's dark navy rounded square is <b>dropped</b>. A crest sits <i>on</i> the
/// plate the way the game's own emblems do; a filled tile pasted on it would read as a
/// sticker.</item>
/// <item>the strokes are <b>thicker in proportion</b> than the 512-pixel original's. A ring drawn
/// four pixels wide at 512 is under half a pixel at 52 and simply vanishes; the ring, the ticks and
/// the hub are all sized against the crest slot rather than scaled down from the source.</item>
/// </list></para>
///
/// <para><b>Why it is generated rather than shipped as a file.</b> The same reasons as
/// <see cref="ArrowBitmap"/> and <see cref="CogBitmap"/>: art that loads can fail to load, and a
/// second copy of a mark drifts from the first. Computing it costs a few tens of thousands of pixels
/// once per session.</para>
///
/// <para><b>The one thing added to the mark.</b> A near-black outline under every gold stroke. The
/// installer icon does not need one because it sits on its own dark tile; the crest sits on cream
/// parchment, where unoutlined gold has nothing to stand against. That is the same treatment
/// <see cref="ArrowPalette.OutlineColor"/> already gives every generated glyph in this plugin.</para>
/// </summary>
public static class WayfarerBitmap
{
    /// <summary>The generated texture is square and this is its side, in pixels. Getting on for four
    /// times the 52 the crest slot draws it at, so the downscale does the anti-aliasing.</summary>
    public const int Size = 192;

    /// <summary>Bytes per generated image — <c>Size * Size * 4</c>, RGBA, straight (not
    /// premultiplied) alpha.</summary>
    public const int ByteCount = Size * Size * 4;

    // Geometry, in units of half the image, measured off images/icon.png at 512 and then adjusted
    // where the original's proportions do not survive being drawn at 52 pixels. The source's own
    // numbers are quoted beside each so the two can be compared.

    /// <summary>The ring's radius. Source: 210 of 256.</summary>
    private const float RingRadius = 0.82f;

    /// <summary>Half the ring's stroke. Source: about 2 of 256, which is a third of a pixel at the
    /// crest's size — this is the first of the two thickenings the class comment describes.</summary>
    private const float RingHalfWidth = 0.045f;

    /// <summary>How far in and out the four cardinal ticks reach across the ring. Source: roughly
    /// 195..228 of 256.</summary>
    private const float TickInner = 0.72f;

    /// <inheritdoc cref="TickInner"/>
    private const float TickOuter = 0.94f;

    /// <summary>Half a tick's width — the second thickening. Source: about 2 of 256.</summary>
    private const float TickHalfWidth = 0.05f;

    /// <summary>The needle's tip, above the waist. Source: 156 of 256.</summary>
    private const float NeedleApex = 0.64f;

    /// <summary>The needle's tail, below the waist. Source: 64 of 256 — the short end is what makes
    /// it a needle rather than a diamond.</summary>
    private const float NeedleTail = 0.30f;

    /// <summary>Half the needle's width at the waist. Source: 60 of 256.</summary>
    private const float NeedleHalfWidth = 0.215f;

    /// <summary>The pin the needle turns on. Source: about 20 of 256, nudged up because a two-pixel
    /// dot closes up entirely once the outline is drawn around it.</summary>
    private const float HubRadius = 0.075f;

    // Outline widths, in texture pixels: about one pixel each once the texture is drawn at 52.
    private const float RingOutline = 3.4f;
    private const float NeedleOutline = 3.6f;
    private const float HubOutline = 2.2f;
    private const float EdgeSoftness = 1.1f;

    private static readonly Vector3 OutlineColor = ArrowPalette.OutlineColor;

    /// <summary>The ring and its ticks, sampled off the source icon.</summary>
    private static readonly Vector3 RingGold = Rgb(232, 194, 74);

    /// <summary>The lit half of the needle — everything above the waist. Sampled off the source
    /// icon.</summary>
    private static readonly Vector3 NeedleLit = Rgb(239, 201, 76);

    /// <summary>The shaded half — everything below the waist.</summary>
    private static readonly Vector3 NeedleShaded = Rgb(201, 150, 46);

    /// <summary>The hub. The source icon draws it as its own dark navy background showing through
    /// the needle; with the background gone it has to be painted, so it is painted that same
    /// navy.</summary>
    private static readonly Vector3 HubNavy = Rgb(26, 26, 36);

    /// <summary>Renders the emblem as straight-alpha RGBA bytes, row-major from the top-left, ready
    /// for <c>ITextureProvider.CreateFromRaw</c>.</summary>
    public static byte[] Render()
    {
        var pixels = new byte[ByteCount];
        const float Half = Size / 2f;

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var point = new Vector2(((x + 0.5f) / Half) - 1f, ((y + 0.5f) / Half) - 1f);

                // Painter's order, back to front, exactly as the source icon stacks: the ring and
                // its ticks, the needle over them, the pin through the needle.
                var pixel = Vector4.Zero;
                Paint(ref pixel, RingDistance(point) * Half, RingOutline, RingGold);
                Paint(
                    ref pixel,
                    NeedleDistance(point) * Half,
                    NeedleOutline,
                    point.Y <= 0f ? NeedleLit : NeedleShaded);
                Paint(ref pixel, (point.Length() - HubRadius) * Half, HubOutline, HubNavy);

                if (pixel.W <= 0f)
                {
                    continue;
                }

                var offset = ((y * Size) + x) * 4;
                pixels[offset] = Channel(pixel.X);
                pixels[offset + 1] = Channel(pixel.Y);
                pixels[offset + 2] = Channel(pixel.Z);
                pixels[offset + 3] = Channel(pixel.W);
            }
        }

        return pixels;
    }

    private static Vector3 Rgb(byte r, byte g, byte b) => new(r / 255f, g / 255f, b / 255f);

    private static byte Channel(float value) => (byte)Math.Clamp((value * 255f) + 0.5f, 0f, 255f);

    /// <summary>Turns a signed distance in texture pixels (negative inside) into an anti-aliased
    /// coverage in 0..1. The same treatment <see cref="CogBitmap"/> uses, so every generated glyph
    /// softens its edge over the same distance.</summary>
    private static float Coverage(float distance) =>
        Math.Clamp(0.5f - (distance / EdgeSoftness), 0f, 1f);

    /// <summary>Composites one outlined shape over whatever has already been painted, in straight
    /// alpha.
    ///
    /// <para>Each layer is its own silhouette — the shape grown by its outline width — filled with a
    /// ramp from the outline colour to the fill colour across that width. Straight "over" blending
    /// rather than one distance field for the whole emblem, because the emblem is three stacked
    /// shapes and a single field cannot express "the needle is gold on top of the ring, with a dark
    /// pin on top of the needle".</para></summary>
    private static void Paint(ref Vector4 destination, float distance, float outline, Vector3 fill)
    {
        var alpha = Coverage(distance - outline);
        if (alpha <= 0f)
        {
            return;
        }

        var color = Vector3.Lerp(OutlineColor, fill, Coverage(distance));
        var outAlpha = alpha + (destination.W * (1f - alpha));
        if (outAlpha <= 0f)
        {
            destination = Vector4.Zero;
            return;
        }

        var blended =
            ((color * alpha) + (new Vector3(destination.X, destination.Y, destination.Z) * destination.W * (1f - alpha)))
            / outAlpha;

        destination = new Vector4(blended, outAlpha);
    }

    /// <summary>Signed distance to the ring and its four cardinal ticks together, negative inside,
    /// in units of half the image.</summary>
    private static float RingDistance(Vector2 point)
    {
        var radius = point.Length();
        var ring = Math.Abs(radius - RingRadius) - RingHalfWidth;
        return Math.Min(ring, TickDistance(point, radius));
    }

    /// <summary>Signed distance to whichever of the four cardinal ticks is nearest — a radial bar
    /// crossing the ring at north, east, south and west, exactly as the source icon draws
    /// them.</summary>
    private static float TickDistance(Vector2 point, float radius)
    {
        if (radius <= float.Epsilon)
        {
            return TickInner;
        }

        const float Sector = 0.25f;
        var turn = ((MathF.Atan2(point.Y, point.X) / MathF.Tau) + 1f) % 1f;

        // Angle to the NEAREST axis rather than to the sector's start, so all four ticks are one
        // description.
        var folded = (((turn + (Sector / 2f)) % Sector) - (Sector / 2f)) * MathF.Tau;
        var across = Math.Abs(radius * MathF.Sin(folded)) - TickHalfWidth;
        var along = Math.Max(TickInner - radius, radius - TickOuter);
        return Math.Max(across, along);
    }

    /// <summary>Signed distance to the needle: a long isosceles triangle above the waist and a short
    /// one below it, sharing the same base.
    ///
    /// <para>Each half is described by the line through its apex and the waist's corner, normalised
    /// so the result is a real distance rather than an implicit-function value. Exact on the
    /// needle's four edges; a close approximation at the two waist corners, which is the only place
    /// the two halves meet.</para></summary>
    private static float NeedleDistance(Vector2 point)
    {
        var across = Math.Abs(point.X);
        var length = point.Y <= 0f ? NeedleApex : NeedleTail;
        var along = Math.Abs(point.Y);

        return ((across / NeedleHalfWidth) + (along / length) - 1f)
            / MathF.Sqrt((1f / (NeedleHalfWidth * NeedleHalfWidth)) + (1f / (length * length)));
    }
}
