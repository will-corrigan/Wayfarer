namespace Wayfarer.Modules.Quests;

/// <summary>A key item a ToDo has the player use.</summary>
/// <param name="Id">The item's id.</param>
/// <param name="Name">The item's name.</param>
/// <param name="IconId">The item's icon.</param>
internal sealed record QuestItem(uint Id, string Name, uint IconId)
{
    /// <summary>Key items live in their own id range above the ordinary ones, and the game uses
    /// them through a different action kind. A ToDo's item below the range is a turn-in, not a use.</summary>
    private const uint FirstKeyItemId = 2_000_000;

    /// <inheritdoc cref="FirstKeyItemId"/>
    public static bool IsKeyItem(uint itemId) => itemId >= FirstKeyItemId;
}
