namespace Wayfarer.Tests;

/// <summary>Structural guards on the hub's cursor graph — the three places where writing the right
/// number is not enough. See <see cref="SourceGuard"/> for what a guard of this kind proves: the
/// window links against the game, so none of this can be run.</summary>
public class HubNavigationGraphTests
{
    private const string HubWindow = "Wayfarer/Windows/NativeHubWindow.cs";

    /// <summary>The tab bar and the list both store their <c>Nav*</c> values and copy them onto their
    /// real children inside a private recalculation that only a size change triggers — and this
    /// window's layout pass fires that before the numbering, never after. So the numbering has to
    /// trigger it itself, or every value it wrote is one generation stale: on first open the tab bar's
    /// "up" was still <c>NoNavigation</c>, which left the Following strip and the Stop button
    /// unreachable by pad until the player switched tab.</summary>
    [Fact]
    public void TheNumberingPublishesTheTabBarsAndTheListsOwnLinks()
    {
        var publish = SourceGuard.Body(SourceGuard.SourceOf(HubWindow), "private void PublishOwnLinks()");

        Assert.Contains("hubTabs.Size =", publish, StringComparison.Ordinal);
        Assert.Contains("list.Size =", publish, StringComparison.Ordinal);
    }

    /// <summary>And it publishes before it repairs, because publishing renumbers every row node from
    /// the list's own values — the repair of the toolkit's last-row defect has to be the last write.
    /// </summary>
    [Fact]
    public void TheRowRepairIsTheLastThingWritten()
    {
        var apply = SourceGuard.Body(
            SourceGuard.SourceOf(HubWindow),
            "private void ApplyListNavigation(int populated, int lastRegionIndex, int paneEntry)");

        var publish = apply.IndexOf("PublishOwnLinks()", StringComparison.Ordinal);
        var repair = apply.IndexOf("RepairLastPopulatedRow(", StringComparison.Ordinal);

        Assert.True(publish >= 0 && repair > publish, "The last-row repair must come after the publish that would undo it.");
    }

    /// <summary>"Down" out of the control region must not point into a list that is not on screen.
    /// The row count alone does not answer that: the Settings tab hides the list without emptying it,
    /// so the count is still the previous tab's.</summary>
    [Fact]
    public void DownOutOfTheControlsNeverPointsIntoAHiddenList()
    {
        var apply = SourceGuard.Body(SourceGuard.SourceOf(HubWindow), "private void ApplyNavigation(NodeBase? controls)");

        Assert.Contains("populated > 0 && list.IsVisible", apply, StringComparison.Ordinal);
    }

    /// <summary>Turning the cursor machinery off has to unwire every region, not merely stop
    /// numbering three of them. A region numbered on an earlier pass keeps those indices for ever, so
    /// declining to renumber leaves live nav targets with nothing pointing at them — the same
    /// half-wired window the setting exists to avoid, from the other direction.</summary>
    [Fact]
    public void TurningTheCursorOffUnwiresEveryRegion()
    {
        var remove = SourceGuard.Body(
            SourceGuard.SourceOf(HubWindow), "private void RemoveFromCursorGraph(NodeBase? controls)");

        Assert.Contains("NavigationWalker.Remove(stripControls)", remove, StringComparison.Ordinal);
        Assert.Contains("NavigationWalker.Remove(controls)", remove, StringComparison.Ordinal);
        Assert.Contains("NavigationWalker.Remove(detailPane.ActionRow)", remove, StringComparison.Ordinal);
        Assert.Contains("hubTabs.NavIndex = NavGraphPlanner.NoNavigation", remove, StringComparison.Ordinal);
        Assert.Contains("list.NavIndex = NavGraphPlanner.NoNavigation", remove, StringComparison.Ordinal);
    }
}
