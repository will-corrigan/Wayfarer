using Wayfarer.Routing;

namespace Wayfarer.Modules.Quests;

/// <summary>One of the places a quest's to-do line names, as the sheet gives it and before
/// anything decides whether the step is about it.</summary>
/// <param name="Row">The Level row that names it, which is how the same place is recognised across
/// the steps of one quest.</param>
/// <param name="ObjectId">What stands there, or zero for bare ground to search.</param>
/// <param name="IsObject">Whether what stands there is a thing rather than a person. A quest sends
/// the player back to the same person on purpose, so a person is never taken for scenery.</param>
/// <param name="ObjectName">What the thing is called in the player's own language, empty when
/// nothing stands there or it has no name.</param>
/// <param name="At">Where it is, with its radius when it is ground to search.</param>
internal sealed record StepPlace(uint Row, uint ObjectId, bool IsObject, string ObjectName, Place At);
