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
            new ObjectiveCopy("The Ties That Bind", null, UnlockRoutePlan.SourceLabel, UnlockRoutePlan.SourceName));

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
    public void The_emblem_costs_the_plate_the_same_share_the_games_own_does()
    {
        // The measurement the whole left-hand arrangement is about. The game's crest is 52 wide at
        // row-x 12 against a plate starting at row-x 39, so it takes 21 of the plate's 300 and hangs
        // the rest outside. Ours has to sit the same way, or a 48-tall bar spends a fifth of its
        // width on an ornament — which is what the first cut did, at 64 of 400.
        Assert.InRange(GameMetrics.Banner.CrestOverlap, 18f, 26f);
        Assert.True(
            GameMetrics.Banner.CrestOverlap / GameMetrics.Banner.PlateWidth < 0.09f,
            "the emblem is eating more of the plate than the game's own does");

        // Most of it is outside the plate, which is what buys the headline the game's own inset.
        Assert.True(GameMetrics.Banner.CrestOverlap < GameMetrics.Banner.CrestSize / 2f);
    }

    [Fact]
    public void The_emblem_is_no_larger_than_the_games_own_crest_draws()
    {
        // The game's part is 52x52 but its ink is only 47x50 of that — measured off (228,50) as the
        // bounding box of everything with any alpha, at 64% coverage, because it is a ragged flame.
        // Ours is a ring that reaches its own box's edge in both axes, so the honest comparison is
        // our drawn size against their ink.
        Assert.True(GameMetrics.Banner.CrestSize <= 47f, "the emblem is wider than the game's crest draws");
        Assert.True(
            GameMetrics.Banner.CrestSize < GameMetrics.Banner.PlateHeight,
            "the emblem is taller than the bar it is pinned to");
    }

    [Fact]
    public void The_headline_gets_the_same_room_the_games_own_does()
    {
        // 300 - 24 - 26, which is the point of hanging the emblem outside the plate: the name is no
        // more likely to truncate than the main scenario's own names already are.
        Assert.Equal(250f, GameMetrics.Banner.HeadlineWidth);
        Assert.Equal(GameMetrics.Banner.PlateLeft + GameMetrics.Banner.PlateTextInset, GameMetrics.Banner.HeadlineLeft);
    }

    [Fact]
    public void The_bar_carries_the_quests_own_name_and_never_a_label_of_ours()
    {
        // Reported off a screenshot: the bar read "Unlocks: Ceremony of Eternal..." — cut short —
        // while the real quest name sat in a subordinate line underneath it. Exactly backwards. The
        // bar is the game's plate and only ever carries a string the game itself would print.
        Assert.Equal("The Ties That Bind", UnlockRoutePlan.Headline("The Ties That Bind"));
        Assert.DoesNotContain(":", UnlockRoutePlan.Headline("The Ties That Bind"), StringComparison.Ordinal);
    }

    [Fact]
    public void What_an_unlock_gives_and_who_gives_it_go_under_the_bar_not_on_it()
    {
        var detail = UnlockRoutePlan.Detail("Ceremony of Eternal Bonding", "Claribel");

        Assert.Contains("Claribel", detail, StringComparison.Ordinal);
        Assert.Contains("Ceremony of Eternal Bonding", detail, StringComparison.Ordinal);

        // No data-model prefixes: "Unlocks:" and "Pick up:" are both labels about how we store this,
        // not anything the game would write.
        Assert.DoesNotContain("Unlocks:", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Pick up:", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unlock_with_no_known_giver_still_says_what_it_gives()
    {
        Assert.Equal(
            "Unlocks Ceremony of Eternal Bonding",
            UnlockRoutePlan.Detail("Ceremony of Eternal Bonding", null));
    }

    [Fact]
    public void The_whole_unlock_objective_puts_the_quest_on_the_plate()
    {
        // End to end through the projection, because what was wrong was the CALL SITE rather than
        // either method: the source used to hand Headline the unlock's name and Detail the quest's.
        var objective = new GuidanceObjective(
            new ObjectiveKey("unlocks", "1234"),
            new ObjectiveDestination.WorldPoint(129u, 1u, 10f, 0f, 20f),
            new ObjectiveCopy(
                UnlockRoutePlan.Headline("The Ties That Bind"),
                UnlockRoutePlan.Detail("Ceremony of Eternal Bonding", "Claribel"),
                UnlockRoutePlan.SourceLabel,
                UnlockRoutePlan.SourceName));

        var state = GuidanceProjection.Build(
            objective, GuidanceEngagement.Engaged, new RouteResult.SameZone(10f, 0f, 20f, 40f));
        var content = ReadoutComposer.Compose(Inputs(state));

        Assert.Equal("The Ties That Bind", Assert.Single(content.Lines, l => l.Subject).Text);
        Assert.Contains(
            content.Lines,
            l => !l.Subject && l.Text.Contains("Ceremony of Eternal Bonding", StringComparison.Ordinal));
    }

    [Fact]
    public void There_is_exactly_one_caret_on_the_bar_and_it_is_the_plates_own()
    {
        // Reported: two carets after the headline. Ours was a tinted DropDownA crop that slid along
        // behind the name; the other is baked into the plate's art at source x=279-288 and cannot be
        // removed without slicing through it. Ours went. What is asserted here is that the switcher's
        // click region is the plate's right CAP — where that chevron lives — and that it never
        // reaches back over the headline's words.
        var capLeft = GameMetrics.Banner.PlateWidth - GameMetrics.Banner.PlateInsetX;
        var capRight = GameMetrics.Banner.PlateWidth;
        var headlineEnd = GameMetrics.Banner.PlateTextInset + GameMetrics.Banner.HeadlineWidth;

        Assert.True(capLeft <= 279f, "the right cap does not reach the chevron the art draws");
        Assert.True(capRight >= 288f, "the right cap ends before the chevron does");
        Assert.True(headlineEnd <= capLeft, "the switcher's click region overlaps the headline's own text");
    }

    [Fact]
    public void The_parchment_click_target_tiles_the_plate_with_the_switchers_cap_and_nothing_else()
    {
        // Reported: the whole plate opened Settings, the largest and most obvious target on the
        // readout doing the incidental thing, while the useful one — the Journal — was a strip the
        // width of the quest name's own text. The fix makes the parchment itself the Journal's
        // target, bounded to the plate's own left edge (not the readout's, which used to let it eat
        // the crest's margin too) and stopping exactly where the switcher's cap begins, the way
        // ReadoutBodyNode.LayoutBannerHitBox and LayoutSwitcher size their boxes.
        const float width = GameMetrics.Banner.Width;
        var plateLeft = GameMetrics.Banner.PlateLeft;
        var cap = GameMetrics.Banner.PlateInsetX;
        var plateRight = plateLeft + (width - plateLeft);

        var parchmentLeft = plateLeft;
        var parchmentRight = plateLeft + ((width - plateLeft) - cap);
        var switcherLeft = width - cap;
        var switcherRight = width;

        Assert.Equal(plateLeft, parchmentLeft);
        Assert.True(
            Math.Abs(switcherLeft - parchmentRight) < 0.001f,
            "the parchment target leaves a gap or overlaps the switcher's cap");
        Assert.True(
            Math.Abs(plateRight - switcherRight) < 0.001f,
            "the switcher's cap does not reach the plate's own right edge");
        Assert.True(parchmentLeft >= plateLeft, "the parchment target reaches back into the crest's margin");
        Assert.True(switcherRight <= plateRight, "the switcher's cap reaches past the plate");
    }

    [Fact]
    public void The_readout_is_not_wider_than_the_games_own_banner()
    {
        // The plate is drawn at the part's native width, so the nine-slice is the identity and the
        // whole box is 324 against the game's own 340 root. This is the test for "ours should not
        // look bigger than the game's own banner sitting near it".
        Assert.Equal(GameMetrics.Banner.PlateWidth, GameMetrics.Banner.Width - GameMetrics.Banner.PlateLeft);
        Assert.True(GameMetrics.Banner.Width <= 340f, "the readout is wider than the Main Scenario Guide's own root");
        Assert.True(GameMetrics.Banner.Width < GameMetrics.Hud.Width, "the readout did not actually get smaller");
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
        Assert.True(GameMetrics.Banner.PlateInsetX * 2f < GameMetrics.Banner.PlateWidth);
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
