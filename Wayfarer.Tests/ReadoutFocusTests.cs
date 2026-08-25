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

    /// <summary>Every one of the readout's controls gets an anchor, so a controller reaches each of
    /// them the way a mouse clicks it. Cutting this back to the plate alone was tried and rejected —
    /// the controls are the design.
    ///
    /// <para>The banner's three come first and the pressable lines are appended after them, which is
    /// asserted as well as counted: the plate's index is what the host hands the game as the addon's
    /// focus node, so it must not move because a line was added.</para></summary>
    [Fact]
    public void EveryControlOnTheReadoutIsReachableWithAPad()
    {
        var anchors = SourceGuard.Body(SourceGuard.SourceOf(Body), "private void BuildInteractions(");
        var slots = new[] { "NavCog", "NavBanner", "NavSwitcher", "NavTeleport", "NavDuty" };

        Assert.Equal(slots.Length, SourceGuard.Occurrences(anchors, "BuildNavAnchor("));
        foreach (var slot in slots)
        {
            Assert.Contains($"navTargets[{slot}]", anchors, StringComparison.Ordinal);
        }

        Assert.Contains(
            "private const int NavBanner = 1;", SourceGuard.SourceOf(Body), StringComparison.Ordinal);
    }

    /// <summary>A pressable line's three parts, which are only right together — asserted once over the
    /// shared machinery rather than once per line, because "once per line" is how a second line comes
    /// to be wired up almost right.
    ///
    /// <para><b>One rectangle, two devices.</b> The pointer's click box and the pad's anchor are the
    /// same node: the box is placed once, from the line's own measured text, and the anchor is
    /// mirrored onto it. Give either device a rectangle of its own and the two drift apart on the
    /// next change to the readout's spacing, with nothing to say so — which is exactly what the box's
    /// height stopped being a fraction of its block to prevent.</para>
    ///
    /// <para><b>And the highlight is not that rectangle.</b> The box is the full width of the line
    /// because a generous target is right for a pointer and necessary for a pad; the hover lights the
    /// line's own text node, so a short place name does not light a band of empty plate beside
    /// itself. Every one of the three reads as harmless to change on its own.</para>
    ///
    /// <para><b>And every press goes through all three.</b> The last assertion is what makes this one
    /// guard rather than a guard for the teleport line and a hole for the next one: the box, the
    /// anchor and the highlight are all indexed by the same press, so a new pressable line is a
    /// constant in the source rather than a fourth copy of these three methods — and a copy would show
    /// up here as a hard-coded name where an index belongs.</para></summary>
    [Fact]
    public void APressableLinesTargetIsOneRectangleAndItsHighlightIsNot()
    {
        var body = SourceGuard.SourceOf(Body);

        // The box: the whole width of the line, and a height measured from the line's own text rather
        // than taken as a fraction of the block it sits in.
        var box = SourceGuard.Body(body, "private int LayoutLineHitBoxes()");
        Assert.Contains("box.Size = new Vector2(slot.Width,", box, StringComparison.Ordinal);
        Assert.Contains("slot.FontSize", box, StringComparison.Ordinal);
        Assert.DoesNotContain("slot.Height *", box, StringComparison.Ordinal);

        // The anchor: that same node, so the d-pad cannot come to rest anywhere the pointer cannot
        // click — and for every press, not just the first one that ever had a box.
        var settle = SourceGuard.Body(body, "private void SettleNav()");
        Assert.Contains("MirrorNav(navTargets[NavFor(line)], lineHitBoxes[line])", settle, StringComparison.Ordinal);

        // The highlight: the words, and never the box.
        var highlight = SourceGuard.Body(body, "private void SetLineHighlight(");
        Assert.Contains("lineNodes[slot.Index].Alpha", highlight, StringComparison.Ordinal);
        Assert.DoesNotContain("lineHitBoxes", highlight, StringComparison.Ordinal);

        // All three walk the same set, so a press cannot have two of them and not the third.
        foreach (var walk in new[] { box, settle, SourceGuard.Body(body, "private void ClaimPresses(") })
        {
            Assert.Contains("PressableLineCount", walk, StringComparison.Ordinal);
        }
    }

    /// <summary>The plate's second press is the game's own "Display Subcommands" (<c>InputId.MENU</c>,
    /// <c>ConfigKey</c> row 215), not a button of our choosing, and it is a press BESIDE Confirm
    /// rather than instead of it — Confirm on the plate still opens the Journal, exactly as a click
    /// does. Both halves are asserted, because either one alone would be the wrong design.</summary>
    [Fact]
    public void ThePlateAnswersTheGamesOwnSubcommandPress()
    {
        var subcommand = SourceGuard.Body(SourceGuard.SourceOf(Body), "private void AddSubcommand(");

        Assert.Contains("InputId.MENU", subcommand, StringComparison.Ordinal);
        Assert.Contains("AtkEventType.InputReceived", subcommand, StringComparison.Ordinal);
        Assert.DoesNotContain("InputId.OK", subcommand, StringComparison.Ordinal);

        // The plate's Confirm is still the Journal: its anchor is built from the same callback the
        // plate's own mouse hit box is.
        var anchors = SourceGuard.Body(SourceGuard.SourceOf(Body), "private void BuildInteractions(");
        Assert.Contains("BuildNavAnchor(onQuestNameClicked", anchors, StringComparison.Ordinal);
    }

    /// <summary>Both menus onto Wayfarer's actions render the one source, so neither can offer
    /// something the other does not. A menu label written out in either renderer is the drift this
    /// guards against.
    ///
    /// <para>The followable set is the one place they legitimately differ, and each still reads a
    /// single source: the readout's menu lists what the switcher cap beside it lists (the window's
    /// Following tab's own choices, every accepted quest included), while the game's menu keeps the
    /// shorter list it has always had, ending in the hand-off to that tab.</para></summary>
    [Fact]
    public void BothMenusRenderTheSameActionSource()
    {
        var readoutMenu = SourceGuard.SourceOf("Wayfarer/Windows/Native/ReadoutMenu.cs");
        var gameMenu = SourceGuard.SourceOf("Wayfarer/ContextMenuActions.cs");

        foreach (var source in new[] { readoutMenu, gameMenu })
        {
            Assert.Contains("actions.Route()", source, StringComparison.Ordinal);
            Assert.Contains("actions.Windows()", source, StringComparison.Ordinal);

            // The words and the conditions live in GuidanceActions; a renderer that spells one out
            // is a renderer that can come to disagree with the other.
            Assert.DoesNotContain("Open Settings", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Start Hunting", source, StringComparison.Ordinal);
        }

        Assert.Contains("actions.Follow()", gameMenu, StringComparison.Ordinal);
        Assert.Contains("getFollowChoices()", readoutMenu, StringComparison.Ordinal);
        Assert.Contains("FollowSwitcherMenu.Entry(", readoutMenu, StringComparison.Ordinal);
    }

    private static string Setup() =>
        SourceGuard.Body(SourceGuard.SourceOf(Host), "void OnSetup");
}
