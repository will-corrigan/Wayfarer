using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Wayfarer.Guidance;

/// <summary>What the game knows about the player that guidance depends on. Asked fresh every time
/// and guarded here, because none of the game's own state exists before the player is in the world.</summary>
internal static unsafe class PlayerState
{
    /// <summary>Whether the player has attuned to an aetheryte, which is what decides whether a
    /// route may teleport through it. Not attuned is the answer while the game has no state yet,
    /// so a route found too early walks rather than teleports.</summary>
    public static bool IsAttuned(uint aetheryteId)
    {
        var state = UIState.Instance();
        return state != null && state->IsAetheryteUnlocked(aetheryteId);
    }

    /// <summary>Whether the player has completed a quest, which is what decides whether a door kept
    /// until one is done may be taken. Not done is the answer while the game has no state yet.</summary>
    public static bool IsQuestComplete(uint questId) =>
        QuestManager.Instance() != null && QuestManager.IsQuestComplete(questId);

    /// <summary>Whether a seasonal event is running, in the given phase or in any when the phase is
    /// zero, which is what decides whether a door only there during it may be taken. The game
    /// holds the events it is running itself. Not running is the answer while it has no state yet.</summary>
    public static bool IsFestivalOn(ushort festival, ushort phase)
    {
        var game = GameMain.Instance();
        if (game == null)
        {
            return false;
        }

        foreach (var running in game->ActiveFestivals)
        {
            if (running.Id == festival && (phase == 0 || running.Phase == phase))
            {
                return true;
            }
        }

        return false;
    }
}
