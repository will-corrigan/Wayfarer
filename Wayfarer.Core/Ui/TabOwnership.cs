namespace Wayfarer.Core.Ui;

/// <summary>Two small, pure decisions behind the hub window's tab machinery, both about the one
/// invariant that broke live: nothing that belongs to a tab may keep showing, or keep being the
/// selected tab, once that tab is no longer the one on screen.
///
/// <para>Kept generic over the tab type — plain enums, which is what the hub's tab identifier is,
/// implement <see cref="object.Equals(object?)"/> but not <see cref="IEquatable{T}"/>, so this
/// compares through <see cref="EqualityComparer{T}.Default"/> rather than constraining T — so both
/// invariants are checked by a fast unit test here in Core, without pulling the game-dependent hub
/// window — and with it Dalamud and KamiToolKit — into the test project just to exercise a
/// comparison.</para></summary>
public static class TabOwnership
{
    /// <summary>Whether a node that is shared by every tab except <paramref name="excludedTab"/>
    /// should be visible while <paramref name="selectedTab"/> is on screen. The hub's shared list —
    /// and the detail pane, which mirrors the list's visibility — used exactly this rule for every
    /// tab but Settings, and nothing ever told them what to do for Settings, so they stayed visible
    /// underneath it forever after the first time any list tab was shown.</summary>
    /// <typeparam name="T">The tab identifier type — an enum in every real caller.</typeparam>
    public static bool IsVisibleOn<T>(T selectedTab, T excludedTab)
        => !EqualityComparer<T>.Default.Equals(selectedTab, excludedTab);

    /// <summary>What the selected tab must become once the module owning <paramref name="ownedTab"/>
    /// disables. Leaves <paramref name="currentTab"/> untouched unless it is exactly the tab going
    /// away, in which case it resolves to <paramref name="fallbackTab"/> — never
    /// <paramref name="ownedTab"/> itself, so a caller can never end up with the selected tab
    /// pointing at a module that just went dark, and a tab the player is not looking at is never
    /// disturbed by a toggle that has nothing to do with it.</summary>
    /// <typeparam name="T">The tab identifier type — an enum in every real caller.</typeparam>
    public static T ResolveAfterModuleDisabled<T>(T currentTab, T ownedTab, T fallbackTab)
        => EqualityComparer<T>.Default.Equals(currentTab, ownedTab) ? fallbackTab : currentTab;
}
