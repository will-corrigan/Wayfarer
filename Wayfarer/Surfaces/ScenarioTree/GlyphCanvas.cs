using System.Numerics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The square texture every mark Wayfarer generates is drawn on, and the one way of
/// drawing on it. A mark is described by its shape, as a distance from the nearest edge of it, and
/// by its colour at a point; the canvas walks the pixels, works out how much of each the shape
/// covers, and lays the mark over its own dark outline.
///
/// <para>Marks are generated rather than shipped as art because the game has none to borrow, and
/// because generated geometry can be asserted in a test instead of eyeballed. Positions are in
/// glyph units: half the texture's width, +Y down, origin at the centre.</para></summary>
public static class GlyphCanvas
{
    /// <summary>Every texture is square and this is its side, in pixels.</summary>
    public const int Size = 96;

    /// <summary>Bytes per image: RGBA, straight alpha.</summary>
    public const int ByteCount = Size * Size * 4;

    /// <summary>How far a pixel's coverage fades across the edge of a shape. Softer than a pixel,
    /// or a mark drawn at a dozen pixels looks ragged.</summary>
    private const float EdgeSoftness = 1.1f;

    private const float Half = Size / 2f;

    /// <summary>The game's warm HUD gold, bright at the tip and saturated at the tail.</summary>
    public static Vector3 GoldTip { get; } = Rgb(255, 242, 194);

    /// <inheritdoc cref="GoldTip"/>
    public static Vector3 GoldTail { get; } = Rgb(214, 148, 40);

    /// <summary>The red a compass needle's pointing half is painted, the world over. Gold on gold
    /// says which way the needle lies but not which end of it is the front.</summary>
    public static Vector3 NeedleRed { get; } = Rgb(198, 62, 48);

    /// <summary>The near-black every mark is outlined in, which is what keeps gold readable
    /// against bright terrain.</summary>
    public static Vector3 OutlineColor { get; } = new(0.07f, 0.055f, 0.03f);

    /// <summary>Draws one mark and hands back its straight-alpha RGBA bytes, row-major from the
    /// top-left.</summary>
    /// <param name="unit">How far out from the centre the texture's edge is, in glyph units: 1
    /// when the mark is described against the texture itself, more when it is described against
    /// something larger that it has to fit inside.</param>
    /// <param name="outlineWidth">How far the dark outline stands off the mark, in pixels.</param>
    /// <param name="shape">How far a point is from the nearest edge of the mark, in glyph units,
    /// negative inside it.</param>
    /// <param name="body">The mark's own colour at a point, outline aside. The second argument is
    /// how many pixels there are to a glyph unit, for a shape that shades by distance.</param>
    public static byte[] Render(float unit, float outlineWidth, Func<Vector2, float> shape, Func<Vector2, float, Vector3> body)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(body);

        var pixels = new byte[ByteCount];
        var perUnit = Half / unit;

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var point = new Vector2(((x + 0.5f) / Half) - 1f, ((y + 0.5f) / Half) - 1f) * unit;
                var distance = shape(point) * perUnit;

                var silhouette = Coverage(distance - outlineWidth);
                if (silhouette <= 0f)
                {
                    continue;
                }

                var color = Vector3.Lerp(OutlineColor, body(point, perUnit), Coverage(distance));
                Write(pixels, x, y, color, silhouette);
            }
        }

        return pixels;
    }

    /// <summary>The alpha of the pixel at (<paramref name="x"/>, <paramref name="y"/>) in an image
    /// this canvas rendered.</summary>
    public static byte AlphaAt(byte[] pixels, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        return pixels[(((y * Size) + x) * 4) + 3];
    }

    /// <summary>How much of a pixel a shape covers at this distance from its edge.</summary>
    public static float Coverage(float distance) => Math.Clamp(0.5f - (distance / EdgeSoftness), 0f, 1f);

    /// <summary>How far a point is from a line between two others.</summary>
    public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var edge = b - a;
        var lengthSquared = edge.LengthSquared();
        var t = lengthSquared <= 0f ? 0f : Math.Clamp(Vector2.Dot(point - a, edge) / lengthSquared, 0f, 1f);
        return (point - a - (edge * t)).Length();
    }

    /// <summary>A colour written the way the game's own art is: three bytes.</summary>
    private static Vector3 Rgb(byte r, byte g, byte b) => new(r / 255f, g / 255f, b / 255f);

    private static void Write(byte[] pixels, int x, int y, Vector3 color, float alpha)
    {
        var offset = ((y * Size) + x) * 4;
        pixels[offset] = Channel(color.X);
        pixels[offset + 1] = Channel(color.Y);
        pixels[offset + 2] = Channel(color.Z);
        pixels[offset + 3] = Channel(alpha);
    }

    private static byte Channel(float value) => (byte)Math.Clamp(value * 255f, 0f, 255f);
}
