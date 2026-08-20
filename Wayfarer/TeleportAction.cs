using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Wayfarer;

/// <summary>The plugin's single game action (everything else is read-only).
/// One deliberate user click = one teleport cast. Called only from ArrowWindow.</summary>
internal static unsafe class TeleportAction
{
    public static void Execute(uint aetheryteId, Plugin plugin)
    {
        if (!plugin.Config.QuestHelper.ClickTeleportEnabled || !plugin.ClientState.IsLoggedIn)
        {
            return;
        }

        var ui = UIState.Instance();
        if (ui == null || !ui->IsAetheryteUnlocked(aetheryteId))
        {
            plugin.Log.Warning($"Teleport refused: aetheryte {aetheryteId} is not attuned");
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
            plugin.Log.Warning($"Teleport to aetheryte {aetheryteId} was rejected by the game");
        }
    }
}
