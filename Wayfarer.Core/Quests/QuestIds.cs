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

    /// <summary>The quest manager's id for a number written in either numbering. The two cannot be
    /// confused: the manager's ids fit in a ushort and the sheet's rows begin just past the end of
    /// one, so the number itself says which it is. Zero is no quest either way.</summary>
    public static ushort? FromAnyId(uint id) => id switch
    {
        0 => null,
        <= ushort.MaxValue => (ushort)id,
        _ => id - SheetRowOffset <= ushort.MaxValue ? (ushort)(id - SheetRowOffset) : null,
    };
}
