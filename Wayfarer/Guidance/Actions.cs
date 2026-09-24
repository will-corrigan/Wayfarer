using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Wayfarer.App;

namespace Wayfarer.Guidance;

/// <summary>The game's own agents and managers behind <see cref="IActions"/>. A refusal is logged,
/// because a press that does nothing in silence looks like a control that was never wired up.
///
/// <para>Every one of the game's singletons is asked for fresh and checked before it is used: none
/// of them exist before the player is in the world, and a press can arrive at any moment.</para></summary>
internal sealed unsafe class Actions(IClientState clientState, IGameGui gameGui, ITargetManager targets, IPluginLog log) : IActions
{
    /// <summary>The sub-index is for aetherytes with several destinations, such as housing; every
    /// aetheryte on a route is a plain one.</summary>
    private const byte PlainAetheryte = 0;

    /// <summary>What the game means by nothing picked out. It is what an item is used on when it
    /// is not told otherwise, and an item that wants something to be used on is refused.</summary>
    private const ulong NoTarget = 0xE000_0000;

    /// <summary>The chat window, which is where a phrase the quest wants said is written.</summary>
    private static readonly string ChatLogAddon = GameAddon.NameOf<AddonChatLog>();

    /// <inheritdoc/>
    public void TeleportTo(uint aetheryteId)
    {
        if (!clientState.IsLoggedIn)
        {
            return;
        }

        if (!PlayerState.IsAttuned(aetheryteId))
        {
            log.Warning($"no teleport was cast: aetheryte {aetheryteId} is not attuned.");
            return;
        }

        var telepo = Telepo.Instance();
        if (Missing(telepo, "teleport list"))
        {
            return;
        }

        telepo->UpdateAetheryteList();
        if (!telepo->Teleport(aetheryteId, PlainAetheryte))
        {
            log.Warning($"the game rejected the teleport to aetheryte {aetheryteId}: not enough gil, in combat, or in a duty are the usual reasons.");
        }
    }

    /// <inheritdoc/>
    public void OpenDutyFinder(uint dutyId)
    {
        var finder = AgentContentsFinder.Instance();
        if (!Missing(finder, "Duty Finder"))
        {
            finder->OpenRegularDuty(dutyId, false);
        }
    }

    /// <inheritdoc/>
    public void OpenRoulette(byte rouletteId)
    {
        var finder = AgentContentsFinder.Instance();
        if (!Missing(finder, "Duty Finder"))
        {
            finder->OpenRouletteDuty(rouletteId, false);
        }
    }

    /// <inheritdoc/>
    public void UseItem(uint itemId, bool keyItem)
    {
        var actionManager = ActionManager.Instance();
        if (Missing(actionManager, "action manager"))
        {
            return;
        }

        // A step that has the player use an item on something means the thing they have picked
        // out. Left to itself the game uses it on nothing and refuses, which is what a burlap sack
        // and a targeted beast look like when the two are never introduced.
        var kind = keyItem ? ActionType.EventItem : ActionType.Item;
        if (!actionManager->UseAction(kind, itemId, targets.Target?.GameObjectId ?? NoTarget))
        {
            log.Warning($"the game did not use item {itemId}: the usual reason is that nothing suitable is targeted.");
        }
    }

    /// <inheritdoc/>
    public void Emote(ushort emoteId)
    {
        var agent = AgentEmote.Instance();
        if (Missing(agent, "emote list"))
        {
            return;
        }

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

    /// <summary>Whether one of the game's own singletons is not there, which it is not until the
    /// player is in the world. Says so in the log, because a press that quietly did nothing is
    /// indistinguishable from a control that was never wired up.</summary>
    private bool Missing<T>(T* instance, string what)
        where T : unmanaged
    {
        if (instance != null)
        {
            return false;
        }

        log.Warning($"the game's {what} is not there yet, so nothing happened.");
        return true;
    }
}
