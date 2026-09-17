namespace Wayfarer.Tests;

/// <summary>Structural proof that every surface onto "what am I following" reads one decision, and
/// that no control that offers a press can be pressed and do nothing.
///
/// <para><b>Why these are guards.</b> None of the code below can be instantiated in a test process —
/// it is windows, native nodes and menus that link against the game. What a test can do is pin the
/// shape, and the shape is the whole defect: every one of these conditions read as harmless on its
/// own, and every one of them was a control that accepted a press from a controller and did nothing.
/// See <see cref="SourceGuard"/> for what that is worth and what it is not.</para></summary>
public class FollowSurfaceTests
{
    private const string Actions = "Wayfarer/GuidanceActions.cs";
    private const string Navigator = "Wayfarer/QuestNavigator.cs";

    /// <summary>Every surface that offers a way back to the Main Scenario derives it from the one
    /// decision, and none of them reads the followed-quest override to make that judgement.
    ///
    /// <para><c>FollowedOverride is null</c> is not the same fact as "following the main scenario"
    /// once a source can be engaged without setting it. Surfaces that read it that way concluded,
    /// mid-route, that the player was already home and greyed their own entry out.</para></summary>
    [Fact]
    public void EveryWayBackToTheMainScenarioReadsTheOneDecision()
    {
        // The navigator computes it once, from the live snapshot.
        var reset = SourceGuard.Expression(SourceGuard.SourceOf(Navigator), "public FollowReset MainScenarioReset");
        Assert.Contains("MainScenarioReturn.From(", reset, StringComparison.Ordinal);
        Assert.Contains("Current.Engaged", reset, StringComparison.Ordinal);

        // The menu entry that is not a Stop.
        var windows = SourceGuard.Body(SourceGuard.SourceOf(Actions), "public IReadOnlyList<GuidanceAction> Windows()");
        Assert.Contains("MainScenarioReset.Acts", windows, StringComparison.Ordinal);
    }

    /// <summary>The Main Scenario entry is offered unconditionally, and it performs both halves of the
    /// reset. Either half alone is not a way home: clearing the quest leaves the engaged source
    /// running, and releasing the source drops the player onto a side quest they did not ask
    /// for.</summary>
    [Fact]
    public void TheMainScenarioEntryAlwaysActsAndActsCompletely()
    {
        var source = SourceGuard.SourceOf(Actions);

        var follow = SourceGuard.Body(source, "public IReadOnlyList<GuidanceAction> Follow()");
        Assert.Contains("new GuidanceAction(MainScenarioLabel, ReturnToMainScenario)", follow, StringComparison.Ordinal);

        var reset = SourceGuard.Body(source, "private void ReturnToMainScenario()");
        Assert.Contains("navigator.ClearPickup()", reset, StringComparison.Ordinal);
        Assert.Contains("navigator.FollowedOverride = null", reset, StringComparison.Ordinal);
    }

    /// <summary>The subject's press always has somewhere to go. <c>Subject()</c> returns a
    /// non-nullable action, so there is no arrangement of state in which a hit box, a controller
    /// anchor and the first entry of a menu exist over nothing.</summary>
    [Fact]
    public void ThePlateAlwaysOpensSomething()
    {
        var source = SourceGuard.SourceOf(Actions);

        // Not GuidanceAction? — the nullability IS the guarantee.
        Assert.Contains("public GuidanceAction Subject()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public GuidanceAction? Subject()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public GuidanceAction? Journal()", source, StringComparison.Ordinal);

        // A quest goes to the game's own Journal, and everything else to settings — never to nothing.
        var subject = SourceGuard.Body(source, "public GuidanceAction Subject()");
        Assert.Contains("QuestJournalAction.Execute", subject, StringComparison.Ordinal);
        Assert.Contains("openSettings", subject, StringComparison.Ordinal);
    }

    /// <summary>The teleport line is marked as a control only when the id its press needs is there.
    /// The words still name the aetheryte either way — what is withheld is the mark, and the mark is
    /// what the hit box and the d-pad anchor are built from.</summary>
    [Fact]
    public void TheTeleportLineIsOnlyMarkedWhenItCanBeTaken()
    {
        var advice = SourceGuard.Body(
            SourceGuard.SourceOf("Wayfarer.Core/Ui/ReadoutComposer.cs"), "private static void AddTeleportAdvice(");

        Assert.Contains("state.AetheryteId is null", advice, StringComparison.Ordinal);
        Assert.Contains("ReadoutLineAction.Teleport", advice, StringComparison.Ordinal);
    }
}
