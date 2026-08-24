namespace Wayfarer.Core.Ui;

/// <summary>One subordinate line of the readout, reduced to the only three things its height depends
/// on.
///
/// <para><b>Why the row count is an input and not something computed here.</b> How many rows a string
/// wraps into is a question only the engine can answer — it depends on the loaded font, the kerning
/// and where the words happen to break — so the live node asks the engine and hands the answer over.
/// What this deliberately does <i>not</i> do is let that answer move anything but the line it belongs
/// to: a row count that comes back wrong makes one line the wrong height, and the flow places
/// everything after it against that height rather than against a running total. A bad measurement
/// costs a visible line; it can no longer cost a legible readout.</para></summary>
/// <param name="Marked">Whether the line hangs one of the game's own "!" medallions in the gutter,
/// which also decides which of the game's two row pitches it takes — see
/// <see cref="ReadoutLine.Marked"/>.</param>
/// <param name="Separated">Whether a rule is drawn above it — see
/// <see cref="ReadoutLine.Separated"/>.</param>
/// <param name="Rows">How many rows the line's words actually wrapped into, as the engine measured
/// them. Clamped by <see cref="ReadoutBodyLayout.MaxWrappedLines"/>.</param>
public readonly record struct ReadoutBlock(bool Marked, bool Separated, float Rows);
