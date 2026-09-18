using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Wayfarer.App.Guidance;

/// <summary>The game's own agents and managers behind <see cref="IActions"/>. A refusal is logged,
/// because a press that does nothing in silence looks like a control that was never wired up.</summary>
internal sealed unsafe class Actions(IClientState clientState, IGameGui gameGui, IPluginLog log) : IActions
{
    /// <summary>The sub-index is for aetherytes with several destinations, such as housing; every
    /// aetheryte on a route is a plain one.</summary>
    private const byte PlainAetheryte = 0;

    /// <summary>Key items live in their own id range and are used as event items.</summary>
    private const uint FirstEventItemId = 2_000_000;

    private const string ChatLogAddon = "ChatLog";

    /// <inheritdoc/>
    public void TeleportTo(uint aetheryteId)
    {
        if (!clientState.IsLoggedIn)
        {
            return;
        }

        if (!UIState.Instance()->IsAetheryteUnlocked(aetheryteId))
        {
            log.Warning($"no teleport was cast: aetheryte {aetheryteId} is not attuned.");
            return;
        }

        var telepo = Telepo.Instance();
        telepo->UpdateAetheryteList();
        if (!telepo->Teleport(aetheryteId, PlainAetheryte))
        {
            log.Warning($"the game rejected the teleport to aetheryte {aetheryteId}: not enough gil, in combat, or in a duty are the usual reasons.");
        }
    }

    /// <inheritdoc/>
    public void OpenDutyFinder(uint dutyId) => AgentContentsFinder.Instance()->OpenRegularDuty(dutyId, false);

    /// <inheritdoc/>
    public void UseItem(uint itemId)
    {
        var kind = itemId >= FirstEventItemId ? ActionType.EventItem : ActionType.Item;
        if (!ActionManager.Instance()->UseAction(kind, itemId))
        {
            log.Warning($"the game did not use item {itemId}: the usual reason is that nothing suitable is targeted.");
        }
    }

    /// <inheritdoc/>
    public void Emote(ushort emoteId)
    {
        var agent = AgentEmote.Instance();
        if (!agent->CanUseEmote(emoteId))
        {
            log.Warning($"emote {emoteId} cannot be used right now.");
            return;
        }

        agent->ExecuteEmote(emoteId, null, addToHistory: false, liveUpdateHistory: false);
    }

    /// <inheritdoc/>
    public void FillChat(string text)
    {
        var chat = (AddonChatLog*)gameGui.GetAddonByName(ChatLogAddon).Address;
        if (chat == null || chat->TextInput == null)
        {
            log.Warning("the chat window is not open, so the phrase could not be written into it.");
            return;
        }

        chat->TextInput->SetText(text);
    }
}
