using Wayfarer.Core.Guidance;

namespace Wayfarer.Tests;

public class GuidanceArbiterTests
{
    private static readonly GuidanceContext Ctx = new(129, 129, 10f, 0f, 20f, LoggedIn: true);

    [Fact]
    public void AmbientOnly_PublishesAmbientObjective()
    {
        var arbiter = new GuidanceArbiter();
        var ambient = new FakeSource("quest", Offer("quest", "1", GuidanceEngagement.Ambient));
        arbiter.Register(ambient);

        var objective = arbiter.Tick(Ctx);

        Assert.NotNull(objective);
        Assert.Equal(new ObjectiveKey("quest", "1"), objective.Key);
        Assert.Equal(GuidanceEngagement.Ambient, arbiter.Engagement);
        Assert.Null(arbiter.EngagedSource);
    }

    [Fact]
    public void Engaged_WinsSameTick_AmbientNeverPolled()
    {
        var arbiter = new GuidanceArbiter();
        var ambient = new FakeSource("quest", Offer("quest", "1", GuidanceEngagement.Ambient));
        var hunt = new FakeSource("hunting", Offer("hunting", "mob", GuidanceEngagement.Engaged));
        arbiter.Register(ambient);
        arbiter.Register(hunt);

        arbiter.Engage(hunt);
        var objective = arbiter.Tick(Ctx);

        Assert.Equal(new ObjectiveKey("hunting", "mob"), objective!.Key);
        Assert.Equal(GuidanceEngagement.Engaged, arbiter.Engagement);
        Assert.Equal(0, ambient.PollCount);
    }

    /// <summary>THE REGRESSION TEST. An engaged source offers an unchanging objective that carries
    /// <c>QuestId = 0</c> — the exact payload a hunting target produced when it was forced through a
    /// quest-pickup shape — while an ambient quest source simultaneously offers a real quest. The
    /// old navigator asked <c>IsQuestAccepted((ushort)(0 - 65536))</c> about that payload, got an
    /// answer with no meaning for a monster, and cleared the target on the next framework tick: one
    /// tick of correct guidance, then flicker.
    ///
    /// This cannot recur, and not because a guard was added: the arbiter has no code path that
    /// reads an objective's payload, so there is nothing to ask. A hundred ticks must yield the same
    /// objective and exactly one <see cref="GuidanceArbiter.OnObjectiveChanged"/>.</summary>
    [Fact]
    public void ObjectiveWithQuestIdZero_SurvivesOneHundredTicks()
    {
        var arbiter = new GuidanceArbiter();
        var ambient = new FakeSource("quest", Offer("quest", "msq", GuidanceEngagement.Ambient));
        var hunt = new FakeSource(
            "hunting",
            new GuidanceOffer(
                new GuidanceObjective(
                    new ObjectiveKey("hunting", "12345"),
                    new ObjectiveDestination.WorldPoint(148, 4, 1f, 2f, 3f),
                    new ObjectiveCopy("Ornery Karakul", "0/3 kills", "Hunting Log · Gladiator"),
                    QuestId: 0),
                GuidanceEngagement.Engaged));
        arbiter.Register(ambient);
        arbiter.Register(hunt);

        var events = 0;
        arbiter.OnObjectiveChanged += _ => events++;
        arbiter.Engage(hunt);

        for (var i = 0; i < 100; i++)
        {
            var objective = arbiter.Tick(Ctx);
            Assert.Equal(new ObjectiveKey("hunting", "12345"), objective!.Key);
            Assert.Equal(GuidanceEngagement.Engaged, arbiter.Engagement);
        }

        Assert.Equal(1, events);
        Assert.Equal(0, hunt.DisengagedCount);
        Assert.Equal(0, ambient.PollCount);
    }

    [Fact]
    public void EngagingB_DisengagesA_ExactlyOnce()
    {
        var arbiter = new GuidanceArbiter();
        var a = new FakeSource("unlocks", Offer("unlocks", "1", GuidanceEngagement.Engaged));
        var b = new FakeSource("hunting", Offer("hunting", "1", GuidanceEngagement.Engaged));

        arbiter.Engage(a);
        arbiter.Engage(b);
        arbiter.Engage(b);

        Assert.Equal(1, a.DisengagedCount);
        Assert.Equal(DisengageReason.Preempted, a.LastReason);
        Assert.Equal(0, b.DisengagedCount);
        Assert.Same(b, arbiter.EngagedSource);
    }

    [Fact]
    public void TokenHolderReturnsNull_FallsThroughToAmbientInSameTick()
    {
        var arbiter = new GuidanceArbiter();
        var ambient = new FakeSource("quest", Offer("quest", "msq", GuidanceEngagement.Ambient));
        var hunt = new FakeSource("hunting", Offer("hunting", "1", GuidanceEngagement.Engaged), null);
        arbiter.Register(ambient);
        arbiter.Register(hunt);

        var events = new List<GuidanceObjective?>();
        arbiter.OnObjectiveChanged += events.Add;
        arbiter.Engage(hunt);

        arbiter.Tick(Ctx);
        var second = arbiter.Tick(Ctx);

        Assert.Equal(new ObjectiveKey("quest", "msq"), second!.Key);
        Assert.Equal(GuidanceEngagement.Ambient, arbiter.Engagement);
        Assert.Null(arbiter.EngagedSource);
        Assert.Equal(1, hunt.DisengagedCount);
        Assert.Equal(DisengageReason.Completed, hunt.LastReason);
        Assert.Equal(2, events.Count); // hunting objective, then the quest objective — no idle frame
        Assert.All(events, e => Assert.NotNull(e));
    }

    [Fact]
    public void Suppression_HidesWithoutReleasingToken()
    {
        var arbiter = new GuidanceArbiter();
        var hunt = new FakeSource("hunting", Offer("hunting", "1", GuidanceEngagement.Engaged));
        arbiter.Engage(hunt);

        var events = 0;
        arbiter.OnObjectiveChanged += _ => events++;
        arbiter.Tick(Ctx);

        // Suppressed frames: the host stops ticking guidance and publishes a hidden readout. The
        // token is untouched, so nothing is disengaged and no identity change is observed.
        var suppressed = new SuppressionInputs(
            LoggedIn: true,
            PlayerPresent: true,
            InCutscene: false,
            BetweenAreas: false,
            InCombat: true,
            HideInCombat: true,
            BoundByDuty: false,
            HideInDuty: true);
        Assert.True(GuidanceSuppression.ShouldHide(suppressed));

        var resumed = arbiter.Tick(Ctx);

        Assert.Same(hunt, arbiter.EngagedSource);
        Assert.Equal(new ObjectiveKey("hunting", "1"), resumed!.Key);
        Assert.Equal(0, hunt.DisengagedCount);
        Assert.Equal(1, events);
    }

    [Fact]
    public void ReleaseAll_WithNothingEngaged_IsNoOp()
    {
        var arbiter = new GuidanceArbiter();
        var ambient = new FakeSource("quest", Offer("quest", "msq", GuidanceEngagement.Ambient));
        arbiter.Register(ambient);

        arbiter.ReleaseAll();
        arbiter.ReleaseAll();

        Assert.Null(arbiter.EngagedSource);
        Assert.Equal(0, ambient.DisengagedCount);
    }

    [Fact]
    public void ThrowingSource_IsGuarded_LosesToken_LogsOnce()
    {
        var logged = 0;
        var arbiter = new GuidanceArbiter((_, _) => logged++);
        var ambient = new FakeSource("quest", Offer("quest", "msq", GuidanceEngagement.Ambient));
        var thrower = new ThrowingSource("hunting");
        arbiter.Register(ambient);
        arbiter.Register(thrower);
        arbiter.Engage(thrower);

        var first = arbiter.Tick(Ctx);
        var second = arbiter.Tick(Ctx);

        Assert.Equal(new ObjectiveKey("quest", "msq"), first!.Key);
        Assert.Equal(new ObjectiveKey("quest", "msq"), second!.Key);
        Assert.Null(arbiter.EngagedSource);
        Assert.Equal(DisengageReason.ModuleDisabled, thrower.LastReason);
        Assert.Equal(1, logged);
    }

    [Fact]
    public void SameKeyDifferentPosition_DoesNotRaiseObjectiveChanged()
    {
        var arbiter = new GuidanceArbiter();
        var live = new LiveSource("hunting");
        arbiter.Engage(live);

        var events = 0;
        arbiter.OnObjectiveChanged += _ => events++;
        for (var i = 0; i < 10; i++)
        {
            arbiter.Tick(Ctx);
        }

        Assert.Equal(1, events);
        var point = Assert.IsType<ObjectiveDestination.WorldPoint>(arbiter.Current!.Destination);
        Assert.Equal(9f, point.X); // the freshest position is published, it just raises no event
        Assert.True(point.IsLive);
    }

    [Fact]
    public void EngagedObjectiveWithoutSourceLabel_Throws()
    {
        var arbiter = new GuidanceArbiter();
        var unlabelled = new FakeSource(
            "hunting",
            new GuidanceOffer(
                new GuidanceObjective(
                    new ObjectiveKey("hunting", "1"),
                    new ObjectiveDestination.Unresolved("somewhere"),
                    new ObjectiveCopy("Ornery Karakul", null, null)),
                GuidanceEngagement.Engaged));
        arbiter.Engage(unlabelled);

        Assert.Throws<InvalidOperationException>(() => arbiter.Tick(Ctx));
    }

    [Fact]
    public void Unregister_ReleasesTokenAndDisengages()
    {
        var arbiter = new GuidanceArbiter();
        var ambient = new FakeSource("quest", Offer("quest", "msq", GuidanceEngagement.Ambient));
        var hunt = new FakeSource("hunting", Offer("hunting", "1", GuidanceEngagement.Engaged));
        arbiter.Register(ambient);
        arbiter.Register(hunt);
        arbiter.Engage(hunt);
        arbiter.Tick(Ctx);

        arbiter.Unregister(hunt);

        Assert.Null(arbiter.EngagedSource);
        Assert.Equal(1, hunt.DisengagedCount);
        Assert.Equal(DisengageReason.ModuleDisabled, hunt.LastReason);
        Assert.Null(arbiter.Current);

        var next = arbiter.Tick(Ctx);
        Assert.Equal(new ObjectiveKey("quest", "msq"), next!.Key);
    }

    [Fact]
    public void AmbientSourcesResolveInRegistrationOrder()
    {
        var arbiter = new GuidanceArbiter();
        var first = new FakeSource("quest", Offer("quest", "msq", GuidanceEngagement.Ambient));
        var second = new FakeSource("fates", Offer("fates", "77", GuidanceEngagement.Ambient));
        arbiter.Register(first);
        arbiter.Register(second);

        var objective = arbiter.Tick(Ctx);

        Assert.Equal(new ObjectiveKey("quest", "msq"), objective!.Key);
        Assert.Equal(0, second.PollCount);
    }

    [Fact]
    public void SourceClaimingEngagementWithoutTheToken_IsPublishedAsAmbient()
    {
        var arbiter = new GuidanceArbiter();
        var liar = new FakeSource("fates", Offer("fates", "77", GuidanceEngagement.Engaged));
        arbiter.Register(liar);

        arbiter.Tick(Ctx);

        Assert.Equal(GuidanceEngagement.Ambient, arbiter.Engagement);
        Assert.Null(arbiter.EngagedSource);
    }

    [Fact]
    public void ObjectiveActivatedAndDeactivated_FireOnIdentityChangeOnly()
    {
        var arbiter = new GuidanceArbiter();
        var chain = new ChainSource("hunting");
        arbiter.Engage(chain);

        arbiter.Tick(Ctx);
        arbiter.Tick(Ctx); // same key re-emitted
        chain.Advance();
        arbiter.Tick(Ctx);

        Assert.Equal(2, chain.Activated.Count);
        Assert.Single(chain.Deactivated);
        Assert.Equal("leg1", chain.Deactivated[0].Key.Value);
    }

    private static GuidanceOffer Offer(string sourceId, string value, GuidanceEngagement engagement) =>
        new(
            new GuidanceObjective(
                new ObjectiveKey(sourceId, value),
                new ObjectiveDestination.WorldPoint(129, 129, 1f, 2f, 3f),
                new ObjectiveCopy($"objective {value}", null, $"{sourceId} mode")),
            engagement);

    /// <summary>Scripted source: returns the i-th offer on the i-th poll and repeats the last one
    /// forever after. Hand-written rather than mocked, following this suite's existing idiom.</summary>
    private sealed class FakeSource(string id, params GuidanceOffer?[] script) : IGuidanceSource
    {
        private int tick;

        public string SourceId => id;

        public int PollCount { get; private set; }

        public int DisengagedCount { get; private set; }

        public DisengageReason? LastReason { get; private set; }

        public GuidanceOffer? Poll(GuidanceContext ctx)
        {
            PollCount++;
            return script[Math.Min(tick++, script.Length - 1)];
        }

        public void OnDisengaged(DisengageReason reason)
        {
            DisengagedCount++;
            LastReason = reason;
        }
    }

    private sealed class ThrowingSource(string id) : IGuidanceSource
    {
        public string SourceId => id;

        public DisengageReason? LastReason { get; private set; }

        public GuidanceOffer? Poll(GuidanceContext ctx) => throw new InvalidOperationException("boom");

        public void OnDisengaged(DisengageReason reason) => LastReason = reason;
    }

    /// <summary>Re-emits the SAME key every tick with a fresh position, the way a live-tracked mob
    /// does.</summary>
    private sealed class LiveSource(string id) : IGuidanceSource
    {
        private float x;

        public string SourceId => id;

        public GuidanceOffer? Poll(GuidanceContext ctx) =>
            new(
                new GuidanceObjective(
                    new ObjectiveKey(id, "mob"),
                    new ObjectiveDestination.WorldPoint(129, 129, x++, 0f, 0f, IsLive: true),
                    new ObjectiveCopy("Ornery Karakul", null, "Hunting Log")),
                GuidanceEngagement.Engaged);

        public void OnDisengaged(DisengageReason reason)
        {
        }
    }

    private sealed class ChainSource(string id) : IGuidanceSource
    {
        private int leg = 1;

        public string SourceId => id;

        public List<GuidanceObjective> Activated { get; } = [];

        public List<GuidanceObjective> Deactivated { get; } = [];

        public void Advance() => leg++;

        public GuidanceOffer? Poll(GuidanceContext ctx) =>
            new(
                new GuidanceObjective(
                    new ObjectiveKey(id, $"leg{leg}"),
                    new ObjectiveDestination.WorldPoint(129, 129, 0f, 0f, 0f),
                    new ObjectiveCopy($"leg {leg}", null, "Hunting Log")),
                GuidanceEngagement.Engaged);

        public void OnDisengaged(DisengageReason reason)
        {
        }

        public void OnObjectiveActivated(GuidanceObjective objective) => Activated.Add(objective);

        public void OnObjectiveDeactivated(GuidanceObjective objective) => Deactivated.Add(objective);
    }
}
