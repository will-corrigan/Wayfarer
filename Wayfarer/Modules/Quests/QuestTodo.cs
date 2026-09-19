using Wayfarer.Routing;

namespace Wayfarer.Modules.Quests;

/// <summary>One line of a quest's to-do list, finished: the words as the player would read them
/// on screen, with every macro the sheet left already resolved.</summary>
/// <param name="Index">Its slot in the quest's to-do table.</param>
/// <param name="Sequence">The step it completes.</param>
/// <param name="Text">The line's words, resolved.</param>
/// <param name="Needed">How many of the thing this line wants.</param>
/// <param name="Positions">Where the quest data puts it: one per Level row, possibly none.</param>
internal sealed record QuestTodo(
    int Index,
    byte Sequence,
    string Text,
    int Needed,
    IReadOnlyList<Place> Positions);
