using Wayfarer.Core.Routing;

namespace Wayfarer.Core.Quests;

/// <summary>One line of a quest's own to-do list, as the game's data authors it: which step it
/// belongs to, its words, how many of its thing are needed, and where the data says it is.</summary>
/// <param name="Index">Its position in the quest's to-do table, which is also the index of its
/// text row.</param>
/// <param name="Sequence">The step it completes.</param>
/// <param name="Text">The line's words. May carry an unresolved placeholder where the game would
/// print a live count, in which case <paramref name="HasUnresolvedPlaceholder"/> is true.</param>
/// <param name="HasUnresolvedPlaceholder">Whether the words still contain a macro the game fills in
/// at runtime and the sheet alone cannot.</param>
/// <param name="Needed">How many of the thing this line wants.</param>
/// <param name="Locations">Where the data places it: one entry per location row, possibly empty.
/// </param>
public sealed record QuestTodo(
    int Index,
    byte Sequence,
    string Text,
    bool HasUnresolvedPlaceholder,
    int Needed,
    IReadOnlyList<Place> Locations);
