using Wayfarer.Routing;

namespace Wayfarer.Modules.Quests;

/// <summary>One of the game's live map markers for a quest: where it is, and the label the game
/// draws beside it when it draws one. The game removes a marker as its objective completes, which
/// is the only signal the data gives that a to-do line is done.</summary>
/// <param name="At">Where the marker stands, with the search radius when the step is an area.</param>
/// <param name="Label">The marker's own words, or null when it has none.</param>
/// <param name="Objective">Whether the game draws this one as somewhere the step wants the player,
/// rather than as somewhere the quest merely involves. A quest pins both at once: verified in game
/// on "Heavens Weep", whose own marker list carried the step's search area and the sealed door
/// leading out of it under one objective id, differing in nothing but this.</param>
internal sealed record QuestMarker(Place At, string? Label, bool Objective = true);
