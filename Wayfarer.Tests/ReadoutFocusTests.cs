namespace Wayfarer.Tests;

/// <summary>Structural proof that the controller anchors a guidance surface hands the game's cursor
/// claim nothing on screen.
///
/// <para><b>Why this is a guard and not a behaviour test.</b> Whether the game's own HUD Select can
/// bring the cursor to a surface, and whether that surface swallows a world click on the way, are
/// answered by the game's cursor, in the game. What a test can do is pin the posture — and this one
/// reads as harmless on its own while being exactly what makes a pad-reachable control a rectangle
/// that eats a camera drag. See <see cref="SourceGuard"/> for what that is worth.</para></summary>
public class ReadoutFocusTests
{
    private const string Shared = "Wayfarer/Windows/Native/PressTargets.cs";

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
}
