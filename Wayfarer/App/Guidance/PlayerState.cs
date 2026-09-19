using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Wayfarer.App.Guidance;

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
}
