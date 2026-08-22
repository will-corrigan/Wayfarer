using Wayfarer.Core.Guidance;

namespace Wayfarer.Tests;

public class MapFlagCoordinatorTests
{
    private static readonly GuidanceContext Ctx = new(129, 129, 0f, 0f, 0f, LoggedIn: true);

    /// <summary>The flag the player had before we touched anything.</summary>
    private static readonly MapFlagSnapshot PlayersOwnFlag = new(true, 140, 5, 22.5f, 18.5f, 60561);

    /// <summary>THE 60 Hz GUARD. A live-tracked target re-emits its position every single frame with
    /// the same identity; if that reached the flag, the player's map would be rewritten sixty times
    /// a second. One hundred ticks must produce exactly ONE write.</summary>
    [Fact]
    public void MapFlag_SetOnce_PerObjectiveChange_NotPerTick()
    {
        var flag = new RecordingFlag();
        var arbiter = new GuidanceArbiter();
        using var coordinator = Coordinator(arbiter, flag).Start();
        var source = new MovingSource("hunting", flagged: true);
        arbiter.Engage(source);

        for (var i = 0; i < 100; i++)
        {
            arbiter.Tick(Ctx);
        }

        Assert.Equal(1, flag.Sets);
        Assert.Equal(1, flag.Reads);
    }

    [Fact]
    public void MapFlag_MovesOncePerAdvance()
    {
        var flag = new RecordingFlag();
        var arbiter = new GuidanceArbiter();
        using var coordinator = Coordinator(arbiter, flag).Start();
        var source = new MovingSource("hunting", flagged: true);
        arbiter.Engage(source);

        arbiter.Tick(Ctx);
        source.Advance();
        arbiter.Tick(Ctx);
        arbiter.Tick(Ctx);

        Assert.Equal(2, flag.Sets);
        Assert.Equal(1, flag.Reads); // ownership is taken once, not once per target
    }

    [Fact]
    public void MapFlag_RestoresThePlayersOwnFlag_OnExit()
    {
        var flag = new RecordingFlag();
        var arbiter = new GuidanceArbiter();
        using var coordinator = Coordinator(arbiter, flag).Start();
        var source = new MovingSource("hunting", flagged: true);
        arbiter.Engage(source);
        arbiter.Tick(Ctx);

        arbiter.ReleaseAll();
        arbiter.Tick(Ctx);

        Assert.Equal([PlayersOwnFlag], flag.Restored);
    }

    [Fact]
    public void MapFlag_RestoresOnChainCompletion()
    {
        var flag = new RecordingFlag();
        var arbiter = new GuidanceArbiter();
        using var coordinator = Coordinator(arbiter, flag).Start();
        var source = new MovingSource("hunting", flagged: true);
        arbiter.Engage(source);
        arbiter.Tick(Ctx);

        source.Finish();
        arbiter.Tick(Ctx);

        Assert.Equal([PlayersOwnFlag], flag.Restored);
    }

    [Fact]
    public void MapFlag_RestoresWhenPreemptedByAnObjectiveThatDoesNotWantIt()
    {
        var flag = new RecordingFlag();
        var arbiter = new GuidanceArbiter();
        using var coordinator = Coordinator(arbiter, flag).Start();
        var flagged = new MovingSource("hunting", flagged: true);
        var plain = new MovingSource("unlocks", flagged: false);
        arbiter.Engage(flagged);
        arbiter.Tick(Ctx);

        arbiter.Engage(plain);
        arbiter.Tick(Ctx);

        Assert.Equal([PlayersOwnFlag], flag.Restored);
        Assert.Equal(1, flag.Sets);
    }

    [Fact]
    public void MapFlag_RestoresOnDispose_SoAnUnloadLeavesNothingBehind()
    {
        var flag = new RecordingFlag();
        var arbiter = new GuidanceArbiter();
        var coordinator = Coordinator(arbiter, flag).Start();
        var source = new MovingSource("hunting", flagged: true);
        arbiter.Engage(source);
        arbiter.Tick(Ctx);

        coordinator.Dispose();

        Assert.Equal([PlayersOwnFlag], flag.Restored);
    }

    [Theory]
    [InlineData("duty")]
    [InlineData("territory")]
    [InlineData("unresolved")]
    public void MapFlag_IgnoredForDestinationsWithNoCoordinate(string kind)
    {
        var flag = new RecordingFlag();
        var arbiter = new GuidanceArbiter();
        using var coordinator = Coordinator(arbiter, flag).Start();
        ObjectiveDestination destination = kind switch
        {
            "duty" => new ObjectiveDestination.InstancedDuty(1036),
            "territory" => new ObjectiveDestination.TerritoryOnly(140, 5),
            _ => new ObjectiveDestination.Unresolved("no location"),
        };
        arbiter.Engage(new MovingSource("hunting", flagged: true, destination));

        arbiter.Tick(Ctx);

        Assert.Equal(0, flag.Sets);
        Assert.Equal(0, flag.Reads);
    }

    [Fact]
    public void MapFlag_NoWriteWithoutOptIn()
    {
        var flag = new RecordingFlag();
        var arbiter = new GuidanceArbiter();
        using var coordinator = new MapFlagCoordinator(
            arbiter, () => false, flag.Read, flag.Set, flag.Restore).Start();
        arbiter.Engage(new MovingSource("hunting", flagged: true));

        arbiter.Tick(Ctx);

        Assert.Equal(0, flag.Sets);
        Assert.Equal(0, flag.Reads);
    }

    /// <summary>An ambient objective is not an explicit mode, so it never marks the map — the
    /// player has to have asked for something for their flag to be touched at all.</summary>
    [Fact]
    public void MapFlag_IgnoredForAmbientObjectives()
    {
        var flag = new RecordingFlag();
        var arbiter = new GuidanceArbiter();
        using var coordinator = Coordinator(arbiter, flag).Start();
        arbiter.Register(new MovingSource("quest", flagged: true));

        arbiter.Tick(Ctx);

        Assert.Equal(0, flag.Sets);
    }

    /// <summary>When the flag cannot be read safely (no map agent, mid-zone-change, PvP) the
    /// coordinator declines to take ownership rather than planting a flag it could never
    /// restore.</summary>
    [Fact]
    public void MapFlag_UnreadableFlag_TakesNoOwnershipAndWritesNothing()
    {
        var flag = new RecordingFlag { Readable = false };
        var arbiter = new GuidanceArbiter();
        using var coordinator = Coordinator(arbiter, flag).Start();
        arbiter.Engage(new MovingSource("hunting", flagged: true));

        arbiter.Tick(Ctx);
        arbiter.ReleaseAll();
        arbiter.Tick(Ctx);

        Assert.Equal(0, flag.Sets);
        Assert.Empty(flag.Restored);
    }

    private static MapFlagCoordinator Coordinator(GuidanceArbiter arbiter, RecordingFlag flag) =>
        new(arbiter, () => true, flag.Read, flag.Set, flag.Restore);

    private sealed class RecordingFlag
    {
        public bool Readable { get; init; } = true;

        public int Reads { get; private set; }

        public int Sets { get; private set; }

        public List<MapFlagSnapshot> Restored { get; } = [];

        public MapFlagSnapshot? Read()
        {
            if (!Readable)
            {
                return null;
            }

            Reads++;
            return PlayersOwnFlag;
        }

        public void Set(uint territory, uint mapId, float x, float y, float z) => Sets++;

        public void Restore(MapFlagSnapshot snapshot) => Restored.Add(snapshot);
    }

    /// <summary>Re-emits the same objective with a fresh position every tick, the way a live-tracked
    /// target does, and can advance to the next one or finish.</summary>
    private sealed class MovingSource(string id, bool flagged, ObjectiveDestination? destination = null)
        : IGuidanceSource
    {
        private float x;
        private int leg = 1;
        private bool finished;

        public string SourceId => id;

        public void Advance() => leg++;

        public void Finish() => finished = true;

        public GuidanceOffer? Poll(GuidanceContext ctx)
        {
            if (finished)
            {
                return null;
            }

            var objective = new GuidanceObjective(
                new ObjectiveKey(id, $"leg{leg}"),
                destination ?? new ObjectiveDestination.WorldPoint(129, 129, x++, 0f, 0f, IsLive: true),
                new ObjectiveCopy("target", null, "mode"),
                Affordances: flagged ? new ObjectiveAffordances(MapFlag: true) : null);
            var engagement = string.Equals(id, "quest", StringComparison.Ordinal)
                ? GuidanceEngagement.Ambient
                : GuidanceEngagement.Engaged;
            return new GuidanceOffer(objective, engagement);
        }

        public void OnDisengaged(DisengageReason reason)
        {
        }
    }
}
