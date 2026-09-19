using Wayfarer.Guidance;
using Wayfarer.Modules.Quests;

namespace Wayfarer.Tests;

/// <summary>What a ToDo's words and item say the player has to do. The texts are the game's own.</summary>
public class QuestTodoActionsTests
{
    private static readonly Dictionary<string, EmoteCommand> Emotes = new(StringComparer.Ordinal)
    {
        ["/bow"] = new(5, "/bow", 64005),
        ["/cheer"] = new(6, "/cheer", 64006),
    };

    [Fact]
    public void A_key_item_is_used()
    {
        var action = QuestTodoActions.From("Use the linkpearl.", new QuestItem(2001346, "Linkpearl", 21001), Emotes);

        Assert.Equal(new EntryAction.UseItem(2001346, "Linkpearl", 21001, KeyItem: true), action);
    }

    [Fact]
    public void A_say_todo_carries_the_exact_phrase()
    {
        var action = QuestTodoActions.From("With the chat mode in Say, enter “Well met!” to greet Botulf.", null, Emotes);

        Assert.Equal(new EntryAction.Say("Well met!"), action);
    }

    [Fact]
    public void An_emote_todo_names_its_command()
    {
        var action = QuestTodoActions.From("Greet Aunillie with a /bow.", null, Emotes);

        Assert.Equal(new EntryAction.Emote(5, "/bow", 64005), action);
    }

    [Fact]
    public void A_command_that_is_not_an_emote_is_nothing()
    {
        Assert.Null(QuestTodoActions.From("Type /shout and hope.", null, Emotes));
    }

    [Fact]
    public void A_plain_todo_has_no_action()
    {
        Assert.Null(QuestTodoActions.From("Speak with Momodi.", null, Emotes));
    }
}
