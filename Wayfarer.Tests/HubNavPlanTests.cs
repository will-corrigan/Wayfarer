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

    /// <summary>The window's own guard against outgrowing the space it reserved: a region that would
    /// not fit is refused whole rather than wired past its ceiling.
    ///
    /// <para>This used to carry a comment claiming the Settings tab had "22 today (19 static
    /// definitions plus one per registered module), which fits with eight spare". Every number in it
    /// was wrong — 23 static definitions and three modules is 26, against a capacity of 30, so the
    /// real headroom was four — and, worse, nothing asserted any of it. The comment was the only
    /// record of the budget and it was not checked by the test it was attached to. The count is now
    /// asserted for real by
    /// <see cref="The_settings_tab_fits_the_control_region_with_room_to_grow"/>.</para></summary>
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

    /// <summary>The whole point of clamping the pool: the block's last index is a constant rather
    /// than a function of the player's window height, and it is under the byte ceiling.</summary>
    [Fact]
    public void The_clamped_list_block_stays_inside_the_index_space()
    {
        Assert.Equal(HubNavPlan.ListPoolLimit, HubNavPlan.MaxListPoolSize);
        Assert.True(
            HubNavPlan.ListLast <= NavGraphPlanner.MaxIndex,
            $"the list block ends at {HubNavPlan.ListLast}, past the {NavGraphPlanner.MaxIndex} ceiling.");
    }

    /// <summary>The budget that was only ever a comment, asserted.
    ///
    /// <para>The Settings tab numbers <b>one index per setting</b> — headings are text nodes and cost
    /// nothing, and a slider's caption-plus-slider pair costs one, for its slider. When the count
    /// passes <see cref="HubNavPlan.RegionCapacity"/> the failure is silent and total:
    /// <c>NavigationWalker.Apply</c> refuses the region rather than overrunning it, so the whole tab
    /// becomes unreachable with a controller and the only trace is one line in the log. Nothing
    /// checked this before, and the comment that stood in for a check was out by four.</para>
    ///
    /// <para>Counted out of the catalogue's source because the catalogue lives in the plugin
    /// assembly, which references Dalamud and cannot be loaded here — the same reason
    /// <see cref="SettingsCopyTests"/> reads it that way.</para></summary>
    [Fact]
    public void The_settings_tab_fits_the_control_region_with_room_to_grow()
    {
        var source = SourceGuard.SourceOf(Path.Combine("Wayfarer", "Settings", "SettingsCatalog.cs"));

        // One of the declarations is inside the per-module loop, so it is not a static definition —
        // it is the allowance that is multiplied by however many modules are registered.
        var declarations = SourceGuard.Occurrences(source, "new SettingDefinition");
        Assert.True(declarations > 0, "no settings were found in the catalogue's source.");

        // Generous on purpose: the guard is worth having only if it still holds after a few more
        // modules are registered, and being told now is the entire point.
        const int PlausibleModules = 8;
        var worstCase = declarations - 1 + PlausibleModules;

        var message =
            $"the Settings tab would number {worstCase} controls ({declarations - 1} settings plus "
            + $"{PlausibleModules} modules) into a region that reserves {HubNavPlan.RegionCapacity}. "
            + "NavigationWalker.Apply refuses a region that does not fit, so the tab would become "
            + "entirely unreachable with a controller. Raise RegionCapacity and move HubNavPlan.List.";

        Assert.True(worstCase <= HubNavPlan.RegionCapacity, message);
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
