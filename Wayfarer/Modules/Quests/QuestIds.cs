using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace Wayfarer.Modules.Quests;

/// <summary>How the game numbers quests. There are two numberings: the quest manager, the event
/// framework's handler ids and the journal agent count quests from zero, while the Quest sheet and
/// the journal's own quest ids put them 65536 higher.
///
/// <para>They are not two numbers. The game models both as one <see cref="EventId"/>: the low half
/// is the quest, the high half says it is a quest rather than a warp or a guildleve, and the whole
/// of it is the sheet's row. Reading a quest out of either numbering is asking that for its entry,
/// and writing one is saying which kind it is, so the two can no longer be handed to each other by
/// mistake.</para></summary>
internal static class QuestIds
{
    /// <summary>The journal agent's kind for an ordinary quest, as against a leve or a quest the
    /// player has already finished. Only an ordinary quest can be followed. Not to be confused with
    /// an event's own kind: this one is the journal's way of sorting what it shows.</summary>
    public const uint OrdinaryQuest = 1;

    /// <summary>The Quest sheet row, event handler id, or journal quest id for a quest.</summary>
    public static uint RowId(ushort questId) => Event(questId).Id;

    /// <summary>The quest manager's id for a number written in either numbering, or null when the
    /// number is not a quest at all. The low half of an event's id is the quest either way, so a
    /// number already in the manager's numbering answers with itself; the high half says what kind
    /// of event it is, and a warp or a shop is not one however much it looks like a sheet row.</summary>
    public static ushort? FromAnyId(uint id)
    {
        if (id == 0)
        {
            return null;
        }

        // The manager's numbering says nothing about kind, so anything that fits its half is
        // already a quest's own id and has nothing to decode.
        if (id <= ushort.MaxValue)
        {
            return (ushort)id;
        }

        var named = new EventId { Id = id };
        return named.ContentId == EventHandlerContent.Quest && named.EntryId != 0 ? named.EntryId : null;
    }

    /// <summary>A quest as the event framework names it.</summary>
    public static EventId Event(ushort questId) =>
        new() { EntryId = questId, ContentId = EventHandlerContent.Quest };
}
