using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

public class NavGraphPlannerTests
{
    [Fact]
    public void Numbers_rows_densely_from_the_start_index()
    {
        var plan = NavGraphPlanner.Plan([2, 3], startIndex: 10, navUp: 1, navDown: 40);

        Assert.Equal([10, 11], plan[0].Select(link => link.Index));
        Assert.Equal([12, 13, 14], plan[1].Select(link => link.Index));
    }

    [Fact]
    public void Never_allocates_the_reserved_zero_index()
    {
        var plan = NavGraphPlanner.Plan([4, 4, 4], startIndex: 1, navUp: 0, navDown: 0);

        Assert.All(plan.SelectMany(row => row), link => Assert.NotEqual(NavGraphPlanner.NoNavigation, link.Index));
    }

    [Fact]
    public void Assigns_every_element_a_unique_index()
    {
        var plan = NavGraphPlanner.Plan([1, 5, 3, 2], startIndex: 10, navUp: 1, navDown: 40);

        var indices = plan.SelectMany(row => row).Select(link => link.Index).ToList();
        Assert.Equal(indices.Count, indices.Distinct().Count());
    }

    [Fact]
    public void Leaves_the_block_through_the_given_exits()
    {
        var plan = NavGraphPlanner.Plan([2, 2], startIndex: 10, navUp: 1, navDown: 40);

        Assert.All(plan[0], link => Assert.Equal(1, link.Up));
        Assert.All(plan[1], link => Assert.Equal(40, link.Down));
    }

    [Fact]
    public void Links_vertical_neighbours_by_column()
    {
        var plan = NavGraphPlanner.Plan([3, 3], startIndex: 10, navUp: 1, navDown: 40);

        // Column 1 of the second row (index 14) goes up to column 1 of the first row (index 11).
        Assert.Equal(11, plan[1][1].Up);
        Assert.Equal(14, plan[0][1].Down);
    }

    [Fact]
    public void Clamps_the_column_when_the_adjacent_row_is_narrower()
    {
        var plan = NavGraphPlanner.Plan([4, 1], startIndex: 10, navUp: 1, navDown: 40);

        // Every chip in the wide row drops onto the single button below it...
        Assert.All(plan[0], link => Assert.Equal(14, link.Down));

        // ...and coming back up lands on the chip in the same column, i.e. the first one.
        Assert.Equal(10, plan[1][0].Up);
    }

    [Fact]
    public void Wraps_left_and_right_within_a_row()
    {
        var plan = NavGraphPlanner.Plan([3], startIndex: 10, navUp: 1, navDown: 40);

        Assert.Equal(12, plan[0][0].Left);
        Assert.Equal(10, plan[0][2].Right);
    }

    [Fact]
    public void Leaves_a_single_element_row_without_horizontal_links()
    {
        var plan = NavGraphPlanner.Plan([1], startIndex: 10, navUp: 1, navDown: 40);

        Assert.Equal(NavGraphPlanner.NoNavigation, plan[0][0].Left);
        Assert.Equal(NavGraphPlanner.NoNavigation, plan[0][0].Right);
    }

    [Fact]
    public void Skips_empty_rows_without_leaving_a_hole_in_the_graph()
    {
        var plan = NavGraphPlanner.Plan([2, 0, 2], startIndex: 10, navUp: 1, navDown: 40);

        Assert.Empty(plan[1]);
        Assert.Equal([12, 13], plan[2].Select(link => link.Index));

        // The empty row consumes no index at all: row 0 links straight through to row 2.
        Assert.Equal([12, 13], plan[0].Select(link => link.Down));
        Assert.Equal([10, 11], plan[2].Select(link => link.Up));
    }

    [Fact]
    public void An_empty_layout_produces_no_links()
    {
        var plan = NavGraphPlanner.Plan([0, 0], startIndex: 10, navUp: 1, navDown: 40);

        Assert.Empty(plan.SelectMany(row => row));
    }

    [Fact]
    public void Highest_index_matches_the_largest_index_actually_planned()
    {
        int[] rows = [3, 0, 4, 1];
        var plan = NavGraphPlanner.Plan(rows, startIndex: 10, navUp: 1, navDown: 40);

        Assert.Equal(plan.SelectMany(row => row).Max(link => link.Index), NavGraphPlanner.HighestIndex(rows, 10));
    }

    [Fact]
    public void Reports_a_layout_that_would_be_truncated_by_the_byte_index_space()
    {
        // KamiToolKit casts NavIndex to a byte unchecked, so 256 becomes 0 — "no navigation" —
        // and the entire region silently disappears from the graph with no error anywhere.
        Assert.True(NavGraphPlanner.Fits([6], startIndex: 250));
        Assert.False(NavGraphPlanner.Fits([7], startIndex: 250));
    }

    [Fact]
    public void Rejects_a_start_index_on_the_reserved_zero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NavGraphPlanner.Plan([1], startIndex: 0, navUp: 0, navDown: 0));
    }
}
