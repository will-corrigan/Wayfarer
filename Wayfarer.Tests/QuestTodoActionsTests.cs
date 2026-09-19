using Wayfarer.Core.Guidance;
using Wayfarer.Core.Quests;

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

        Assert.Equal(new EntryAction.UseItem(2001346, "Linkpearl", 21001), action);
    }

    [Fact]
    public void A_key_item_the_words_name_is_used_when_the_handler_reports_none()
    {
        var carried = new[]
        {
            new QuestItem(2002547, "Smoke Bomb", 26015),
            new QuestItem(2002548, "Large Burlap Sack", 26110),
        };

        var action = QuestTodoActions.From("Use a smoke bomb on the beehive.", null, Emotes, carried);

        Assert.Equal(new EntryAction.UseItem(2002547, "Smoke Bomb", 26015), action);
    }

    [Fact]
    public void The_longest_named_key_item_wins()
    {
        var carried = new[]
        {
            new QuestItem(2002548, "Burlap Sack", 26110),
            new QuestItem(2002549, "Buzzing Burlap Sack", 25919),
        };

        var action = QuestTodoActions.From("Deliver the buzzing burlap sack to Rhalgr.", null, Emotes, carried);

        Assert.Equal(new EntryAction.UseItem(2002549, "Buzzing Burlap Sack", 25919), action);
    }

    [Fact]
    public void A_key_item_the_words_never_name_is_left_alone()
    {
        var carried = new[] { new QuestItem(2002547, "Smoke Bomb", 26015) };

        Assert.Null(QuestTodoActions.From("Speak with Momodi.", null, Emotes, carried));
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
