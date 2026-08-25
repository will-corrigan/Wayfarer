using System.Numerics;

namespace Wayfarer.Core.Ui;

/// <summary>The two ends of each arrow-colour variant's vertical gradient: bright at the tip,
/// saturated at the tail. Amber is the game's own warm HUD gold and is the default.
///
/// <para>Shared rather than duplicated because more than one generated glyph is drawn in the
/// player's chosen arrow colour, and two copies of a palette drift the moment one of them is
/// tweaked.</para></summary>
public static class ArrowPalette
{
    /// <summary>The near-black every generated glyph is outlined in — the same "dark edge under a
    /// gold glyph" the game's own HUD icons use, which is what makes them readable against bright
    /// terrain.</summary>
    public static Vector3 OutlineColor { get; } = new(0.07f, 0.055f, 0.03f);

    /// <summary>The gradient ends for a variant.</summary>
    public static (Vector3 Tip, Vector3 Tail) For(ArrowIconVariant variant) => variant switch
    {
        ArrowIconVariant.Green => (Rgb(214, 255, 210), Rgb(46, 168, 68)),
        ArrowIconVariant.Blue => (Rgb(208, 238, 255), Rgb(40, 134, 208)),
        ArrowIconVariant.Red => (Rgb(255, 214, 206), Rgb(198, 54, 44)),
        ArrowIconVariant.White => (Rgb(255, 255, 255), Rgb(196, 196, 196)),
        _ => (Rgb(255, 242, 194), Rgb(214, 148, 40)),
    };

    private static Vector3 Rgb(byte r, byte g, byte b) => new(r / 255f, g / 255f, b / 255f);
}
