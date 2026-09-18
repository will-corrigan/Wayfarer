using Wayfarer.Core.Guidance;
using Wayfarer.Core.Quests;
using Wayfarer.Core.Routing;

namespace Wayfarer.Tests;

/// <summary>How a quest step becomes an objective. The fixtures are the shapes the quest sheet
/// really has: "Close to Home" with three lines in one step, and a single-line step.</summary>
public class QuestObjectiveBuilderTests
{
    private const uint Gridania = 132;

    private static readonly Place Aetheryte = new(Gridania, 2, 32.9f, 2.7f, 30f);
    private static readonly Place Lancers = new(133, 3, 147.1f, 15.5f, -268f);
    private static readonly Place Markets = new(133, 3, 172.4f, 15.5f, -89.9f);

    private static readonly QuestTodo[] CloseToHome =
    [
        new(0, 1, "Attune yourself to the aetheryte found inside the city.", false, 1, [Aetheryte]),
        new(1, 1, "Visit the Lancers' Guild.", false, 1, [Lancers]),
        new(2, 1, "Listen to Parsemontret's explanation of the markets.", false, 1, [Markets]),
        new(3, 255, "Report to Miounne at the Carline Canopy.", false, 1, [new Place(Gridania, 2, 23.8f, -8f, 115.9f)]),
    ];

    [Fact]
    public void Every_line_of_the_step_is_an_entry_while_its_marker_stands()
    {
        var markers = new QuestMarker[] { new(Aetheryte, null), new(Lancers, null), new(Markets, null) };

        var objective = QuestObjectiveBuilder.Build("Close to Home", 1, CloseToHome, markers);

        Assert.Equal("Close to Home", objective!.Headline);
        Assert.Equal(3, objective.Entries.Count);
        Assert.Equal("Visit the Lancers' Guild.", objective.Entries[1].Text);
        Assert.Equal([Lancers], Assert.IsType<Destination.Reachable>(objective.Entries[1].Where).Places);
    }

    [Fact]
    public void A_line_whose_marker_the_game_took_down_is_done_and_dropped()
    {
        var markers = new QuestMarker[] { new(Lancers, null), new(Markets, null) };

        var objective = QuestObjectiveBuilder.Build("Close to Home", 1, CloseToHome, markers);

        Assert.Equal(2, objective!.Entries.Count);
        Assert.DoesNotContain(objective.Entries, e => e.Text.StartsWith("Attune", StringComparison.Ordinal));
    }

    [Fact]
    public void Lines_of_other_steps_are_never_entries()
    {
        var markers = new QuestMarker[] { new(Aetheryte, null), new(Lancers, null), new(Markets, null) };

        var objective = QuestObjectiveBuilder.Build("Close to Home", 1, CloseToHome, markers);

        Assert.DoesNotContain(objective!.Entries, e => e.Text.StartsWith("Report", StringComparison.Ordinal));
    }

    [Fact]
    public void With_no_markers_at_all_the_authored_locations_stand_in()
    {
        var objective = QuestObjectiveBuilder.Build("Close to Home", 1, CloseToHome, []);

        Assert.Equal(3, objective!.Entries.Count);
        Assert.Equal([Aetheryte], Assert.IsType<Destination.Reachable>(objective.Entries[0].Where).Places);
    }

    [Fact]
    public void A_live_marker_wins_over_the_authored_location_it_stands_on()
    {
        // The game nudged the marker two yalms; the place handed on is the marker's, radius included.
        var live = Aetheryte with { X = Aetheryte.X + 2f, Radius = 30f };

        var objective = QuestObjectiveBuilder.Build("Close to Home", 1, CloseToHome, [new QuestMarker(live, null)]);

        Assert.Equal([live], Assert.IsType<Destination.Reachable>(objective!.Entries[0].Where).Places);
    }

    [Fact]
    public void A_placeholder_line_takes_the_markers_resolved_label()
    {
        var todos = new QuestTodo[] { new(0, 1, "Slay  karakul.", true, 3, [Lancers]) };
        var markers = new QuestMarker[] { new(Lancers, "Slay 2/3 karakul.") };

        var objective = QuestObjectiveBuilder.Build("Way of the Lancer", 1, todos, markers);

        Assert.Equal("Slay 2/3 karakul.", objective!.Entries[0].Text);
    }

    [Fact]
    public void A_line_with_no_location_anywhere_is_blocked_so_its_words_still_show()
    {
        var todos = new QuestTodo[] { new(0, 1, "Wait for nightfall.", false, 1, []) };

        var objective = QuestObjectiveBuilder.Build("A Vigil", 1, todos, []);

        var entry = Assert.Single(objective!.Entries);
        Assert.IsType<Destination.Blocked>(entry.Where);
    }

    [Fact]
    public void A_step_the_sheet_says_nothing_about_is_described_by_its_markers()
    {
        var markers = new QuestMarker[] { new(Lancers, "Speak with the guildmaster."), new(Markets, null) };

        var objective = QuestObjectiveBuilder.Build("Unwritten", 7, CloseToHome, markers);

        var entry = Assert.Single(objective!.Entries);
        Assert.Equal("Speak with the guildmaster.", entry.Text);
        Assert.Equal(2, Assert.IsType<Destination.Reachable>(entry.Where).Places.Count);
    }

    [Fact]
    public void Nothing_left_to_do_is_null()
    {
        Assert.Null(QuestObjectiveBuilder.Build("Unwritten", 7, CloseToHome, []));
    }
}
