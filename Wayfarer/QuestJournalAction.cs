using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Wayfarer.Core.Guidance;

namespace Wayfarer;

/// <summary>Opens the game's own Quest Journal at a particular quest, WITH that quest selected and
/// scrolled into view — not merely open on whatever sat at the top of the list.
///
/// <para><b>This is client UI navigation, not a server-affecting action</b> — the same category as
/// <see cref="DutyFinderAction"/>, and the same distinction <see cref="TeleportAction"/>'s note
/// draws. <c>AgentQuestJournal</c> is a UI agent: it selects a row and shows the Journal and
/// JournalDetail addons, which is the identical path the game takes when the player clicks a quest
/// link in chat or opens the Journal themselves. Everything it needs is already client-resident —
/// accepted quests and their progress live in <c>QuestManager</c> and <c>UIState</c> — so nothing is
/// asked of the server to draw it. The one deliberate click the plugin makes that the server does
/// see is still the teleport, and only the teleport.</para>
///
/// <para><b>The id.</b> <c>NavigationState.QuestId</c> carries the <c>Quest</c> sheet's own row id —
/// the 65536-based one — but <c>OpenForQuest</c> wants the OTHER form, the raw un-offset id: see
/// <see cref="QuestJournalSelection"/> for the evidence (the chat quest-link handler and a second
/// plugin's own Journal button, neither of which pass the sheet row id straight through) and the
/// exact bit-arithmetic that makes recovering it from the row id exact rather than approximate.
/// Passing the row id unmasked, as this used to do, opens the right addon but never finds a match to
/// select — precisely the "always lands at the top" report this fixes. The type argument
/// distinguishes an ordinary quest from a levequest and is always 1 here: Wayfarer follows quests,
/// and a leve is not one of the things it can be following.</para></summary>
internal static unsafe class QuestJournalAction
{
    /// <summary>The <c>type</c> argument of <c>OpenForQuest</c>: 1 is an ordinary quest, 2 is a
    /// levequest.</summary>
    private const uint OrdinaryQuest = 1;

    public static void Execute(uint questRowId)
    {
        if (questRowId == 0)
        {
            return;
        }

        var agent = AgentQuestJournal.Instance();
        if (agent != null)
        {
            agent->OpenForQuest(QuestJournalSelection.RawQuestId(questRowId), OrdinaryQuest);
        }
    }
}
