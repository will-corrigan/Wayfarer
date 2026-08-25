namespace Wayfarer.Tests;

/// <summary>Mechanical proof that "the journal page is open" cannot drift from whether a journal
/// page is actually on screen.
///
/// <para><b>What went wrong.</b> The hub recorded the row a page was opened for <i>before</i> it knew
/// the page had opened, and that record is what switches the open tab's per-tick refresh off. Two
/// ordinary things stop an open succeeding: the addon refuses to reopen while its previous close is
/// still finishing, and the page switches itself off permanently after one of its own steps throws.
/// Either left the tab frozen on the rows it happened to be showing while everything around it —
/// the Following strip, the button hint — carried on refreshing. So the window looked alive and
/// lied: a finished unlock kept reading Available, a dead hunting target kept its count, the quest
/// objective went stale. Nothing recovered until the player switched tab or closed the window.
/// Reachable by pressing Cancel and then Confirm within the fade.</para>
///
/// <para><b>What this proves and what it does not.</b> These are structural guards — see
/// <see cref="SourceGuard"/>. They pin that the code still reconciles the two states and still
/// retries; they cannot run the reconciliation, because it lives in a window that links against the
/// game.</para></summary>
public class HubJournalPageStateTests
{
    private const string HubWindow = "Wayfarer/Windows/NativeHubWindow.cs";
    private const string Plugin = "Wayfarer/Plugin.cs";

    /// <summary>The reconciliation itself. Asking only what this window believes is the defect; the
    /// other window's own open state has to be part of the answer, so a page that is not there can
    /// never hold the refresh off.</summary>
    [Fact]
    public void TheRefreshGateAsksTheJournalWindowAndNotOnlyItsOwnRecord()
    {
        var gate = SourceGuard.Expression(SourceGuard.SourceOf(HubWindow), "private bool IsPageOpen");

        Assert.Contains("pageRow is not null", gate, StringComparison.Ordinal);
        Assert.Contains("journal.IsOpen", gate, StringComparison.Ordinal);
    }

    /// <summary>The row is recorded as the open page only on the branch where the page said it is
    /// open; the other branch parks it. A single unconditional assignment here is the original
    /// defect, so both branches are required to be present.</summary>
    [Fact]
    public void TheOpenPageIsRecordedOnlyOnceThePageSaysItIsOpen()
    {
        var open = SourceGuard.Body(SourceGuard.SourceOf(HubWindow), "private void OpenJournal(");

        Assert.Contains("if (journal.IsOpen)", open, StringComparison.Ordinal);
        Assert.Contains("pageRow = row;", open, StringComparison.Ordinal);
        Assert.Contains("pendingPage = (row, detail);", open, StringComparison.Ordinal);
    }

    /// <summary>And a parked page is actually retried, from the per-frame tick, with a budget — so a
    /// confirm pressed during a fade opens the page a frame later instead of doing nothing, and a
    /// page that can never open stops being asked for.</summary>
    [Fact]
    public void AParkedPageIsRetriedFromTheFrameworkTickAndThenGivenUpOn()
    {
        var code = SourceGuard.SourceOf(HubWindow);

        Assert.Contains("RetryPendingPage();", SourceGuard.Body(code, "private void OnFrameworkUpdate("), StringComparison.Ordinal);

        var retry = SourceGuard.Body(code, "private void RetryPendingPage()");
        Assert.Contains("journal.Show(", retry, StringComparison.Ordinal);
        Assert.Contains("pendingPageFrames--", retry, StringComparison.Ordinal);
        Assert.Contains("journal.IsAvailable", retry, StringComparison.Ordinal);
    }

    /// <summary>Every route that takes the page away drops a parked one too. Otherwise the retry
    /// re-opens a window the player has just dismissed, or opens a page built from a row the list has
    /// since replaced.</summary>
    [Theory]
    [InlineData("private void OnJournalClosed()")]
    [InlineData("private void DismissJournalPage()")]
    public void EveryRouteThatClosesThePageAlsoDropsAParkedOne(string declaration)
    {
        var body = SourceGuard.Body(SourceGuard.SourceOf(HubWindow), declaration);

        Assert.Contains("pendingPage = null;", body, StringComparison.Ordinal);
    }

    /// <summary>The other half of the fix, and the one that keeps the retry rare: without it the
    /// addon reports itself closed for the whole of its hide animation while refusing to reopen, and
    /// that window is many frames wide. The readout's own host already does this.</summary>
    [Fact]
    public void TheJournalDoesNotFadeOnTheWayOut()
    {
        var build = SourceGuard.Body(SourceGuard.SourceOf(Plugin), "private JournalWindow BuildJournal(");

        Assert.Contains("DisableCloseTransition = true", build, StringComparison.Ordinal);
    }
}
