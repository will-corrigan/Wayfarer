namespace Wayfarer.Core.Presentation;

/// <summary>A stretch of one line of words, placed: the words themselves, whether they are the
/// line's keyword, and where they sit relative to the line's top left.</summary>
/// <param name="Text">The words in this stretch.</param>
/// <param name="Keyword">Whether this stretch is the keyword, the words that name what a press does.</param>
/// <param name="Left">Where the stretch starts, in pixels from the line's left.</param>
/// <param name="Top">Where the stretch sits, in pixels from the line's top.</param>
/// <param name="Width">How wide the stretch is, including any room kept in front of a keyword.</param>
public sealed record LineRun(string Text, bool Keyword, float Left, float Top, float Width);
