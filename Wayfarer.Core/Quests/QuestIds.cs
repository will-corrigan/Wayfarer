namespace Wayfarer.Core.Quests;

/// <summary>The two numberings a quest has. The quest manager, the event framework's handler ids
/// and the journal agent count quests from zero; the Quest sheet and the journal's own quest ids
/// put them 65536 higher.</summary>
public static class QuestIds
{
    private const uint SheetRowOffset = 65536;

    /// <summary>The Quest sheet row, event handler id, or journal quest id for a quest.</summary>
    public static uint RowId(ushort questId) => questId + SheetRowOffset;

    /// <summary>The quest manager's id for a sheet row or journal quest id, or null when the number
    /// is not a quest's.</summary>
    public static ushort? FromRowId(uint rowId) =>
        rowId >= SheetRowOffset && rowId - SheetRowOffset <= ushort.MaxValue ? (ushort)(rowId - SheetRowOffset) : null;
}
