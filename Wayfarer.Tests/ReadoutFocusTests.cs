namespace Wayfarer.Tests;

/// <summary>Structural proofs about the surfaces a controller reaches Wayfarer through: that the
/// anchors the game's cursor lands on claim nothing on screen, and that the game's own context menu
/// renders Wayfarer's one action source rather than words of its own.
///
/// <para><b>Why these are guards and not behaviour tests.</b> Neither can be asked of a test
/// process — one is answered by the game's cursor, in the game, and the other is a menu that links
/// against the client. What a test can do is pin the shape, and in both cases the shape is the whole
/// defect: each reads as harmless on its own while being exactly what makes a pad-reachable control
/// a rectangle that eats a camera drag, or a menu that quietly drifts from the source it is supposed
/// to render. See <see cref="SourceGuard"/> for what that is worth.</para></summary>
public class ReadoutFocusTests
{
    private const string Shared = "Wayfarer/Windows/Native/PressTargets.cs";
    private const string GameMenu = "Wayfarer/ContextMenuActions.cs";

    /// <summary>The anchors the cursor lands on claim no pixels: no size, and no <c>Fill</c> on the
    /// collision node a component builds for itself. Together those are what keep a control that a
    /// pad can reach from being a rectangle that swallows a world click or a camera drag.</summary>
    [Fact]
    public void TheControllerAnchorsClaimNothingOnScreen()
    {
        var anchor = SourceGuard.Body(SourceGuard.SourceOf(Shared), "BuildNavAnchor(Action? onSelected");

        Assert.Contains("Size = Vector2.Zero", anchor, StringComparison.Ordinal);
        Assert.Contains("RemoveNodeFlags(NodeFlags.Fill)", anchor, StringComparison.Ordinal);
    }

    /// <summary>The game's right-click menu is a renderer and nothing more: it asks
    /// <see cref="SourceGuard"/>-visibly for each of <c>GuidanceActions</c>' three lists and writes no
    /// label of its own.
    ///
    /// <para><b>A menu label written out here is the drift this guards against.</b> The words and the
    /// conditions belong in one place, so a menu that spells one out is a menu that can come to
    /// disagree with the source — offering an entry the source would have withheld, or naming it
    /// differently. <c>"Start Hunting"</c> stays as a forbidden needle even though the hunting log is
    /// gone: it is precisely the kind of hard-coded label this forbids, and a renderer that can no
    /// longer legitimately contain it is a stricter check, not a weaker one.</para></summary>
    [Fact]
    public void TheGamesMenuRendersWayfarersOneActionSource()
    {
        var gameMenu = SourceGuard.SourceOf(GameMenu);

        Assert.Contains("actions.Route()", gameMenu, StringComparison.Ordinal);
        Assert.Contains("actions.Windows()", gameMenu, StringComparison.Ordinal);
        Assert.Contains("actions.Follow()", gameMenu, StringComparison.Ordinal);

        Assert.DoesNotContain("Open Settings", gameMenu, StringComparison.Ordinal);
        Assert.DoesNotContain("Start Hunting", gameMenu, StringComparison.Ordinal);
    }
}
