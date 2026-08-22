namespace Wayfarer.Core.Ui;

/// <summary>The hub window's single flat cursor-navigation index space, written down in one place
/// because the indices are absolute: two regions that overlap do not fail, they teleport the
/// cursor, and a region pushed past 255 does not fail either, it silently disappears from the
/// graph. Both are invisible without a controller in hand, so <see cref="Validate"/> exists to be
/// asserted by a unit test rather than discovered in the field.
///
/// <code>
///   0        reserved — "no navigation"
///   1..3     hub tab bar (Checklist | Hunting Log | Settings)
///   10..39   the active tab's control region: filter chips, action buttons, or — on the Settings
///            tab — every setting control, numbered top to bottom by the walker
///   40       the list node itself (its upward scroll sentinel)
///   41..     list rows, four indices apart (KamiToolKit reserves four slots per row)
///   40 + 4n + 1  the downward scroll sentinel, immediately after the last pooled row
/// </code></summary>
public static class HubNavPlan
{
    /// <summary>The tab bar's own index. A tab bar consumes <c>NavIndex .. NavIndex + tabs - 1</c>
    /// — tab 0 sits <b>on</b> the bar's own index, it does not follow it.</summary>
    public const int TabBar = 1;

    /// <summary>Checklist, Hunting Log, Settings.</summary>
    public const int TabCount = 3;

    /// <summary>First index of the active tab's control region.</summary>
    public const int Region = 10;

    /// <summary>Indices reserved for the control region before the list block starts.</summary>
    public const int RegionCapacity = List - Region;

    /// <summary>The list block's own index.</summary>
    public const int List = 40;

    /// <summary>Highest index the tab bar occupies.</summary>
    public static int TabBarLast => TabBar + TabCount - 1;

    /// <summary>Largest row pool the list can carry inside the byte index space.</summary>
    public static int MaxListPoolSize => NavListBlock.MaxPoolSize(List);

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

        if (tabBarLast >= region)
        {
            return $"The tab bar ends at {tabBarLast}, which collides with the control region at {region}.";
        }

        if (regionEnd > list)
        {
            return $"The control region ({region}..{regionEnd - 1}) collides with the list block at {list}.";
        }

        return NavListBlock.Fits(List, MaxListPoolSize)
            ? null
            : $"A list of {MaxListPoolSize} rows starting at {list} exceeds the {NavGraphPlanner.MaxIndex} index ceiling.";
    }
}
