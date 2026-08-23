using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>Scroll-follows-focus. The reported symptom was a controller cursor that "would select
/// things clipped off the box as if it were visible in the box" on the Settings tab.</summary>
public class ScrollIntoViewTests
{
    private const float Viewport = 300f;
    private const float Max = 700f;

    [Fact]
    public void An_item_already_in_view_does_not_move_the_list() =>
        Assert.Equal(120f, ScrollIntoView.Adjust(140f, 24f, Viewport, 120f, Max));

    [Fact]
    public void An_item_below_the_viewport_is_scrolled_up_to_its_bottom_edge()
    {
        // Minimum movement: the item ends up flush with the bottom, not centred, so walking the
        // cursor down the list moves it one row at a time instead of lurching.
        var scroll = ScrollIntoView.Adjust(500f, 24f, Viewport, 120f, Max);

        Assert.Equal(524f - Viewport, scroll, 0.01f);
    }

    [Fact]
    public void An_item_above_the_viewport_is_scrolled_down_to_its_top_edge() =>
        Assert.Equal(40f, ScrollIntoView.Adjust(40f, 24f, Viewport, 300f, Max), 0.01f);

    [Fact]
    public void The_result_never_leaves_the_scrollable_range()
    {
        Assert.Equal(Max, ScrollIntoView.Adjust(5000f, 24f, Viewport, 0f, Max));
        Assert.Equal(0f, ScrollIntoView.Adjust(-500f, 24f, Viewport, 400f, Max));
    }

    [Fact]
    public void A_container_that_cannot_scroll_is_left_at_the_top() =>
        Assert.Equal(0f, ScrollIntoView.Adjust(900f, 24f, Viewport, 0f, 0f));

    [Fact]
    public void An_item_taller_than_the_viewport_is_aligned_to_its_top() =>

        // Its label is at the top; scrolling to its bottom would show the part that does not say
        // what it is.
        Assert.Equal(200f, ScrollIntoView.Adjust(200f, 500f, Viewport, 0f, Max), 0.01f);

    [Fact]
    public void Walking_down_a_list_of_rows_keeps_every_focused_row_visible()
    {
        const float RowHeight = 30f;
        var scroll = 0f;

        for (var row = 0; row < 30; row++)
        {
            var top = row * RowHeight;
            scroll = ScrollIntoView.Adjust(top, RowHeight, Viewport, scroll, Max);

            Assert.True(top >= scroll - 0.01f, $"row {row} is above the viewport");
            Assert.True(top + RowHeight <= scroll + Viewport + 0.01f, $"row {row} is below the viewport");
        }
    }
}
