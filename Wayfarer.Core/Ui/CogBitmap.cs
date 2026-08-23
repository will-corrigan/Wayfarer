using System.Numerics;

namespace Wayfarer.Core.Ui;

/// <summary>Draws the readout's settings cog, as pixels.
///
/// <para><b>Why it is generated rather than cropped out of a game sheet.</b> The same reason the
/// direction arrow is — see <see cref="ArrowBitmap"/>. A crop out of a texture sheet has to be right
/// about coordinates, about the node's fit flags and about what the art on that sheet actually is,
/// and getting any of the three wrong draws something nobody intended. The cog is one shape with no
/// state; computing it costs a few thousand pixels once per session and cannot be wrong about a
/// sheet.</para>
///
/// <para>The treatment is deliberately the arrow's: a warm gold fill inside a near-black edge, which
/// is what the game's own gold HUD glyphs do and what keeps a small icon legible against bright
/// terrain. It is centred in its own square image, so it can be parked by its centre without
/// anything having to know about the art.</para></summary>
public static class CogBitmap
{
    /// <summary>The generated texture is square and this is its side, in pixels. Several times the
    /// size the readout draws it at, so the downscale does the anti-aliasing.</summary>
    public const int Size = 64;

    /// <summary>Bytes per generated image — <c>Size * Size * 4</c>, RGBA, straight (not
    /// premultiplied) alpha.</summary>
    public const int ByteCount = Size * Size * 4;

    /// <summary>How many teeth. Eight reads as a cog at 14 pixels; twelve turns into a blur.</summary>
    private const int Teeth = 8;

    // All in units of half the image, matching ArrowBitmap's convention. Deliberately inside the
    // edges so the outline still fits within the texture.
    private const float ToothRadius = 0.86f;
    private const float RootRadius = 0.66f;
    private const float HubRadius = 0.30f;

    /// <summary>The tooth's angular half-width, as a fraction of one tooth-plus-gap period. Below a
    /// quarter the teeth are narrower than the gaps, which is what a real cog looks like.</summary>
    private const float ToothHalfWidth = 0.22f;

    private const float OutlineWidth = 2.2f;
    private const float EdgeSoftness = 1.1f;

    private static readonly Vector3 OutlineColor = new(0.07f, 0.055f, 0.03f);

    /// <summary>The game's warm HUD gold — the same family as the arrow's default amber, so the cog
    /// reads as part of the same object rather than as a second thing bolted on.</summary>
    private static readonly Vector3 FillColor = new(1f, 0.906f, 0.71f);

    /// <summary>Renders the cog as straight-alpha RGBA bytes, row-major from the top-left, ready for
    /// <c>ITextureProvider.CreateFromRaw</c>.</summary>
    public static byte[] Render()
    {
        var pixels = new byte[ByteCount];
        const float Half = Size / 2f;

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var point = new Vector2(((x + 0.5f) / Half) - 1f, ((y + 0.5f) / Half) - 1f);
                var distance = SignedDistance(point) * Half;

                var silhouette = Coverage(distance - OutlineWidth);
                if (silhouette <= 0f)
                {
                    continue;
                }

                var color = Vector3.Lerp(OutlineColor, FillColor, Coverage(distance));
                var offset = ((y * Size) + x) * 4;
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

    /// <summary>Signed distance from <paramref name="point"/> to the cog, negative inside, in units
    /// of half the image.
    ///
    /// <para>Worked in polar coordinates because that is the shape's own frame: the body is an
    /// annulus, and a tooth is an arc of a wider circle. The one approximation is the tooth's side,
    /// where the angular gap is converted to a length by multiplying by the radius — exact on the
    /// tooth's own arc and close enough either side of it at this resolution.</para></summary>
    private static float SignedDistance(Vector2 point)
    {
        var radius = point.Length();
        if (radius <= float.Epsilon)
        {
            return HubRadius;
        }

        // 0 at the centre of a tooth, 0.5 at the centre of a gap.
        var turn = ((MathF.Atan2(point.Y, point.X) / MathF.Tau) + 1f) % 1f;
        var offsetInTooth = Math.Abs(((turn * Teeth) % 1f) - 0.5f);

        // Outside the root circle the boundary is the tooth: either its outer arc or its side,
        // whichever is further out. Inside it, the root circle is not a boundary at all.
        var toBody = radius - RootRadius;
        var toToothArc = radius - ToothRadius;
        var toToothSide = (offsetInTooth - ToothHalfWidth) * MathF.Tau * radius / Teeth;
        var outer = toBody <= 0f ? toBody : Math.Max(toToothArc, toToothSide);

        // The hub is a hole: being inside it is being outside the cog.
        return Math.Max(outer, HubRadius - radius);
    }
}
