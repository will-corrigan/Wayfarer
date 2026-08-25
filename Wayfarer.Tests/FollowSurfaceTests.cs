namespace Wayfarer.Tests;

/// <summary>Structural proof that the four surfaces onto "what am I following" read one decision
/// each, and that no control on the readout can be pressed and do nothing.
///
/// <para><b>Why these are guards.</b> None of the code below can be instantiated in a test process —
/// it is windows, native nodes and menus that link against the game. What a test can do is pin the
/// shape, and the shape is the whole defect: every one of these conditions read as harmless on its
/// own, and every one of them was a control that accepted a press from a controller and did nothing.
/// See <see cref="SourceGuard"/> for what that is worth and what it is not.</para></summary>
public class FollowSurfaceTests
{
    private const string Hub = "Wayfarer/Windows/NativeHubWindow.cs";
    private const string Actions = "Wayfarer/GuidanceActions.cs";
    private const string Navigator = "Wayfarer/QuestNavigator.cs";
    private const string Overlay = "Wayfarer/Windows/Native/GuidanceOverlay.cs";
    private const string Body = "Wayfarer/Windows/Native/ReadoutBodyNode.cs";
    private const string Switcher = "Wayfarer/Windows/Native/FollowSwitcherMenu.cs";
    private const string HuntingSource = "Wayfarer/Guidance/Sources/HuntingSource.cs";
    private const string HuntingFallback = "Wayfarer/Windows/HuntingWindow.cs";

    /// <summary>Every surface that offers a way back to the Main Scenario derives it from the one
    /// decision, and none of them reads the followed-quest override to make that judgement.
    ///
    /// <para><c>FollowedOverride is null</c> is not the same fact as "following the main scenario"
    /// once a hunt or an unlock route can be engaged without setting it. Two surfaces read it that way
    /// and both concluded, mid-hunt, that the player was already home: the switcher greyed its own
    /// entry out and the window's "Resume Main Scenario" button went dead.</para></summary>
    [Fact]
    public void EveryWayBackToTheMainScenarioReadsTheOneDecision()
    {
        // The navigator computes it once, from the live snapshot.
        var reset = SourceGuard.Expression(SourceGuard.SourceOf(Navigator), "public FollowReset MainScenarioReset");
        Assert.Contains("MainScenarioReturn.From(", reset, StringComparison.Ordinal);
        Assert.Contains("Current.Engaged", reset, StringComparison.Ordinal);

        // The window's button.
        var refresh = SourceGuard.Body(SourceGuard.SourceOf(Hub), "private void RefreshQuestActions()");
        Assert.Contains("MainScenarioReset.Acts", refresh, StringComparison.Ordinal);
        Assert.DoesNotContain("followMsqButton.IsEnabled = navigator?.FollowedOverride", refresh, StringComparison.Ordinal);

        // The menu entry that is not a Stop.
        var windows = SourceGuard.Body(SourceGuard.SourceOf(Actions), "public IReadOnlyList<GuidanceAction> Windows()");
        Assert.Contains("MainScenarioReset.Acts", windows, StringComparison.Ordinal);

        // And the switcher's own entry, whose enabled-ness is the followed-ness of the choice.
        var choices = SourceGuard.Body(SourceGuard.SourceOf(Hub), "internal IReadOnlyList<FollowChoice> GetFollowChoices()");
        Assert.Contains("navigator?.FollowMode", choices, StringComparison.Ordinal);
        Assert.DoesNotContain("FollowedOverride is null", choices, StringComparison.Ordinal);
    }

    /// <summary>The Main Scenario entry is offered unconditionally, and it performs both halves of the
    /// reset. Either half alone is not a way home: clearing the quest leaves a hunt running, and
    /// releasing the hunt drops the player onto a side quest they did not ask for.</summary>
    [Fact]
    public void TheMainScenarioEntryAlwaysActsAndActsCompletely()
    {
        var source = SourceGuard.SourceOf(Actions);

        var follow = SourceGuard.Body(source, "public IReadOnlyList<GuidanceAction> Follow()");
        Assert.Contains("new GuidanceAction(MainScenarioLabel, ReturnToMainScenario)", follow, StringComparison.Ordinal);

        var reset = SourceGuard.Body(source, "private void ReturnToMainScenario()");
        Assert.Contains("navigator.ClearPickup()", reset, StringComparison.Ordinal);
        Assert.Contains("navigator.FollowedOverride = null", reset, StringComparison.Ordinal);

        // The window's own handler, which the switcher's entry runs.
        var clicked = SourceGuard.Body(SourceGuard.SourceOf(Hub), "private void OnFollowMsqClicked()");
        Assert.Contains("navigator.ClearPickup()", clicked, StringComparison.Ordinal);
        Assert.Contains("navigator.FollowedOverride = null", clicked, StringComparison.Ordinal);
    }

    /// <summary>Exactly one follow choice can be marked as followed, and each of the four kinds
    /// derives that mark from the mode actually running. The two engaged kinds used to pass
    /// <c>false</c> literally, so the list could not say a hunt was being followed at all.</summary>
    [Fact]
    public void EveryFollowChoiceReportsItselfFromTheModeActuallyRunning()
    {
        var choices = SourceGuard.Body(SourceGuard.SourceOf(Hub), "internal IReadOnlyList<FollowChoice> GetFollowChoices()");

        Assert.Contains("mode == FollowMode.MainScenario", choices, StringComparison.Ordinal);
        Assert.Contains("mode == FollowMode.UnlockRoute", choices, StringComparison.Ordinal);
        Assert.Contains("mode == FollowMode.Hunting", choices, StringComparison.Ordinal);

        var quests = SourceGuard.Body(SourceGuard.SourceOf(Hub), "private void AddAcceptedQuestChoices(");
        Assert.Contains("mode == FollowMode.Quest", quests, StringComparison.Ordinal);
    }

    /// <summary>Every follow entry does something when it is pressed. An entry with nothing to start
    /// opens the tab that says so; the only ones without an action at all are the ones whose feature
    /// is switched off, and then there is nothing to open either.
    ///
    /// <para>This is the fault that made the switcher look broken during a hunt. The Hunting Log entry
    /// was gated on the current zone's remaining targets, so the moment the player walked out of the
    /// zone she started in the entry was listed, focusable and inert — with the rest of the rank still
    /// waiting.</para></summary>
    [Fact]
    public void NoFollowEntryIsListedWithNothingBehindIt()
    {
        var choices = SourceGuard.Body(SourceGuard.SourceOf(Hub), "internal IReadOnlyList<FollowChoice> GetFollowChoices()");

        Assert.Contains("unlocksReady ? OnRouteClicked : OpenUnlocksTab", choices, StringComparison.Ordinal);
        Assert.Contains("huntReady ? OnHuntClicked : OpenHuntingTab", choices, StringComparison.Ordinal);

        // And the menu disables a row for one reason only: it is what is already being followed.
        var entry = SourceGuard.Body(SourceGuard.SourceOf(Switcher), "internal static ContextMenuItem Entry(");
        Assert.Contains("!choice.IsFollowed", entry, StringComparison.Ordinal);
    }

    /// <summary>The readout's plate always has somewhere to send its press. <c>Subject()</c> returns a
    /// non-nullable action, so there is no arrangement of state in which the plate's hit box, its
    /// controller anchor and the first entry of its menu exist over nothing.</summary>
    [Fact]
    public void ThePlateAlwaysOpensSomething()
    {
        var source = SourceGuard.SourceOf(Actions);

        // Not GuidanceAction? — the nullability IS the guarantee.
        Assert.Contains("public GuidanceAction Subject()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public GuidanceAction? Subject()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public GuidanceAction? Journal()", source, StringComparison.Ordinal);

        // A hunt goes to the Hunting Log, an unlock route to the Unlocks list, and everything else to
        // the tab that owns the choice — never to nothing.
        var subject = SourceGuard.Body(source, "public GuidanceAction Subject()");
        Assert.Contains("QuestJournalAction.Execute", subject, StringComparison.Ordinal);
        Assert.Contains("FollowMode.Hunting", subject, StringComparison.Ordinal);
        Assert.Contains("FollowMode.UnlockRoute", subject, StringComparison.Ordinal);
        Assert.Contains("openFollowing", subject, StringComparison.Ordinal);

        // The readout hands the plate that action and nothing narrower. A read of QuestId here would
        // be the regression: it is what made the press do nothing on a hunt.
        var overlay = SourceGuard.SourceOf(Overlay);
        Assert.Contains("OpenSubject", overlay, StringComparison.Ordinal);
        var open = SourceGuard.Body(overlay, "private void OpenSubject()");
        Assert.Contains("actions.Subject().Invoke()", open, StringComparison.Ordinal);
        Assert.DoesNotContain("QuestId", open, StringComparison.Ordinal);
    }

    /// <summary>And the words of the name on the plate offer their press on the same condition the
    /// plate does. They used to require the line to be marked as a Journal entry while the plate
    /// required nothing at all, so two targets over one piece of parchment disagreed and the larger of
    /// them lied.</summary>
    [Fact]
    public void TheNameOnThePlateOffersTheSamePressThePlateDoes()
    {
        var layout = SourceGuard.Body(SourceGuard.SourceOf(Body), "public Vector2 Layout(ReadoutFrame frame)");

        Assert.Contains("subjectClickable && subjectContent is not null", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadoutLineAction.OpenJournal", layout, StringComparison.Ordinal);
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

    /// <summary><b>The count and the plan are one source.</b> Every surface that offers "Start
    /// Hunting" counts <c>RemainingTargets</c> — the rank — because that is what the plan is built
    /// from, and none of them counts <c>HuntHereOrder</c>, which is only the player's current zone.
    ///
    /// <para>This is the guard on "the list says 13 and the button says 3". Both numbers were correct
    /// about their own set; the sets were different, and nothing said so.</para></summary>
    [Fact]
    public void TheHuntsCountAndItsPlanComeFromOneSource()
    {
        // The plan.
        var legs = SourceGuard.Body(SourceGuard.SourceOf(HuntingSource), "private List<HuntingTargetView> BuildLegs(");
        Assert.Contains("hunting.RemainingTargets", legs, StringComparison.Ordinal);
        Assert.DoesNotContain("HuntHereOrder", legs, StringComparison.Ordinal);

        // The four places that count it, and the label they all print.
        var counters = new (string File, string Member)[]
        {
            (Hub, "private void RebuildHunting()"),
            (Hub, "internal IReadOnlyList<FollowChoice> GetFollowChoices()"),
            (Actions, "private GuidanceAction? StartHunting("),
            (HuntingFallback, "private void DrawHuntHereButton()"),
        };

        foreach (var (file, member) in counters)
        {
            var body = SourceGuard.Body(SourceGuard.SourceOf(file), member);
            Assert.DoesNotContain("HuntHereOrder", body, StringComparison.Ordinal);
            Assert.Contains("RemainingTargets", body, StringComparison.Ordinal);
        }

        // The label and the enabled-ness are HuntingPlan's, so neither surface can spell out a number
        // of its own.
        foreach (var (file, member) in counters)
        {
            var body = SourceGuard.Body(SourceGuard.SourceOf(file), member);
            Assert.DoesNotContain("Start Hunting (", body, StringComparison.Ordinal);
        }
    }

    /// <summary>Starting a hunt goes straight to the source that owns the plan. It used to build a
    /// list of pickups out of the current zone and hand it to <c>SetRoute</c>, which recognised them
    /// as hunting targets and discarded the list — so the list was a fiction, and an EMPTY one made
    /// the press return without starting anything at all.</summary>
    [Fact]
    public void StartingAHuntDoesNotLaunderThePlanThroughAPickupList()
    {
        foreach (var (file, member) in new[]
        {
            (Hub, "private void OnHuntClicked()"),
            (Actions, "private GuidanceAction? StartHunting("),
            (HuntingFallback, "private void DrawHuntHereButton()"),
        })
        {
            var body = SourceGuard.Body(SourceGuard.SourceOf(file), member);
            Assert.Contains("StartHunt", body, StringComparison.Ordinal);
            Assert.DoesNotContain("SetRoute", body, StringComparison.Ordinal);
            Assert.DoesNotContain("ToPickupTarget", body, StringComparison.Ordinal);
        }
    }
}
