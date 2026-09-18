namespace Wayfarer.Core.Guidance;

/// <summary>What a module wants the player guided to right now. The one thing a module hands the
/// app: every word about the thing, and where each part of it is. The app never writes any of
/// these words; it routes the places and adds its own words about getting there.</summary>
/// <param name="Headline">What the thing is called: the quest's name, the monster's name.</param>
/// <param name="Progress">How far through the whole objective the player is — "7 of 10 attuned" —
/// or null when there is nothing to count at that level.</param>
/// <param name="Entries">The lines the game's own tracker would show for it, in order. A step with
/// one target is one entry; a step whose parts can be done in any order is several.</param>
public sealed record Objective(string Headline, Progress? Progress, IReadOnlyList<ObjectiveEntry> Entries);
