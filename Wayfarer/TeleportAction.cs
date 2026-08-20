using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Wayfarer;

/// <summary>The plugin's single game action (everything else is read-only).
/// One deliberate user click = one teleport cast. Called only from ArrowWindow.</summary>
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
            log.Warning($"Teleport refused: aetheryte {aetheryteId} is not attuned");
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
            log.Warning($"Teleport to aetheryte {aetheryteId} was rejected by the game");
        }
    }
}
