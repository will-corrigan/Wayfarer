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

    /// <summary>Whether the player is carrying a key item. Key items have their own bag, and a
    /// quest hands them out as the player gets to the step that needs them, so this is what tells
    /// a step that fetches one apart from the step that uses it. Not carrying it is the answer
    /// while the game has no inventory yet.</summary>
    public static bool Holds(uint keyItemId)
    {
        var inventory = InventoryManager.Instance();
        return inventory != null
            && inventory->GetItemCountInContainer(keyItemId, InventoryType.KeyItems, isHq: false, minCollectability: 0) > 0;
    }
}
