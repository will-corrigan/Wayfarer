namespace Wayfarer.Core.Ui;

/// <summary>The hub window's single flat cursor-navigation index space, written down in one place
/// because the indices are absolute: two regions that overlap do not fail, they teleport the
/// cursor, and a region pushed past 255 does not fail either, it silently disappears from the
/// graph. Both are invisible without a controller in hand, so <see cref="Validate"/> exists to be
/// asserted by a unit test rather than discovered in the field.
///
/// <code>
///   0        reserved — "no navigation"
///   1..5     hub tab bar (Following | Unlocks | Hunting Log | Settings), with room for a fifth
///   6..9     the Following strip's own controls, on screen whatever tab is open
///   10..39   the active tab's control region: filter chips, action buttons, or — on the Settings
///            tab — every setting control, numbered top to bottom by the walker
///   40       the list node itself (its upward scroll sentinel)
///   41..     list rows, four indices apart (KamiToolKit reserves four slots per row)
///   40 + 4n + 1  the downward scroll sentinel, immediately after the last pooled row
///   170..179 the detail pane's action buttons
/// </code>
///
/// <para><b>Why the list pool is clamped.</b> A region <i>after</i> the list was impossible to
/// place while the pool was "as many rows as fit under 255": the list's extent depended on the pool
/// size, which depended on the window's height, so no fixed index below the ceiling was safe. The
/// pool is capped at <see cref="ListPoolLimit"/> instead, which pins the list block's last index and
/// frees everything above it. The cap is not a real limit — thirty 44px rows is 1,320px of list,
/// taller than the window can ever be at its own viewport cap — but it is a limit that can be
/// asserted, which the previous arrangement was not.</para></summary>
public static class HubNavPlan
{
    /// <summary>The tab bar's own index. A tab bar consumes <c>NavIndex .. NavIndex + tabs - 1</c>
    /// — tab 0 sits <b>on</b> the bar's own index, it does not follow it.</summary>
    public const int TabBar = 1;

    /// <summary>Checklist, Hunting Log, Quests, Settings.</summary>
    public const int TabCount = 4;

    /// <summary>First index of the Following strip's controls — the one row that is on screen
    /// whatever tab is open. Above the tab bar in the graph because it is above it on screen.</summary>
    public const int Strip = 6;

    /// <summary>Indices reserved for the strip. Two buttons today (Change, Stop).</summary>
    public const int StripCapacity = 4;

    /// <summary>First index of the active tab's control region.</summary>
    public const int Region = 10;

    /// <summary>Indices reserved for the control region before the list block starts.
    ///
    /// <para>Written down as its own number rather than derived as <c>List - Region</c>. Derived,
    /// it could never disagree with the layout, which made <see cref="Validate"/>'s collision
    /// guard unreachable and its covering test the tautology <c>39 &lt; 40</c> — moving
    /// <see cref="List"/> from 40 to 12 left the whole suite green. The two are independent now,
    /// so the guard has something real to compare.</para></summary>
    public const int RegionCapacity = 30;

    /// <summary>The list block's own index.</summary>
    public const int List = 40;

    /// <summary>Rows the list's recycled node pool is allowed to grow to.
    ///
    /// <para>The window has to enforce this by capping the list's <b>height</b>, because that is
    /// what KamiToolKit derives the pool from (<c>(int)(Height / (itemHeight + ItemSpacing))</c>).
    /// Thirty 44px rows is 1,320px, which no window this plugin can produce reaches — so the cap
    /// costs nothing and buys a list block whose last index is a constant instead of a function of
    /// the player's screen.</para></summary>
    public const int ListPoolLimit = 30;

    /// <summary>First index of the detail pane's action row. Placed above the clamped list block
    /// rather than below the control region, so growing either the region or the pane cannot
    /// silently push the other into the list's rows.</summary>
    public const int DetailPane = 170;

    /// <summary>Indices reserved for the detail pane. Three buttons is the most any status offers;
    /// ten leaves room to be wrong about that without another re-plan.</summary>
    public const int DetailPaneCapacity = 10;

    /// <summary>Highest index the tab bar occupies.</summary>
    public static int TabBarLast => TabBar + TabCount - 1;

    /// <summary>Largest row pool the list may carry: the clamp, or the byte ceiling, whichever
    /// bites first.</summary>
    public static int MaxListPoolSize => Math.Min(ListPoolLimit, NavListBlock.MaxPoolSize(List));

    /// <summary>Last index the list block occupies at its maximum pool size — the downward scroll
    /// sentinel. Everything numbered after the list has to start above this.</summary>
    public static int ListLast => NavListBlock.DownwardSentinelIndex(List, MaxListPoolSize);

    /// <summary>Confirms the plan is internally consistent: nothing sits on the reserved 0 index,
    /// the three regions do not overlap, and a full-size list still fits under the 255 ceiling.
    /// Returns the reason it does not hold, or <see langword="null"/> when it does.</summary>
    public static string? Validate()
    {
        // Read through locals so the compiler treats these as real comparisons rather than
        // folding the constants and calling the guards unreachable — the point of this method is
        // that a future edit to one of the constants makes a test fail instead of a controller
        // silently losing a region.
        var tabBar = TabBar;
        var tabBarLast = TabBarLast;
        var region = Region;
        var regionEnd = Region + RegionCapacity;
        var list = List;

        if (tabBar < 1)
        {
            return "The tab bar must not occupy the reserved index 0.";
        }

        var strip = Strip;
        var stripEnd = Strip + StripCapacity;
        if (tabBarLast >= strip)
        {
            return $"The tab bar ends at {tabBarLast}, which collides with the Following strip at {strip}.";
        }

        if (stripEnd > region)
        {
            return $"The Following strip ({strip}..{stripEnd - 1}) collides with the control region at {region}.";
        }

        if (tabBarLast >= region)
        {
            return $"The tab bar ends at {tabBarLast}, which collides with the control region at {region}.";
        }

        if (regionEnd > list)
        {
            return $"The control region ({region}..{regionEnd - 1}) collides with the list block at {list}.";
        }

        if (!NavListBlock.Fits(List, MaxListPoolSize))
        {
            return $"A list of {MaxListPoolSize} rows starting at {list} exceeds the {NavGraphPlanner.MaxIndex} index ceiling.";
        }

        var listLast = ListLast;
        var pane = DetailPane;
        if (listLast >= pane)
        {
            return $"The list block ends at {listLast}, which collides with the detail pane at {pane}.";
        }

        var paneEnd = DetailPane + DetailPaneCapacity - 1;
        return paneEnd <= NavGraphPlanner.MaxIndex
            ? null
            : $"The detail pane ({pane}..{paneEnd}) exceeds the {NavGraphPlanner.MaxIndex} index ceiling.";
    }
}
