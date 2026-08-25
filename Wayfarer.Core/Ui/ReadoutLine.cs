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
/// <para>Marked lines get the medallion and the tracker's icon-bearing pitch; unmarked lines get
/// neither, and take the tracker's own annotation block
/// (<see cref="GameMetrics.Banner.AnnotationBlock"/>).</para></param>
/// <param name="Glyph">One of the game's own bitmap-font icons, drawn <i>inside</i> this line's words
/// at <paramref name="GlyphAt"/>. <see cref="DtrGlyph.None"/> for the lines that are words only.
///
/// <para><b>A subordinate line carries a glyph if and only if it carries an <see cref="Action"/>.</b>
/// The glyph IS the affordance — it is the only mark on a line of the readout's block that says the
/// line can be pressed — so a glyph on a line that does nothing is a promise the readout does not
/// keep, and an action on a line with no glyph is a press nobody will ever find. Both halves are
/// enforced where lines are built (<see cref="ReadoutComposer.Pressable"/>) and asserted over
/// everything the composer can produce.</para>
///
/// <para>The duty line is the worked example, and it is why the rule is a biconditional rather than
/// "actionable lines get a glyph". An objective inside a dungeon the player has unlocked is a duty
/// they can queue for, so the line takes the duty mark and queues on a press. The same objective
/// inside a dungeon they have <i>not</i> unlocked cannot be queued at all — so it keeps its words,
/// loses its mark, and reads as the plain statement of fact it actually is.</para>
///
/// <para><b><see cref="Subject"/> is outside the rule, and deliberately.</b> It is not a line of text
/// in the block at all — it is the name written across the banner's parchment plate, and what says
/// that plate can be pressed is the plate: a large, obviously-clicked object carrying the chevron the
/// game's own art already puts at its right end. An icon dropped inside the quest name would be a
/// bullet in the middle of a title, and it would say nothing the parchment does not already say. So
/// the subject carries an action and no glyph, which is the one shape the biconditional
/// excludes.</para>
///
/// <para><b>Why a mark and not a character in the string.</b> This assembly has no Dalamud
/// dependency, so it cannot name a <c>BitmapFontIcon</c>; the layer that draws does the mapping,
/// exactly as <see cref="DtrComposer"/> and the server info bar entry already do. And a sentinel
/// character in <paramref name="Text"/> for the drawing layer to find by string-matching is the
/// precise mistake <see cref="ReadoutLineAction"/> exists to have corrected — the plugin used to
/// decide a line was clickable by looking for a "(click)" suffix on it.</para></param>
/// <param name="GlyphAt">Where in <paramref name="Text"/> the glyph is inserted, as a character
/// index. 0 puts it in front of the words; <c>Text.Length</c> puts it after them; anything between
/// puts it inline, which is what the teleport line does — "Teleport to " then the crystal then the
/// aetheryte's name, so the mark reads as part of the sentence rather than as a bullet on it.
/// Ignored when <paramref name="Glyph"/> is <see cref="DtrGlyph.None"/>, and clamped where it is
/// drawn, which must never throw on a line's own text.</param>
public sealed record ReadoutLine(
    string Text,
    ReadoutEmphasis Emphasis,
    bool Separated = false,
    ReadoutLineAction Action = ReadoutLineAction.None,
    bool Subject = false,
    bool Marked = false,
    DtrGlyph Glyph = DtrGlyph.None,
    int GlyphAt = 0);
