namespace Wayfarer.Core.Quests;

/// <summary>One of the game's emotes as its chat command names it, "/bow".</summary>
/// <param name="Id">The emote's id.</param>
/// <param name="Command">The command, with its slash.</param>
/// <param name="IconId">The emote's icon.</param>
public sealed record EmoteCommand(ushort Id, string Command, uint IconId);
