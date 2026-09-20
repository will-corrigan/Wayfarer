namespace Wayfarer.Modules.Quests;

/// <summary>One to-do line of a quest, with everything needed to say where it sends the player.
/// </summary>
/// <param name="Index">Its slot in the quest's to-do table.</param>
/// <param name="Sequence">The step it completes.</param>
/// <param name="Words">The line as the player reads it, used only to notice when a step names the
/// very thing the rest of the quest treats as scenery.</param>
/// <param name="Places">Every place the line names, in the order the sheet gives them.</param>
internal sealed record StepShape(int Index, byte Sequence, string Words, IReadOnlyList<StepPlace> Places);
