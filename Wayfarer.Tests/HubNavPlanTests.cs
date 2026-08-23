using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

public class HubNavPlanTests
{
    [Fact]
    public void The_hub_index_plan_holds()
    {
        Assert.Null(HubNavPlan.Validate());
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
}
