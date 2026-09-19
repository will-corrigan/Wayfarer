using Wayfarer.Surfaces.ScenarioTree;

namespace Wayfarer.Tests;

/// <summary>The rectangle a line's stretches are gathered into, which is what says how tall the
/// line is and so where the line under it goes.</summary>
public class RectTests
{
    private const float Leading = 20f;

    [Fact]
    public void An_empty_rectangle_measures_nothing()
    {
        Assert.Equal(0f, Rect.Empty.Height);
        Assert.Equal(0f, Rect.Empty.Width);
    }

    [Fact]
    public void Taking_in_the_first_stretch_gives_that_stretch()
    {
        var only = new Rect(4f, 0f, 30f, Leading);

        Assert.Equal(only, Rect.Empty.Union(only));
    }

    [Fact]
    public void A_stretch_on_a_second_line_makes_the_whole_thing_two_lines_tall()
    {
        var first = new Rect(0f, 0f, 30f, Leading);
        var wrapped = new Rect(0f, Leading, 18f, Leading);

        var both = first.Union(wrapped);

        Assert.Equal(0f, both.Top);
        Assert.Equal(2f * Leading, both.Height);
    }

    [Fact]
    public void Stretches_side_by_side_stay_one_line_tall_and_span_both()
    {
        var left = new Rect(0f, 0f, 30f, Leading);
        var right = new Rect(34f, 0f, 20f, Leading);

        var both = left.Union(right);

        Assert.Equal(Leading, both.Height);
        Assert.Equal(0f, both.Left);
        Assert.Equal(54f, both.Width);
    }

    [Fact]
    public void Order_does_not_matter()
    {
        var first = new Rect(0f, 0f, 30f, Leading);
        var wrapped = new Rect(6f, Leading, 18f, Leading);

        Assert.Equal(first.Union(wrapped), wrapped.Union(first));
    }
}
