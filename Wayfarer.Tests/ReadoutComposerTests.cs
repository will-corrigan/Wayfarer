using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

public class ReadoutComposerTests
{
    [Fact]
    public void A_hidden_snapshot_draws_nothing_at_all()
    {
        var content = ReadoutComposer.Compose(Inputs(new NavigationState { Mode = NavigationState.Modes.Hidden }));

        Assert.True(content.IsEmpty);
        Assert.False(content.ShowArrow);
    }

    [Fact]
    public void The_heading_names_the_active_mode()
    {
        var content = ReadoutComposer.Compose(Inputs(Engaged("Hunting Log · Gladiator")));

        var heading = Assert.Single(content.Lines, line => line.Emphasis == ReadoutEmphasis.Heading);
        Assert.Equal("Hunting Log · Gladiator", heading.Text);
    }

    [Fact]
    public void There_is_never_more_than_one_heading()
    {
        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = Engaged("Unlock route"),
            AmbientObjectiveName = "A Realm Reborn",
            HuntingSummary = "Ornery Karakul 2/3",
            NearbyUnlocks = ["Chocobo racing"],
            DistanceYalms = 120f,
        });

        Assert.Single(content.Lines, line => line.Emphasis == ReadoutEmphasis.Heading);
    }

    [Fact]
    public void Chain_progress_rides_on_the_mode_line_rather_than_the_objective()
    {
        var state = Engaged("Hunting Log · Gladiator") with { RouteStop = 3, RouteTotal = 11 };

        var content = ReadoutComposer.Compose(Inputs(state));

        Assert.Equal("Hunting Log · Gladiator — 3 of 11", content.Lines[0].Text);
    }

    [Fact]
    public void The_ambient_objective_is_demoted_and_fenced_off_while_a_mode_is_engaged()
    {
        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = Engaged("Hunting Log · Gladiator"),
            AmbientObjectiveName = "The Ul'dahn Envoy",
        });

        var ambient = Assert.Single(content.Lines, line => line.Text.Contains("Ul'dahn", StringComparison.Ordinal));
        Assert.Equal(ReadoutEmphasis.Muted, ambient.Emphasis);
        Assert.True(ambient.Separated, "the subordinate block must be fenced off from the active objective");
        Assert.Equal("Main Scenario: The Ul'dahn Envoy", ambient.Text);

        // ...and it is never drawn with the same weight as the thing the arrow follows.
        Assert.DoesNotContain(content.Lines, line => line.Emphasis == ReadoutEmphasis.Primary && line.Text.Contains("Ul'dahn", StringComparison.Ordinal));
    }

    [Fact]
    public void The_ambient_objective_is_not_repeated_when_nothing_is_engaged()
    {
        var state = new NavigationState
        {
            Mode = NavigationState.Modes.SameZone,
            SourceLabel = "Main Scenario",
            QuestName = "The Ul'dahn Envoy",
            TargetX = 10f,
            TargetZ = 10f,
        };

        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = state,
            AmbientObjectiveName = "The Ul'dahn Envoy",
        });

        Assert.Single(content.Lines, line => line.Text.Contains("Ul'dahn", StringComparison.Ordinal));
    }

    [Fact]
    public void A_hunt_that_is_already_the_primary_objective_is_not_repeated_further_down()
    {
        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = Engaged("Hunting Log · Gladiator") with { QuestName = "Ornery Karakul" },
            HuntingSummary = "Ornery Karakul 2/3",
            HuntingIsPrimary = true,
        });

        Assert.Single(content.Lines, line => line.Text.Contains("Ornery Karakul", StringComparison.Ordinal));
    }

    [Fact]
    public void A_hunt_that_is_not_the_active_objective_still_gets_one_muted_line()
    {
        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = Engaged("Unlock route") with { QuestName = "Unlocks: Chocobo racing" },
            HuntingSummary = "Ornery Karakul 2/3",
            HuntingIsPrimary = false,
        });

        var line = Assert.Single(content.Lines, l => l.Text.Contains("Karakul", StringComparison.Ordinal));
        Assert.Equal(ReadoutEmphasis.Muted, line.Emphasis);
    }

    [Fact]
    public void The_arrow_points_at_the_same_zone_target()
    {
        var state = new NavigationState
        {
            Mode = NavigationState.Modes.SameZone,
            SourceLabel = "Main Scenario",
            QuestName = "The Ul'dahn Envoy",
            TargetX = 12f,
            TargetY = 3f,
            TargetZ = -40f,
        };

        var content = ReadoutComposer.Compose(Inputs(state) with { DistanceYalms = 84f });

        Assert.True(content.ShowArrow);
        Assert.Equal(12f, content.TargetX);
        Assert.Equal(-40f, content.TargetZ);
        Assert.Contains(content.Lines, line => line.Emphasis == ReadoutEmphasis.Primary && line.Text.Contains("yalms", StringComparison.Ordinal));
    }

    [Fact]
    public void The_arrow_points_at_the_entrance_when_the_objective_is_in_another_zone()
    {
        var state = new NavigationState
        {
            Mode = NavigationState.Modes.OtherZone,
            SourceLabel = "Main Scenario",
            QuestName = "The Ul'dahn Envoy",
            EntranceName = "Gate of Nald",
            EntranceX = 5f,
            EntranceZ = 6f,
            ZoneName = "Western Thanalan",
        };

        var content = ReadoutComposer.Compose(Inputs(state) with { DistanceYalms = 200f });

        Assert.True(content.ShowArrow);
        Assert.Equal(5f, content.TargetX);
        Assert.Equal(6f, content.TargetZ);
        Assert.Contains(content.Lines, line => line.Text.Contains("Through Gate of Nald", StringComparison.Ordinal));
    }

    [Fact]
    public void There_is_no_arrow_when_the_route_is_a_teleport()
    {
        var state = new NavigationState
        {
            Mode = NavigationState.Modes.OtherZone,
            SourceLabel = "Main Scenario",
            QuestName = "The Ul'dahn Envoy",
            AetheryteName = "Horizon",
            AetheryteId = 2,
            AetheryteUnlocked = true,
            ZoneName = "Western Thanalan",
        };

        var content = ReadoutComposer.Compose(Inputs(state));

        Assert.False(content.ShowArrow);
        Assert.Contains(content.Lines, line => line.Text.Contains("Teleport to Horizon", StringComparison.Ordinal));
    }

    [Fact]
    public void The_teleport_line_only_says_click_where_clicking_is_possible()
    {
        var state = new NavigationState
        {
            Mode = NavigationState.Modes.OtherZone,
            SourceLabel = "Main Scenario",
            AetheryteName = "Horizon",
            AetheryteUnlocked = true,
        };

        var withMouse = ReadoutComposer.Compose(Inputs(state) with { TeleportOnClick = true });
        var withPad = ReadoutComposer.Compose(Inputs(state));

        Assert.Contains(withMouse.Lines, line => line.Text.EndsWith("(click)", StringComparison.Ordinal));
        Assert.DoesNotContain(withPad.Lines, line => line.Text.Contains("(click)", StringComparison.Ordinal));
    }

    [Fact]
    public void Nearby_unlocks_collapse_to_a_count_while_a_mode_is_engaged()
    {
        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = Engaged("Hunting Log · Gladiator"),
            NearbyUnlocks = ["Chocobo racing", "Triple Triad", "The Gold Saucer"],
        });

        Assert.Contains(content.Lines, line => string.Equals(line.Text, "Unlocks nearby: 3", StringComparison.Ordinal));
        Assert.DoesNotContain(content.Lines, line => line.Text.Contains("Triple Triad", StringComparison.Ordinal));
    }

    [Fact]
    public void Nearby_unlocks_are_named_when_nothing_is_engaged()
    {
        var state = new NavigationState { Mode = NavigationState.Modes.Idle, SourceLabel = "Wayfarer" };

        var content = ReadoutComposer.Compose(Inputs(state) with { NearbyUnlocks = ["Chocobo racing", "Triple Triad"] });

        Assert.Contains(content.Lines, line => string.Equals(line.Text, "Triple Triad", StringComparison.Ordinal));
    }

    [Fact]
    public void An_arrival_reads_as_words_rather_than_a_meaningless_bearing()
    {
        var state = new NavigationState
        {
            Mode = NavigationState.Modes.SameZone,
            SourceLabel = "Main Scenario",
            TargetX = 1f,
            TargetZ = 1f,
        };

        var content = ReadoutComposer.Compose(Inputs(state) with { DistanceYalms = 2f });

        Assert.Contains(content.Lines, line => string.Equals(line.Text, "You have arrived", StringComparison.Ordinal));
    }

    private static NavigationState Engaged(string sourceLabel) => new()
    {
        Mode = NavigationState.Modes.SameZone,
        SourceLabel = sourceLabel,
        Engaged = true,
        QuestName = "Ornery Karakul",
        TargetX = 0f,
        TargetZ = 0f,
    };

    private static ReadoutInputs Inputs(NavigationState state) => new() { State = state };
}
