namespace Wayfarer.Core.Guidance;

/// <summary>The one conversion <c>QuestJournalAction.Execute</c> needs, pulled out here so it can be
/// pinned by a test instead of only inspected — the native call it feeds is fussy about which of two
/// id forms it gets, and getting this wrong is exactly what left the Journal opening at the right
/// addon but always landing on whatever sat at the top of the list.
///
/// <para><b>Two forms, one quest.</b> <c>NavigationState.QuestId</c> (and
/// <see cref="GuidanceObjective.QuestId"/> before it) carries the <c>Quest</c> Excel sheet's OWN row
/// id — the raw quest number plus 65536, since that offset is baked into the sheet itself (an
/// ordinary quest's sheet row never falls below <c>0x10000</c>). Everywhere ELSE in the engine —
/// <c>QuestManager</c>'s own ushort-typed API, the network/UI-state quest tracking, and the numeric
/// suffix a quest LINK in chat carries — the same quest is named by the raw, un-offset id,
/// 0-65535.</para>
///
/// <para><c>AgentQuestJournal.OpenForQuest</c> is in the second camp — verified against the working
/// precedents that call it: a chat quest-link handler recovers the raw id from the quest's own
/// internal-name suffix rather than its sheet row id, and another plugin's own Journal button masks
/// the sheet row id with <c>&amp; 0xFFFF</c> before the call. Neither passes the sheet row id
/// straight through, which is what this call used to do.</para></summary>
public static class QuestJournalSelection
{
    /// <summary>Recovers the raw, un-offset quest id from the <c>Quest</c> sheet's own row id.
    /// Exact for every ordinary quest, not an approximation: the sheet's 65536 (<c>0x10000</c>)
    /// offset sets bit 16 and touches nothing below it, so masking it back off with
    /// <c>&amp; 0xFFFF</c> is a precise inverse of the <c>+ 65536</c> applied on the way in.</summary>
    public static uint RawQuestId(uint sheetRowId) => sheetRowId & 0xFFFFu;
}
