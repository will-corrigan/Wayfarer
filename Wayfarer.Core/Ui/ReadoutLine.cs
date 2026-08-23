namespace Wayfarer.Core.Ui;

/// <summary>One line of the guidance readout.</summary>
/// <param name="Text">The words, already in the game's own phrasing.</param>
/// <param name="Emphasis">How much weight it carries.</param>
/// <param name="Separated">Draw a rule above this line. Used exactly once, to fence the
/// subordinate context off from the active objective.</param>
/// <param name="Action">What clicking it does, where clicking is possible. See
/// <see cref="ReadoutLineAction"/>.</param>
/// <param name="Subject">This line NAMES what the readout is about — the followed quest, the hunt,
/// the route, or "No quest followed" when there is nothing. At most one line ever carries it.
///
/// <para>It is marked here, by the composer, rather than guessed at where the readout is drawn.
/// Three separate things hang off knowing which line is the subject and none of them could tell on
/// their own: the switcher that changes what is followed sits at its right, a long name is
/// truncated on it rather than wrapped, and clicking it opens the game's own Journal.
/// <see cref="ReadoutEmphasis.Primary"/> cannot answer the question — the distance line is Primary
/// too — and neither can the position, because the idle readout's subject is a muted
/// line.</para></param>
public sealed record ReadoutLine(
    string Text,
    ReadoutEmphasis Emphasis,
    bool Separated = false,
    ReadoutLineAction Action = ReadoutLineAction.None,
    bool Subject = false);
