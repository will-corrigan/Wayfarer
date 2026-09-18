namespace Wayfarer.Core.Guidance;

/// <summary>One line of an objective: its words, its count if it has one, where it is, and what
/// has to be done there besides arriving.</summary>
/// <param name="Text">The words the game's own tracker would show for this line.</param>
/// <param name="Progress">How far along this line is, or null when it has no count.</param>
/// <param name="Where">Where the line happens, as the app routes to it.</param>
/// <param name="Action">What the player does there, or null when arriving and interacting is all.</param>
public sealed record ObjectiveEntry(string Text, Progress? Progress, Destination Where, EntryAction? Action = null);
