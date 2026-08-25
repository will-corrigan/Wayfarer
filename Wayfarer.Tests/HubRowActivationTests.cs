namespace Wayfarer.Tests;

/// <summary>Mechanical proof that a controller confirm on a hub list row reaches the same handler a
/// mouse click reaches, and therefore that the journal page opens on a pad.
///
/// <para><b>Why this is a test and not a code review note.</b> The chain has four links and three of
/// them are in a vendored library, so no unit test can run it: the row raises <c>OnClick</c>,
/// <c>ListNode</c>'s per-row handler forwards to <c>OnItemSelected</c>, the window points that at
/// <c>OnRowClicked</c>, and <c>OnRowClicked</c> is the only place <c>HubListRow.OpensPage</c> is
/// consulted. Break any link and nothing throws, nothing logs and every test stays green — the
/// player simply presses confirm on an unlock and gets the row's fallback action instead of its
/// page. That is exactly how the page shipped mouse-only: the row overrode <c>OnNavSelected</c> to
/// call <c>ItemData.Activate</c> directly, which skips the decision entirely.</para>
///
/// <para>What can be caught, and is caught here, is the shape of that mistake at every link,
/// including the vendored ones.</para></summary>
public class HubRowActivationTests
{
    private const string RowNode = "Wayfarer/Windows/Native/HubListRowNode.cs";
    private const string HubWindow = "Wayfarer/Windows/NativeHubWindow.cs";
    private const string VendoredList = "external/KamiToolKit/Nodes/Layout/ListNode.cs";

    /// <summary>Link one. The row's controller confirm defers to the base implementation, which
    /// raises <c>OnClick</c> — the same event the vendored <c>SelectableNode</c> raises on
    /// <c>MouseDown</c>. Anything else makes the two inputs two behaviours.</summary>
    [Fact]
    public void ControllerConfirmRaisesTheSameEventAMouseClickDoes()
    {
        var code = SourceGuard.SourceOf(RowNode);

        Assert.Contains("base.OnNavSelected()", code, StringComparison.Ordinal);
    }

    /// <summary>Link one's other half. The row must not act on its own data: reading
    /// <c>Activate</c> or <c>OpensPage</c> here is the bypass, because the row cannot see which of
    /// the two a given press should do — only the window can.</summary>
    [Fact]
    public void TheRowNeverActsOnItsOwnDataInsteadOfRaisingTheEvent()
    {
        var code = SourceGuard.SourceOf(RowNode);

        Assert.DoesNotContain("Activate", code, StringComparison.Ordinal);
        Assert.DoesNotContain("OpensPage", code, StringComparison.Ordinal);
    }

    /// <summary>Links two and three. The window points the list's one selection callback at the one
    /// handler, and that handler is what consults <c>OpensPage</c> and opens the page.</summary>
    [Fact]
    public void TheListsSelectionCallbackIsTheHandlerThatOpensThePage()
    {
        var code = SourceGuard.SourceOf(HubWindow);

        Assert.Contains("OnItemSelected = OnRowClicked", code, StringComparison.Ordinal);

        var handler = SourceGuard.Body(code, "private void OnRowClicked(HubListRow? row)");
        Assert.Contains("OpensPage: true", handler, StringComparison.Ordinal);
        Assert.Contains("OpenJournal(", handler, StringComparison.Ordinal);
        Assert.Contains("Activate?.Invoke()", handler, StringComparison.Ordinal);
    }

    /// <summary>Link four, and the reason link one is safe. The vendored list raises <c>OnClick</c>
    /// on rows its own scroll-follows-focus passes over, which on a held d-pad would fire an
    /// activation per row — but only when <c>AllowMultipleSelection</c> is false. The window sets it
    /// true, so deferring to base cannot storm. Both halves are asserted, because the assumption
    /// lives in a submodule that gets bumped.</summary>
    [Fact]
    public void ScrollFollowsFocusCannotFireActivationsOnThisList()
    {
        Assert.Contains("AllowMultipleSelection = true", SourceGuard.SourceOf(HubWindow), StringComparison.Ordinal);

        var vendored = SourceGuard.SourceOf(VendoredList);
        foreach (var handler in new[] { "private void OnUpNavReceived()", "private void OnDownNavReceived()" })
        {
            var body = SourceGuard.Body(vendored, handler);
            var raise = body.IndexOf("OnClick?.Invoke", StringComparison.Ordinal);
            var gate = body.IndexOf("if (!AllowMultipleSelection)", StringComparison.Ordinal);

            var message = $"KamiToolKit's {handler} no longer gates its OnClick on AllowMultipleSelection. "
                + "HubListRowNode.OnNavSelected defers to base on the strength of that gate; without it, "
                + "a held d-pad activates every row it scrolls past.";

            Assert.True(raise >= 0 && gate >= 0 && gate < raise, message);
        }
    }
}
