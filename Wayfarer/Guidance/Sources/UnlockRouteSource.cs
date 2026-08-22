using System.Globalization;
using FFXIVClientStructs.FFXIV.Client.Game;
using Wayfarer.Core.Guidance;

namespace Wayfarer.Guidance.Sources;

/// <summary>Guides the player to unlock-quest givers, one stop at a time. An ENGAGED source: it
/// owns the arrow only because the player asked it to, and it says so on the readout.
///
/// It answers its own completion question — a stop is done when its quest has been accepted or was
/// already complete — and the arbiter never asks. That is the whole contract: this class is the
/// only place in the plugin that knows a quest row id means anything.</summary>
internal sealed unsafe class UnlockRouteSource(IGuidanceArbiter arbiter) : IGuidanceSource
{
    private const uint QuestRowIdOffset = 65536;

    /// <summary>Declared, not performed — see <see cref="HuntingSource"/>. One bool marks each stop
    /// as the route advances; the framework owns the flag and gives the player's own back
    /// afterwards.</summary>
    private static readonly ObjectiveAffordances MarkTheStop = new(MapFlag: true);

    private GuidanceChain<PickupTarget>? chain;

    /// <summary>True for a multi-stop route (progress is worth showing), false for a single
    /// pickup — matching the previous behaviour, where "Stop 2 of 5" appeared only for a route.</summary>
    private bool showProgress;

    /// <summary>Raised when the plan moves on: the current stop was picked up (or turned out to be
    /// done already) and the arrow advanced, or the route finished. The unlock checklist listens so
    /// it can recompute availability at exactly the moment it changed.</summary>
    public event Action? OnAdvanced;

    public string SourceId => "unlocks";

    /// <summary>The stop being guided to right now, or null when no route is active. Transitional:
    /// it exists so the context menu can keep asking "is an explicit pickup active?" unchanged.</summary>
    public PickupTarget? CurrentLeg => chain?.Current;

    /// <summary>Guides to a single unlock pickup, replacing any active route.</summary>
    public void GoTo(PickupTarget target) => Start([target], showProgress: false);

    /// <summary>Guides through a multi-stop route, starting at its first stop.</summary>
    public void StartRoute(IReadOnlyList<PickupTarget> route) => Start(route, showProgress: true);

    public GuidanceOffer? Poll(GuidanceContext ctx)
    {
        if (chain is not { } plan)
        {
            return null;
        }

        var before = plan.Current;
        var leg = plan.Advance();
        if (!ReferenceEquals(before, leg))
        {
            OnAdvanced?.Invoke();
        }

        if (leg is null)
        {
            chain = null;
            return null;
        }

        var progress = showProgress ? new ObjectiveProgress(plan.Index, plan.Total, null) : null;
        var objective = new GuidanceObjective(
            new ObjectiveKey(SourceId, leg.QuestRowId.ToString(CultureInfo.InvariantCulture)),
            new ObjectiveDestination.WorldPoint(leg.Territory, leg.MapId, leg.X, leg.Y, leg.Z),
            new ObjectiveCopy(
                UnlockRoutePlan.Headline(leg.UnlockName),
                UnlockRoutePlan.Detail(leg.QuestName, leg.GiverName),
                UnlockRoutePlan.SourceLabel),
            progress,
            MarkTheStop,
            QuestId: leg.QuestRowId);

        return new GuidanceOffer(objective, GuidanceEngagement.Engaged);
    }

    public void OnDisengaged(DisengageReason reason)
    {
        // Preempted means another mode took over: the route is still the player's plan, so it is
        // kept and resumes where it left off. Every other reason drops it.
        if (reason != DisengageReason.Preempted)
        {
            chain = null;
        }
    }

    /// <summary>A stop is done when its quest is accepted (the player reached the giver and took
    /// it) or already complete. This predicate is evaluated against live game state on every poll —
    /// it is the unlock source's completion signal and exists nowhere else.</summary>
    private static bool IsPickedUp(PickupTarget target)
    {
        var qm = QuestManager.Instance();
        var raw = (ushort)(target.QuestRowId - QuestRowIdOffset);
        return UnlockRoutePlan.IsPickedUp(
            qm != null && qm->IsQuestAccepted(raw),
            QuestManager.IsQuestComplete(target.QuestRowId));
    }

    private void Start(IReadOnlyList<PickupTarget> legs, bool showProgress)
    {
        if (legs.Count == 0)
        {
            return;
        }

        chain = new GuidanceChain<PickupTarget>(legs, IsPickedUp);
        this.showProgress = showProgress;
        arbiter.Engage(this);
    }
}
