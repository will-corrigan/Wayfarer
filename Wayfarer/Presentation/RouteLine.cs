namespace Wayfarer.Presentation;

/// <summary>The one line a surface draws about the route: a mark, the words, and what a press
/// does, or null press when the line is words only.</summary>
/// <param name="Glyph">The mark in front of the words.</param>
/// <param name="Text">The words.</param>
/// <param name="Press">What pressing the line does, or null when nothing.</param>
/// <param name="Keyword">The words inside <paramref name="Text"/> that name what a press does, or
/// null when nothing in the line is pressable. A surface lights and presses these words.</param>
public sealed record RouteLine(RouteGlyph Glyph, string Text, RoutePress? Press, string? Keyword = null);
