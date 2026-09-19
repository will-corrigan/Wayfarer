namespace Wayfarer.Modules.Quests;

/// <summary>One quest's step as the game's own to-do list has it: its lines, and what the game
/// says about each of them. Both come off the same line of the same list, so a line's words and
/// its check mark can never be about different lines.</summary>
/// <param name="Todos">The lines, in the order the game holds them.</param>
/// <param name="Progress">What the game says about each line, one per line in <paramref name="Todos"/>.</param>
internal sealed record TrackedStep(IReadOnlyList<QuestTodo> Todos, IReadOnlyList<QuestTodoProgress> Progress);
