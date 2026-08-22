using Dalamud.Game.Text;

namespace Wayfarer;

/// <summary>Confirm/cancel button labels for controller-mode hints. Reflection over Dalamud's
/// SeIconChar enum found only the geometric PlayStation-style glyph shapes (Cross/Circle/Square/
/// Triangle) embeddable via the game's own icon font — no Xbox-lettered variant exists as a font
/// glyph there (the game renders those via a texture swap elsewhere in its UI, not through this
/// font). So on anything other than a confirmed PlayStation button-icon setting (Xbox, or unknown/
/// unreadable), this renders plain text labels ("A"/"B") instead of a wrong-shaped glyph — see
/// <see cref="InputModeService"/>, which reads the game's own <c>PadSelectButtonIcon</c> config to
/// decide which. Orientation — which button means "confirm" — tracks PadReverseConfirmCancel in
/// both cases.</summary>
internal readonly record struct GlyphSet(string Confirm, string Cancel)
{
    /// <summary>PlayStation glyphs, default orientation: Cross confirms, Circle cancels.</summary>
    public static readonly GlyphSet PlayStationStandard =
        new(SeIconChar.Cross.ToIconChar().ToString(), SeIconChar.Circle.ToIconChar().ToString());

    /// <summary>PlayStation glyphs, PadReverseConfirmCancel flipped: Circle confirms, Cross cancels.</summary>
    public static readonly GlyphSet PlayStationReversed =
        new(SeIconChar.Circle.ToIconChar().ToString(), SeIconChar.Cross.ToIconChar().ToString());

    /// <summary>Xbox (or unknown) text labels, default orientation: A confirms, B cancels.</summary>
    public static readonly GlyphSet XboxStandard = new("A", "B");

    /// <summary>Xbox (or unknown) text labels, PadReverseConfirmCancel flipped: B confirms, A cancels.</summary>
    public static readonly GlyphSet XboxReversed = new("B", "A");

    public static GlyphSet For(bool isPlayStation, bool reverseConfirmCancel) => (isPlayStation, reverseConfirmCancel) switch
    {
        (true, false) => PlayStationStandard,
        (true, true) => PlayStationReversed,
        (false, false) => XboxStandard,
        (false, true) => XboxReversed,
    };
}
