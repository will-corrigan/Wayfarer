namespace Wayfarer.Core.Quests;

/// <summary>A key item a ToDo has the player use.</summary>
/// <param name="Id">The item's id.</param>
/// <param name="Name">The item's name.</param>
/// <param name="IconId">The item's icon.</param>
public sealed record QuestItem(uint Id, string Name, uint IconId);
