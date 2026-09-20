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
        IReadOnlyDictionary<string, EmoteCommand> emotes,
        IReadOnlyList<QuestItem>? questItems = null)
    {
        ArgumentNullException.ThrowIfNull(todoText);
        ArgumentNullException.ThrowIfNull(emotes);

        // Only an item the game gives something to do is offered for use. The rest are carried
        // and handed over, and a step that says to deliver one is not a step that uses it.
        if ((item ?? Named(todoText, questItems)) is { Usable: true } used)
        {
            return new EntryAction.UseItem(used.Id, used.Name, KeyItem: true);
        }

        if (SayPhrase().Match(todoText) is { Success: true } say)
        {
            return new EntryAction.Say(say.Groups[PhraseGroup].Value);
        }

        if (SlashCommand().Match(todoText) is { Success: true } command && emotes.TryGetValue(command.Value, out var emote))
        {
            return new EntryAction.Emote(emote.Id, emote.Command);
        }

        return null;
    }

    /// <summary>The quest's own key item a ToDo's words name, or null when they name none.
    ///
    /// <para>The event handler says which item a step is for when it is asked, and for a good many
    /// steps it says nothing. The quest still lists its items, and a step that wants one says so in
    /// its own words: "Use burlap sacks on weakened teleoceroses". The longest name that appears
    /// wins, so a large burlap sack is not mistaken for a burlap sack.</para></summary>
    private static QuestItem? Named(string todoText, IReadOnlyList<QuestItem>? questItems) =>
        questItems?
            .Where(item => item.Name.Length > 0 && todoText.Contains(item.Name, StringComparison.OrdinalIgnoreCase))
            .MaxBy(item => item.Name.Length);

    [GeneratedRegex("enter “(?<phrase>[^”]+)”", RegexOptions.ExplicitCapture, MatchTimeoutMilliseconds)]
    private static partial Regex SayPhrase();

    [GeneratedRegex("/[a-z]+", RegexOptions.ExplicitCapture, MatchTimeoutMilliseconds)]
    private static partial Regex SlashCommand();
}
