using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The info bar's one rule: every part of the entry describes the actual next step.
///
/// These exist because it did not. The entry showed an aetheryte crystal for "a route is in
/// progress", which put a teleport glyph beside a target fifty-six yalms away in the same zone, and
/// the player asked what the crystal was for. It is now emitted from the next step and nothing
/// else, and these pin that.</summary>
public class DtrComposerTests
{
    [Fact]
    public void Idle_and_nothing_nearby_falls_back_to_the_plugin_name()
    {
        var text = DtrComposer.Compose(new DtrInputs());

        Assert.Equal("Wayfarer", text.Text);
        Assert.Equal(DtrGlyph.None, text.Glyph);
    }

    [Fact]
    public void A_teleport_leg_says_where_and_carries_the_crystal()
    {
        var text = DtrComposer.Compose(new DtrInputs
        {
            Engaged = true,
            Step = DtrNextStep.Teleport,
            StepTarget = "Horizon",
            RouteStop = 3,
            RouteTotal = 11,
            DistanceYalms = 240f,
        });

        Assert.Equal("Teleport: Horizon", text.Text);
        Assert.Equal(DtrGlyph.Aetheryte, text.Glyph);
    }

    [Fact]
    public void An_aethernet_leg_is_worded_distinctly_from_a_teleport()
    {
        var text = DtrComposer.Compose(new DtrInputs
        {
            Engaged = true,
            Step = DtrNextStep.Aethernet,
            StepTarget = "Aetheryte Plaza",
        });

        Assert.Equal("Aethernet: Aetheryte Plaza", text.Text);
        Assert.Equal(DtrGlyph.Aetheryte, text.Glyph);
    }

    [Fact]
    public void A_same_zone_walk_shows_progress_and_distance_and_no_crystal()
    {
        // The reported case, exactly: a hunt one step into six, with the target 56 yalms away.
        var text = DtrComposer.Compose(new DtrInputs
        {
            Engaged = true,
            Step = DtrNextStep.Walk,
            RouteStop = 1,
            RouteTotal = 6,
            DistanceYalms = 56f,
        });

        Assert.Equal("1/6, 56y", text.Text);
        Assert.Equal(DtrGlyph.None, text.Glyph);
    }

    [Fact]
    public void A_solo_hunt_walking_shows_its_label_and_the_distance()
    {
        var text = DtrComposer.Compose(new DtrInputs
        {
            Engaged = true,
            Step = DtrNextStep.Walk,
            HuntingIsPrimary = true,
            HuntingLabel = "Rank 2 4/5",
            DistanceYalms = 120f,
        });

        Assert.Equal("Rank 2 4/5, 120y", text.Text);
        Assert.Equal(DtrGlyph.None, text.Glyph);
    }

    [Fact]
    public void A_hunting_label_is_ignored_when_hunting_is_not_the_primary_objective()
    {
        var text = DtrComposer.Compose(new DtrInputs
        {
            Engaged = true,
            HuntingIsPrimary = false,
            HuntingLabel = "Rank 2 4/5",
        });

        Assert.Equal(DtrText.Wayfarer, text);
    }

    [Fact]
    public void Engaged_with_nothing_more_specific_falls_back_to_the_plugin_name()
    {
        var text = DtrComposer.Compose(new DtrInputs { Engaged = true });

        Assert.Equal(DtrText.Wayfarer, text);
    }

    [Fact]
    public void A_distance_is_never_shown_beside_a_teleport()
    {
        // It would be the distance to somewhere the player is not going yet.
        var text = DtrComposer.Compose(new DtrInputs
        {
            Engaged = true,
            Step = DtrNextStep.Teleport,
            StepTarget = "Horizon",
            DistanceYalms = 900f,
        });

        Assert.DoesNotContain("900", text.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Nearby_unlocks_are_named_while_nothing_is_engaged()
    {
        var idle = DtrComposer.Compose(new DtrInputs { NearbyUnlockCount = 3 });

        Assert.Equal("3 unlocks here", idle.Text);
        Assert.True(idle.UnlocksNearby);
    }

    [Fact]
    public void The_alert_survives_being_in_the_middle_of_something()
    {
        // Passive, not guidance: the mode keeps the text and the glyph, and the alert rides
        // alongside it — walking past a pickup while on a route is exactly when it is useful.
        var route = DtrComposer.Compose(new DtrInputs
        {
            Engaged = true,
            Step = DtrNextStep.Walk,
            RouteStop = 3,
            RouteTotal = 11,
            NearbyUnlockCount = 2,
        });

        Assert.Equal("3/11", route.Text);
        Assert.Equal(DtrGlyph.None, route.Glyph);
        Assert.True(route.UnlocksNearby);
    }

    [Fact]
    public void Nothing_nearby_means_no_alert()
    {
        // The exclamation has exactly one cause. If it is on the bar, there are unlocks; if there
        // are none, it cannot appear — which is what makes it honest beside the readout.
        Assert.False(DtrComposer.Compose(new DtrInputs { Engaged = true }).UnlocksNearby);
        Assert.False(DtrComposer.Compose(new DtrInputs()).UnlocksNearby);
        Assert.False(DtrComposer
            .Compose(new DtrInputs { Engaged = true, Step = DtrNextStep.Teleport, StepTarget = "Horizon" })
            .UnlocksNearby);
    }

    [Fact]
    public void A_single_nearby_unlock_is_not_pluralised()
    {
        var text = DtrComposer.Compose(new DtrInputs { NearbyUnlockCount = 1 });

        Assert.Equal("1 unlock here", text.Text);
    }
}
