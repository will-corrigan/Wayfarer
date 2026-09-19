namespace Wayfarer.Modules.Quests;

/// <summary>One step of a quest as it stands right now: its lines with the words finished, and
/// what the quest's own script says about each of them. One entry of each per line, in the same
/// order, so a line's words and its progress can never be about different lines.</summary>
/// <param name="Todos">The step's lines, with every macro resolved.</param>
/// <param name="Progress">What the script says about each line.</param>
internal sealed record QuestStep(IReadOnlyList<QuestTodo> Todos, IReadOnlyList<QuestTodoProgress> Progress);
