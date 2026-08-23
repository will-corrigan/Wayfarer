using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>When the readout may claim the target is above or below the player, and how it says it.
///
/// <para>The whole value of this indicator is that it is off most of the time. One that lights up
/// for every hillock carries no information and trains the player to ignore it, so the threshold and
/// the hysteresis are the feature, not an implementation detail.</para></summary>
public class ElevationTests
{
    [Fact]
    public void An_unknown_height_says_nothing()
    {
        // The honest case: the target's stored height could not be checked against the world, so
        // there is no claim to make. Never "level" by assumption — Level here means "say nothing".
        Assert.Equal(ElevationHint.Level, Elevation.Classify(null));
        Assert.Equal(ElevationHint.Level, Elevation.Classify(float.NaN));

        // And an unknown height clears an indicator that was already showing.
        Assert.Equal(ElevationHint.Level, Elevation.Classify(null, ElevationHint.Above));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.8f)] // a jump
    [InlineData(-2.5f)] // running down a slope
    [InlineData(3.9f)] // a rise in the terrain
    [InlineData(-5.9f)] // still not a storey
    public void Terrain_is_not_a_different_floor(float delta)
    {
        Assert.Equal(ElevationHint.Level, Elevation.Classify(delta));
    }

    [Theory]
    [InlineData(6f, ElevationHint.Above)]
    [InlineData(14f, ElevationHint.Above)]
    [InlineData(-6f, ElevationHint.Below)]
    [InlineData(-30f, ElevationHint.Below)]
    public void A_storey_or_more_is(float delta, ElevationHint expected)
    {
        Assert.Equal(expected, Elevation.Classify(delta));
    }

    [Fact]
    public void The_threshold_to_show_is_higher_than_the_threshold_to_hide()
    {
        // The hysteresis itself. Without a gap, a target parked at the threshold blinks its
        // indicator on and off as the player walks up and down a ramp.
        Assert.True(Elevation.ShowAtYalms > Elevation.HideAtYalms);
    }

    [Fact]
    public void Once_shown_it_survives_a_step_back_towards_level()
    {
        // 5 yalms is below the show threshold but above the hide threshold: an indicator that is
        // already up stays up, and one that is not does not appear.
        Assert.Equal(ElevationHint.Above, Elevation.Classify(5f, ElevationHint.Above));
        Assert.Equal(ElevationHint.Level, Elevation.Classify(5f, ElevationHint.Level));
    }

    [Fact]
    public void Once_shown_it_still_goes_away_when_the_player_arrives_on_the_level()
    {
        Assert.Equal(ElevationHint.Level, Elevation.Classify(3.9f, ElevationHint.Above));
        Assert.Equal(ElevationHint.Level, Elevation.Classify(-3.9f, ElevationHint.Below));
    }

    [Fact]
    public void Crossing_the_player_flips_it_without_hysteresis_holding_the_old_answer()
    {
        // Showing "above you" while the target is six yalms below would be the worst of both.
        Assert.Equal(ElevationHint.Below, Elevation.Classify(-7f, ElevationHint.Above));
        Assert.Equal(ElevationHint.Above, Elevation.Classify(7f, ElevationHint.Below));

        // Just past the player is not yet a claim in the other direction, though.
        Assert.Equal(ElevationHint.Level, Elevation.Classify(-1f, ElevationHint.Above));
    }

    [Fact]
    public void A_run_up_a_hill_never_turns_it_on()
    {
        // Walked as a sequence, because that is how it is actually used: each frame's answer feeds
        // the next one, and a hysteresis bug shows up as an indicator that latches.
        var hint = ElevationHint.Level;
        foreach (var delta in new[] { 0f, 1.2f, -0.8f, 2.4f, 3.1f, -2.2f, 3.9f, 1.5f, 0f })
        {
            hint = Elevation.Classify(delta, hint);
            Assert.Equal(ElevationHint.Level, hint);
        }
    }

    [Fact]
    public void The_words_are_second_person_and_only_exist_when_there_is_something_to_say()
    {
        Assert.Equal("above you", Elevation.Words(ElevationHint.Above));
        Assert.Equal("below you", Elevation.Words(ElevationHint.Below));
        Assert.Null(Elevation.Words(ElevationHint.Level));
    }
}
