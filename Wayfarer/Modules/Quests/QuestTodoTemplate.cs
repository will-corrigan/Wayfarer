using Lumina.Text.ReadOnly;
using Wayfarer.Routing;

namespace Wayfarer.Modules.Quests;

/// <summary>One line of a quest's to-do list as the sheet authors it, before the game fills
/// anything in. The words are still a template: they may carry macros standing for a count the
/// quest is keeping, an object's name, or a whole branch of wording chosen by progress.
///
/// <para>Read once per quest and kept, because none of it changes. What the player sees is made
/// from this every frame by <see cref="QuestReader.Step"/>, which is where the quest's own numbers
/// are put in.</para></summary>
/// <param name="Index">Its slot in the quest's to-do table, which is what the quest's running
/// script answers about.</param>
/// <param name="Sequence">The step it completes.</param>
/// <param name="Words">The line as authored, macros and all.</param>
/// <param name="Needed">How many of the thing the sheet says this line wants.</param>
/// <param name="Positions">Where the quest data puts it: one per Level row, possibly none.</param>
internal sealed record QuestTodoTemplate(
    int Index,
    byte Sequence,
    ReadOnlySeString Words,
    int Needed,
    IReadOnlyList<Place> Positions);
