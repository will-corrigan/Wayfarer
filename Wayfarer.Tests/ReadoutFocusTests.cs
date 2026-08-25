namespace Wayfarer.Tests;

/// <summary>Structural proof that the readout keeps the focus posture a controller needs and a
/// player at war does not notice.
///
/// <para><b>Why these are guards and not behaviour tests.</b> The four flags below decide whether the
/// game's own HUD Select can bring the cursor to the readout, and whether the readout can take the
/// cursor when nobody asked it to. Neither question can be asked of a test process: they are
/// answered by the game's cursor, in the game. What a test can do is pin the posture, because every
/// one of these flags reads as harmless on its own and three of them were set for good reasons that
/// have since stopped being true. See <see cref="SourceGuard"/> for what that is worth.</para></summary>
public class ReadoutFocusTests
{
    private const string Host = "Wayfarer/Windows/Native/ReadoutAddon.cs";
    private const string Owner = "Wayfarer/Windows/Native/GuidanceOverlay.cs";
    private const string Body = "Wayfarer/Windows/Native/ReadoutBodyNode.cs";

    /// <summary>The two flags that kept a controller out are gone. <c>DisableFocusability</c> says
    /// "never focus this at all", and the second bit of <c>Flags1A2</c> is what KamiToolKit sets to
    /// take an addon out of controller navigation; either one makes the cog and the plate unreachable
    /// without a mouse, which is the whole defect this posture fixes.</summary>
    [Fact]
    public void TheReadoutStaysFocusableSoAControllerCanReachIt()
    {
        var setup = Setup();

        Assert.DoesNotContain("DisableFocusability", setup, StringComparison.Ordinal);
        Assert.DoesNotContain("Flags1A2", setup, StringComparison.Ordinal);
    }

    /// <summary>But it never takes the cursor by appearing. The readout comes and goes with what the
    /// player is doing, and an addon without this flag is focused by the game when it is shown — so
    /// without it, a readout that appears mid-fight takes the d-pad away from someone using it.
    /// </summary>
    [Fact]
    public void TheReadoutNeverTakesTheCursorUnasked()
    {
        Assert.Contains("DisableFocusOnShow = true", Setup(), StringComparison.Ordinal);
    }

    /// <summary>And Escape cannot make it go away. Both halves: the game's flag for the unfocused
    /// case, and the toolkit's own close-all opt-out where the host is constructed.</summary>
    [Fact]
    public void EscapeCannotCloseTheReadout()
    {
        Assert.Contains("DisableUnfocusedCloseOnEsc = true", Setup(), StringComparison.Ordinal);

        var create = SourceGuard.Body(SourceGuard.SourceOf(Owner), "private void CreateAddon()");
        Assert.Contains("RespectCloseAll = false", create, StringComparison.Ordinal);
    }

    /// <summary>One host, whatever the player is holding. The readout used to pick its host from the
    /// input mode, which is what left a controller looking at controls it could not press; a mention
    /// of the input mode in this decision again would be that regression.</summary>
    [Fact]
    public void TheHostIsChosenWithoutAskingWhatThePlayerIsHolding()
    {
        var decision = SourceGuard.Expression(SourceGuard.SourceOf(Owner), "private bool UseAddonHost");

        Assert.DoesNotContain("InputMode", decision, StringComparison.Ordinal);
        Assert.DoesNotContain("inputMode", decision, StringComparison.Ordinal);
    }

    /// <summary>The anchors the cursor lands on claim no pixels: no size, and no <c>Fill</c> on the
    /// collision node a component builds for itself. Together those are what keep a control that a
    /// pad can reach from being a rectangle that swallows a world click or a camera drag.</summary>
    [Fact]
    public void TheControllerAnchorsClaimNothingOnScreen()
    {
        var anchor = SourceGuard.Body(SourceGuard.SourceOf(Body), "BuildNavAnchor(Action? onSelected");

        Assert.Contains("Size = Vector2.Zero", anchor, StringComparison.Ordinal);
        Assert.Contains("RemoveNodeFlags(NodeFlags.Fill)", anchor, StringComparison.Ordinal);
    }

    private static string Setup() =>
        SourceGuard.Body(SourceGuard.SourceOf(Host), "void OnSetup");
}
