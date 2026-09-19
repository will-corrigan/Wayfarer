namespace Wayfarer.App.Guidance;

/// <summary>Everything a press on the guidance block can do in the game. Each is one deliberate
/// press for one action; nothing here repeats, queues or automates. The teleport is the only one
/// that reaches the server; the rest are the game's own client-side commands, and filling the chat
/// box leaves sending to the player.</summary>
internal interface IActions
{
    /// <summary>Casts a teleport to the aetheryte, if attuned and allowed now. A refusal is logged.</summary>
    void TeleportTo(uint aetheryteId);

    /// <summary>Opens the game's Duty Finder at this duty, ready to queue.</summary>
    void OpenDutyFinder(uint dutyId);

    /// <summary>Uses a key item on the player's current target.</summary>
    void UseItem(uint itemId);

    /// <summary>Performs an emote, at the player's current target if any.</summary>
    void Emote(ushort emoteId);

    /// <summary>Writes text into the chat box without sending it.</summary>
    void FillChat(string text);

    /// <summary>Opens the quest journal at a quest.</summary>
    void OpenQuestJournal(ushort questId);
}
