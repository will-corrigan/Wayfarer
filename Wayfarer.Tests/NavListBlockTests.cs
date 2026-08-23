using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

public class NavListBlockTests
{
    [Fact]
    public void Rows_sit_four_indices_apart_starting_one_past_the_list()
    {
        Assert.Equal(41, NavListBlock.RowIndex(40, 0));
        Assert.Equal(45, NavListBlock.RowIndex(40, 1));
        Assert.Equal(49, NavListBlock.RowIndex(40, 2));
    }

    [Fact]
    public void The_downward_sentinel_sits_immediately_after_the_last_pooled_row()
    {
        // A five-row pool occupies 41, 45, 49, 53, 57 — the sentinel takes 61.
        Assert.Equal(61, NavListBlock.DownwardSentinelIndex(40, 5));
        Assert.Equal(NavListBlock.RowIndex(40, 5), NavListBlock.DownwardSentinelIndex(40, 5));
    }

    [Fact]
    public void A_list_reserves_four_indices_per_row_plus_both_sentinels()
    {
        Assert.Equal(22, NavListBlock.Reserve(5));
    }

    [Fact]
    public void Max_pool_size_keeps_every_index_inside_the_byte_space()
    {
        var pool = NavListBlock.MaxPoolSize(40);

        Assert.True(NavListBlock.Fits(40, pool));
        Assert.False(NavListBlock.Fits(40, pool + 1));
        Assert.True(NavListBlock.DownwardSentinelIndex(40, pool) <= NavGraphPlanner.MaxIndex);
    }

    [Fact]
    public void A_list_placed_at_the_very_top_of_the_index_space_carries_no_rows()
    {
        Assert.Equal(0, NavListBlock.MaxPoolSize(NavGraphPlanner.MaxIndex));
    }
}
