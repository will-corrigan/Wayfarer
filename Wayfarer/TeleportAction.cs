using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Wayfarer;

/// <summary>The plugin's only SERVER-affecting action (everything else is read-only;
/// client UI navigation, like opening the Duty Finder, is permitted — it doesn't
/// touch the game's simulation, just the UI). One deliberate user click = one
/// teleport cast. Called from every surface that offers the teleport: the readout, the
/// window's Quests tab, the game's context menu and the ImGui fallback.</summary>
internal static unsafe class TeleportAction
{
    public static void Execute(uint aetheryteId, QuestHelperConfig cfg, IClientState clientState, IPluginLog log)
    {
        if (!cfg.ClickTeleportEnabled || !clientState.IsLoggedIn)
        {
            return;
        }

        var ui = UIState.Instance();
        if (ui == null || !ui->IsAetheryteUnlocked(aetheryteId))
        {
            log.Warning(
                $"Wayfarer: no teleport was cast — aetheryte {aetheryteId} is not attuned, so the route still "
                + "stands but you will have to travel to it yourself.");
            return;
        }

        var telepo = Telepo.Instance();
        if (telepo == null)
        {
            return;
        }

        telepo->UpdateAetheryteList();
        if (!telepo->Teleport(aetheryteId, 0))
        {
            log.Warning(
                $"Wayfarer: the game rejected the teleport to aetheryte {aetheryteId} — nothing was cast, and "
                + "the usual reasons (not enough gil, in combat, in a duty) are the ones to check first.");
        }
    }
}
