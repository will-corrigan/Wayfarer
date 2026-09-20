using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Wayfarer.Modules.Quests;

/// <summary>The quest journal, opened at a quest of ours.
///
/// <para>The agent keeps its own selection by the quest manager id, not the sheet row, and opens
/// and selects in a single call. Asking twice with the window already open shuts it again.</para></summary>
internal sealed unsafe class QuestJournal(IPluginLog log)
{
    /// <summary>Opens the journal at a quest, whether or not it is already open.
    ///
    /// <para>Asked once and only once: the agent opens the window and selects the quest in the one
    /// call, and asking again with the window already open shuts it.</para></summary>
    public void Open(ushort questId) => Select(questId);

    private void Select(ushort questId)
    {
        var journal = AgentQuestJournal.Instance();
        if (journal == null)
        {
            log.Warning("the game's quest journal is not there yet, so nothing was opened.");
            return;
        }

        journal->OpenForQuest(questId, QuestIds.OrdinaryQuest);
    }
}
