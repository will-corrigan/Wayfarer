namespace Wayfarer.Core.Ui;

/// <summary>Which of the game's own bitmap-font icons the server info bar entry should be
/// prefixed with, chosen by <see cref="DtrComposer"/> from the same inputs as its text. This is
/// an abstract enum rather than <c>Dalamud.Game.Text.SeStringHandling.BitmapFontIcon</c> itself
/// because this project has no Dalamud dependency and stays testable without the game running;
/// the entry that actually owns the bar maps each value to a concrete glyph.</summary>
public enum DtrGlyph
{
    /// <summary>No icon — the bare "Wayfarer" fallback text.</summary>
    None,

    /// <summary>A hunt is the active objective.</summary>
    Hunting,

    /// <summary>An ordered route (a hunt chain or an unlock route) is stepping through stops.</summary>
    Route,
}
