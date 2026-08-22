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
        // reserved capacity — that must still stop short of the list block.
        var rows = Enumerable.Repeat(1, HubNavPlan.RegionCapacity).ToList();

        Assert.True(NavGraphPlanner.HighestIndex(rows, HubNavPlan.Region) < HubNavPlan.List);
        Assert.True(HubNavPlan.TabBarLast < HubNavPlan.Region);
    }

    [Fact]
    public void A_full_size_hub_stays_under_the_index_ceiling()
    {
        Assert.True(HubNavPlan.MaxListPoolSize > 20, "the hub must be able to show a screenful of rows");
        Assert.True(
            NavListBlock.DownwardSentinelIndex(HubNavPlan.List, HubNavPlan.MaxListPoolSize) <= NavGraphPlanner.MaxIndex);
    }
}
