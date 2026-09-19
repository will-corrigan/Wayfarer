namespace Wayfarer.Guidance;

/// <summary>Something the player has to do at an entry besides be there: use a key item, perform
/// an emote, say a phrase. The module that knows the quest fills it in; the surface offers it as a
/// press; the app performs it. One press, one action, never more.</summary>
public abstract record EntryAction
{
    private EntryAction()
    {
    }

    /// <summary>The words in the ToDo's own sentence that name this action: the item, the emote's
    /// command, the phrase to say. A surface lights and presses these words rather than the whole
    /// sentence, so the player can see what the press is about.</summary>
    public abstract string Keyword { get; }

    /// <summary>Use a key item, on whatever the player has targeted.</summary>
    /// <param name="ItemId">The item's id.</param>
    /// <param name="Name">The item's name.</param>
    /// <param name="IconId">The item's icon.</param>
    /// <param name="KeyItem">Whether it is a key item, which the game uses through its own action
    /// kind. The module that found the item knows which it is; nothing else has to work it out.</param>
    public sealed record UseItem(uint ItemId, string Name, uint IconId, bool KeyItem) : EntryAction
    {
        /// <inheritdoc/>
        public override string Keyword => Name;
    }

    /// <summary>Perform an emote, at whatever the player has targeted.</summary>
    /// <param name="EmoteId">The emote's id.</param>
    /// <param name="Command">The emote's chat command, "/bow".</param>
    /// <param name="IconId">The emote's icon.</param>
    public sealed record Emote(ushort EmoteId, string Command, uint IconId) : EntryAction
    {
        /// <inheritdoc/>
        public override string Keyword => Command;
    }

    /// <summary>Something only the module that produced the step can do: opening its own log, its
    /// own window, its own page. The app performs nothing and is told nothing about it; the source
    /// is asked to do it itself through <see cref="IObjectiveSource.PressEntry"/>. The words and the
    /// icon are still here, because the surface has to draw the line whoever performs it.</summary>
    /// <param name="Words">The words in the sentence that name what the press does.</param>
    /// <param name="IconId">An icon to draw in front of those words, or null.</param>
    public sealed record Own(string Words, uint? IconId) : EntryAction
    {
        /// <inheritdoc/>
        public override string Keyword => Words;
    }

    /// <summary>Say a phrase in chat. The press fills the chat box; the player sends it.</summary>
    /// <param name="Phrase">The exact words the quest wants.</param>
    public sealed record Say(string Phrase) : EntryAction
    {
        /// <inheritdoc/>
        public override string Keyword => Phrase;
    }
}
