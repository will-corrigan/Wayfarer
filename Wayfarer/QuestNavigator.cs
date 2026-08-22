using System.Globalization;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Navigation;
using Wayfarer.Guidance;
using GameMap = FFXIVClientStructs.FFXIV.Client.Game.UI.Map;

namespace Wayfarer;

/// <summary>A single unlock-quest pickup step in a navigator route: the location to
/// walk to in order to accept the quest that unlocks something.</summary>
public sealed record PickupTarget(
    string UnlockName, string QuestName, uint QuestRowId,
    uint Territory, uint MapId, float X, float Y, float Z, string? GiverName = null);

/// <summary>Resolves the followed quest's (or the active pickup's) objective once per framework
/// tick and publishes an immutable NavigationState (read by ArrowWindow and get_navigation;
/// cross-thread reads are safe because only the reference is swapped). Owned by
/// <see cref="Modules.QuestHelperModule"/>, which subscribes <see cref="OnUpdate"/>
/// to <c>Framework.Update</c> in <c>Enable()</c> and unsubscribes in <c>Disable()</c> —
/// this class runs only while that module is enabled.
///
/// Since the guidance-architecture extraction it decides only WHAT to guide to: it builds a
/// <see cref="GuidanceObjective"/> and hands it to <see cref="GuidanceRouter"/> (how to get there)
/// and <see cref="GuidanceProjection"/> (what the state object says).</summary>
internal sealed unsafe class QuestNavigator(
    IPluginLog log,
    QuestHelperConfig cfg,
    IClientState clientState,
    ICondition condition,
    IObjectTable objects,
    IDataManager dataManager,
    GuidanceRouter router) : INavigationProvider
{
    private const uint QuestRowIdOffset = 65536;

    /// <summary>Source ids for the two things this navigator can guide to today. They become the
    /// prefix of every <see cref="ObjectiveKey"/> it emits and the wire's
    /// <c>NavigationState.SourceId</c>.</summary>
    private const string QuestSourceId = "quest";
    private const string PickupSourceId = "unlocks";

    private readonly Queue<PickupTarget> routeQueue = new();
    private volatile NavigationState current = new();
    private bool errorLogged;

    // Route progress (Step 4): set together in SetRoute, advanced together in the
    // pickup-advance path in Compute(), cleared together by SetPickup/ClearPickup/route
    // exhaustion. Null total means "no route active" — including a single SetPickup.
    private int? routeTotal;
    private int routeStop;

    public event System.Action? OnPickupAdvanced;

    public ushort? FollowedOverride { get; set; }

    public NavigationState Current => current;

    public PickupTarget? Pickup { get; private set; }

    public void SetPickup(PickupTarget t)
    {
        routeQueue.Clear();
        routeTotal = null;
        Pickup = t;
    }

    public void SetRoute(List<PickupTarget> route)
    {
        routeQueue.Clear();
        foreach (var t in route)
        {
            routeQueue.Enqueue(t);
        }

        if (routeQueue.Count > 0)
        {
            routeTotal = routeQueue.Count;
            routeStop = 1;
            Pickup = routeQueue.Dequeue();
        }
        else
        {
            routeTotal = null;
            Pickup = null;
        }
    }

    public void ClearPickup()
    {
        routeQueue.Clear();
        routeTotal = null;
        Pickup = null;
    }

    public void OnUpdate(IFramework framework)
    {
        try
        {
            current = Compute();
            errorLogged = false;
        }
        catch (Exception ex)
        {
            if (!errorLogged)
            {
                log.Error(ex, "QuestNavigator: compute failed");
                errorLogged = true;
            }

            current = new() { Mode = NavigationState.Modes.NoLocation, Reason = "no location data" };
        }
    }

    /// <summary>Accepted quests for the picker popup. Framework thread only (called from Draw).</summary>
    public List<(ushort Id, string Name)> GetAcceptedQuests()
    {
        var result = new List<(ushort Id, string Name)>();
        var qm = QuestManager.Instance();
        if (qm == null)
        {
            return result;
        }

        foreach (ref var q in qm->NormalQuests)
        {
            if (q.QuestId == 0 || q.IsHidden)
            {
                continue;
            }

            result.Add((q.QuestId, QuestName(q.QuestId)));
        }

        result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return result;
    }

    /// <summary>Live current-objective label for any accepted quest, keyed by its raw id. Same
    /// scan as the followed-quest StepLabel computation in <see cref="Compute"/> — see that
    /// method's "1) The game's own live quest markers" step — but standalone so
    /// <see cref="Windows.UnlockWindow"/> can look up an accepted row's objective without
    /// following it first. Framework thread only (called from Draw).</summary>
    public string? GetAcceptedQuestObjective(uint rawQuestId)
    {
        var gameMap = GameMap.Instance();
        if (gameMap == null)
        {
            return null;
        }

        foreach (ref var mi in gameMap->QuestMarkers)
        {
            if ((mi.ObjectiveId & 0xFFFF) != rawQuestId)
            {
                continue;
            }

            var label = mi.Label.ToString();
            if (label.Length > 0)
            {
                return label;
            }
        }

        return null;
    }

    private NavigationState Compute()
    {
        if (!clientState.IsLoggedIn)
        {
            return new();
        }

        var cond = condition;
        if (cond[ConditionFlag.OccupiedInCutSceneEvent] || cond[ConditionFlag.WatchingCutscene]
            || cond[ConditionFlag.WatchingCutscene78] || cond[ConditionFlag.BetweenAreas])
        {
            return new();
        }

        if (cfg.ArrowHideInCombat && cond[ConditionFlag.InCombat])
        {
            return new();
        }

        if (cfg.ArrowHideInDuty && cond[ConditionFlag.BoundByDuty])
        {
            return new();
        }

        var player = objects.LocalPlayer;
        if (player == null)
        {
            return new();
        }

        var pos = player.Position;
        var ctx = new GuidanceContext(
            clientState.TerritoryType, clientState.MapId, pos.X, pos.Y, pos.Z, LoggedIn: true);

        return ComputePickup(ctx) ?? ComputeFollowedQuest(ctx);
    }

    /// <summary>The active unlock-quest pickup, advancing the route when the current one has been
    /// accepted or completed. Null when nothing is queued, which is what makes the caller fall
    /// through to the followed quest.</summary>
    private NavigationState? ComputePickup(GuidanceContext ctx)
    {
        if (Pickup is not { } pickup)
        {
            return null;
        }

        var raw = (ushort)(pickup.QuestRowId - QuestRowIdOffset);
        var qm2 = QuestManager.Instance();
        if ((qm2 != null && qm2->IsQuestAccepted(raw)) || QuestManager.IsQuestComplete(pickup.QuestRowId))
        {
            // Picked up (or already done) — advance the route or resume quests.
            Pickup = routeQueue.Count > 0 ? routeQueue.Dequeue() : null;
            if (routeTotal is not null)
            {
                if (Pickup is not null)
                {
                    routeStop++;
                }
                else
                {
                    routeTotal = null; // route exhausted
                }
            }

            OnPickupAdvanced?.Invoke();
        }

        if (Pickup is not { } p)
        {
            return null;
        }

        var label = p.GiverName is { Length: > 0 } giver
            ? $"Pick up: {p.QuestName} from {giver}"
            : $"Pick up: {p.QuestName}";
        var progress = routeTotal is { } total ? new ObjectiveProgress(routeStop, total, null) : null;

        var objective = new GuidanceObjective(
            new ObjectiveKey(PickupSourceId, p.QuestRowId.ToString(CultureInfo.InvariantCulture)),
            new ObjectiveDestination.WorldPoint(p.Territory, p.MapId, p.X, p.Y, p.Z),
            new ObjectiveCopy($"Unlocks: {p.UnlockName}", label, "Unlock route"),
            progress,
            QuestId: p.QuestRowId);

        return GuidanceProjection.Build(objective, GuidanceEngagement.Engaged, router.Route(objective, ctx));
    }

    private NavigationState ComputeFollowedQuest(GuidanceContext ctx)
    {
        var followed = ResolveFollowedQuest();
        if (followed == null)
        {
            return new() { Mode = NavigationState.Modes.Idle };
        }

        var (questId, questName) = followed.Value;
        var (stepLabel, markers) = ReadQuestMarkers(questId);
        var destination = ResolveDestination(questId, markers, ctx);

        var objective = new GuidanceObjective(
            new ObjectiveKey(QuestSourceId, questId.ToString(CultureInfo.InvariantCulture)),
            destination,
            new ObjectiveCopy(questName, stepLabel, FollowedOverride is null ? "Main Scenario" : "Followed quest"),
            QuestId: questId + QuestRowIdOffset);

        return GuidanceProjection.Build(objective, GuidanceEngagement.Ambient, router.Route(objective, ctx));
    }

    /// <summary>The game's own live quest markers — authoritative for the current step. Returns the
    /// step label (the first non-empty marker label) and every marker position for this quest.</summary>
    private (string? StepLabel, List<MarkerPoint> Markers) ReadQuestMarkers(ushort questId)
    {
        string? stepLabel = null;
        var markers = new List<MarkerPoint>();
        var gameMap = GameMap.Instance();
        if (gameMap == null)
        {
            return (null, markers);
        }

        foreach (ref var mi in gameMap->QuestMarkers)
        {
            if ((mi.ObjectiveId & 0xFFFF) != questId)
            {
                continue;
            }

            if (stepLabel == null)
            {
                var label = mi.Label.ToString();
                if (label.Length > 0)
                {
                    stepLabel = label;
                }
            }

            for (var i = 0; i < (int)mi.MarkerData.LongCount; i++)
            {
                var md = mi.MarkerData[i];
                markers.Add(new(md.Position.X, md.Position.Y, md.Position.Z, md.TerritoryTypeId, md.MapId));
            }
        }

        return (stepLabel, markers);
    }

    /// <summary>Where the followed quest's current step is. Marker precedence is a pure decision
    /// (MarkerSelection.Select, Core-tested) — see that type's doc comments for exactly what's
    /// verified live (the Fortemps Manor entrance-marker case) versus assumed (the general
    /// territory-only shape). Summary of the three tiers:
    ///   Exact           — same territory AND same map: walk straight there.
    ///   TerritoryOnly   — same territory, different map (a different floor, OR an entrance marker
    ///                     for a technically-interior objective sitting in the player's outdoor
    ///                     zone — the marker data alone can't tell these apart). Still a
    ///                     WorldPoint: the ROUTER is what tries cross-map candidate routing first
    ///                     and only falls back to a direct arrow when no candidate exists at all.
    ///   None            — no marker in the player's current territory; fall through to the
    ///                     cross-territory (markers[0]) and static-sheet paths.</summary>
    private ObjectiveDestination ResolveDestination(ushort questId, List<MarkerPoint> markers, GuidanceContext ctx)
    {
        var (markerMatch, matched) = MarkerSelection.Select(
            markers, ctx.Territory, ctx.MapId, ctx.PlayerX, ctx.PlayerY, ctx.PlayerZ);
        if (markerMatch is MarkerMatch.Exact or MarkerMatch.TerritoryOnly)
        {
            var mk = matched!;
            return new ObjectiveDestination.WorldPoint(ctx.Territory, mk.MapId, mk.X, mk.Y, mk.Z);
        }

        if (markers.Count > 0)
        {
            var m = markers[0];
            return new ObjectiveDestination.WorldPoint(m.TerritoryId, m.MapId, m.X, m.Y, m.Z);
        }

        // Static sheet fallback: quest ToDo location for the current sequence.
        var seq = QuestManager.GetQuestSequence(questId);
        if (dataManager.GetExcelSheet<Quest>().GetRowOrDefault(questId + QuestRowIdOffset) is { } q)
        {
            foreach (var p in q.TodoParams)
            {
                if (p.ToDoCompleteSeq != seq)
                {
                    continue;
                }

                foreach (var locRef in p.ToDoLocation)
                {
                    if (locRef.RowId == 0 || locRef.ValueNullable is not { } level)
                    {
                        continue;
                    }

                    return new ObjectiveDestination.WorldPoint(
                        level.Territory.RowId, level.Map.RowId, level.X, level.Y, level.Z);
                }

                break;
            }
        }

        return new ObjectiveDestination.Unresolved(
            "this step has no map location (it may take place inside a duty or cutscene)");
    }

    private (ushort Id, string Name)? ResolveFollowedQuest()
    {
        var qm = QuestManager.Instance();
        if (FollowedOverride is { } o)
        {
            if (qm == null)
            {
                return (o, QuestName(o)); // can't confirm right now — keep override, don't fall back to MSQ
            }

            if (qm->IsQuestAccepted(o))
            {
                return (o, QuestName(o));
            }

            FollowedOverride = null; // confirmed completed/abandoned → back to MSQ
        }

        var tree = AgentScenarioTree.Instance();
        if (tree == null || tree->Data == null)
        {
            return null;
        }

        var ids = tree->Data->MainScenarioQuestIds;
        for (var i = 0; i < 3 && i < ids.Length; i++)
        {
            if (ids[i] != 0)
            {
                return (ids[i], QuestName(ids[i]));
            }
        }

        return null;
    }

    private string QuestName(ushort id) =>
        dataManager.GetExcelSheet<Quest>().GetRowOrDefault(id + QuestRowIdOffset)?.Name.ExtractText()
        ?? $"Quest {id}";
}
