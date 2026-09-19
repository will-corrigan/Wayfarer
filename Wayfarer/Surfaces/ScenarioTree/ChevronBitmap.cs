using System.Numerics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The above/below mark beside the compass: two stacked chevrons, pointing up. Drawn
/// pointing up so "above" needs no rotation and "below" is half a turn. Drawn on
/// <see cref="GlyphCanvas"/>, which owns everything about turning a shape into pixels.</summary>
public static class ChevronBitmap
{
    private const int Strokes = 2;
    private const float OutlineWidth = 2f;

    /// <summary>Half the stroke's width, in glyph units.</summary>
    private const float StrokeHalfWidth = 0.075f;

    /// <summary>How far out the arms reach and how far the apex rises, in glyph units: rise half
    /// the reach, the same proportion as the game's own carets.</summary>
    private const float ArmReach = 0.80f;
    private const float ArmRise = 0.40f;

    /// <summary>The gap between the two chevrons, enough to survive being drawn at a dozen pixels.</summary>
    private const float Spacing = 0.70f;

    /// <summary>The whole texture, so the chevrons fill it.</summary>
    private const float GlyphUnit = 1f;

    /// <summary>The mark as straight-alpha RGBA bytes, row-major from the top-left.</summary>
    public static byte[] Render()
    {
        var apexes = Apexes();
        return GlyphCanvas.Render(
            GlyphUnit,
            OutlineWidth,
            point => StrokeDistance(point, apexes) - StrokeHalfWidth,
            (point, _) => Vector3.Lerp(GlyphCanvas.GoldTip, GlyphCanvas.GoldTail, Math.Clamp((point.Y + 1f) / 2f, 0f, 1f)));
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

    /// <summary>How far a point is from the nearest arm of the nearest chevron.</summary>
    private static float StrokeDistance(Vector2 point, float[] apexes)
    {
        var distance = float.MaxValue;
        foreach (var apexY in apexes)
        {
            var apex = new Vector2(0f, apexY);
            var left = new Vector2(-ArmReach, apexY + ArmRise);
            var right = new Vector2(ArmReach, apexY + ArmRise);
            distance = Math.Min(distance, Math.Min(GlyphCanvas.SegmentDistance(point, left, apex), GlyphCanvas.SegmentDistance(point, apex, right)));
        }

        return distance;
    }
}
