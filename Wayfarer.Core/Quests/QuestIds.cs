namespace Wayfarer.Core.Quests;

/// <summary>How the game numbers and sorts quests. There are two numberings: the quest manager,
/// the event framework's handler ids and the journal agent count quests from zero, while the Quest
/// sheet and the journal's own quest ids put them 65536 higher. The journal also sorts what it is
/// showing by kind.</summary>
public static class QuestIds
{
    /// <summary>The journal agent's kind for an ordinary quest, as against a leve or a quest the
    /// player has already finished. Only an ordinary quest can be followed.</summary>
    public const uint OrdinaryQuest = 1;

    private const uint SheetRowOffset = 65536;

    /// <summary>The Quest sheet row, event handler id, or journal quest id for a quest.</summary>
    public static uint RowId(ushort questId) => questId + SheetRowOffset;

    /// <summary>The quest manager's id for a sheet row or journal quest id, or null when the number
    /// is not a quest's.</summary>
    public static ushort? FromRowId(uint rowId) =>
        rowId >= SheetRowOffset && rowId - SheetRowOffset <= ushort.MaxValue ? (ushort)(rowId - SheetRowOffset) : null;
}
