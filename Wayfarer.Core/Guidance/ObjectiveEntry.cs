namespace Wayfarer.Core.Guidance;

/// <summary>One line of an objective, as the game's own tracker shows it: its words, its count,
/// and where its target is.</summary>
/// <param name="Text">The game's own wording for this line: "Visit the Lancers' Guild", "Slay
/// karakul", "Accept from Marcette".</param>
/// <param name="Progress">This line's own count — "2 of 3 killed" — or null for a plain "go here".
/// </param>
/// <param name="Where">Where the target is, as one of the kinds routing knows how to handle.</param>
public sealed record ObjectiveEntry(string Text, Progress? Progress, Destination Where);
