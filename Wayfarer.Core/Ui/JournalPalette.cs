using System.Numerics;

namespace Wayfarer.Core.Ui;

/// <summary>The journal page's text colours, and the parchment they are read against.
///
/// <para><b>Why these are here and not with the rest of the plugin's colours.</b> Every other text
/// role Wayfarer draws is light-on-transparent, resolved live from the game's <c>UIColor</c> sheet, and
/// therefore only meaningful with a client attached. These four are dark-on-cream literals, which
/// makes them the one part of the palette that <i>can</i> be checked without a game running — and the
/// defect they exist to fix was exactly a legibility failure a test could have caught: the page
/// shipped wearing the readout's near-white on cream parchment and the player photographed a giver
/// line he could not read. <c>JournalPaletteTests</c> now asserts every one of them against
/// <see cref="Parchment"/>.</para>
///
/// <para><b>What they are, honestly.</b> Literals, not <c>UIColor</c> rows, and not extracted from
/// <c>JournalDetail</c>'s own text nodes: <c>ui/uld/JournalDetail.uld</c> was not available on the
/// machine this was written on, so its authored colours could not be read, and naming a row id we had
/// not checked would look like evidence while being a guess. The roles and their relative weight are
/// the game's — near-black prose on cream, a muted grey-brown for the section headings and the line at
/// the foot — and the contrast is measured.</para></summary>
public static class JournalPalette
{
    /// <summary>The smallest contrast ratio a body-size text colour on this page may have. WCAG's
    /// figure for normal-size text, used here as the floor a role has to clear rather than as a
    /// target.</summary>
    public const float MinimumContrast = 4.5f;

    /// <summary>The paper. Mean (200, 195, 174), sampled across the whole stretchable band of the
    /// game's own cream parchment — the same figure recorded for the readout's banner plate, whose art
    /// is the same family. The Journal's own sheet
    /// (<c>Journal_Detail.tex</c> (376,28)) could not be sampled here, which is stated rather than
    /// glossed: if it turns out to be materially darker, every ratio below is conservative in the
    /// wrong direction and these values want re-checking.</summary>
    public static Vector4 Parchment { get; } = new(200f / 255f, 195f / 255f, 174f / 255f, 1f);

    /// <summary>The entry's name. #251D14, a very dark warm brown.</summary>
    public static Vector4 Title { get; } = Rgb(0x25, 0x1D, 0x14);

    /// <summary>The prose. #2B2318 — a shade off the title, so the two read as a hierarchy rather
    /// than as one weight.</summary>
    public static Vector4 Body { get; } = Rgb(0x2B, 0x23, 0x18);

    /// <summary>A section heading: Reward, Description, Requirements. #4A4234, a muted grey-brown —
    /// quieter than the prose it introduces, which is the relationship the game's own page has.
    /// </summary>
    public static Vector4 Heading { get; } = Rgb(0x4A, 0x42, 0x34);

    /// <summary>The lines that are <i>about</i> the entry rather than part of it: the kind caption,
    /// the giver at the foot, the confidence footnote. #544B3D — the quietest thing on the page that
    /// is still text and not decoration.</summary>
    public static Vector4 Meta { get; } = Rgb(0x54, 0x4B, 0x3D);

    /// <summary>Every role on the page, so a test can sweep them without having to be told when one
    /// is added.</summary>
    public static IReadOnlyList<(string Role, Vector4 Colour)> Roles =>
    [
        ("title", Title),
        ("body", Body),
        ("heading", Heading),
        ("meta", Meta),
    ];

    /// <summary>WCAG relative luminance of an sRGB colour. Alpha is ignored: these are opaque.
    /// </summary>
    public static float Luminance(Vector4 colour) =>
        (0.2126f * Linear(colour.X)) + (0.7152f * Linear(colour.Y)) + (0.0722f * Linear(colour.Z));

    /// <summary>WCAG contrast ratio between two colours, lighter over darker.</summary>
    public static float Contrast(Vector4 a, Vector4 b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        var (light, dark) = la >= lb ? (la, lb) : (lb, la);
        return (light + 0.05f) / (dark + 0.05f);
    }

    private static Vector4 Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f, 1f);

    private static float Linear(float channel) =>
        channel <= 0.04045f
            ? channel / 12.92f
            : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
}
