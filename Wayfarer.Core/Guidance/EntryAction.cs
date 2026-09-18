namespace Wayfarer.Core.Guidance;

/// <summary>Something the player has to do at an entry besides be there: use a key item, perform
/// an emote, say a phrase. The module that knows the quest fills it in; the surface offers it as a
/// press; the app performs it. One press, one action, never more.</summary>
public abstract record EntryAction
{
    private EntryAction()
    {
    }

    /// <summary>Use a key item, on whatever the player has targeted.</summary>
    /// <param name="ItemId">The item's id.</param>
    /// <param name="Name">The item's name.</param>
    /// <param name="IconId">The item's icon.</param>
    public sealed record UseItem(uint ItemId, string Name, uint IconId) : EntryAction;

    /// <summary>Perform an emote, at whatever the player has targeted.</summary>
    /// <param name="EmoteId">The emote's id.</param>
    /// <param name="Command">The emote's chat command, "/bow".</param>
    /// <param name="IconId">The emote's icon.</param>
    public sealed record Emote(ushort EmoteId, string Command, uint IconId) : EntryAction;

    /// <summary>Say a phrase in chat. The press fills the chat box; the player sends it.</summary>
    /// <param name="Phrase">The exact words the quest wants.</param>
    public sealed record Say(string Phrase) : EntryAction;
}
