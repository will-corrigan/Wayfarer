using System.Numerics;

namespace Wayfarer.Core.Ui;

/// <summary>Draws the readout's direction arrow, as pixels.
///
/// <b>Why the plugin draws its own arrow rather than using the game's.</b> The readout used to cut a
/// 24x24 part out of <c>ui/uld/NaviMap.tex</c>. Two things were wrong with that, and the second is
/// why this file exists rather than a corrected pair of texture coordinates:
///
/// <list type="number">
/// <item><description><b>The crop never reached the screen.</b> The image node had
/// <c>FitTexture</c> set, which is KamiToolKit's shorthand for the <c>AutoFit</c> image-node flag —
/// documented there as making "the loaded texture fit itself to the size of the node". The
/// <i>texture</i>, not the part. So the node drew the entire 448x212 minimap sheet squashed into 34
/// pixels: two ornate compass rings, the cardinal letters and every caret at once. That is exactly
/// what the player photographed and described as "an ornate scrollwork bar" and "artifacts of many
/// items".</description></item>
/// <item><description><b>The art is not an arrow.</b> With the sheet extracted and looked at, the
/// five parts really are there at the coordinates the visual spec listed — but they are the
/// minimap's off-screen marker <i>carets</i>: wide, shallow, hollow "hats", sitting off-centre in
/// their cells so that rotating about the cell centre makes them wobble. They read as a chevron on
/// a compass rim, which is what they are for, and not as "run this way".</description></item>
/// </list>
///
/// So the arrow is generated: one texture whose entire content <i>is</i> the arrow, which makes
/// <c>AutoFit</c> the correct flag instead of a bug, removes texture coordinates from the problem
/// altogether, and guarantees the two properties the readout actually depends on — the arrow is
/// centred in its own image, so it spins in place, and it points <b>straight up unrotated</b>, so
/// <c>NavMath.ArrowAngle</c>'s "0 = straight ahead" needs no correcting offset anywhere.
///
/// The shape is the game's own HUD vocabulary rather than a generic triangle: a tall isosceles head
/// with a notched tail, filled with a vertical gold gradient and ringed in a near-black outline, the
/// same fill-plus-dark-edge treatment the game's own gold HUD glyphs use so it reads against bright
/// terrain.
///
/// <para><b>What draws it now.</b> Nothing on screen: the readout's direction indicator is a compass
/// — a static ring with a needle turning in it, <see cref="CompassBitmap"/> — and this is what it
/// replaced. The arrow is kept because it is the <b>yardstick</b> the compass is sized against, and
/// that is a claim about pixels rather than about intent: <see cref="GlyphHeightFraction"/> is read
/// straight out of the geometry below, and the test that proves the needle is no smaller than the
/// arrow measures the ink in this image against the ink in the needle's.</para></summary>
public static class ArrowBitmap
{
    /// <summary>The generated texture is square and this is its side, in pixels. Larger than the
    /// ~34 pixels the readout draws it at, so the downscale does the anti-aliasing and the arrow
    /// stays clean when the player turns the arrow-size setting up.</summary>
    public const int Size = 96;

    /// <summary>Bytes per generated image — <c>Size * Size * 4</c>, RGBA, straight (not
    /// premultiplied) alpha.</summary>
    public const int ByteCount = Size * Size * 4;

    /// <summary>How much of the image's height the arrow's own ink fills — its point down to the ends
    /// of its tail, as a fraction of <see cref="Size"/>.
    ///
    /// <para>Public because it is the yardstick anything that replaces this arrow has to measure up
    /// to. The readout draws its direction indicator in a fixed box beside a line, so a glyph that
    /// fills less of its own texture is a glyph that is smaller on screen at the same setting — which
    /// for a player who has turned the arrow-size setting up to read the readout across a room is a
    /// regression, not a redesign. <see cref="CompassBitmap.NeedleScale"/> is derived from this number
    /// rather than guessed against it.</para></summary>
    public const float GlyphHeightFraction = (TailY - TipY) / 2f;

    private const float OutlineWidth = 2.6f;
    private const float EdgeSoftness = 1.1f;

    // The arrow, in units of half the image, with +Y down and the origin at the image's centre.
    // Deliberately a hair inside the edges so the outline below still fits inside the texture.
    private const float TipY = -0.88f;
    private const float TailY = 0.70f;
    private const float NotchY = 0.28f;
    private const float ShoulderX = 0.78f;

    private static readonly Vector2[] Outline =
    [
        new(0f, TipY),
        new(ShoulderX, TailY),
        new(0f, NotchY),
        new(-ShoulderX, TailY),
    ];

    /// <summary>Renders the arrow for one colour variant as straight-alpha RGBA bytes, row-major
    /// from the top-left, ready for <c>ITextureProvider.CreateFromRaw</c>.</summary>
    public static byte[] Render(ArrowIconVariant variant)
    {
        var (tip, tail) = ArrowPalette.For(variant);
        var pixels = new byte[ByteCount];
        const float Half = Size / 2f;

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                // Pixel centres, in the same units as Outline.
                var point = new Vector2(((x + 0.5f) / Half) - 1f, ((y + 0.5f) / Half) - 1f);
                var distance = SignedDistance(point) * Half;

                var silhouette = Coverage(distance - OutlineWidth);
                var offset = ((y * Size) + x) * 4;
                if (silhouette <= 0f)
                {
                    continue;
                }

                // 1 inside the arrow proper, 0 out in the outline ring, soft across the boundary.
                var fill = Coverage(distance);
                var gradient = Math.Clamp((point.Y - TipY) / (TailY - TipY), 0f, 1f);
                var color = Vector3.Lerp(ArrowPalette.OutlineColor, Vector3.Lerp(tip, tail, gradient), fill);

                pixels[offset] = Channel(color.X);
                pixels[offset + 1] = Channel(color.Y);
                pixels[offset + 2] = Channel(color.Z);
                pixels[offset + 3] = Channel(silhouette);
            }
        }

        return pixels;
    }

    private static byte Channel(float value) => (byte)Math.Clamp(value * 255f, 0f, 255f);

    /// <summary>Turns a signed distance (negative inside) into an anti-aliased coverage in 0..1.</summary>
    private static float Coverage(float distance) =>
        Math.Clamp(0.5f - (distance / EdgeSoftness), 0f, 1f);

    /// <summary>Signed distance from <paramref name="point"/> to the arrow outline, negative inside.
    /// Straightforward point-to-polygon distance plus a crossing-number inside test — the polygon has
    /// four vertices, and this runs once per pixel at build time, not per frame.</summary>
    private static float SignedDistance(Vector2 point)
    {
        var distance = float.MaxValue;
        var inside = false;

        for (var i = 0; i < Outline.Length; i++)
        {
            var a = Outline[i];
            var b = Outline[(i + 1) % Outline.Length];
            distance = Math.Min(distance, SegmentDistance(point, a, b));

            if ((a.Y > point.Y) != (b.Y > point.Y)
                && point.X < (((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X))
            {
                inside = !inside;
            }
        }

        return inside ? -distance : distance;
    }

    private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var edge = b - a;
        var lengthSquared = edge.LengthSquared();
        var t = lengthSquared <= 0f
            ? 0f
            : Math.Clamp(Vector2.Dot(point - a, edge) / lengthSquared, 0f, 1f);
        return (point - a - (edge * t)).Length();
    }
}
