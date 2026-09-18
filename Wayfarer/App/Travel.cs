using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Wayfarer.App;

/// <summary>The game's own teleport and Duty Finder agents, behind <see cref="ITravel"/>. Every
/// refusal explains itself in the log, because a press that does nothing in silence is
/// indistinguishable from a control that was never wired up.</summary>
internal sealed unsafe class Travel(IClientState clientState, IPluginLog log) : ITravel
{
    /// <summary>The game's teleport takes a sub-index for aetherytes with several destinations,
    /// such as housing; every aetheryte on a route is a plain one.</summary>
    private const byte PlainAetheryte = 0;

    /// <inheritdoc/>
    public void TeleportTo(uint aetheryteId)
    {
        if (!clientState.IsLoggedIn)
        {
            return;
        }

        var ui = UIState.Instance();
        if (ui == null || !ui->IsAetheryteUnlocked(aetheryteId))
        {
            log.Warning($"Wayfarer: no teleport was cast: aetheryte {aetheryteId} is not attuned.");
            return;
        }

        var telepo = Telepo.Instance();
        if (telepo == null)
        {
            log.Warning("Wayfarer: no teleport was cast: the game's teleport service could not be reached. Try again in a moment.");
            return;
        }

        telepo->UpdateAetheryteList();
        if (!telepo->Teleport(aetheryteId, PlainAetheryte))
        {
            log.Warning($"Wayfarer: the game rejected the teleport to aetheryte {aetheryteId}: not enough gil, in combat, or in a duty are the usual reasons.");
        }
    }

    /// <inheritdoc/>
    public void OpenDutyFinder(uint dutyId)
    {
        var agent = AgentContentsFinder.Instance();
        if (agent != null)
        {
            agent->OpenRegularDuty(dutyId, false);
        }
    }
}
