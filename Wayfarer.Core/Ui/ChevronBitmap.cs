using System.Numerics;

namespace Wayfarer.Core.Ui;

/// <summary>The above/below mark beside the compass: two stacked chevrons, pointing up, as
/// pixels. Drawn pointing up so "above" needs no rotation and "below" is half a turn. Generated
/// for the same reason the compass is: the game has no such art to borrow, and generated geometry
/// can be asserted in a test.</summary>
public static class ChevronBitmap
{
    /// <summary>The texture is square and this is its side, in pixels.</summary>
    public const int Size = 96;

    /// <summary>Bytes per image: RGBA, straight alpha.</summary>
    public const int ByteCount = Size * Size * 4;

    private const int Strokes = 2;
    private const float OutlineWidth = 2f;
    private const float EdgeSoftness = 1.1f;

    /// <summary>Half the stroke's width, in units of half the image.</summary>
    private const float StrokeHalfWidth = 0.075f;

    /// <summary>How far out the arms reach and how far the apex rises, in units of half the
    /// image: rise half the reach, the same proportion as the game's own carets.</summary>
    private const float ArmReach = 0.80f;
    private const float ArmRise = 0.40f;

    /// <summary>The gap between the two chevrons, enough to survive being drawn at a dozen pixels.</summary>
    private const float Spacing = 0.70f;

    private static readonly Vector3 GoldTip = new(255f / 255f, 242f / 255f, 194f / 255f);
    private static readonly Vector3 GoldTail = new(214f / 255f, 148f / 255f, 40f / 255f);
    private static readonly Vector3 OutlineColor = new(0.07f, 0.055f, 0.03f);

    /// <summary>The mark as straight-alpha RGBA bytes, row-major from the top-left.</summary>
    public static byte[] Render()
    {
        var apexes = Apexes();
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

                var fill = Coverage(distance);
                var gradient = Math.Clamp((point.Y + 1f) / 2f, 0f, 1f);
                var color = Vector3.Lerp(OutlineColor, Vector3.Lerp(GoldTip, GoldTail, gradient), fill);

                var offset = ((y * Size) + x) * 4;
                pixels[offset] = Channel(color.X);
                pixels[offset + 1] = Channel(color.Y);
                pixels[offset + 2] = Channel(color.Z);
                pixels[offset + 3] = Channel(silhouette);
            }
        }

        return pixels;
    }

    /// <summary>The alpha of the pixel at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static byte AlphaAt(byte[] pixels, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        return pixels[(((y * Size) + x) * 4) + 3];
    }

    /// <summary>Apex Y of each chevron, spread about the centre so the mark is balanced.</summary>
    private static float[] Apexes()
    {
        var apexes = new float[Strokes];
        var top = -(Spacing * (Strokes - 1) / 2f) - (ArmRise / 2f);
        for (var i = 0; i < Strokes; i++)
        {
            apexes[i] = top + (Spacing * i);
        }

        return apexes;
    }

    private static byte Channel(float value) => (byte)Math.Clamp(value * 255f, 0f, 255f);

    private static float Coverage(float distance) => Math.Clamp(0.5f - (distance / EdgeSoftness), 0f, 1f);

    private static float StrokeDistance(Vector2 point, float[] apexes)
    {
        var distance = float.MaxValue;
        foreach (var apexY in apexes)
        {
            var apex = new Vector2(0f, apexY);
            var left = new Vector2(-ArmReach, apexY + ArmRise);
            var right = new Vector2(ArmReach, apexY + ArmRise);
            distance = Math.Min(distance, Math.Min(SegmentDistance(point, left, apex), SegmentDistance(point, apex, right)));
        }

        return distance;
    }

    private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var edge = b - a;
        var lengthSquared = edge.LengthSquared();
        var t = lengthSquared <= 0f ? 0f : Math.Clamp(Vector2.Dot(point - a, edge) / lengthSquared, 0f, 1f);
        return (point - a - (edge * t)).Length();
    }
}
