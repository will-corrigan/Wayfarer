using System.Numerics;

namespace Wayfarer.Ui;

/// <summary>Draws the compass as pixels: a ring that never moves and a needle that turns inside
/// it. Two textures, because only one of them rotates.
///
/// <para>Generated rather than shipped as art because the game has no rotatable direction art to
/// borrow, and generated geometry can be asserted in a test instead of eyeballed. The shape is
/// the plugin's own installer icon: a gold hairline ring with four cardinal ticks, and a two-tone
/// gold needle on a dark hub. Every number is in dial units — half the texture's width, +Y down,
/// origin at the dial's centre.</para>
///
/// <para>The needle is centred on its own image so it spins in place, and points straight up
/// unrotated, so a needle angle of zero is straight ahead.</para></summary>
public static class CompassBitmap
{
    /// <summary>The ring itself, in dial units.</summary>
    public const float RingRadius = 0.804f;

    /// <summary>Where a cardinal tick starts and ends, in dial units. The ticks straddle the ring.</summary>
    public const float TickInner = 0.712f;

    /// <inheritdoc cref="TickInner"/>
    public const float TickOuter = 0.896f;

    /// <summary>How far the needle's point reaches, in dial units: just inside the ring.</summary>
    public const float NeedleFore = 0.78f;

    /// <summary>How far the needle's tail reaches, in dial units.</summary>
    public const float NeedleAft = 0.62f;

    /// <summary>Half the needle's width at its shoulders, on the dial's centre line.</summary>
    public const float NeedleHalfWidth = 0.28f;

    /// <summary>The dark dot the needle turns on, in dial units.</summary>
    public const float HubRadius = 0.075f;

    /// <summary>How much of each texture's half-extent its glyph reaches, leaving room for the
    /// dark outline. Each glyph fills its own texture to this, which is why the two are drawn at
    /// different sizes on screen: see <see cref="NeedleToRingSize"/>.</summary>
    public const float GlyphMargin = 0.92f;

    /// <summary>The needle's box as a fraction of the ring's box, so the two stay concentric and in
    /// proportion when each fills its own texture.</summary>
    public const float NeedleToRingSize = NeedleFore / TickOuter;

    private const float RingUnit = TickOuter / GlyphMargin;
    private const float NeedleUnit = NeedleFore / GlyphMargin;

    /// <summary>Half the ring's and ticks' stroke, in dial units: the thinnest hairline that
    /// survives the downscale.</summary>
    private const float StrokeHalfWidth = 0.03f;

    private const float OutlineWidth = 2.2f;

    /// <summary>Where between the two golds the ring, the needle's front and the needle's back sit.</summary>
    private const float RingBlend = 0.7f;
    private const float ForeBlend = 0.5f;
    private const float AftShade = 0.15f;

    /// <summary>The dark the hub is filled with: a hole in the needle, not an edge around it.</summary>
    private static readonly Vector3 HubColor = new(0.078f, 0.086f, 0.122f);

    /// <summary>The needle, in dial units: the point, the two shoulders, the tail.</summary>
    private static readonly Vector2[] Needle =
    [
        new(0f, -NeedleFore),
        new(NeedleHalfWidth, 0f),
        new(0f, NeedleAft),
        new(-NeedleHalfWidth, 0f),
    ];

    /// <summary>The ring and its four ticks, and nothing else, as straight-alpha RGBA bytes,
    /// row-major from the top-left.</summary>
    public static byte[] RenderRing()
    {
        var gold = Vector3.Lerp(GlyphCanvas.GoldTip, GlyphCanvas.GoldTail, RingBlend);
        return GlyphCanvas.Render(RingUnit, OutlineWidth, RingDistance, (_, _) => gold);
    }

    /// <summary>The needle alone, pointing straight up, centred on its own hub.</summary>
    public static byte[] RenderNeedle()
    {
        var fore = Vector3.Lerp(GlyphCanvas.GoldTip, GlyphCanvas.GoldTail, ForeBlend);
        var aft = Vector3.Lerp(GlyphCanvas.GoldTail, GlyphCanvas.OutlineColor, AftShade);
        return GlyphCanvas.Render(NeedleUnit, OutlineWidth, SignedDistance, (point, perUnit) => NeedleBody(point, perUnit, fore, aft));
    }

    /// <summary>The needle's own colours at a point: lighter ahead of the shoulders than behind
    /// them, and the dark hub punched through the middle.</summary>
    private static Vector3 NeedleBody(Vector2 point, float perUnit, Vector3 fore, Vector3 aft)
    {
        var half = GlyphCanvas.Coverage(point.Y * perUnit);
        var hub = GlyphCanvas.Coverage((point.Length() - HubRadius) * perUnit);
        return Vector3.Lerp(Vector3.Lerp(aft, fore, half), HubColor, hub);
    }

    private static float RingDistance(Vector2 point)
    {
        var circle = Math.Abs(point.Length() - RingRadius);
        return Math.Min(circle, TickDistance(point)) - StrokeHalfWidth;
    }

    private static float TickDistance(Vector2 point)
    {
        var distance = float.MaxValue;
        for (var i = 0; i < 4; i++)
        {
            var direction = i switch
            {
                0 => new Vector2(0f, -1f),
                1 => new Vector2(1f, 0f),
                2 => new Vector2(0f, 1f),
                _ => new Vector2(-1f, 0f),
            };

            distance = Math.Min(distance, GlyphCanvas.SegmentDistance(point, direction * TickInner, direction * TickOuter));
        }

        return distance;
    }

    private static float SignedDistance(Vector2 point)
    {
        var distance = float.MaxValue;
        var inside = false;

        for (var i = 0; i < Needle.Length; i++)
        {
            var a = Needle[i];
            var b = Needle[(i + 1) % Needle.Length];
            distance = Math.Min(distance, GlyphCanvas.SegmentDistance(point, a, b));

            if ((a.Y > point.Y) != (b.Y > point.Y)
                && point.X < (((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X))
            {
                inside = !inside;
            }
        }

        return inside ? -distance : distance;
    }
}
