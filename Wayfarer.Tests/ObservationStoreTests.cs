using Wayfarer.Core.Unlocks;
using Wayfarer.Core.Unlocks.Gates;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Tests;

/// <summary>Remembering a value the game only shows you sometimes, and the two rules that keep it
/// from becoming a lie.
///
/// <para>Rule one: only values that cannot fall may be remembered, which is a property of the
/// TYPE — a reader with no such proof has no <see cref="IMonotonicSource{TId}"/> to implement.
/// Rule two: a remembered value may prove a requirement met and may never prove one unmet, because
/// it is a lower bound and the real value may have risen since.</para></summary>
public class ObservationStoreTests
{
    private const string Kind = "sharedFateRankAtLeast";
    private const string Alice = "a1";
    private const string Bob = "b2";
    private const uint Zone = 813;

    private static readonly DateTimeOffset Now = new(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Observe_KeepsTheHighestValue()
    {
        var store = new ObservationStore();
        store.Observe(Alice, Kind, Zone, 3, Now);
        store.Observe(Alice, Kind, Zone, 1, Now);

        Assert.True(store.TryFloor(Alice, Kind, Zone, out var floor));
        Assert.Equal(3, floor);
    }

    [Fact]
    public void TryFloor_NeverObserved_IsFalse()
    {
        Assert.False(new ObservationStore().TryFloor(Alice, Kind, Zone, out var floor));
        Assert.Equal(0, floor);
    }

    [Fact]
    public void Characters_DoNotSeeEachOthersObservations()
    {
        var store = new ObservationStore();
        store.Observe(Alice, Kind, Zone, 3, Now);

        Assert.False(store.TryFloor(Bob, Kind, Zone, out _));
    }

    [Fact]
    public void Kinds_DoNotCollideOnAnId()
    {
        var store = new ObservationStore();
        store.Observe(Alice, Kind, Zone, 3, Now);

        Assert.False(store.TryFloor(Alice, "zoneProgressAtLeast.bozja", Zone, out _));
    }

    [Fact]
    public void Prune_DropsObservationsOlderThanTheLimit()
    {
        var store = new ObservationStore();
        store.Observe(Alice, Kind, Zone, 3, Now - TimeSpan.FromDays(200));
        store.Observe(Alice, Kind, 814, 2, Now);

        store.Prune(TimeSpan.FromDays(180), Now);

        Assert.False(store.TryFloor(Alice, Kind, Zone, out _));
        Assert.True(store.TryFloor(Alice, Kind, 814, out _));
    }

    [Fact]
    public void ObservedFloor_LiveRead_AnswersAndIsRemembered()
    {
        var source = new StubSource { Live = 3 };
        var store = new ObservationStore();
        var floor = Floor(source, store);

        Assert.True(floor.TryAtLeast(Alice, Zone, 3, out var met));
        Assert.True(met);

        source.Live = null;
        Assert.True(floor.TryAtLeast(Alice, Zone, 3, out var remembered));
        Assert.True(remembered);
    }

    /// <summary>The asymmetry the whole design turns on. A floor of 2 against a threshold of 3
    /// cannot say "no" — the player may have reached rank 3 since the reading — so the answer is
    /// "cannot tell", which the gate model turns into RequirementsUnknown rather than into a trip
    /// back to content they have already finished.</summary>
    [Fact]
    public void ObservedFloor_BelowTheThreshold_CannotBlock()
    {
        var source = new StubSource { Live = 2 };
        var store = new ObservationStore();
        var floor = Floor(source, store);

        Assert.True(floor.TryAtLeast(Alice, Zone, 3, out var live));
        Assert.False(live);

        source.Live = null;
        Assert.False(floor.TryAtLeast(Alice, Zone, 3, out _));
    }

    [Fact]
    public void ObservedFloor_NeverObservedAndNotLive_IsUnknown()
    {
        Assert.False(Floor(new StubSource(), new ObservationStore()).TryAtLeast(Alice, Zone, 1, out _));
    }

    /// <summary>Eureka's elemental level can decrease, so it is excluded from remembering — the
    /// evaluator that reads it is the same one Bozja uses, and the difference lives entirely in the
    /// adapter, which is why this is asserted against the gate rather than against a reader.</summary>
    [Fact]
    public void EurekaZoneProgress_OutsideTheZone_IsNeverAnsweredFromMemory()
    {
        var node = Gates.Node(
            GateKinds.ZoneProgressAtLeast, amount: 60, scope: GateKinds.ScopeEureka, display: "Eureka");

        var result = GateEvaluatorRegistry.Standard.Evaluate(node, Gates.Ctx(70).Live);

        Assert.Equal(GateOutcome.Indeterminate, result.Outcome);
    }

    private static ObservedFloor<uint> Floor(IMonotonicSource<uint> source, ObservationStore store) =>
        new(source, store, Kind, id => id, () => Now);

    private sealed class StubSource : IMonotonicSource<uint>
    {
        public int? Live { get; set; }

        public bool TryReadLive(uint id, out int value)
        {
            value = Live ?? 0;
            return Live.HasValue;
        }
    }
}
