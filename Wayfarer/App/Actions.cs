using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Wayfarer.App;

/// <summary>The game's own agents and managers behind <see cref="IActions"/>. A refusal is logged,
/// because a press that does nothing in silence looks like a control that was never wired up.</summary>
internal sealed unsafe class Actions(IClientState clientState, IGameGui gameGui, IFaultLog faults) : IActions
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
            faults.Record("teleport", $"aetheryte {aetheryteId} is not attuned");
            return;
        }

        var telepo = Telepo.Instance();
        telepo->UpdateAetheryteList();
        if (!telepo->Teleport(aetheryteId, PlainAetheryte))
        {
            faults.Record("teleport", $"the game rejected the teleport to aetheryte {aetheryteId}");
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
            faults.Record("use item", $"the game did not use item {itemId}; is a suitable target selected?");
        }
    }

    /// <inheritdoc/>
    public void Emote(ushort emoteId)
    {
        var agent = AgentEmote.Instance();
        if (!agent->CanUseEmote(emoteId))
        {
            faults.Record("emote", $"emote {emoteId} cannot be used right now");
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
            faults.Record("say", "the chat window is not open");
            return;
        }

        chat->TextInput->SetText(text);
    }
}
