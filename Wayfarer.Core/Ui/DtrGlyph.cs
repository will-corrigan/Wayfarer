namespace Wayfarer.Core.Ui;

/// <summary>Which of the game's own bitmap-font icons the server info bar entry should be
/// prefixed with, chosen by <see cref="DtrComposer"/> from the same inputs as its text. This is
/// an abstract enum rather than <c>Dalamud.Game.Text.SeStringHandling.BitmapFontIcon</c> itself
/// because this project has no Dalamud dependency and stays testable without the game running;
/// the entry that actually owns the bar maps each value to a concrete glyph.
///
/// <b>A glyph here describes the next step, not the mode.</b> That is the whole of the rule, and it
/// is a correction: the entry used to show an aetheryte crystal for "a route is in progress",
/// which meant the bar advertised a teleport while the target was fifty yalms away in the same
/// zone. The player asked, reasonably, why there was an aetheryte on it. There is now exactly one
/// glyph, it means "the next thing to do involves the aetheryte network", and anything that cannot
/// be said with it is said in words instead.</summary>
public enum DtrGlyph
{
    /// <summary>No icon. The default, and the right answer whenever the next step is simply to walk
    /// somewhere — a decorative glyph on a walk tells the player nothing.</summary>
    None,

    /// <summary>The next step uses the aetheryte network: a teleport, or an aethernet shard hop.
    /// The words beside it say which.</summary>
    Aetheryte,
}
