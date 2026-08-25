using System.Globalization;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Navigation;

namespace Wayfarer.Guidance.Sources;

/// <summary>Routes the player through the aether currents a zone still owes them. An ENGAGED source:
/// asking for the route is an explicit mode, and the readout names the zone it is working through.
///
/// <para>THIS CLASS IS THE ONLY PLACE THAT KNOWS WHEN AN AETHER CURRENT IS DONE, and the answer
/// differs by kind: a placed current is done when its attunement bit flips, a quest current when the
/// quest is in hand. Both live in <see cref="AetherCurrentPlan.IsReached"/> and neither is a question
/// the arbiter can ask.</para></summary>
internal sealed class AetherCurrentSource(
    IGuidanceArbiter arbiter,
    AetherCurrentService currents,
    GuidanceRouter router,
    IClientState clientState,
    IObjectTable objects) : IGuidanceSource
{
    /// <summary>Declared, not performed — see <see cref="HuntingSource"/>. One bool asks the framework
    /// to flag each stop as the route advances, which for a current the player has to spot from the
    /// air is the difference between a bearing and a destination.</summary>
    private static readonly ObjectiveAffordances MarkTheStop = new(MapFlag: true);

    private GuidanceChain<AetherCurrentPoint>? chain;
    private AetherCurrentTally? tally;
    private uint? tallyTerritory;
    private uint? lastTerritory;
    private bool tallyStale;

    public string SourceId => "aether-currents";

    /// <summary>The current being guided to right now, or null when no route is active.</summary>
    public AetherCurrentPoint? CurrentLeg => chain?.Current;

    /// <summary>Routes through every current the given zone still owes, nearest first. Does nothing
    /// when the zone has none left — starting an empty mode would put the player in a state with
    /// nothing in it.</summary>
    public void StartRoute(uint territory)
    {
        var remaining = currents.RemainingIn(territory);
        if (remaining.Count == 0)
        {
            return;
        }

        var player = objects.LocalPlayer;
        chain = new GuidanceChain<AetherCurrentPoint>(
            OrderForTravel(
                remaining, clientState.TerritoryType, player?.Position.X ?? 0f, player?.Position.Z ?? 0f),
            currents.IsReached);
        tallyTerritory = territory;
        tally = currents.TallyFor(territory);
        arbiter.Engage(this);
    }

    public GuidanceOffer? Poll(GuidanceContext ctx)
    {
        if (chain is not { } plan)
        {
            lastTerritory = ctx.Territory;
            return null;
        }

        ReplanIfThePlayerTurnedUpSomewhereElse(plan, ctx);

        var before = plan.Current;
        var leg = plan.Advance();
        if (leg is null)
        {
            Clear();
            return null;
        }

        // The zone's standing is re-read when the plan moves on, and on the first tick back after
        // something else held the arrow. Everything else is left alone: Poll runs every frame, and
        // between those two moments the count cannot have changed, since attuning a current IS what
        // completes a leg.
        if ((tallyStale || !ReferenceEquals(before, leg)) && tallyTerritory is { } territory)
        {
            tallyStale = false;
            tally = currents.TallyFor(territory);
        }

        return new GuidanceOffer(Objective(leg, plan), GuidanceEngagement.Engaged);
    }

    public void OnDisengaged(DisengageReason reason)
    {
        // Preempted means the player started something else: the route is still their plan, so it is
        // kept and resumes at the same current. Every other reason drops it.
        if (reason != DisengageReason.Preempted)
        {
            Clear();
            return;
        }

        // The plan survives, but the count beside it may not: currents can be attuned while another
        // mode owns the arrow, and one of them being further down this chain would leave a stale
        // number on the readout rather than a stale plan. Re-read on the way back in.
        tallyStale = true;
    }

    /// <summary>Staying put is free, hopping the shared city aethernet is cheap, and a teleport costs
    /// a loading screen. Identical to the hunting route's costing, and it matters here for one real
    /// case: nine of the game's quest currents are handed out in a neighbouring city, so even a
    /// single zone's route can span territories and must not ping-pong between them.</summary>
    private float ZoneToZoneCost(uint from, uint to) =>
        from == to ? 0f : router.SharesAethernetNetwork(from, to) ? 1f : 2f;

    private GuidanceObjective Objective(AetherCurrentPoint leg, GuidanceChain<AetherCurrentPoint> plan)
    {
        var progressText = tally is { } standing ? AetherCurrentPlan.ProgressText(standing) : null;
        return new GuidanceObjective(
            new ObjectiveKey(SourceId, leg.CurrentRowId.ToString(CultureInfo.InvariantCulture)),
            AetherCurrentPlan.Destination(leg),
            new ObjectiveCopy(
                AetherCurrentPlan.Headline(leg),
                AetherCurrentPlan.Detail(leg),
                AetherCurrentPlan.SourceLabel(leg.ZoneName),
                AetherCurrentPlan.SourceName),
            new ObjectiveProgress(plan.Index, plan.Total, progressText),
            MarkTheStop,
            QuestId: leg.QuestRowId == 0 ? null : leg.QuestRowId);
    }

    /// <summary>The ordering rule itself is <see cref="AetherCurrentRoute.Order"/>'s and is pure; all
    /// this adds is the two facts only the router knows — what travel costs, and where a teleport
    /// lands.</summary>
    private List<AetherCurrentPoint> OrderForTravel(
        IReadOnlyList<AetherCurrentPoint> points, uint fromTerritory, float fromX, float fromZ) =>
        AetherCurrentRoute.Order(
            points,
            fromTerritory,
            fromX,
            fromZ,
            ZoneToZoneCost,
            zone => ArrivalPointFor(zone, points));

    private (float X, float Z)? ArrivalPointFor(uint zone, IReadOnlyList<AetherCurrentPoint> points)
    {
        AetherCurrentPoint? first = null;
        foreach (var point in points)
        {
            if (point.Territory == zone)
            {
                first = point;
                break;
            }
        }

        return first is null ? null : router.ArrivalPoint(zone, first.X, first.Z);
    }

    /// <summary>Re-plans the remaining stops when the player turns up somewhere the plan was not
    /// expecting — teleporting away to pick up an out-of-zone quest giver, typically. The stop they
    /// are currently flying to is pinned: re-ordering must change where they go next, never where
    /// they are already going.</summary>
    private void ReplanIfThePlayerTurnedUpSomewhereElse(
        GuidanceChain<AetherCurrentPoint> plan, GuidanceContext ctx)
    {
        var moved = lastTerritory is { } last && last != ctx.Territory;
        lastTerritory = ctx.Territory;
        if (!moved || plan.Current is not { } current || current.Territory == ctx.Territory)
        {
            return;
        }

        plan.ReplanTail(tail => OrderForTravel(tail, ctx.Territory, ctx.PlayerX, ctx.PlayerZ));
    }

    private void Clear()
    {
        chain = null;
        tally = null;
        tallyTerritory = null;
        tallyStale = false;
    }
}
