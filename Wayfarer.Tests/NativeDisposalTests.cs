namespace Wayfarer.Tests;

/// <summary>Mechanical proof that the readout host gives the game's UI memory back on the game's own
/// thread.
///
/// <para><b>Why this is a test and not a code review note.</b> Dalamud unloads plugins on a
/// thread-pool thread, so every disposal path in this plugin runs off the main thread on every
/// <c>/xlreload</c> and at game exit. The readout host marshalled its own node tree correctly and
/// then disposed the follow menu four lines earlier, outside the marshalled section — and that menu
/// owns a context-menu event interface allocated out of the game's UI heap, handed back with two
/// <c>IMemorySpace.Free</c> calls. Freeing that heap while the game is using it is unsynchronised
/// mutation: nothing throws, nothing is logged (the toolkit's own off-thread guard logs only under a
/// verbose flag that is compiled off), and the corruption surfaces later somewhere else entirely.
/// There is no test that can observe it and no crash dump that can attribute it.</para>
///
/// <para>These are structural guards — see <see cref="SourceGuard"/>. They pin the ordering the fix
/// depends on; they cannot run it.</para></summary>
public class NativeDisposalTests
{
    private const string ReadoutHost = "Wayfarer/Windows/Native/ClickableReadoutAddon.cs";
    private const string FollowMenu = "Wayfarer/Windows/Native/FollowSwitcherMenu.cs";

    /// <summary>Nothing is freed above the thread check. That is where the old code put the menu — the
    /// check below it guarded the node tree and nothing else — so the first thing to pin is that no
    /// release at all precedes it.</summary>
    [Fact]
    public void TheReadoutHostFreesNothingBeforeItHasTheFrameworkThread()
    {
        var dispose = ReadoutDispose();

        var check = dispose.IndexOf("if (framework.IsInFrameworkUpdateThread)", StringComparison.Ordinal);
        Assert.True(check >= 0, "ClickableReadoutAddon.Dispose no longer checks which thread it is on.");

        var early = "ClickableReadoutAddon.Dispose frees the follow menu before it has the framework thread. That "
            + "menu hands an allocation back to the game's UI heap, and Dalamud unloads plugins on a thread-pool "
            + "thread.";

        Assert.True(dispose.IndexOf("followMenu.Dispose()", StringComparison.Ordinal) > check, early);

        Assert.True(
            dispose.IndexOf("base.Dispose()", StringComparison.Ordinal) > check,
            "ClickableReadoutAddon.Dispose frees its node tree before it has the framework thread.");
    }

    /// <summary>And the menu is freed on every path the node tree is freed on — the
    /// already-on-the-thread shortcut as well as the marshalled call. One of two is how the defect
    /// looked from a distance: correct-looking marshalling with a release outside it.</summary>
    [Fact]
    public void TheMenuIsFreedOnEveryPathTheNodeTreeIs()
    {
        var dispose = ReadoutDispose();

        var menus = SourceGuard.Occurrences(dispose, "followMenu.Dispose()");
        var trees = SourceGuard.Occurrences(dispose, "base.Dispose()");

        Assert.True(trees > 0, "ClickableReadoutAddon.Dispose no longer disposes the addon itself.");
        Assert.Equal(trees, menus);
    }

    /// <summary>The menu is closed before it is freed. Freeing first leaves the game holding a
    /// pointer to released memory in whatever entries are still on screen, and it calls into that
    /// pointer on the next click.</summary>
    [Fact]
    public void TheFollowMenuIsClosedBeforeItIsFreed()
    {
        var dispose = SourceGuard.Body(SourceGuard.SourceOf(FollowMenu), "public void Dispose()");

        var close = dispose.IndexOf("menu?.Close()", StringComparison.Ordinal);
        var free = dispose.IndexOf("menu?.Dispose()", StringComparison.Ordinal);

        Assert.True(close >= 0, "FollowSwitcherMenu.Dispose no longer closes the menu.");
        Assert.True(free > close, "FollowSwitcherMenu.Dispose frees the menu before closing it.");
    }

    private static string ReadoutDispose() =>
        SourceGuard.Body(SourceGuard.SourceOf(ReadoutHost), "public override void Dispose()");
}
