using System.Numerics;

namespace Wayfarer.Core.Ui;

/// <summary>Draws the readout's direction indicator, as pixels: a <b>ring that never moves</b> and a
/// <b>needle that turns inside it</b>. Two textures, because only one of them rotates.
///
/// <para><b>Why it is generated rather than shipped as art.</b> The same reason
/// <see cref="ArrowBitmap"/> is, and its own summary is the long version: the game has no rotatable
/// direction art to borrow — the nearest candidates are the minimap's off-centre off-screen carets,
/// which wobble when they are rotated about their cell's centre. Generating it also means nothing
/// here is a redistribution of somebody else's artwork, and the geometry can be asserted in a test
/// instead of eyeballed in a paint program.</para>
///
/// <para><b>What it draws, and where the shape comes from.</b> The plugin's own installer icon: a
/// plain gold hairline ring with four tick marks at the cardinals, and a two-tone gold needle on a
/// dark hub. Nothing is added to it — no bezel, no glow, no gradient — because the icon's needle is
/// hard-edged two-tone geometry, so this is a redraw of that mark rather than an approximation of
/// it. Every number below is in <b>dial units</b>: half the icon's own width, +Y down, origin at the
/// dial's centre, which is the same convention <see cref="ArrowBitmap"/> states for the arrow.</para>
///
/// <para><b>The two properties the readout depends on, kept exactly as the arrow kept them.</b> The
/// needle is centred in its own image — the hub sits on the image's centre — so it spins in place
/// rather than orbiting, and it points <b>straight up unrotated</b>, so <c>NavMath.ArrowAngle</c>'s
/// "0 = straight ahead" still needs no correcting offset anywhere. The ring is drawn unrotated and
/// its rotation is never written at all.</para>
///
/// <para><b>The one deliberate departure from the icon: the needle is longer.</b> The icon's needle
/// runs 0.61 forward and 0.25 aft, because on an icon the dial is the subject and the needle sits
/// well inside it. A dial filling the box the arrow used to have, with that needle inside it, puts
/// the pointer at about <b>half</b> the arrow's height — 21 drawn pixels against 40 at the arrow-size
/// setting's maximum — and that setting exists precisely because the arrow was hard to read across a
/// room, so shrinking the pointer would be the one change nobody asked for. Two things fix it and
/// both are needed: the ring and the needle are separate images, each filling its own texture and
/// drawn at its own size (see <see cref="RingScale"/>), and the needle is stretched to
/// <see cref="NeedleFore"/> forward — just inside the ring — and <see cref="NeedleAft"/> aft, which
/// is what a compass needle actually looks like: a balanced lozenge that nearly spans its
/// dial.</para></summary>
public static class CompassBitmap
{
    /// <summary>Each generated texture is square and this is its side, in pixels. The arrow's, so
    /// every generated glyph on the readout downscales from the same resolution.</summary>
    public const int Size = ArrowBitmap.Size;

    /// <summary>Bytes per generated image — <c>Size * Size * 4</c>, RGBA, straight (not
    /// premultiplied) alpha. The arrow's convention, unchanged.</summary>
    public const int ByteCount = Size * Size * 4;

    /// <summary>The ring itself, in dial units — measured off the plugin icon.</summary>
    public const float RingRadius = 0.804f;

    /// <summary>How far in a cardinal tick starts, in dial units. The ticks straddle the ring rather
    /// than sitting inside or outside it, which is what the icon does.</summary>
    public const float TickInner = 0.712f;

    /// <inheritdoc cref="TickInner"/>
    public const float TickOuter = 0.896f;

    /// <summary>How far the needle's pointing end reaches, in dial units — just inside
    /// <see cref="RingRadius"/>, so the needle clears the ring at every turn without touching
    /// it.</summary>
    public const float NeedleFore = 0.78f;

    /// <summary>How far the needle's tail reaches, in dial units. Longer than the icon's 0.25 on
    /// purpose: see the type's own note, and <see cref="RingScale"/> for why nothing shorter can hold
    /// the pointer's height.</summary>
    public const float NeedleAft = 0.62f;

    /// <summary>Half the needle's width at its shoulders, which are on the dial's own centre line.
    /// The icon's 0.214 carried up with the needle's forward reach, so the needle keeps the icon's
    /// proportions rather than becoming a spike.</summary>
    public const float NeedleHalfWidth = 0.28f;

    /// <summary>The dark dot the needle turns on, in dial units. It is what makes the pivot visible,
    /// which is what makes a needle read as a needle rather than as an arrow that happens to be in a
    /// circle.</summary>
    public const float HubRadius = 0.075f;

    /// <summary>How much of each texture's half-extent its own glyph is allowed to reach, leaving the
    /// rest for the dark outline every generated glyph here wears. The ring and the needle each fill
    /// their own texture to this, which is why the two are drawn at different sizes on screen — see
    /// <see cref="RingScale"/>.
    ///
    /// <para>Not any tighter than this: at 0.94 the dark outline around the four cardinal ticks ran
    /// about a pixel off the edge of the ring's image and was cut there, which is a hairline losing
    /// its dark edge at exactly the four points a compass is read by. At 0.92 the ticks and their
    /// outline both land inside.</para></summary>
    public const float GlyphMargin = 0.92f;

    /// <summary>How much taller than the arrow it replaces the needle is drawn. Not parity but a
    /// twentieth over it, because the ring puts competing ink around the needle that the bare arrow
    /// never had.</summary>
    public const float LegibilityMargin = 1.05f;

    /// <summary>How much of the needle texture's height the needle's own ink fills — the same
    /// measurement <see cref="ArrowBitmap.GlyphHeightFraction"/> is of the arrow, so the two are
    /// directly comparable.</summary>
    public const float NeedleHeightFraction = (NeedleFore + NeedleAft) * GlyphMargin / (2f * NeedleFore);

    /// <summary>What the needle's box is worth against the box the arrow used to be drawn in.
    /// Derived, not chosen: it is exactly what makes the needle's ink
    /// <see cref="LegibilityMargin"/> times the arrow's ink in real pixels at whatever the player's
    /// arrow-size setting is.</summary>
    public const float NeedleScale = LegibilityMargin * ArrowBitmap.GlyphHeightFraction / NeedleHeightFraction;

    /// <summary>What the ring's box is worth against the same yardstick. The ring is drawn larger
    /// than the needle by exactly the ratio of what each glyph reaches in dial units, which is what
    /// keeps the two concentric and in the icon's own proportion while each still fills its own
    /// texture.
    ///
    /// <para><b>This is the number that decided the needle's length.</b> Substituting the two
    /// definitions above collapses to
    /// <c>2 * ArrowBitmap.GlyphHeightFraction * LegibilityMargin * TickOuter</c> over
    /// <c>GlyphMargin * (NeedleFore + NeedleAft)</c> — that is, once the needle has to hold a given
    /// height in real pixels, the whole element's drawn width depends on the needle's <i>total
    /// length</i> and on nothing else about the dial. The readout has a hard horizontal budget for
    /// this element: the medallion gutter plus the empty margin to its left, and not one pixel of the
    /// words to its right (see <c>ReadoutBodyLayout.Arrow</c>). At the maximum arrow-size setting a
    /// needle of the icon's own proportions would need an element about a quarter wider than that
    /// budget — 70 pixels against 57 — so the choice was a longer needle or a smaller pointer. A
    /// longer needle costs proportion; a smaller pointer costs the player the thing the setting is
    /// for.</para></summary>
    public const float RingScale = NeedleScale * TickOuter / NeedleFore;

    /// <summary>Dial units per texture half-unit, per texture. Everything in the ring's image is
    /// measured against the ticks' reach and everything in the needle's against the needle's, which
    /// is what "each glyph fills its own texture" means in arithmetic.</summary>
    private const float RingUnit = TickOuter / GlyphMargin;

    /// <inheritdoc cref="RingUnit"/>
    private const float NeedleUnit = NeedleFore / GlyphMargin;

    /// <summary>Half the width of the ring's and the ticks' stroke, in dial units.
    ///
    /// <para><b>A hairline by eye rather than to scale.</b> The icon's ring is about three pixels of
    /// 512, which at the size the readout draws this element would be a third of a pixel — nothing at
    /// all. This is the thinnest stroke that survives the downscale: a little over a pixel of ink
    /// with the dark outline either side of it, which is the same fill-plus-dark-edge treatment the
    /// arrow uses and the reason either reads against bright terrain.</para></summary>
    private const float StrokeHalfWidth = 0.03f;

    private const float OutlineWidth = 2.2f;
    private const float EdgeSoftness = 1.1f;

    /// <summary>Where between the palette's two ends the ring's gold sits. Chosen so the default
    /// amber lands on the icon's own ring gold; every other variant follows the player's chosen
    /// colour, because a ring that stayed gold around a blue needle would be a colour setting that
    /// only half works.</summary>
    private const float RingBlend = 0.7f;

    /// <inheritdoc cref="RingBlend"/>
    private const float ForeBlend = 0.5f;

    /// <summary>How far the needle's aft half is taken down towards the outline's near-black. The
    /// icon's needle is one gold in front of the shoulders and a deeper one behind them; this is that
    /// second gold, derived from the palette rather than written down, so it deepens whichever colour
    /// the player picked.</summary>
    private const float AftShade = 0.15f;

    /// <summary>The dark the hub is filled with — the icon's own background, and deliberately not the
    /// outline's warm near-black: the hub is a hole in the needle, not an edge around it.</summary>
    private static readonly Vector3 HubColor = new(0.078f, 0.086f, 0.122f);

    /// <summary>The needle, in dial units: the pointing end, the two shoulders on the dial's centre
    /// line, and the tail. The origin is the hub, which is the image's centre, which is what makes
    /// the needle spin in place.</summary>
    private static readonly Vector2[] Needle =
    [
        new(0f, -NeedleFore),
        new(NeedleHalfWidth, 0f),
        new(0f, NeedleAft),
        new(-NeedleHalfWidth, 0f),
    ];

    /// <summary>Renders the static ring — the hairline circle and its four cardinal ticks, and
    /// nothing else — as straight-alpha RGBA bytes, row-major from the top-left, ready for
    /// <c>ITextureProvider.CreateFromRaw</c>.
    ///
    /// <para>There is no needle anywhere in this image, and that is the whole point of there being
    /// two of them: the node holding this one is never given a rotation, so the dial cannot drift by
    /// a fraction of a degree a frame the way a single rotating texture with a baked ring
    /// would.</para></summary>
    public static byte[] RenderRing(ArrowIconVariant variant)
    {
        var (tip, tail) = ArrowPalette.For(variant);
        var gold = Vector3.Lerp(tip, tail, RingBlend);
        var pixels = new byte[ByteCount];
        const float Half = Size / 2f;
        const float PerUnit = Half / RingUnit;

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                // Pixel centres, in dial units.
                var point = new Vector2(((x + 0.5f) / Half) - 1f, ((y + 0.5f) / Half) - 1f) * RingUnit;
                var distance = RingDistance(point) * PerUnit;

                var silhouette = Coverage(distance - OutlineWidth);
                if (silhouette <= 0f)
                {
                    continue;
                }

                // 1 inside the stroke proper, 0 out in the outline around it, soft across the edge.
                var fill = Coverage(distance);
                var color = Vector3.Lerp(ArrowPalette.OutlineColor, gold, fill);
                Write(pixels, x, y, color, silhouette);
            }
        }

        return pixels;
    }

    /// <summary>Renders the needle alone — no ring, no ticks — in the player's chosen arrow colour,
    /// pointing straight up, centred on its own hub.</summary>
    public static byte[] RenderNeedle(ArrowIconVariant variant)
    {
        var (tip, tail) = ArrowPalette.For(variant);
        var fore = Vector3.Lerp(tip, tail, ForeBlend);
        var aft = Vector3.Lerp(tail, ArrowPalette.OutlineColor, AftShade);
        var pixels = new byte[ByteCount];
        const float Half = Size / 2f;
        const float PerUnit = Half / NeedleUnit;

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var point = new Vector2(((x + 0.5f) / Half) - 1f, ((y + 0.5f) / Half) - 1f) * NeedleUnit;
                var distance = SignedDistance(point) * PerUnit;

                var silhouette = Coverage(distance - OutlineWidth);
                if (silhouette <= 0f)
                {
                    continue;
                }

                var fill = Coverage(distance);

                // The two golds meet at the shoulders, hard-edged as the icon has them, softened
                // across a single pixel so the seam does not stagger as the needle turns.
                var half = Coverage(point.Y * PerUnit);
                var hub = Coverage((point.Length() - HubRadius) * PerUnit);
                var body = Vector3.Lerp(Vector3.Lerp(aft, fore, half), HubColor, hub);
                var color = Vector3.Lerp(ArrowPalette.OutlineColor, body, fill);
                Write(pixels, x, y, color, silhouette);
            }
        }

        return pixels;
    }

    private static void Write(byte[] pixels, int x, int y, Vector3 color, float alpha)
    {
        var offset = ((y * Size) + x) * 4;
        pixels[offset] = Channel(color.X);
        pixels[offset + 1] = Channel(color.Y);
        pixels[offset + 2] = Channel(color.Z);
        pixels[offset + 3] = Channel(alpha);
    }

    private static byte Channel(float value) => (byte)Math.Clamp(value * 255f, 0f, 255f);

    /// <summary>Turns a signed distance (negative inside) into an anti-aliased coverage in 0..1.</summary>
    private static float Coverage(float distance) =>
        Math.Clamp(0.5f - (distance / EdgeSoftness), 0f, 1f);

    /// <summary>Signed distance from <paramref name="point"/> to the ring's ink, negative inside it —
    /// the circle's stroke and the four ticks, whichever is nearer.</summary>
    private static float RingDistance(Vector2 point)
    {
        var circle = Math.Abs(point.Length() - RingRadius);
        return Math.Min(circle, TickDistance(point)) - StrokeHalfWidth;
    }

    /// <summary>Distance to the nearest cardinal tick's centre line. Four radial segments, at the top,
    /// the bottom and the two sides, each running from <see cref="TickInner"/> out to
    /// <see cref="TickOuter"/>.</summary>
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

            distance = Math.Min(
                distance, SegmentDistance(point, direction * TickInner, direction * TickOuter));
        }

        return distance;
    }

    /// <summary>Signed distance from <paramref name="point"/> to the needle's outline, negative
    /// inside. The arrow's own method — point-to-polygon distance plus a crossing-number inside test
    /// — over four vertices, once per pixel at build time rather than per frame.</summary>
    private static float SignedDistance(Vector2 point)
    {
        var distance = float.MaxValue;
        var inside = false;

        for (var i = 0; i < Needle.Length; i++)
        {
            var a = Needle[i];
            var b = Needle[(i + 1) % Needle.Length];
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
