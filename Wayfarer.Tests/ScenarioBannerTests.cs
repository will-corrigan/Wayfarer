using Wayfarer.Core.Guidance;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The banner the readout wears: what its header pill says, which of its subordinate lines
/// get the game's quest medallion, and the geometry that makes the whole thing the game's own
/// arrangement rather than a lookalike.
///
/// <para>All of it is arithmetic and strings, so all of it is testable — which is the point. The one
/// thing these cannot check is whether the plate looks right on screen; that is what the extracted
/// art and the build report's screenshots are for.</para></summary>
public class ScenarioBannerTests
{
    [Theory]
    [InlineData("Quest", "Current Quest")]
    [InlineData("Unlock", "Current Unlock")]
    [InlineData("Hunting Log", "Current Hunting Log")]
    public void The_header_pill_says_Current_plus_whatever_the_module_calls_itself(string module, string expected)
    {
        var content = ReadoutComposer.Compose(Inputs(SameZone() with { SourceName = module }));

        Assert.Equal(expected, content.StripLabel);
    }

    [Fact]
    public void A_module_that_does_not_name_itself_leaves_the_plugins_own_name_on_the_pill()
    {
        // Which is exactly what an idle readout wants: nothing is being followed, so there is no
        // category to announce, and "Wayfarer" is the honest answer to "what is this element?".
        var idle = new NavigationState { Mode = NavigationState.Modes.Idle };

        Assert.Equal("Wayfarer", ReadoutComposer.Compose(Inputs(idle)).StripLabel);
    }

    [Fact]
    public void The_pill_carries_a_route_position_where_the_heading_used_to()
    {
        var state = SameZone() with { SourceName = "Unlock", RouteStop = 3, RouteTotal = 11 };

        Assert.Equal("Current Unlock (3 of 11)", ReadoutComposer.Compose(Inputs(state)).StripLabel);
    }

    [Fact]
    public void The_pill_never_names_the_thing_being_tracked_only_its_kind()
    {
        // The pill and the plate are a category and an instance. If the pill started carrying the
        // quest's own name there would be two places saying the same thing and no place saying what
        // kind of thing it is.
        var content = ReadoutComposer.Compose(Inputs(SameZone() with { SourceName = "Quest" }));

        Assert.DoesNotContain("Ul'dahn", content.StripLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("The Ul'dahn Envoy", Assert.Single(content.Lines, l => l.Subject).Text);
    }

    [Fact]
    public void The_pills_words_come_from_the_source_and_are_never_derived_from_its_id()
    {
        // The architectural rule, asserted rather than trusted: a source id that nothing supplied a
        // name for produces the fallback, NOT a word looked up from "quest"/"unlocks"/"hunting". If
        // anybody ever adds that switch, this fails.
        var state = SameZone() with { SourceId = "quest", SourceName = null };

        Assert.Equal("Wayfarer", ReadoutComposer.Compose(Inputs(state)).StripLabel);
    }

    [Fact]
    public void A_sources_own_name_reaches_the_pill_through_the_projection()
    {
        var objective = new GuidanceObjective(
            new ObjectiveKey("unlocks", "1234"),
            new ObjectiveDestination.WorldPoint(129u, 1u, 10f, 0f, 20f),
            new ObjectiveCopy("Unlocks: Glamours", null, UnlockRoutePlan.SourceLabel, UnlockRoutePlan.SourceName));

        var state = GuidanceProjection.Build(
            objective, GuidanceEngagement.Engaged, new RouteResult.SameZone(10f, 0f, 20f, 40f));

        Assert.Equal("Unlock", state.SourceName);
        Assert.Equal("Current Unlock", ReadoutComposer.Compose(Inputs(state)).StripLabel);
    }

    [Fact]
    public void Nearby_unlocks_are_the_lines_that_get_the_quest_medallion()
    {
        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = SameZone() with { SourceName = "Quest" },
            DistanceYalms = 120f,
            NearbyUnlocks = ["Chocobo Companion (240 yalms)", "Blue Mage (1.2k yalms)"],
        });

        var marked = content.Lines.Where(line => line.Marked).ToList();

        Assert.Equal(2, marked.Count);
        Assert.All(marked, line => Assert.Contains("yalms)", line.Text, StringComparison.Ordinal));
    }

    [Fact]
    public void An_annotation_about_the_tracked_thing_never_gets_a_medallion()
    {
        // "1,240 yalms away" is not somewhere you can walk to, it is a fact about somewhere you can
        // walk to. Putting a quest medallion on it would be a lie about what kind of line it is.
        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = SameZone() with { SourceName = "Quest", StepLabel = "Speak with Frixio" },
            DistanceYalms = 1240f,
        });

        Assert.DoesNotContain(content.Lines, line => line.Marked);
    }

    [Fact]
    public void A_count_of_nearby_unlocks_is_an_annotation_rather_than_a_destination()
    {
        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = SameZone() with { SourceName = "Hunting Log", Engaged = true },
            DistanceYalms = 60f,
            NearbyUnlocks = ["Chocobo Companion", "Blue Mage", "Amaro"],
        });

        Assert.Contains(content.Lines, line => line.Text.Contains("3 unlocks nearby", StringComparison.Ordinal));
        Assert.DoesNotContain(content.Lines, line => line.Marked);
    }

    [Fact]
    public void The_subordinate_lines_hang_off_the_headline_the_way_the_games_own_do()
    {
        // The whole visual signature of the relationship, in three numbers: the words beneath sit a
        // touch right of the name above, and the markers hang out to its left. ScenarioTree.uld's own
        // root-absolute figures are 63 for the headline text, 72 for a sub-line's and 44 for its
        // icon.
        Assert.Equal(
            GameMetrics.Banner.HeadlineLeft + 9f, GameMetrics.Banner.SubLineLeft);
        Assert.Equal(
            GameMetrics.Banner.HeadlineLeft - 19f, GameMetrics.Banner.MarkerLeft);
        Assert.True(
            GameMetrics.Banner.MarkerLeft > 0f,
            "the marker column has to be inside the readout, not off its left edge");
    }

    [Fact]
    public void The_crest_slot_leaves_room_for_the_headline_and_the_marker_column_both()
    {
        // The emblem, the gap after it and the name have to add up to where the name actually starts,
        // or the crest overlaps the first letter.
        Assert.Equal(
            GameMetrics.Banner.CrestLeft + GameMetrics.Banner.CrestSize + GameMetrics.Banner.CrestGap,
            GameMetrics.Banner.HeadlineLeft);

        // And the markers hang into the crest's own column, below it — which is fine, because the
        // crest stops at the plate's bottom edge and the markers start there.
        Assert.True(GameMetrics.Banner.MarkerLeft < GameMetrics.Banner.CrestLeft + GameMetrics.Banner.CrestSize);
    }

    [Fact]
    public void The_headline_is_centred_in_the_plate_and_the_pill_sits_above_it()
    {
        Assert.Equal(
            (GameMetrics.Banner.PlateHeight - GameMetrics.Banner.HeadlineHeight) / 2f,
            GameMetrics.Banner.HeadlineTop);

        // The pill overhangs the plate's top and the plate covers its last few pixels — which is
        // what makes the two read as one object rather than as a label parked above a bar.
        Assert.True(GameMetrics.Banner.StripTop < GameMetrics.Banner.PlateTop);
        Assert.True(
            GameMetrics.Banner.StripTop + GameMetrics.Banner.StripHeight > GameMetrics.Banner.PlateTop,
            "the pill has to reach into the plate or the two float apart");
    }

    [Fact]
    public void The_nine_slice_keeps_the_plates_chevron_inside_its_right_cap()
    {
        // The art's one interior detail sits at source x=279-288 of a 300-wide part, so an inset
        // under 24 slices through it and the readout gets a smeared chevron at every width.
        Assert.True(GameMetrics.Banner.PlateInsetX >= 300f - 288f + 12f);
        Assert.True(GameMetrics.Banner.PlateInsetX * 2f < GameMetrics.Hud.Width);
    }

    [Fact]
    public void The_whole_banner_and_a_full_readout_still_fit_the_placement_slot()
    {
        // The no-overflow property the metrics pass established, restated for the banner: the plate,
        // its pill, and the deepest block of subordinate lines the composer can produce have to fit
        // inside the fixed slot every placement decision is made against, or the readout grows out
        // of the bottom of the screen instead of down its own box.
        var deepest = GameMetrics.Banner.Height
            + (ReadoutComposer.MaxNearbyUnlockLines * GameMetrics.Banner.SubLinePitch)
            + (4f * GameMetrics.Banner.AnnotationBlock);

        Assert.True(
            deepest <= ReadoutLayout.ReferenceHeight,
            $"a full readout is {deepest} tall against a {ReadoutLayout.ReferenceHeight} slot");
    }

    private static ReadoutInputs Inputs(NavigationState state) => new() { State = state };

    private static NavigationState SameZone() => new()
    {
        Mode = NavigationState.Modes.SameZone,
        SourceLabel = "Main Scenario",
        QuestName = "The Ul'dahn Envoy",
        TargetX = 12f,
        TargetZ = -40f,
    };
}
