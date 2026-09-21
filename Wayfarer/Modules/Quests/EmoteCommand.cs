namespace Wayfarer.Modules.Quests;

/// <summary>One of the game's emotes as its chat command names it, "/bow".</summary>
/// <param name="Id">The emote's id.</param>
/// <param name="Command">The command, with its slash.</param>
internal sealed record EmoteCommand(ushort Id, string Command);
