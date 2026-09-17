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
    public void Idle_falls_back_to_the_plugin_name()
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
}
