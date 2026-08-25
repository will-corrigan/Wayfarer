namespace Wayfarer.Core.Ui;

/// <summary>Which of the game's own bitmap-font icons a surface should draw beside — or inside — its
/// words, chosen from the same inputs as the words themselves. This is an abstract enum rather than
/// <c>Dalamud.Game.Text.SeStringHandling.BitmapFontIcon</c> itself because this project has no
/// Dalamud dependency and stays testable without the game running; each layer that actually draws —
/// the server info bar entry, the readout's line nodes — maps a value to a concrete glyph.
///
/// <b>A glyph describes the next step, not the mode.</b> That is the first half of the rule, and it
/// is a correction: the bar used to show an aetheryte crystal for "a route is in progress", which
/// meant it advertised a teleport while the target was fifty yalms away in the same zone. The player
/// asked, reasonably, why there was an aetheryte on it. A glyph names what the line it sits in is
/// ABOUT, and anything that cannot be said with one is said in words instead.
///
/// <b>On the readout, a glyph also means "this can be pressed".</b> That is the second half, and it
/// is <see cref="ReadoutLine"/>'s invariant rather than this enum's — see that type for the
/// biconditional and for why a duty the player has not unlocked gets no glyph at all.
///
/// <b>Named for meaning, not for the icon.</b> <see cref="Aetheryte"/> happens to map to an icon of
/// the same name and the two below do not, which is the whole point of the indirection: a value says
/// what the line means and the drawing layer picks the closest thing the font actually has. Where
/// that is a compromise it is admitted at the mapping rather than hidden here.
///
/// <b>No surface has to draw all of them.</b> The bar chooses only <see cref="None"/> and
/// <see cref="Aetheryte"/> — see <see cref="DtrComposer"/>, whose whole point is that it has exactly
/// one glyph — while the readout's lines use every value. A surface maps what it can draw and falls
/// back to no glyph for the rest, so neither has to know what the other wants.</summary>
public enum DtrGlyph
{
    /// <summary>No icon. The default, and the right answer whenever the next step is simply to walk
    /// somewhere — a decorative glyph on a walk tells the player nothing.</summary>
    None,

    /// <summary>The next step uses the aetheryte network: a teleport, or an aethernet shard hop.
    /// The words beside it say which.</summary>
    Aetheryte,

    /// <summary>The line names instanced duty content the objective is inside — a dungeon, trial or
    /// raid — and pressing it queues for that duty. Readout only.</summary>
    Duty,

    /// <summary>The line names monsters to go and kill: the hunting log's own summary of what is left
    /// of a rank. Readout only.</summary>
    Monster,
}
