using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

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
    public void Route_progress_takes_priority_over_everything_else_while_engaged()
    {
        var text = DtrComposer.Compose(new DtrInputs
        {
            Engaged = true,
            RouteStop = 3,
            RouteTotal = 11,
            HuntingIsPrimary = true,
            HuntingLabel = "Rank 2 4/5",
        });

        Assert.Equal("Stop 3/11", text.Text);
        Assert.Equal(DtrGlyph.Route, text.Glyph);
    }

    [Fact]
    public void A_solo_hunt_shows_its_precomposed_label_when_there_is_no_route_progress()
    {
        var text = DtrComposer.Compose(new DtrInputs
        {
            Engaged = true,
            HuntingIsPrimary = true,
            HuntingLabel = "Rank 2 4/5",
        });

        Assert.Equal("Rank 2 4/5", text.Text);
        Assert.Equal(DtrGlyph.Hunting, text.Glyph);
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
    public void Nearby_unlocks_are_only_shown_while_nothing_is_engaged()
    {
        var idle = DtrComposer.Compose(new DtrInputs { NearbyUnlockCount = 3 });
        var engaged = DtrComposer.Compose(new DtrInputs { Engaged = true, NearbyUnlockCount = 3 });

        Assert.Equal("3 unlocks here", idle.Text);
        Assert.Equal(DtrGlyph.Unlocks, idle.Glyph);
        Assert.Equal(DtrText.Wayfarer, engaged);
    }

    [Fact]
    public void A_single_nearby_unlock_is_not_pluralised()
    {
        var text = DtrComposer.Compose(new DtrInputs { NearbyUnlockCount = 1 });

        Assert.Equal("1 unlock here", text.Text);
    }
}
