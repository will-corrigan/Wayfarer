using Dalamud.Utility;

namespace Wayfarer.Modules.Quests;

/// <summary>A key item a ToDo has the player use.</summary>
/// <param name="Id">The item's id.</param>
/// <param name="Name">The item's name.</param>
/// <param name="IconId">The item's icon.</param>
internal sealed record QuestItem(uint Id, string Name, uint IconId)
{
    /// <summary>Whether an id is a key item's rather than an ordinary item's. Key items are what the
    /// game calls event items: they live in their own range and are used through a different action
    /// kind, so a ToDo's ordinary item is a turn-in and not a use. Dalamud knows where that range
    /// is and that an id inside it is really one of them.</summary>
    public static bool IsKeyItem(uint itemId) => ItemUtil.IsEventItem(itemId);
}
