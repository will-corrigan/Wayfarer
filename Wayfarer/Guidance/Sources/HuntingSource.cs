using System.Globalization;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Hunting;

namespace Wayfarer.Guidance.Sources;

/// <summary>Guides the player through their hunting log's remaining targets. An ENGAGED source:
/// selecting a target is an explicit mode, and the readout names it.
///
/// THIS CLASS IS THE ONLY PLACE THAT KNOWS WHEN A HUNTING TARGET IS DONE, and the answer is a kill
/// count. That is the fix for the defect where selecting a target showed guidance for exactly one
/// frame: the target was forced through a quest-pickup shape carrying quest row id 0, and the
/// navigator asked the quest system whether row 0 had been accepted. There is now no code path from
/// the arbiter to a quest read, so the question cannot be asked, of this source or any other.</summary>
internal sealed class HuntingSource(
    IGuidanceArbiter arbiter,
    HuntingLogService hunting,
    GuidanceRouter router,
    IClientState clientState,
    IObjectTable objects) : IGuidanceSource
{
    /// <summary>Declared, not performed: one bool asks the framework to mark each target as the
    /// chain advances, and buys the save/restore of the player's own flag, the one-writer guarantee
    /// and the change-only cadence without this class ever touching the map.</summary>
    private static readonly ObjectiveAffordances MarkTheTarget = new(MapFlag: true);

    private GuidanceChain<HuntingTargetView>? chain;
    private uint? lastTerritory;

    public string SourceId => "hunting";

    /// <summary>The target being guided to right now, or null when no hunt is active.</summary>
    public HuntingTargetView? CurrentLeg => chain?.Current;

    /// <summary>Transitional bridge for the presentations that still speak in pickups — it lets the
    /// context menu keep asking "is an explicit selection active?" without knowing what a hunting
    /// target is.</summary>
    public PickupTarget? CurrentPickup => CurrentLeg is { } leg ? hunting.ToPickupTarget(leg) : null;

    /// <summary>Guides to one chosen target, then carries on through the rest of the log's
    /// remaining targets — picking a mob is a starting point, not a one-shot.</summary>
    public void GoTo(HuntingTargetView target) => Start(BuildLegs(target));

    /// <summary>Guides through every remaining target on the current log page.</summary>
    public void StartHunt() => Start(BuildLegs(null));

    public GuidanceOffer? Poll(GuidanceContext ctx)
    {
        if (chain is not { } plan)
        {
            lastTerritory = ctx.Territory;
            return null;
        }

        ReplanIfThePlayerTurnedUpSomewhereElse(plan, ctx);
        var leg = plan.Advance();
        if (leg is null)
        {
            chain = null;
            return null;
        }

        // Same ObjectiveKey every tick while this target lives — only the position and kill count
        // are refreshed. That is what stops a live-tracked mob from re-firing every per-objective
        // side effect at frame rate.
        var live = hunting.LiveView(leg);
        var dutyTerritory = live.Monster.Locations.Find(l => !l.Routable)?.DutyTerritoryTypeId;
        var objective = new GuidanceObjective(
            new ObjectiveKey(SourceId, KeyFor(live.Monster)),
            HuntingPlan.Destination(
                live.IsRoutable,
                live.TerritoryTypeId,
                live.MapId,
                live.WorldX,
                live.WorldY,
                live.WorldZ,
                dutyTerritory,
                live.IsLivePosition),
            new ObjectiveCopy(
                live.MonsterName,
                live.IsRoutable ? $"{live.Killed}/{live.Required} killed" : live.DutyName,
                HuntingPlan.SourceLabel(hunting.ActiveLogLabel)),
            new ObjectiveProgress(plan.Index, plan.Total, HuntingPlan.ProgressText(live.Killed, live.Required)),
            MarkTheTarget);

        return new GuidanceOffer(objective, GuidanceEngagement.Engaged);
    }

    public void OnDisengaged(DisengageReason reason)
    {
        // Preempted means the player started something else: the hunt is still their plan, so it is
        // kept and resumes at the same target. Every other reason drops it.
        if (reason != DisengageReason.Preempted)
        {
            chain = null;
        }
    }

    /// <summary>Stable across ticks and unique within the log: the BNpcName row plus the monster's
    /// positional index, since the same creature name can appear in more than one task.</summary>
    private static string KeyFor(HuntingMonster monster) =>
        string.Create(
            CultureInfo.InvariantCulture, $"{monster.BNpcNameId}:{monster.MonsterIndex}");

    private static bool SameMonster(HuntingTargetView a, HuntingTargetView b) =>
        ReferenceEquals(a.Monster, b.Monster)
        || (a.Monster.BNpcNameId == b.Monster.BNpcNameId && a.Monster.MonsterIndex == b.Monster.MonsterIndex);

    /// <summary>Done when the kill count is met — or when the target has left the current log page
    /// entirely (a rank-up), which would otherwise strand the plan on a target the game no longer
    /// tracks.</summary>
    private bool IsLegComplete(HuntingTargetView leg) =>
        !hunting.IsTracked(leg.Monster) || HuntingPlan.IsComplete(hunting.KilledFor(leg.Monster), leg.Required);

    /// <summary>The plan: the chosen target first (if any), then every OTHER remaining target in
    /// the whole rank — grouped by zone and ordered so the route takes one teleport per zone rather
    /// than ping-ponging between them. Duty-gated targets have no coordinate to route to, so they
    /// come last.
    ///
    /// The tail is ordered from where the player will BE when the chosen target is done (that
    /// target's own position), not from where they are standing now.</summary>
    private List<HuntingTargetView> BuildLegs(HuntingTargetView? head)
    {
        var player = objects.LocalPlayer;
        var fromTerritory = head is { IsRoutable: true } ? head.TerritoryTypeId : clientState.TerritoryType;
        var fromX = head is { IsRoutable: true } ? head.WorldX : player?.Position.X ?? 0f;
        var fromZ = head is { IsRoutable: true } ? head.WorldZ : player?.Position.Z ?? 0f;

        var rest = new List<HuntingTargetView>();
        foreach (var target in hunting.RemainingTargets)
        {
            if (head is null || !SameMonster(target, head))
            {
                rest.Add(target);
            }
        }

        var legs = new List<HuntingTargetView>(rest.Count + 1);
        if (head is { } chosen)
        {
            legs.Add(chosen);
        }

        legs.AddRange(OrderForTravel(rest, fromTerritory, fromX, fromZ));
        return legs;
    }

    /// <summary>Zone-grouped, teleport-minimising ordering, with the duty-gated targets appended
    /// unchanged — they are text-and-a-Duty-Finder-link, not a walk.</summary>
    private List<HuntingTargetView> OrderForTravel(
        IReadOnlyList<HuntingTargetView> targets, uint fromTerritory, float fromX, float fromZ)
    {
        var routable = new List<HuntingTargetView>();
        var dutyGated = new List<HuntingTargetView>();
        foreach (var target in targets)
        {
            (target.IsRoutable ? routable : dutyGated).Add(target);
        }

        var ordered = ChainPlanner.Order(
            routable,
            t => t.TerritoryTypeId,
            t => (t.WorldX, t.WorldZ),
            fromTerritory,
            fromX,
            fromZ,
            ZoneToZoneCost,
            zone => ArrivalPointFor(zone, routable));

        ordered.AddRange(dutyGated);
        return ordered;
    }

    /// <summary>Staying put is free, hopping the shared city aethernet is cheap, and a teleport
    /// costs a loading screen — which is all the ordering needs to know to group a rank's targets
    /// by zone instead of bouncing between them.</summary>
    private float ZoneToZoneCost(uint from, uint to)
    {
        if (from == to)
        {
            return 0f;
        }

        return router.SharesAethernetNetwork(from, to) ? 1f : 2f;
    }

    private (float X, float Z)? ArrivalPointFor(uint zone, List<HuntingTargetView> targets)
    {
        var first = targets.Find(t => t.TerritoryTypeId == zone);
        return first is null ? null : router.ArrivalPoint(zone, first.WorldX, first.WorldZ);
    }

    /// <summary>Re-plans the REMAINING targets when the player turns up in a zone the plan was not
    /// expecting — typically because they teleported somewhere else mid-hunt. The target they are
    /// currently walking to is pinned: re-ordering must change where they go next, never where they
    /// are already going.</summary>
    private void ReplanIfThePlayerTurnedUpSomewhereElse(GuidanceChain<HuntingTargetView> plan, GuidanceContext ctx)
    {
        var moved = lastTerritory is { } last && last != ctx.Territory;
        lastTerritory = ctx.Territory;
        if (!moved || plan.Current is not { } current || current.TerritoryTypeId == ctx.Territory)
        {
            return;
        }

        plan.ReplanTail(tail => OrderForTravel(tail, ctx.Territory, ctx.PlayerX, ctx.PlayerZ));
    }

    private void Start(List<HuntingTargetView> legs)
    {
        if (legs.Count == 0)
        {
            return;
        }

        chain = new GuidanceChain<HuntingTargetView>(legs, IsLegComplete);
        arbiter.Engage(this);
    }
}
