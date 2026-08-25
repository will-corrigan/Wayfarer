using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

public class HubNavPlanTests
{
    [Fact]
    public void The_hub_index_plan_holds()
    {
        Assert.Null(HubNavPlan.Validate());
    }

    /// <summary>The Following strip is on screen whatever tab is open, so it needs indices of its
    /// own that no tab's controls can take. It sits between the tab bar and the control region —
    /// above the tabs on screen and above them in the graph, which is what lets a d-pad walk off the
    /// top of the tabs onto Change and Stop instead of into nothing.</summary>
    [Fact]
    public void The_following_strip_has_indices_of_its_own_between_the_tabs_and_the_controls()
    {
        Assert.True(HubNavPlan.TabBarLast < HubNavPlan.Strip);
        Assert.True(HubNavPlan.Strip + HubNavPlan.StripCapacity <= HubNavPlan.Region);

        // Two controls today (Change, Stop), with room to be wrong about that.
        var strip = Enumerable.Repeat(2, 1).ToList();
        Assert.True(
            NavGraphPlanner.Fits(strip, HubNavPlan.Strip, HubNavPlan.Strip + HubNavPlan.StripCapacity - 1));
    }

    [Fact]
    public void The_control_region_never_collides_with_the_tab_bar_or_the_list()
    {
        // The whole region is numbered by the walker, so the worst case is a region that fills its
        // reserved capacity — that must still stop short of the list block. This used to reduce to
        // 39 < 40 because RegionCapacity was defined as List - Region; the two are independent
        // constants now, so moving either one is what this catches.
        var rows = Enumerable.Repeat(1, HubNavPlan.RegionCapacity).ToList();

        Assert.True(NavGraphPlanner.HighestIndex(rows, HubNavPlan.Region) < HubNavPlan.List);
        Assert.True(HubNavPlan.TabBarLast < HubNavPlan.Region);
    }

    /// <summary>The window's own guard against outgrowing the space it reserved. The Settings tab
    /// numbers one index per control and has 22 today (19 static definitions plus one per
    /// registered module), which fits with eight spare — nine more settings, or three more modules,
    /// and the region would silently run into the list block and the cursor would teleport.</summary>
    [Fact]
    public void A_region_that_would_overrun_its_capacity_is_refused_rather_than_wired()
    {
        var comfortable = Enumerable.Repeat(1, HubNavPlan.RegionCapacity).ToList();
        var oneTooMany = Enumerable.Repeat(1, HubNavPlan.RegionCapacity + 1).ToList();
        var ceiling = HubNavPlan.Region + HubNavPlan.RegionCapacity - 1;

        Assert.True(NavGraphPlanner.Fits(comfortable, HubNavPlan.Region, ceiling));
        Assert.False(NavGraphPlanner.Fits(oneTooMany, HubNavPlan.Region, ceiling));

        // And the ceiling is what does it — the byte limit alone would have accepted both.
        Assert.True(NavGraphPlanner.Fits(oneTooMany, HubNavPlan.Region));
    }

    [Fact]
    public void A_full_size_hub_stays_under_the_index_ceiling()
    {
        Assert.True(HubNavPlan.MaxListPoolSize > 20, "the hub must be able to show a screenful of rows");
        Assert.True(
            NavListBlock.DownwardSentinelIndex(HubNavPlan.List, HubNavPlan.MaxListPoolSize) <= NavGraphPlanner.MaxIndex);
    }

    /// <summary>The whole point of clamping the pool. A region after the list was impossible to
    /// place while the pool was "as many rows as fit under 255", because the block's last index was
    /// then a function of the player's window height. The clamp turns it into a constant, and this
    /// is the assertion that the constant is actually below where the pane starts.</summary>
    [Fact]
    public void The_clamped_list_block_leaves_room_for_the_detail_pane_above_it()
    {
        Assert.Equal(HubNavPlan.ListPoolLimit, HubNavPlan.MaxListPoolSize);
        Assert.True(
            HubNavPlan.ListLast < HubNavPlan.DetailPane,
            $"the list block ends at {HubNavPlan.ListLast}, which is not below the pane at {HubNavPlan.DetailPane}.");
    }

    [Fact]
    public void The_detail_pane_fits_its_own_reservation_and_the_byte_ceiling()
    {
        // Three buttons is the most any status offers today; the reservation has to hold that with
        // room to have been wrong, and still stop short of 255.
        var buttons = Enumerable.Repeat(3, 1).ToList();
        var ceiling = HubNavPlan.DetailPane + HubNavPlan.DetailPaneCapacity - 1;

        Assert.True(NavGraphPlanner.Fits(buttons, HubNavPlan.DetailPane, ceiling));
        Assert.True(ceiling <= NavGraphPlanner.MaxIndex);
    }

    /// <summary>A pooled row count large enough to be useful is still available after the clamp:
    /// thirty 44px rows is more list than the window can ever show at once, so the clamp costs
    /// nothing real. If the row height ever grows past this, the clamp starts truncating the list
    /// instead of merely bounding it, and this is what says so.</summary>
    [Fact]
    public void The_clamp_is_larger_than_any_window_can_actually_show()
    {
        const float RowHeight = 44f + 1f;
        const float TallestPlausibleWindow = 1440f * 0.9f;

        Assert.True(HubNavPlan.ListPoolLimit * RowHeight > TallestPlausibleWindow);
    }
}
