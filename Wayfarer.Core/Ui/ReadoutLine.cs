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
/// <param name="Marked">This line names something the player can go and DO — somewhere to be, with
/// a quest at the end of it — rather than saying something about the thing already being tracked.
///
/// <para>It exists because the banner the readout wears has exactly one shape for a subordinate
/// line: the game's own job-quest row, a 32x32 "!" medallion beside Axis-12 text at a 26-pixel
/// pitch. The game gives every one of those rows a medallion because every one of them <i>is</i> a
/// quest you can go do. Our subordinate lines are not homogeneous — "1,240 yalms away" and "Unlocks
/// The Fractal Continuum" are not the same kind of statement — and putting a quest medallion on a
/// distance would be a lie about what it is. So the composer says which lines are which, here, where
/// it can be tested, rather than the drawn readout guessing from emphasis or from position.</para>
///
/// <para>Marked lines get the medallion and the game's 26-pixel pitch; unmarked lines get neither,
/// and take the tracker's own annotation block
/// (<see cref="GameMetrics.Banner.AnnotationBlock"/>).</para></param>
public sealed record ReadoutLine(
    string Text,
    ReadoutEmphasis Emphasis,
    bool Separated = false,
    ReadoutLineAction Action = ReadoutLineAction.None,
    bool Subject = false,
    bool Marked = false);
