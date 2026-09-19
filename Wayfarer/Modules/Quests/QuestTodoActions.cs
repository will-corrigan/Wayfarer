using System.Text.RegularExpressions;
using Wayfarer.Guidance;

namespace Wayfarer.Modules.Quests;

/// <summary>What a ToDo has the player do, read from the only places the game says it: the key
/// item the quest's handler reports, the key item its words name, or the ToDo's own words. An
/// emote ToDo names its command in the text, "Greet Aunillie with a /bow."; a say ToDo always
/// reads "With the chat mode in Say, enter “Well met!” to ...".</summary>
internal static partial class QuestTodoActions
{
    private const int MatchTimeoutMilliseconds = 100;
    private const string PhraseGroup = "phrase";

    /// <summary>What a ToDo has the player do besides be somewhere, or nothing when it only asks
    /// them to be there. Read from the only places the game says it: the key item the ToDo carries,
    /// one of the quest's own key items its words name, and the words themselves, which name an
    /// emote or quote a phrase when they need one.</summary>
    public static EntryAction? From(
        string todoText,
        QuestItem? item,
        IReadOnlyDictionary<string, EmoteCommand> emotes)
    {
        ArgumentNullException.ThrowIfNull(todoText);
        ArgumentNullException.ThrowIfNull(emotes);

        if (item is { } used)
        {
            return new EntryAction.UseItem(used.Id, used.Name, used.IconId, KeyItem: true);
        }

        if (SayPhrase().Match(todoText) is { Success: true } say)
        {
            return new EntryAction.Say(say.Groups[PhraseGroup].Value);
        }

        if (SlashCommand().Match(todoText) is { Success: true } command && emotes.TryGetValue(command.Value, out var emote))
        {
            return new EntryAction.Emote(emote.Id, emote.Command, emote.IconId);
        }

        return null;
    }

    [GeneratedRegex("enter “(?<phrase>[^”]+)”", RegexOptions.ExplicitCapture, MatchTimeoutMilliseconds)]
    private static partial Regex SayPhrase();

    [GeneratedRegex("/[a-z]+", RegexOptions.ExplicitCapture, MatchTimeoutMilliseconds)]
    private static partial Regex SlashCommand();
}
