using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Navigation;
using GameMap = FFXIVClientStructs.FFXIV.Client.Game.UI.Map;

namespace Wayfarer;

/// <summary>A single unlock-quest pickup step in a navigator route: the location to
/// walk to in order to accept the quest that unlocks something.</summary>
public sealed record PickupTarget(
    string UnlockName, string QuestName, uint QuestRowId,
    uint Territory, uint MapId, float X, float Y, float Z);

/// <summary>Resolves the followed quest's objective once per framework tick and
/// publishes an immutable NavigationState (read by ArrowWindow and get_navigation;
/// cross-thread reads are safe because only the reference is swapped). Owned by
/// <see cref="Modules.QuestHelperModule"/>, which subscribes <see cref="OnUpdate"/>
/// to <c>Framework.Update</c> in <c>Enable()</c> and unsubscribes in <c>Disable()</c> —
/// this class runs only while that module is enabled.</summary>
internal sealed unsafe class QuestNavigator(
    IPluginLog log,
    QuestHelperConfig cfg,
    IClientState clientState,
    ICondition condition,
    IObjectTable objects,
    IDataManager dataManager) : INavigationProvider
{
    private const uint QuestRowIdOffset = 65536;

    private readonly Dictionary<uint, List<AetherytePoint>> aetheryteCache = [];
    private readonly Dictionary<uint, List<AetherytePoint>> aethernetCache = [];
    private readonly Queue<PickupTarget> routeQueue = new();
    private readonly Dictionary<(uint FromMap, uint ToMap), (string Name, float X, float Z)?> entranceCache = [];
    private volatile NavigationState current = new();
    private bool errorLogged;

    public event System.Action? OnPickupAdvanced;

    public ushort? FollowedOverride { get; set; }

    public NavigationState Current => current;

    public PickupTarget? Pickup { get; private set; }

    public void SetPickup(PickupTarget t)
    {
        routeQueue.Clear();
        Pickup = t;
    }

    public void SetRoute(List<PickupTarget> route)
    {
        routeQueue.Clear();
        foreach (var t in route)
        {
            routeQueue.Enqueue(t);
        }

        Pickup = routeQueue.Count > 0 ? routeQueue.Dequeue() : null;
    }

    public void ClearPickup()
    {
        routeQueue.Clear();
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

        var territory = clientState.TerritoryType;
        var mapId = clientState.MapId;
        var pos = player.Position;

        if (Pickup is { } pickup)
        {
            var raw = (ushort)(pickup.QuestRowId - QuestRowIdOffset);
            var qm2 = QuestManager.Instance();
            if ((qm2 != null && qm2->IsQuestAccepted(raw)) || QuestManager.IsQuestComplete(pickup.QuestRowId))
            {
                // Picked up (or already done) — advance the route or resume quests.
                Pickup = routeQueue.Count > 0 ? routeQueue.Dequeue() : null;
                OnPickupAdvanced?.Invoke();
            }

            if (Pickup is { } p)
            {
                var label = $"Pick up: {p.QuestName}";
                if (p.Territory == territory && p.MapId == mapId)
                {
                    var d = NavMath.Distance(p.X - pos.X, p.Y - pos.Y, p.Z - pos.Z);
                    return SameZone(p.QuestRowId, $"Unlocks: {p.UnlockName}", label, p.X, p.Y, p.Z, d, territory, pos.X, pos.Z, isPickup: true);
                }

                return OtherZone(p.QuestRowId, $"Unlocks: {p.UnlockName}", label, p.Territory, p.MapId, p.X, p.Z, pos.X, pos.Z, isPickup: true);
            }
        }

        var followed = ResolveFollowedQuest();
        if (followed == null)
        {
            return new() { Mode = NavigationState.Modes.Idle };
        }

        var (questId, questName) = followed.Value;

        // 1) The game's own live quest markers — authoritative for the current step.
        string? stepLabel = null;
        var markers = new List<(float X, float Y, float Z, uint TerritoryId, uint MapId)>();
        var gameMap = GameMap.Instance();
        if (gameMap != null)
        {
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
                    markers.Add((md.Position.X, md.Position.Y, md.Position.Z, md.TerritoryTypeId, md.MapId));
                }
            }
        }

        var bestDist = float.MaxValue;
        (float X, float Y, float Z) best = default;
        foreach (var m in markers)
        {
            if (m.TerritoryId != territory || m.MapId != mapId)
            {
                continue;
            }

            var d = NavMath.Distance(m.X - pos.X, m.Y - pos.Y, m.Z - pos.Z);
            if (d < bestDist)
            {
                best = (m.X, m.Y, m.Z);
                bestDist = d;
            }
        }

        if (bestDist < float.MaxValue)
        {
            return SameZone(questId + QuestRowIdOffset, questName, stepLabel, best.X, best.Y, best.Z, bestDist, territory, pos.X, pos.Z);
        }

        if (markers.Count > 0)
        {
            var m = markers[0];
            return OtherZone(questId + QuestRowIdOffset, questName, stepLabel, m.TerritoryId, m.MapId, m.X, m.Z, pos.X, pos.Z);
        }

        // 2) Static sheet fallback: quest ToDo location for the current sequence.
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

                    if (level.Territory.RowId == territory && level.Map.RowId == mapId)
                    {
                        var d = NavMath.Distance(level.X - pos.X, level.Y - pos.Y, level.Z - pos.Z);
                        return SameZone(questId + QuestRowIdOffset, questName, stepLabel, level.X, level.Y, level.Z, d, territory, pos.X, pos.Z);
                    }

                    return OtherZone(questId + QuestRowIdOffset, questName, stepLabel, level.Territory.RowId, level.Map.RowId, level.X, level.Z, pos.X, pos.Z);
                }

                break;
            }
        }

        return new()
        {
            Mode = NavigationState.Modes.NoLocation,
            QuestId = questId + QuestRowIdOffset,
            QuestName = questName,
            StepLabel = stepLabel,
            Reason = "this step has no map location (it may take place inside a duty or cutscene)",
        };
    }

    private NavigationState SameZone(
        uint displayQuestId,
        string questName,
        string? stepLabel,
        float tx,
        float ty,
        float tz,
        float dist,
        uint territory,
        float px,
        float pz,
        bool isPickup = false)
    {
        // City aethernet routing: if hopping the entry shard nearest the player and
        // out of the shard nearest the objective beats the direct run, retarget the
        // arrow to the entry shard and surface the exit shard's name for the travel menu.
        if (AethernetRoute(territory, px, pz, tx, tz, dist) is { } route)
        {
            var playerToEntry = NavMath.Distance(route.Entry.X - px, 0, route.Entry.Z - pz);
            return new()
            {
                Mode = NavigationState.Modes.SameZone,
                QuestId = displayQuestId,
                QuestName = questName,
                StepLabel = stepLabel,
                TargetX = route.Entry.X, TargetZ = route.Entry.Z, // arrow → entry shard (TargetY absent: widget uses player Y)
                DistanceYalms = playerToEntry,
                AethernetEntryName = route.Entry.Name,
                AethernetExitName = route.Exit.Name,
                IsPickup = isPickup,
            };
        }

        return new()
        {
            Mode = NavigationState.Modes.SameZone,
            QuestId = displayQuestId,
            QuestName = questName,
            StepLabel = stepLabel,
            TargetX = tx, TargetY = ty, TargetZ = tz,
            DistanceYalms = dist,
            IsPickup = isPickup,
        };
    }

    /// <summary>Returns (entry shard nearest the player, exit shard nearest the target)
    /// when hopping the aethernet beats running to the target directly; null otherwise.</summary>
    private (AetherytePoint Entry, AetherytePoint Exit)? AethernetRoute(
        uint territory, float px, float pz, float tx, float tz, float directDist)
    {
        var shards = GetAetherytePoints(territory, aethernet: true);
        if (AetherytePicker.Nearest(shards, px, pz) is { } entry
            && AetherytePicker.Nearest(shards, tx, tz) is { } exit
            && entry.Id != exit.Id)
        {
            var playerToEntry = NavMath.Distance(entry.X - px, 0, entry.Z - pz);
            var exitToTarget = NavMath.Distance(exit.X - tx, 0, exit.Z - tz);
            if (AetherytePicker.ShouldRouteViaAethernet(directDist, playerToEntry, exitToTarget))
            {
                return (entry, exit);
            }
        }

        return null;
    }

    private NavigationState OtherZone(
        uint displayQuestId,
        string questName,
        string? stepLabel,
        uint targetTerritory,
        uint targetMapId,
        float tx,
        float tz,
        float px,
        float pz,
        bool isPickup = false)
    {
        var territorySheet = dataManager.GetExcelSheet<TerritoryType>();
        var zoneName = territorySheet.GetRowOrDefault(targetTerritory)?.PlaceName.ValueNullable?.Name.ExtractText();

        // Teleport recommendation — skipped when the objective is in the current
        // territory (another map layer of the zone you are already standing in).
        uint? aetheryteId = null;
        string? aetheryteName = null;
        var aetheryteUnlocked = false;
        if (targetTerritory != clientState.TerritoryType)
        {
            var all = GetAetherytePoints(targetTerritory, aethernet: false);
            var ui = UIState.Instance();
            var unlockedPts = new List<AetherytePoint>();
            if (ui != null)
            {
                foreach (var a in all)
                {
                    if (ui->IsAetheryteUnlocked(a.Id))
                    {
                        unlockedPts.Add(a);
                    }
                }
            }

            var pick = AetherytePicker.Nearest(unlockedPts.Count > 0 ? unlockedPts : all, tx, tz);
            if (pick != null)
            {
                aetheryteId = pick.Id;
                aetheryteName = pick.Name;
                aetheryteUnlocked = unlockedPts.Count > 0;
            }
            else if (territorySheet.GetRowOrDefault(targetTerritory) is { } tt
                     && tt.Aetheryte.RowId != 0
                     && tt.Aetheryte.ValueNullable is { } fallback)
            {
                // Interiors own no aetheryte; the TerritoryType fallback names the
                // parent city's aetheryte (verified against live game data).
                aetheryteId = tt.Aetheryte.RowId;
                aetheryteName = fallback.PlaceName.ValueNullable?.Name.ExtractText();
                var ui2 = UIState.Instance();
                aetheryteUnlocked = ui2 != null && ui2->IsAetheryteUnlocked(tt.Aetheryte.RowId);
            }
        }

        var entrance = FindEntrance(clientState.MapId, targetMapId);

        string? aethernetEntry = null;
        string? aethernetExit = null;
        if (entrance is { } e)
        {
            var directDist = NavMath.Distance(e.X - px, 0, e.Z - pz);
            if (AethernetRoute(clientState.TerritoryType, px, pz, e.X, e.Z, directDist) is { } route)
            {
                aethernetEntry = route.Entry.Name;
                aethernetExit = route.Exit.Name;
                entrance = (e.Name, route.Entry.X, route.Entry.Z); // arrow → entry shard
            }
        }

        return new()
        {
            Mode = NavigationState.Modes.OtherZone,
            QuestId = displayQuestId,
            QuestName = questName,
            StepLabel = stepLabel,
            ZoneName = zoneName,
            TargetX = tx, TargetZ = tz,
            AetheryteId = aetheryteId,
            AetheryteName = aetheryteName,
            AetheryteUnlocked = aetheryteUnlocked,
            EntranceName = entrance?.Name,
            EntranceX = entrance?.X,
            EntranceZ = entrance?.Z,
            AethernetEntryName = aethernetEntry,
            AethernetExitName = aethernetExit,
            IsPickup = isPickup,
        };
    }

    /// <summary>Finds a map-link marker (door / zone exit) on the current map that
    /// leads to the objective's map. DataType 1 = adjacent map, 2 = interior sub-map;
    /// DataKey is the destination Map row for both (verified against live game data).</summary>
    private (string Name, float X, float Z)? FindEntrance(uint fromMapId, uint toMapId)
    {
        var key = (fromMapId, toMapId);
        if (entranceCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        (string Name, float X, float Z)? found = null;
        var mapSheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>();
        if (fromMapId != toMapId && mapSheet.GetRowOrDefault(fromMapId) is { } from)
        {
            var markerSheet = dataManager.GetSubrowExcelSheet<MapMarker>();
            if (markerSheet.HasRow(from.MapMarkerRange))
            {
                foreach (var m in markerSheet[from.MapMarkerRange])
                {
                    if (m.DataType is not (1 or 2) || m.DataKey.RowId != toMapId)
                    {
                        continue;
                    }

                    var (x, z) = MapCoords.MarkerPixelToWorld(m.X, m.Y, from.SizeFactor, from.OffsetX, from.OffsetY);
                    var name = m.PlaceNameSubtext.ValueNullable?.Name.ExtractText();
                    if (string.IsNullOrEmpty(name))
                    {
                        name = mapSheet.GetRowOrDefault(toMapId)?.PlaceName.ValueNullable?.Name.ExtractText() ?? "entrance";
                    }

                    found = (name!, x, z);
                    break;
                }
            }
        }

        entranceCache[key] = found;
        return found;
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

    private List<AetherytePoint> GetAetherytePoints(uint territory, bool aethernet)
    {
        var cache = aethernet ? aethernetCache : aetheryteCache;
        if (cache.TryGetValue(territory, out var cached))
        {
            return cached;
        }

        var list = new List<AetherytePoint>();
        foreach (var a in dataManager.GetExcelSheet<Aetheryte>())
        {
            if (a.Territory.RowId != territory)
            {
                continue;
            }

            // Aethernet candidates are anything on the city network (shards AND the
            // main aetheryte, which is itself an aethernet stop in-game); plain
            // aetheryte candidates remain IsAetheryte rows only.
            if (aethernet ? a.AethernetName.RowId == 0 : !a.IsAetheryte)
            {
                continue;
            }

            var name = aethernet
                ? a.AethernetName.ValueNullable?.Name.ExtractText()
                : a.PlaceName.ValueNullable?.Name.ExtractText();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (TryGetAetherytePosition(a, out var x, out var z))
            {
                list.Add(new AetherytePoint(a.RowId, name, x, z));
            }
        }

        cache[territory] = list;
        return list;
    }

    private bool TryGetAetherytePosition(Aetheryte a, out float x, out float z)
    {
        x = z = 0;
        foreach (var lv in a.Level)
        {
            if (lv.RowId == 0 || lv.ValueNullable is not { } level)
            {
                continue;
            }

            x = level.X;
            z = level.Z;
            return true;
        }

        if (a.Map.ValueNullable is { } map)
        {
            var markerSheet = dataManager.GetSubrowExcelSheet<MapMarker>();
            if (markerSheet.HasRow(map.MapMarkerRange))
            {
                foreach (var m in markerSheet[map.MapMarkerRange])
                {
                    var matches = m.DataType switch
                    {
                        3 => m.DataKey.RowId == a.RowId,               // aetheryte: DataKey = Aetheryte row
                        4 => m.DataKey.RowId == a.AethernetName.RowId, // shard: DataKey = PlaceName row (verified)
                        _ => false,
                    };
                    if (!matches)
                    {
                        continue;
                    }

                    (x, z) = MapCoords.MarkerPixelToWorld(m.X, m.Y, map.SizeFactor, map.OffsetX, map.OffsetY);
                    return true;
                }
            }
        }

        return false;
    }
}
