using Dalamud.Game.Text.SeStringHandling;
using Lumina.Text.ReadOnly;

namespace Wayfarer;

/// <summary>Button hints drawn with the game's own controller glyphs.
///
/// The old approach branched on the player's pad family and fell back to the literal letters "A"
/// and "B" for anything that was not a confirmed PlayStation pad. That was necessary for
/// <c>SeIconChar</c>, which only carries the PlayStation geometric shapes — but it is unnecessary
/// here, and it was also wrong in practice, because the game setting it branched on
/// (PadSelectButtonIcon) is not a two-value PlayStation/Xbox flag: it is a seven-option list with
/// the Xbox layouts first, so a default Xbox pad read as PlayStation and was shown ✕ and ○.
///
/// <c>BitmapFontIcon.ControllerButton0..3</c> name the four face buttons <b>by position</b>, and
/// the game swaps the glyph atlas itself according to that same setting. One payload therefore
/// renders as Ⓐ/Ⓑ on an Xbox atlas and ✕/○ on a PlayStation one, with no branch on our side and
/// nothing to get wrong.
///
/// <b>The positional mapping is the opposite of the intuitive guess</b>, which is why it is named
/// rather than written as a bare number. Dalamud's own documentation, verbatim: button 0 is
/// "Xbox: B, PlayStation: Circle" and button 1 is "XBox: A, PlayStation: Cross". So button 0 is the
/// East face button — cancel — and button 1 is South — confirm. Its gamepad self-test maps the same
/// ids to the same physical positions.
///
/// <b>Every hint pairs a glyph with a word.</b> If the icon fails to render the sentence still
/// reads, and if the player's confirm/cancel orientation is somehow misread the verb is still
/// right — which turns the one remaining uncertainty here into something harmless.</summary>
internal static class ControllerGlyphs
{
    /// <summary>East face button — Xbox B, PlayStation Circle. The default <b>cancel</b>.</summary>
    private const BitmapFontIcon East = BitmapFontIcon.ControllerButton0;

    /// <summary>South face button — Xbox A, PlayStation Cross. The default <b>confirm</b>.</summary>
    private const BitmapFontIcon South = BitmapFontIcon.ControllerButton1;

    /// <summary>Builds the window's button-hint line: move, select, back. Reads correctly on both
    /// pad families from one payload.</summary>
    /// <param name="reverseConfirmCancel">The player's own PadReverseConfirmCancel setting, which
    /// swaps which physical button confirms. This is a genuinely separate concern from which glyph
    /// is drawn, and is the only reason that setting still needs reading at all.</param>
    public static ReadOnlySeString WindowHint(bool reverseConfirmCancel) =>
        Hint(reverseConfirmCancel, "Back");

    // There used to be a second hint here — "Close" rather than "Back" — for while the journal page
    // was open, because the page was drawn inside the hub window and Cancel shuts the addon that has
    // focus, which was the whole window. The page is its own addon now, so Cancel closes the page and
    // "Back" is true on both surfaces; a wording that only existed to warn about that arrangement went
    // with the arrangement.
    private static ReadOnlySeString Hint(bool reverseConfirmCancel, string cancelVerb)
    {
        var confirm = reverseConfirmCancel ? East : South;
        var cancel = reverseConfirmCancel ? South : East;

        var builder = new SeStringBuilder();
        builder.AddIcon(BitmapFontIcon.ControllerDPadAll).AddText(" Move   ");
        builder.AddIcon(confirm).AddText(" Select   ");
        builder.AddIcon(cancel).AddText($" {cancelVerb}");
        return new ReadOnlySeString(builder.Build().Encode());
    }
}
