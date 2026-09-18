namespace Wayfarer.Core.Quests;

/// <summary>What the game says about one ToDo right now: whether it is done, and how far along
/// its count is. Read from the quest's own event handler, the same source the journal draws from.</summary>
/// <param name="Index">The ToDo's position in the quest's table.</param>
/// <param name="Done">Whether the game has ticked it.</param>
/// <param name="Have">How many of its thing the player has so far.</param>
/// <param name="Needed">How many it wants. One for a plain "speak with" ToDo.</param>
public sealed record QuestTodoProgress(int Index, bool Done, int Have, int Needed);
