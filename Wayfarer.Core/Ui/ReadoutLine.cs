namespace Wayfarer.Core.Ui;

/// <summary>One line of the guidance readout.</summary>
/// <param name="Text">The words, already in the game's own phrasing.</param>
/// <param name="Emphasis">How much weight it carries.</param>
/// <param name="Separated">Draw a rule above this line. Used exactly once, to fence the
/// subordinate context off from the active objective.</param>
public sealed record ReadoutLine(string Text, ReadoutEmphasis Emphasis, bool Separated = false);
