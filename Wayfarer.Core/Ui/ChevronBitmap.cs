using System.Numerics;

namespace Wayfarer.Core.Ui;

/// <summary>Draws a stacked pair of open chevrons, as pixels — the readout's <b>elevation</b> mark,
/// and the small caret that opens the "what am I following" list.
///
/// <para><b>Why this exists.</b> The elevation mark used to be the direction arrow's own artwork,
/// drawn small and turned through half a turn for "below". Two different meanings rendered as the
/// same shape: the player read it as a second direction to travel in and said so — "the above you
/// thing is the same arrow as the compass which is a bit confusing". A mark that means "the target
/// is on a different level" must not be shaped like the mark that means "go that way".</para>
///
/// <para><b>Why a double chevron rather than the game's own elevation icon.</b> Matching the game
/// beats inventing, and the game does hang an elevation mark off a minimap marker on another floor —
/// but this codebase's standing rule is that an icon id is validated against the running client
/// before it is drawn (see <c>NamePlateMarkers</c>), and no id for that mark has been confirmed
/// against a live install. Shipping an unverified id would trade a confusing glyph for a missing
/// one. A generated glyph has no id to be wrong, inherits the player's chosen arrow colour for
/// free, and is verifiable here rather than in the field.</para>
///
/// <para><b>Why the shape reads.</b> The direction arrow is a single filled mass with a notched
/// tail. This is two thin open strokes with a gap between them. The two silhouettes have nothing in
/// common at any size, which is the property that has to survive a television — the distinction is
/// carried by shape, so it holds in every colour variant, including the ones where the arrow and the
/// chevron are the same hue.</para>
///
/// <para>Like the arrow, it points <b>straight up unrotated</b> and is centred in its own image, so
/// "below" is exactly half a turn and it pivots in place.</para></summary>
public static class ChevronBitmap
{
    /// <summary>The generated texture is square and this is its side, in pixels. Matches
    /// <see cref="ArrowBitmap.Size"/> so both glyphs downscale from the same resolution.</summary>
    public const int Size = 96;

    /// <summary>Bytes per generated image — <c>Size * Size * 4</c>, RGBA, straight alpha.</summary>
    public const int ByteCount = Size * Size * 4;

    private const float OutlineWidth = 2f;
    private const float EdgeSoftness = 1.1f;

    /// <summary>Half the stroke's thickness, in units of half the image. Thin on purpose, and the
    /// number that matters most: the whole distinction from the arrow is that this is line-work
    /// with air in it, and a stroke half again this thick closes the gap between the two chevrons
    /// and turns the mark back into a solid blob.</summary>
    private const float StrokeHalfWidth = 0.075f;

    /// <summary>How far out the chevron's arms reach, and how far up its apex rises, in units of
    /// half the image. A shallow chevron reads as a line; this ratio (rise half the reach, so 27
    /// degrees off horizontal) is the same proportion the game's own carets use.</summary>
    private const float ArmReach = 0.80f;

    private const float ArmRise = 0.40f;

    /// <summary>Renders <paramref name="strokes"/> stacked chevrons for one colour variant as
    /// straight-alpha RGBA bytes, row-major from the top-left, ready for
    /// <c>ITextureProvider.CreateFromRaw</c>.</summary>
    /// <param name="variant">Which arrow colour to draw it in.</param>
    /// <param name="strokes">Two for the elevation mark, one for a plain caret.</param>
    public static byte[] Render(ArrowIconVariant variant, int strokes = 2)
    {
        var (tip, tail) = ArrowPalette.For(variant);
        var apexes = Apexes(strokes);
        var pixels = new byte[ByteCount];
        const float Half = Size / 2f;

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var point = new Vector2(((x + 0.5f) / Half) - 1f, ((y + 0.5f) / Half) - 1f);
                var distance = (StrokeDistance(point, apexes) - StrokeHalfWidth) * Half;

                var silhouette = Coverage(distance - OutlineWidth);
                if (silhouette <= 0f)
                {
                    continue;
                }

                // 1 inside the stroke proper, 0 out in the outline ring, soft across the boundary.
                var fill = Coverage(distance);
                var gradient = Math.Clamp((point.Y + 1f) / 2f, 0f, 1f);
                var color = Vector3.Lerp(ArrowPalette.OutlineColor, Vector3.Lerp(tip, tail, gradient), fill);

                var offset = ((y * Size) + x) * 4;
                pixels[offset] = Channel(color.X);
                pixels[offset + 1] = Channel(color.Y);
                pixels[offset + 2] = Channel(color.Z);
                pixels[offset + 3] = Channel(silhouette);
            }
        }

        return pixels;
    }

    /// <summary>Apex Y for each chevron, spread about the image centre so the whole mark is
    /// balanced whatever the stroke count — one chevron sits centred, two straddle the middle.</summary>
    private static float[] Apexes(int strokes)
    {
        var count = Math.Max(strokes, 1);
        if (count == 1)
        {
            return [-(ArmRise / 2f)];
        }

        // Enough separation that the gap between the strokes survives being drawn at a dozen pixels,
        // and still inside the texture once the outline is added.
        const float Spacing = 0.70f;
        var apexes = new float[count];
        var top = -(Spacing * (count - 1) / 2f) - (ArmRise / 2f);
        for (var i = 0; i < count; i++)
        {
            apexes[i] = top + (Spacing * i);
        }

        return apexes;
    }

    private static byte Channel(float value) => (byte)Math.Clamp(value * 255f, 0f, 255f);

    private static float Coverage(float distance) =>
        Math.Clamp(0.5f - (distance / EdgeSoftness), 0f, 1f);

    /// <summary>Distance from a point to the nearest chevron's centre-line. Each chevron is two
    /// segments meeting at an apex; the stroke is everything within
    /// <see cref="StrokeHalfWidth"/> of that line.</summary>
    private static float StrokeDistance(Vector2 point, float[] apexes)
    {
        var distance = float.MaxValue;
        foreach (var apexY in apexes)
        {
            var apex = new Vector2(0f, apexY);
            var left = new Vector2(-ArmReach, apexY + ArmRise);
            var right = new Vector2(ArmReach, apexY + ArmRise);

            distance = Math.Min(distance, SegmentDistance(point, left, apex));
            distance = Math.Min(distance, SegmentDistance(point, apex, right));
        }

        return distance;
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
