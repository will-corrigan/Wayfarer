namespace Wayfarer.Core.Ui;

/// <summary>Which colour the readout's direction arrow is drawn in.
///
/// <b>These used to name crops of the minimap's own texture sheet, and that was the defect.</b> The
/// five 24x24 "chevrons" on <c>ui/uld/NaviMap.tex</c> are real — they are the minimap's off-screen
/// marker carets — but a caret is a hat, not a pointer, and the crop never reached the screen anyway:
/// the image node was set to fit its <i>whole</i> texture into its box, so what actually drew was the
/// entire 448x212 sheet squashed into 34 pixels (two ornate compass rings, the cardinal letters and
/// all six carets at once), which is the "ornate scrollwork bar" the readout was shipping instead of
/// an arrow.
///
/// The arrow is now drawn by <see cref="ArrowBitmap"/> — one texture whose entire content is the
/// arrow, so there is no crop left to get wrong — and these name its colour. The setting stays
/// because which colour reads as "go this way" against a particular player's terrain and HUD theme
/// is the sort of question only an eye can settle.
///
/// <b>Values are append-only</b> — they are persisted in the player's config as integers.</summary>
public enum ArrowIconVariant
{
    /// <summary>The game's own warm HUD gold. The default.</summary>
    Amber,

    Green,

    Blue,

    Red,

    White,
}
