using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Wayfarer.App;

/// <summary>The game's own teleport and Duty Finder agents behind <see cref="ITravel"/>. A refusal
/// is logged, because a press that does nothing in silence looks like a control that was never
/// wired up.</summary>
internal sealed unsafe class Travel(IClientState clientState, IPluginLog log) : ITravel
{
    /// <summary>The sub-index is for aetherytes with several destinations, such as housing; every
    /// aetheryte on a route is a plain one.</summary>
    private const byte PlainAetheryte = 0;

    /// <inheritdoc/>
    public void TeleportTo(uint aetheryteId)
    {
        if (!clientState.IsLoggedIn)
        {
            return;
        }

        if (!UIState.Instance()->IsAetheryteUnlocked(aetheryteId))
        {
            log.Warning($"Wayfarer: no teleport was cast: aetheryte {aetheryteId} is not attuned.");
            return;
        }

        var telepo = Telepo.Instance();
        telepo->UpdateAetheryteList();
        if (!telepo->Teleport(aetheryteId, PlainAetheryte))
        {
            log.Warning($"Wayfarer: the game rejected the teleport to aetheryte {aetheryteId}: not enough gil, in combat, or in a duty are the usual reasons.");
        }
    }

    /// <inheritdoc/>
    public void OpenDutyFinder(uint dutyId) => AgentContentsFinder.Instance()->OpenRegularDuty(dutyId, false);
}
