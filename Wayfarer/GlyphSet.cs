using Dalamud.Game.Text;

namespace Wayfarer;

/// <summary>Confirm/cancel button-shape glyphs for controller-mode hints, drawn from the game's
/// own icon font via <see cref="SeIconChar"/>. Reflection over Dalamud's SeIconChar enum found
/// only the geometric PlayStation-style shapes (Cross/Circle/Square/Triangle) — no Xbox-lettered
/// variant exists as an embeddable font glyph (the game renders those via a texture swap
/// elsewhere in its UI, not through this font), so the game's PadSelectButtonIcon setting has no
/// glyph to select here regardless of value. Orientation — which shape means "confirm" — DOES
/// change live via PadReverseConfirmCancel, which this does track.</summary>
internal readonly record struct GlyphSet(char Confirm, char Cancel)
{
    /// <summary>Default FFXIV orientation: Cross confirms, Circle cancels.</summary>
    public static readonly GlyphSet Standard = new(SeIconChar.Cross.ToIconChar(), SeIconChar.Circle.ToIconChar());

    /// <summary>PadReverseConfirmCancel flipped: Circle confirms, Cross cancels.</summary>
    public static readonly GlyphSet Reversed = new(SeIconChar.Circle.ToIconChar(), SeIconChar.Cross.ToIconChar());

    public static GlyphSet For(bool reverseConfirmCancel) => reverseConfirmCancel ? Reversed : Standard;
}
