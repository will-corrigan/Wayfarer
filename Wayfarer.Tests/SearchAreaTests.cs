using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>Whether the player is outside or inside a "search this area" quest objective's circle,
/// and how it survives being walked back and forth across the boundary.
///
/// <para>The whole value of this classification is that a circle drawn on the map is an arbitrary
/// line, not a wall — players linger right on top of it constantly. A single threshold there would
/// flicker between "outside" and "inside" wording (and the arrow appearing/disappearing) on every
/// step, so the hysteresis is the feature, exactly as with <see cref="Elevation"/>.</para></summary>
public class SearchAreaTests
{
    [Fact]
    public void No_radius_is_not_a_search_area_objective_at_all()
    {
        // Null or non-positive radius must always answer NotApplicable, regardless of distance or
        // what was shown last frame — this is the guarantee that a point objective's behaviour is
        // completely unaffected by this feature.
        Assert.Equal(SearchAreaHint.NotApplicable, SearchArea.Classify(66f, null));
        Assert.Equal(SearchAreaHint.NotApplicable, SearchArea.Classify(0f, 0f));
        Assert.Equal(SearchAreaHint.NotApplicable, SearchArea.Classify(3f, -5f));
        Assert.Equal(SearchAreaHint.NotApplicable, SearchArea.Classify(3f, 0f, SearchAreaHint.Inside));
    }

    [Fact]
    public void No_distance_is_not_applicable_either()
    {
        Assert.Equal(SearchAreaHint.NotApplicable, SearchArea.Classify(null, 20f));
        Assert.Equal(SearchAreaHint.NotApplicable, SearchArea.Classify(float.NaN, 20f));
    }

    [Fact]
    public void Well_outside_the_circle_reads_as_outside()
    {
        // The reported case: 66 yalms from a circle whose radius is, say, 20 — comfortably outside.
        Assert.Equal(SearchAreaHint.Outside, SearchArea.Classify(66f, 20f));
    }

    [Fact]
    public void Well_inside_the_circle_reads_as_inside()
    {
        Assert.Equal(SearchAreaHint.Inside, SearchArea.Classify(2f, 20f));
    }

    [Fact]
    public void The_hysteresis_band_is_symmetric_around_the_radius()
    {
        const float radius = 20f;

        // Still outside: distance sits inside the dead zone but has not crossed the enter bound.
        Assert.Equal(SearchAreaHint.Outside, SearchArea.Classify(radius - 1f, radius, SearchAreaHint.Outside));

        // Crossed the enter bound: now counts as inside.
        Assert.Equal(
            SearchAreaHint.Inside,
            SearchArea.Classify(radius - SearchArea.BoundaryHysteresisYalms - 0.1f, radius, SearchAreaHint.Outside));
    }

    [Fact]
    public void Once_inside_it_survives_a_step_back_out_towards_the_boundary()
    {
        const float radius = 20f;

        // Just past the true boundary, but not past the exit bound: stays inside.
        Assert.Equal(SearchAreaHint.Inside, SearchArea.Classify(radius + 1f, radius, SearchAreaHint.Inside));

        // Past the exit bound: now counts as outside.
        Assert.Equal(
            SearchAreaHint.Outside,
            SearchArea.Classify(radius + SearchArea.BoundaryHysteresisYalms + 0.1f, radius, SearchAreaHint.Inside));
    }

    [Fact]
    public void A_circle_smaller_than_the_hysteresis_band_can_still_be_entered()
    {
        // Radius 1 minus the 2-yalm band would be negative if not clamped, which would make
        // "become inside" unreachable for a small circle. Clamped at zero, standing right on the
        // centre (distance 0) must still read as inside.
        Assert.Equal(SearchAreaHint.Inside, SearchArea.Classify(0f, 1f, SearchAreaHint.Outside));
    }

    [Fact]
    public void Loitering_on_the_boundary_never_flickers()
    {
        // Walked as a sequence, exactly how it is actually used: each frame's answer feeds the
        // next one, and a hysteresis bug shows up as rapid outside/inside/outside chatter while the
        // player stands near the edge of the circle (radius 20, dead zone 18–22).
        const float radius = 20f;
        var distances = new[] { 25f, 21f, 19.5f, 20.5f, 18.9f, 21.1f, 19f, 18.1f, 21.9f };

        var hint = SearchAreaHint.Outside;
        var flips = 0;
        var previous = hint;
        foreach (var d in distances)
        {
            hint = SearchArea.Classify(d, radius, hint);
            if (hint != previous)
            {
                flips++;
            }

            previous = hint;
        }

        // None of these distances ever cross the enter bound (18) from Outside, so this loitering
        // sequence must stay Outside throughout — zero transitions.
        Assert.Equal(SearchAreaHint.Outside, hint);
        Assert.Equal(0, flips);
    }

    [Fact]
    public void Crossing_all_the_way_in_and_back_out_flips_exactly_once_each_way()
    {
        const float radius = 20f;

        var hint = SearchAreaHint.Outside;
        hint = SearchArea.Classify(25f, radius, hint);
        Assert.Equal(SearchAreaHint.Outside, hint);

        // Genuinely walks into the circle, past the enter bound (18).
        hint = SearchArea.Classify(10f, radius, hint);
        Assert.Equal(SearchAreaHint.Inside, hint);

        // Loiters near the true boundary from the inside — must not flicker back to Outside.
        hint = SearchArea.Classify(21f, radius, hint);
        Assert.Equal(SearchAreaHint.Inside, hint);
        hint = SearchArea.Classify(19f, radius, hint);
        Assert.Equal(SearchAreaHint.Inside, hint);

        // Genuinely walks back out, past the exit bound (22).
        hint = SearchArea.Classify(30f, radius, hint);
        Assert.Equal(SearchAreaHint.Outside, hint);
    }
}
