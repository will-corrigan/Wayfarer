using Wayfarer.Guidance;
using Wayfarer.Modules.Quests;

namespace Wayfarer.Tests;

/// <summary>What a ToDo's words and item say the player has to do. The texts are the game's own.</summary>
public class QuestTodoActionsTests
{
    private static readonly Dictionary<string, EmoteCommand> Emotes = new(StringComparer.Ordinal)
    {
        ["/bow"] = new(5, "/bow"),
        ["/cheer"] = new(6, "/cheer"),
    };

    [Fact]
    public void A_key_item_is_used()
    {
        var action = QuestTodoActions.From("Use the linkpearl.", new QuestItem(2001346, "Linkpearl", Usable: true), Emotes);

        Assert.Equal(new EntryAction.UseItem(2001346, "Linkpearl", KeyItem: true), action);
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

        Assert.Equal(new EntryAction.Emote(5, "/bow"), action);
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

    [Fact]
    public void A_step_naming_one_of_the_quests_key_items_uses_it()
    {
        QuestItem[] items = [new(2002324, "Burlap Sack", Usable: true)];

        var action = QuestTodoActions.From("Use burlap sacks on weakened teleoceroses.", null, Emotes, items);

        var use = Assert.IsType<EntryAction.UseItem>(action);
        Assert.Equal(2002324u, use.ItemId);
        Assert.True(use.KeyItem);
    }

    [Fact]
    public void What_the_handler_says_beats_what_the_words_say()
    {
        QuestItem[] items = [new(2002324, "Burlap Sack", Usable: true)];
        var reported = new QuestItem(2002325u, "Large Burlap Sack", Usable: true);

        var action = QuestTodoActions.From("Use burlap sacks on weakened teleoceroses.", reported, Emotes, items);

        Assert.Equal(2002325u, Assert.IsType<EntryAction.UseItem>(action).ItemId);
    }

    [Fact]
    public void The_longest_name_the_words_hold_wins()
    {
        QuestItem[] items = [new(1, "Burlap Sack", Usable: true), new(2, "Large Burlap Sack", Usable: true)];

        var action = QuestTodoActions.From("Use large burlap sacks on the beast.", null, Emotes, items);

        Assert.Equal(2u, Assert.IsType<EntryAction.UseItem>(action).ItemId);
    }

    [Fact]
    public void An_item_with_nothing_to_do_is_delivered_rather_than_used()
    {
        QuestItem[] items = [new(2002325, "Dinosaur-filled Sack", Usable: false)];

        Assert.Null(QuestTodoActions.From("Deliver the dinosaur-filled sacks to M'zimzizi.", null, Emotes, items));
    }

    [Fact]
    public void A_step_naming_no_item_of_the_quests_uses_none()
    {
        QuestItem[] items = [new(2002324, "Burlap Sack", Usable: true)];

        Assert.Null(QuestTodoActions.From("Speak with Sarisha.", null, Emotes, items));
    }
}
