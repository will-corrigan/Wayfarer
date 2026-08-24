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
        Assert.Equal("Hunting Log - Gladiator", heading.Text);
    }

    [Fact]
    public void There_is_never_more_than_one_heading()
    {
        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = Engaged("Unlock route"),
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

        Assert.Equal("Hunting Log - Gladiator (3 of 11)", content.Lines[0].Text);
    }

    [Fact]
    public void The_quest_you_happen_to_be_on_is_not_shown_beside_an_engaged_mode()
    {
        var content = ReadoutComposer.Compose(
            Inputs(Engaged("Hunting Log · Gladiator") with { QuestName = "The Ul'dahn Envoy" }));

        // The objective line belongs to the mode, so the quest name it carries is the mode's own.
        // Nothing anywhere in the readout names a second thing that could be what the arrow follows.
        Assert.DoesNotContain(
            content.Lines,
            line => line.Text.StartsWith("Main Scenario:", StringComparison.Ordinal));
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
            State = Engaged("Unlock route") with { QuestName = "The Ties That Bind" },
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
    public void The_distance_line_says_when_the_target_is_on_another_level()
    {
        // The player asked for this in these words: "56 yalms · above you". The drawn readout also
        // hangs the game's own chevron off the arrow, but the words are what carry it.
        var state = new NavigationState
        {
            Mode = NavigationState.Modes.SameZone,
            SourceLabel = "Main Scenario",
            QuestName = "The Ul'dahn Envoy",
            TargetX = 12f,
            TargetY = 30f,
            TargetZ = -40f,
        };

        var content = ReadoutComposer.Compose(
            Inputs(state) with { DistanceYalms = 56f, Elevation = ElevationHint.Above });

        Assert.Equal(ElevationHint.Above, content.Elevation);
        Assert.Contains(content.Lines, line => string.Equals(line.Text, "56 yalms · above you", StringComparison.Ordinal));
    }

    [Fact]
    public void The_distance_line_says_below_the_same_way()
    {
        var content = ReadoutComposer.Compose(
            Inputs(SameZone()) with { DistanceYalms = 120f, Elevation = ElevationHint.Below });

        Assert.Contains(content.Lines, line => string.Equals(line.Text, "120 yalms · below you", StringComparison.Ordinal));
    }

    [Fact]
    public void The_distance_line_is_left_alone_when_there_is_nothing_to_say_about_height()
    {
        var content = ReadoutComposer.Compose(Inputs(SameZone()) with { DistanceYalms = 56f });

        Assert.Equal(ElevationHint.Level, content.Elevation);
        Assert.Contains(content.Lines, line => string.Equals(line.Text, "56 yalms", StringComparison.Ordinal));
    }

    [Fact]
    public void Arriving_beats_saying_the_target_is_above_you()
    {
        // Within five yalms horizontally, "above you" is the top of the stairs the player is
        // standing at the foot of. Saying both at once contradicts itself.
        var content = ReadoutComposer.Compose(
            Inputs(SameZone()) with { DistanceYalms = 2f, Elevation = ElevationHint.Above });

        Assert.Contains(content.Lines, line => string.Equals(line.Text, "You have arrived", StringComparison.Ordinal));
        Assert.DoesNotContain(content.Lines, line => line.Text.Contains("above you", StringComparison.Ordinal));
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

        Assert.Contains(content.Lines, line => string.Equals(line.Text, "3 unlocks nearby", StringComparison.Ordinal));
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
    public void Nearby_unlocks_never_exceed_the_line_budget()
    {
        var state = new NavigationState { Mode = NavigationState.Modes.Idle, SourceLabel = "Wayfarer" };
        var many = Enumerable.Range(0, 12).Select(i => $"Unlock {i} (30 yalms)").ToList();

        var content = ReadoutComposer.Compose(Inputs(state) with { NearbyUnlocks = many });

        var shown = content.Lines.Count(line => line.Text.StartsWith("Unlock ", StringComparison.Ordinal));
        Assert.Equal(ReadoutComposer.MaxNearbyUnlockLines, shown);
    }

    [Fact]
    public void Nearby_unlocks_are_display_only_and_never_take_the_arrow()
    {
        // They are context, not guidance: naming them must not make one of them the thing the
        // arrow is pointing at, and must not add a second direction indicator.
        var state = new NavigationState
        {
            Mode = NavigationState.Modes.SameZone,
            SourceLabel = "Main Scenario",
            TargetX = 10f,
            TargetZ = 20f,
        };

        var content = ReadoutComposer.Compose(
            Inputs(state) with { NearbyUnlocks = ["Chocobo racing (30 yalms)"], DistanceYalms = 80f });

        Assert.True(content.ShowArrow);
        Assert.Equal(10f, content.TargetX);
        Assert.Equal(20f, content.TargetZ);
        Assert.All(
            content.Lines.Where(line => line.Text.Contains("Chocobo", StringComparison.Ordinal)),
            line => Assert.Equal(ReadoutEmphasis.Muted, line.Emphasis));
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

    // --- Search-area objectives: the reported bug was an arrow sent 66 yalms at the CENTRE of a
    // "search this area" quest step with a precise-looking distance, as though it were a waypoint. ---
    [Fact]
    public void Outside_a_search_area_the_readout_says_it_is_an_area_and_gives_the_distance_to_it()
    {
        var content = ReadoutComposer.Compose(
            Inputs(SearchAreaState()) with { DistanceYalms = 66f, AreaHint = SearchAreaHint.Outside });

        Assert.True(content.ShowArrow);
        Assert.Contains(content.Lines, line => string.Equals(line.Text, "Search the area · 66 yalms", StringComparison.Ordinal));
        Assert.DoesNotContain(content.Lines, line => string.Equals(line.Text, "66 yalms", StringComparison.Ordinal));
    }

    [Fact]
    public void Outside_a_search_area_the_elevation_words_still_ride_along()
    {
        var content = ReadoutComposer.Compose(
            Inputs(SearchAreaState()) with
            {
                DistanceYalms = 66f,
                AreaHint = SearchAreaHint.Outside,
                Elevation = ElevationHint.Above,
            });

        Assert.Contains(content.Lines, line => string.Equals(line.Text, "Search the area · 66 yalms · above you", StringComparison.Ordinal));
    }

    [Fact]
    public void Outside_a_search_area_never_says_arrived_no_matter_how_close_the_centre_is()
    {
        // The centre is not the objective, so being near it is not "arriving" — that would imply a
        // precision the game itself did not give.
        var content = ReadoutComposer.Compose(
            Inputs(SearchAreaState()) with { DistanceYalms = 2f, AreaHint = SearchAreaHint.Outside });

        Assert.DoesNotContain(content.Lines, line => string.Equals(line.Text, "You have arrived", StringComparison.Ordinal));
        Assert.Contains(content.Lines, line => line.Text.StartsWith("Search the area", StringComparison.Ordinal));
    }

    [Fact]
    public void Inside_a_search_area_the_readout_stops_pointing_at_the_centre()
    {
        var content = ReadoutComposer.Compose(
            Inputs(SearchAreaState()) with { DistanceYalms = 4f, AreaHint = SearchAreaHint.Inside });

        Assert.False(content.ShowArrow);
        Assert.Null(content.TargetX);
        Assert.Null(content.TargetZ);
    }

    [Fact]
    public void Inside_a_search_area_the_readout_says_to_look_around_rather_than_naming_a_distance()
    {
        var content = ReadoutComposer.Compose(
            Inputs(SearchAreaState()) with { DistanceYalms = 4f, AreaHint = SearchAreaHint.Inside });

        Assert.DoesNotContain(content.Lines, line => line.Text.Contains("yalms", StringComparison.Ordinal));
        Assert.DoesNotContain(content.Lines, line => string.Equals(line.Text, "You have arrived", StringComparison.Ordinal));
        Assert.Contains(content.Lines, line => line.Text.Contains("look around", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_zero_radius_objective_is_byte_identical_to_before_this_feature_existed()
    {
        // TargetRadiusYalms null (the default for every objective this field did not exist for) and
        // AreaHint left at its default (NotApplicable) must reproduce EXACTLY the pre-existing
        // point-objective output — this is the compatibility guarantee the whole feature depends on.
        var pointState = SameZone();
        var withoutArea = ReadoutComposer.Compose(Inputs(pointState) with { DistanceYalms = 56f });
        var explicitlyNotApplicable = ReadoutComposer.Compose(
            Inputs(pointState) with { DistanceYalms = 56f, AreaHint = SearchAreaHint.NotApplicable });

        Assert.Contains(withoutArea.Lines, line => string.Equals(line.Text, "56 yalms", StringComparison.Ordinal));
        Assert.Equal(
            withoutArea.Lines.Select(l => l.Text),
            explicitlyNotApplicable.Lines.Select(l => l.Text),
            StringComparer.Ordinal);
        Assert.Equal(withoutArea.ShowArrow, explicitlyNotApplicable.ShowArrow);
        Assert.Equal(withoutArea.TargetX, explicitlyNotApplicable.TargetX);
        Assert.Equal(withoutArea.TargetZ, explicitlyNotApplicable.TargetZ);
    }

    [Fact]
    public void The_line_that_names_what_is_followed_is_marked_as_the_subject()
    {
        var content = ReadoutComposer.Compose(Inputs(SameZone()) with { DistanceYalms = 80f });

        var subject = Assert.Single(content.Lines, line => line.Subject);
        Assert.Equal("The Ul'dahn Envoy", subject.Text);
    }

    [Fact]
    public void The_subject_is_never_the_heading_or_the_distance()
    {
        // Both are near neighbours that could be mistaken for it — the heading is the first line,
        // and the distance carries the same Primary weight the name does.
        var content = ReadoutComposer.Compose(Inputs(SameZone()) with { DistanceYalms = 80f });

        Assert.DoesNotContain(content.Lines, line => line.Subject && line.Emphasis == ReadoutEmphasis.Heading);
        Assert.DoesNotContain(
            content.Lines,
            line => line.Subject && line.Text.Contains("yalms", StringComparison.Ordinal));
    }

    [Fact]
    public void An_idle_readout_still_has_a_subject_to_hang_the_switcher_off()
    {
        // "No quest followed" is exactly the moment a player wants to choose one, so the line that
        // says it has to be a subject too — otherwise the one readout that most needs a switcher is
        // the one readout without one.
        var state = new NavigationState { Mode = NavigationState.Modes.Idle, SourceLabel = "Wayfarer" };

        var content = ReadoutComposer.Compose(Inputs(state));

        var subject = Assert.Single(content.Lines, line => line.Subject);
        Assert.Equal("No quest followed", subject.Text);
    }

    [Fact]
    public void The_quest_name_is_marked_as_the_door_to_the_journal()
    {
        var state = SameZone() with { QuestId = 65_600 };

        var content = ReadoutComposer.Compose(Inputs(state) with { DistanceYalms = 80f });

        var subject = Assert.Single(content.Lines, line => line.Subject);
        Assert.Equal(ReadoutLineAction.OpenJournal, subject.Action);
    }

    [Fact]
    public void A_name_with_no_quest_behind_it_offers_no_journal()
    {
        // A hunt has a name worth reading and no journal entry to open. Marking it would put a hand
        // cursor over words that then politely did nothing.
        var content = ReadoutComposer.Compose(Inputs(Engaged("Hunting Log · Gladiator")));

        var subject = Assert.Single(content.Lines, line => line.Subject);
        Assert.Equal(ReadoutLineAction.None, subject.Action);
    }

    [Fact]
    public void The_journal_is_never_offered_on_a_line_that_is_not_the_name()
    {
        var state = SameZone() with { QuestId = 65_600 };

        var content = ReadoutComposer.Compose(Inputs(state) with { DistanceYalms = 80f });

        Assert.All(
            content.Lines.Where(line => line.Action == ReadoutLineAction.OpenJournal),
            line => Assert.True(line.Subject));
    }

    [Fact]
    public void There_is_never_more_than_one_subject()
    {
        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = Engaged("Unlock route"),
            HuntingSummary = "Ornery Karakul 2/3",
            NearbyUnlocks = ["Chocobo racing"],
            DistanceYalms = 120f,
        });

        Assert.Single(content.Lines, line => line.Subject);
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

    private static NavigationState SameZone() => new()
    {
        Mode = NavigationState.Modes.SameZone,
        SourceLabel = "Main Scenario",
        QuestName = "The Ul'dahn Envoy",
        TargetX = 12f,
        TargetY = 30f,
        TargetZ = -40f,
    };

    private static NavigationState SearchAreaState() => SameZone() with { TargetRadiusYalms = 20f };
}
