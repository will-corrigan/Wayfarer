using Wayfarer.Guidance;
using Wayfarer.Modules.Quests;
using Wayfarer.Routing;

namespace Wayfarer.Tests;

/// <summary>How a quest step becomes an objective. The fixtures are the shapes the quest sheet
/// really has: "Close to Home" with three ToDos in one step, and a kill count.</summary>
public class QuestObjectiveBuilderTests
{
    private const uint Gridania = 132;

    /// <summary>The Limitless Blue (Extreme), as the Duty Finder numbers it.</summary>
    private const uint Bismarck = 60u;

    /// <summary>The territory the Limitless Blue (Extreme) runs in, which is inside the duty.</summary>
    private const uint BismarckTerritory = 431u;

    private static readonly Place Aetheryte = new(Gridania, 2, 32.9f, 2.7f, 30f);
    private static readonly Place Lancers = new(133, 3, 147.1f, 15.5f, -268f);
    private static readonly Place Markets = new(133, 3, 172.4f, 15.5f, -89.9f);

    private static readonly QuestTodo[] CloseToHome =
    [
        new(0, 1, "Attune yourself to the aetheryte found inside the city.", 1, [Aetheryte]),
        new(1, 1, "Visit the Lancers' Guild.", 1, [Lancers]),
        new(2, 1, "Listen to Parsemontret's explanation of the markets.", 1, [Markets]),
        new(3, 255, "Report to Miounne at the Carline Canopy.", 1, [new Place(Gridania, 2, 23.8f, -8f, 115.9f)]),
    ];

    private static readonly QuestMarker[] AllThreeMarkers = [new(Aetheryte, null), new(Lancers, null), new(Markets, null)];

    [Fact]
    public void Every_todo_of_the_step_is_an_entry_while_none_is_done()
    {
        var objective = QuestObjectiveBuilder.Build("Close to Home", 1, CloseToHome, [], AllThreeMarkers);

        Assert.Equal("Close to Home", objective!.Headline);
        Assert.Equal(3, objective.Entries.Count);
        Assert.Equal("Visit the Lancers' Guild.", objective.Entries[1].Text);
        Assert.Equal([Lancers], Assert.IsType<Destination.Reachable>(objective.Entries[1].Where).Places);
    }

    [Fact]
    public void A_todo_the_game_has_ticked_is_dropped()
    {
        var progress = new QuestTodoProgress[] { new(0, Done: true, 1, 1) };

        var objective = QuestObjectiveBuilder.Build("Close to Home", 1, CloseToHome, progress, AllThreeMarkers);

        Assert.Equal(2, objective!.Entries.Count);
        Assert.DoesNotContain(objective.Entries, e => e.Text.StartsWith("Attune", StringComparison.Ordinal));
    }

    [Fact]
    public void Todos_of_other_steps_are_never_entries()
    {
        var objective = QuestObjectiveBuilder.Build("Close to Home", 1, CloseToHome, [], AllThreeMarkers);

        Assert.DoesNotContain(objective!.Entries, e => e.Text.StartsWith("Report", StringComparison.Ordinal));
    }

    [Fact]
    public void A_quest_marker_is_preferred_over_the_position_it_stands_on()
    {
        var live = Aetheryte with { X = Aetheryte.X + 2f, Radius = 30f };

        var objective = QuestObjectiveBuilder.Build("Close to Home", 1, CloseToHome, [], [new QuestMarker(live, null)]);

        Assert.Equal([live], Assert.IsType<Destination.Reachable>(objective!.Entries[0].Where).Places);
        Assert.Equal([Lancers], Assert.IsType<Destination.Reachable>(objective.Entries[1].Where).Places);
    }

    [Fact]
    public void A_kill_count_shows_have_over_needed()
    {
        var todos = new QuestTodo[] { new(2, 3, "Slay opo-opos.", 8, [Lancers]) };
        var progress = new QuestTodoProgress[] { new(2, Done: false, 3, 8) };

        var objective = QuestObjectiveBuilder.Build("A Matter of Perspective", 3, todos, progress, []);

        Assert.Equal(new Progress(3, 8), objective!.Entries[0].Progress);
    }

    [Fact]
    public void A_count_the_game_has_not_reported_yet_starts_at_zero_of_the_data_quantity()
    {
        var todos = new QuestTodo[] { new(2, 3, "Slay opo-opos.", 8, [Lancers]) };

        var objective = QuestObjectiveBuilder.Build("A Matter of Perspective", 3, todos, [], []);

        Assert.Equal(new Progress(0, 8), objective!.Entries[0].Progress);
    }

    [Fact]
    public void A_plain_todo_has_no_count()
    {
        var objective = QuestObjectiveBuilder.Build("Close to Home", 1, CloseToHome, [new QuestTodoProgress(1, false, 0, 1)], []);

        Assert.All(objective!.Entries, e => Assert.Null(e.Progress));
    }

    [Fact]
    public void A_markers_label_never_replaces_a_todos_own_words()
    {
        var todos = new QuestTodo[] { new(0, 1, "Slay 2 of 3 karakul.", 3, [Lancers]) };
        var markers = new QuestMarker[] { new(Lancers, "Central Shroud") };

        var objective = QuestObjectiveBuilder.Build("Way of the Lancer", 1, todos, [], markers);

        Assert.Equal("Slay 2 of 3 karakul.", objective!.Entries[0].Text);
    }

    [Fact]
    public void A_todo_with_no_position_anywhere_is_blocked_so_its_words_still_show()
    {
        var todos = new QuestTodo[] { new(0, 1, "Wait for nightfall.", 1, []) };

        var objective = QuestObjectiveBuilder.Build("A Vigil", 1, todos, [], []);

        var entry = Assert.Single(objective!.Entries);
        Assert.IsType<Destination.Blocked>(entry.Where);
    }

    [Fact]
    public void A_step_the_sheet_says_nothing_about_is_described_by_its_markers()
    {
        var markers = new QuestMarker[] { new(Lancers, "Speak with the guildmaster."), new(Markets, null) };

        var objective = QuestObjectiveBuilder.Build("Unwritten", 7, CloseToHome, [], markers);

        var entry = Assert.Single(objective!.Entries);
        Assert.Equal("Speak with the guildmaster.", entry.Text);
        Assert.Equal(2, Assert.IsType<Destination.Reachable>(entry.Where).Places.Count);
    }

    [Fact]
    public void Nothing_left_to_do_is_null()
    {
        var allDone = CloseToHome.Select(todo => new QuestTodoProgress(todo.Index, true, 1, 1)).ToList();

        Assert.Null(QuestObjectiveBuilder.Build("Close to Home", 1, CloseToHome, allDone, []));
        Assert.Null(QuestObjectiveBuilder.Build("Unwritten", 7, CloseToHome, [], []));
    }

    [Fact]
    public void A_step_with_nowhere_to_go_is_inside_the_quests_duty()
    {
        QuestTodo[] confront = [new(0, 1, "Confront Bismarck in the Limitless Blue (Extreme).", 1, [])];

        var objective = QuestObjectiveBuilder.Build("The Diabolical Bismarck", 1, confront, [], [], duty: Bismarck);

        var entry = Assert.Single(objective!.Entries);
        Assert.Equal(Bismarck, Assert.IsType<Destination.InDuty>(entry.Where).DutyId);
    }

    [Fact]
    public void A_step_with_nowhere_to_go_and_no_duty_says_so()
    {
        QuestTodo[] nowhere = [new(0, 1, "Wait.", 1, [])];

        var objective = QuestObjectiveBuilder.Build("Waiting", 1, nowhere, [], []);

        var entry = Assert.Single(objective!.Entries);
        Assert.IsType<Destination.Blocked>(entry.Where);
    }

    [Fact]
    public void A_quest_the_sheet_says_nothing_about_is_its_duty()
    {
        var objective = QuestObjectiveBuilder.Build("The Diabolical Bismarck", 7, CloseToHome, [], [], duty: Bismarck);

        var entry = Assert.Single(objective!.Entries);
        Assert.Equal("The Diabolical Bismarck", entry.Text);
        Assert.Equal(Bismarck, Assert.IsType<Destination.InDuty>(entry.Where).DutyId);
    }

    [Fact]
    public void A_step_the_data_puts_inside_the_duty_is_the_duty()
    {
        Place inside = new(BismarckTerritory, 0, 200.1f, 0f, 230.2f);
        QuestTodo[] confront = [new(0, 1, "Do battle with Bismarck.", 1, [inside])];

        var objective = QuestObjectiveBuilder.Build("The Diabolical Bismarck", 1, confront, [], [], duty: Bismarck, dutyTerritory: BismarckTerritory);

        var entry = Assert.Single(objective!.Entries);
        Assert.Equal(Bismarck, Assert.IsType<Destination.InDuty>(entry.Where).DutyId);
    }

    [Fact]
    public void A_step_outside_the_duty_is_a_place_even_when_the_quest_has_one()
    {
        QuestTodo[] speak = [new(0, 1, "Speak with the Admiral.", 1, [Lancers])];

        var objective = QuestObjectiveBuilder.Build("The Diabolical Bismarck", 1, speak, [], [], duty: Bismarck, dutyTerritory: BismarckTerritory);

        var entry = Assert.Single(objective!.Entries);
        var where = Assert.IsType<Destination.Reachable>(entry.Where);
        Assert.Equal(Lancers, Assert.Single(where.Places));
    }
}
