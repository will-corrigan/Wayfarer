using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>Covers the two decisions <see cref="TabOwnership"/> makes for the hub window: what stays
/// visible for a given selected tab, and what the selected tab must become once a module disables.
/// Regression coverage for the live bug where switching to the Settings tab left the shared list (and
/// the detail pane riding on its visibility) drawn underneath it, and for disabling Unlocks/Hunting
/// Log from Settings closing the whole hub instead of leaving the tab the player was on alone.</summary>
public class TabOwnershipTests
{
    public enum Tab
    {
        Checklist,
        Hunting,
        Quests,
        Settings,
    }

    [Fact]
    public void The_shared_list_is_hidden_on_the_excluded_tab()
    {
        Assert.False(TabOwnership.IsVisibleOn(Tab.Settings, Tab.Settings));
    }

    [Theory]
    [InlineData(Tab.Checklist)]
    [InlineData(Tab.Hunting)]
    [InlineData(Tab.Quests)]
    public void The_shared_list_is_visible_on_every_other_tab(Tab selected)
    {
        Assert.True(TabOwnership.IsVisibleOn(selected, Tab.Settings));
    }

    [Fact]
    public void Disabling_a_module_leaves_an_unrelated_selected_tab_untouched()
    {
        // This is the shape every reachable caller hits today: the only control that can disable a
        // module lives on Settings, so the tab going away (Checklist/Hunting) is never the one
        // selected — and the result must say "do nothing", not "switch anyway".
        var resolved = TabOwnership.ResolveAfterModuleDisabled(Tab.Settings, Tab.Checklist, Tab.Settings);

        Assert.Equal(Tab.Settings, resolved);
    }

    [Fact]
    public void Disabling_a_module_whose_tab_is_selected_moves_off_it()
    {
        var resolved = TabOwnership.ResolveAfterModuleDisabled(Tab.Hunting, Tab.Hunting, Tab.Settings);

        Assert.Equal(Tab.Settings, resolved);
    }

    /// <summary>The guard the brief asked for: whatever tab a module-disable resolves to, it can
    /// never be the tab that just went away. A future edit that passes the wrong fallback, or drops
    /// the fallback and returns <paramref name="ownedTab"/>-adjacent state by mistake, fails this
    /// rather than shipping a hub that can select a dead tab.</summary>
    [Theory]
    [InlineData(Tab.Checklist)]
    [InlineData(Tab.Hunting)]
    [InlineData(Tab.Quests)]
    [InlineData(Tab.Settings)]
    public void A_module_disable_can_never_resolve_to_its_own_tab(Tab ownedTab)
    {
        foreach (var current in Enum.GetValues<Tab>())
        {
            var fallback = ownedTab == Tab.Settings ? Tab.Quests : Tab.Settings;

            var resolved = TabOwnership.ResolveAfterModuleDisabled(current, ownedTab, fallback);

            Assert.NotEqual(ownedTab, resolved);
        }
    }

    [Fact]
    public void A_tab_not_selected_never_moves_regardless_of_what_disabled()
    {
        foreach (var owned in Enum.GetValues<Tab>())
        {
            if (owned == Tab.Quests)
            {
                continue;
            }

            var resolved = TabOwnership.ResolveAfterModuleDisabled(Tab.Quests, owned, Tab.Settings);

            Assert.Equal(Tab.Quests, resolved);
        }
    }
}
