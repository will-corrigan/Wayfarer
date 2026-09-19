using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Wayfarer.App;

namespace Wayfarer.Modules.Quests;

/// <summary>The quest journal, opened at a quest of ours.
///
/// <para>When the journal is already open the quest is simply selected. When it is not, asking for
/// it opens it, and the game shows whatever page it was last on or has just been told to show;
/// the quest is selected again once the window itself says it has finished setting up. That is the
/// game saying when it is ready rather than us counting frames at it.</para></summary>
internal sealed unsafe class QuestJournal(IAddonLifecycle lifecycle, IGameGui gameGui, IPluginLog log) : IDisposable
{
    private static readonly string DetailAddonName = GameAddon.NameOf<AddonJournalDetail>();

    private ushort? wanted;
    private bool listening;

    private bool IsOpen => gameGui.GetAddonByName(DetailAddonName).Address != nint.Zero;

    /// <summary>Opens the journal at a quest, whether or not it is already open.</summary>
    public void Open(ushort questId)
    {
        if (IsOpen)
        {
            Select(questId);
            return;
        }

        wanted = questId;
        Listen();
        Select(questId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (listening)
        {
            lifecycle.UnregisterListener(AddonEvent.PostSetup, DetailAddonName, OnReady);
            listening = false;
        }

        wanted = null;
    }

    private void Select(ushort questId)
    {
        var journal = AgentQuestJournal.Instance();
        if (journal == null)
        {
            log.Warning("the game's quest journal is not there yet, so nothing was opened.");
            return;
        }

        journal->OpenForQuest(QuestIds.RowId(questId), QuestIds.OrdinaryQuest);
    }

    private void Listen()
    {
        if (!listening)
        {
            lifecycle.RegisterListener(AddonEvent.PostSetup, DetailAddonName, OnReady);
            listening = true;
        }
    }

    /// <summary>The journal has finished opening. Whatever page it chose for itself, the quest the
    /// player asked for goes back on show.</summary>
    private void OnReady(AddonEvent type, AddonArgs args)
    {
        if (wanted is { } questId)
        {
            wanted = null;
            Select(questId);
        }
    }
}
