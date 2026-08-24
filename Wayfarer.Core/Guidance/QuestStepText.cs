namespace Wayfarer.Core.Guidance;

/// <summary>One entry from the quest's own ToDo text sheet (<c>TEXT_&lt;QuestId&gt;_TODO_&lt;nn&gt;</c>
/// — the exact strings the game's own quest tracker prints), already vetted for runtime
/// placeholders by the reader: Lumina's payload types are plugin-side only, so
/// <see cref="HasUnresolvedPlaceholder"/> is computed there (see
/// <c>QuestObjectiveSource.ReadQuestStepTexts</c>) and carried in rather than recomputed
/// here.</summary>
/// <param name="Sequence">The same byte <c>QuestManager.GetQuestSequence</c> returns for this
/// quest, taken from the sheet's own <c>TodoParams[i].ToDoCompleteSeq</c> — 0 marks an unused
/// padding slot (every quest's TodoParams array is fixed-size and mostly empty), never an actual
/// step, matching <c>GetQuestSequence</c>'s own "0 = not active" convention.</param>
/// <param name="Text">The row's text, empty when the slot carries none.</param>
/// <param name="HasUnresolvedPlaceholder">True when the sheet string embeds a macro payload that
/// only the live client can fill in (a player name, a script-supplied count, a conditional
/// branch) — see the reader for the exact list. Verified empirically across a sample spanning
/// starting-class quests, Stormblood/Shadowbringers zone quests and beast-tribe deliveries: every
/// sampled string was plain literal text (numeric progress is a separate field on the game's own
/// quest-tracker addon, never baked into the string), so this is a safety net for the case that
/// hasn't been seen yet, not a common path.</param>
public readonly record struct QuestStepText(byte Sequence, string Text, bool HasUnresolvedPlaceholder = false);
