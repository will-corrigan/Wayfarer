using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Navigation;

namespace Wayfarer.Guidance;

/// <summary>How to get to an objective — teleport, city aethernet, a map-link door, or just an
/// arrow — for ANY objective, from any source. Extracted verbatim from the quest navigator's own
/// routing half (task: guidance architecture step 2): the candidate costing, the split-city
/// teleport suppression, the aethernet-group derivation from raw sheet rows, the interior marker
/// fallback and every cache are the same code, moved rather than rewritten, because this is
/// live-verified game-data logic where a "tidy-up" is indistinguishable from a regression.
///
/// It holds sheet/route caches but NO state about what is being guided to: call it with any
/// objective, on any tick, in any order — the answer depends only on the arguments. Every decision
/// still delegates to the pure, already-tested services in <c>Wayfarer.Core.Navigation</c>.
///
/// This is also why no source can have "worse" routing than another: there is one router and it
/// takes a <see cref="GuidanceObjective"/>, so a hunting leg and a quest step reach exactly the
/// same code.</summary>
internal sealed unsafe class GuidanceRouter(IDataManager dataManager)
{
    /// <summary>Presence sentinel for <see cref="OtherZoneResolution.Resolve"/>, which only asks
    /// WHETHER the caller has a marker fallback (it returns the caller's own fallback verbatim, so
    /// the state passed here is never read or rendered).</summary>
    private static readonly NavigationState FallbackPresent = new();

    private readonly Dictionary<uint, List<AetherytePoint>> aetheryteCache = [];
    private readonly Dictionary<uint, List<AetherytePoint>> aethernetCache = [];
    private readonly Dictionary<(uint FromMap, uint ToMap), List<MapLinkPoint>> entranceCache = [];
    private readonly Dictionary<uint, HashSet<uint>> aethernetGroupCache = [];
    private List<AethernetSheetRow>? aethernetSheetRows;
    private Dictionary<uint, DutyInfo>? dutyByTerritory;

    /// <summary>Routes <paramref name="objective"/> from where the player currently stands.
    /// Framework thread only (it reads Lumina sheets and UIState).</summary>
    public RouteResult Route(GuidanceObjective objective, GuidanceContext ctx) => objective.Destination switch
    {
        ObjectiveDestination.WorldPoint p => RouteToPoint(objective, p, ctx),

        // No usable point in the zone: the teleport/aethernet advice still applies, so this goes
        // through the same cross-zone costing with the origin as the notional target — candidate
        // selection degrades to overhead-only, which is exactly what "the right zone, somewhere"
        // deserves.
        ObjectiveDestination.TerritoryOnly t => OtherZone(objective, t.Territory, t.MapId ?? 0, 0f, 0f, ctx),
        ObjectiveDestination.InstancedDuty d => (RouteResult?)DutyRoute(objective, d.DutyTerritory)
            ?? new RouteResult.NoLocation("this objective is inside a duty"),
        ObjectiveDestination.Unresolved u => new RouteResult.NoLocation(u.Reason),
        _ => new RouteResult.NoLocation("no map location for this step"),
    };

    /// <summary>Where the player would arrive in <paramref name="territory"/> if they teleported
    /// there now — the position of the aetheryte this router would recommend, in that territory's
    /// own coordinate space. Null when the territory has no aetheryte of its own (its fallback
    /// aetheryte lives somewhere else, so its coordinates are not comparable). Used by multi-zone
    /// plans to start each zone's walk where the player actually lands.</summary>
    public (float X, float Z)? ArrivalPoint(uint territory, float nearX, float nearZ)
    {
        var (aetheryte, aetheryteTerritory, _) = ResolveTargetAetheryte(territory, nearX, nearZ);
        return aetheryte is { } point && aetheryteTerritory == territory ? (point.X, point.Z) : null;
    }

    /// <summary>Whether two territories sit on the same city aethernet network, i.e. whether you
    /// can walk/shard between them for free instead of paying a teleport and a loading screen.
    /// Derived from raw Aetheryte sheet rows, never from position-resolved points — see
    /// <see cref="GetAethernetGroups"/> for the live bug that distinction fixed.</summary>
    public bool SharesAethernetNetwork(uint territory, uint other) =>
        territory == other || GetAethernetGroups(territory).Overlaps(GetAethernetGroups(other));

    private RouteResult RouteToPoint(GuidanceObjective objective, ObjectiveDestination.WorldPoint p, GuidanceContext ctx)
    {
        if (p.Territory == ctx.Territory && p.MapId == ctx.MapId)
        {
            var d = NavMath.Distance(p.X - ctx.PlayerX, p.Y - ctx.PlayerY, p.Z - ctx.PlayerZ);
            return SameZone(p.X, p.Y, p.Z, d, ctx);
        }

        // Same territory, different map — a different floor, OR an entrance marker for a
        // technically-interior objective sitting in the player's outdoor zone (the data alone
        // can't tell these apart). NEVER jump straight to a raw arrow: try the same cross-map
        // candidate routing a genuine cross-territory objective gets, since a real map-link
        // entrance (stairs, a door) must win over a straight line through a floor. Only when NO
        // candidate exists at all — the Fortemps Manor shape — fall back to a direct arrow at the
        // point itself as the least-bad guidance available.
        RouteResult.SameZone? fallback = null;
        if (p.Territory == ctx.Territory)
        {
            var fallbackDist = NavMath.Distance(p.X - ctx.PlayerX, p.Y - ctx.PlayerY, p.Z - ctx.PlayerZ);
            fallback = SameZone(p.X, p.Y, p.Z, fallbackDist, ctx);
        }

        return OtherZone(objective, p.Territory, p.MapId, p.X, p.Z, ctx, fallback);
    }

    /// <summary>City aethernet routing: if hopping the entry shard nearest the player and out of
    /// the shard nearest the objective beats the direct run, retarget the arrow to the entry shard
    /// and surface the exit shard's name for the travel menu.</summary>
    private RouteResult.SameZone SameZone(float tx, float ty, float tz, float dist, GuidanceContext ctx)
    {
        if (AethernetRoute(ctx.Territory, ctx.PlayerX, ctx.PlayerZ, tx, tz, dist) is { } route)
        {
            var playerToEntry = NavMath.Distance(route.Entry.X - ctx.PlayerX, 0, route.Entry.Z - ctx.PlayerZ);
            return new RouteResult.SameZone(
                route.Entry.X,
                null, // arrow → entry shard; TargetY absent, the widget uses the player's own Y
                route.Entry.Z,
                playerToEntry,
                AethernetEntryName: route.Entry.Name,
                AethernetExitName: route.Exit.Name);
        }

        return new RouteResult.SameZone(tx, ty, tz, dist);
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

    private RouteResult OtherZone(
        GuidanceObjective objective,
        uint targetTerritory,
        uint targetMapId,
        float tx,
        float tz,
        GuidanceContext ctx,
        RouteResult.SameZone? fallbackWhenNoCandidate = null)
    {
        // Duty content (dungeons/trials/raids) has no aetherytes or entrances to route to — route
        // costing below would correctly find nothing and report a useless "no route found". Detect
        // and short-circuit BEFORE building any candidates.
        if (DutyRoute(objective, targetTerritory) is { } duty)
        {
            return duty;
        }

        var territorySheet = dataManager.GetExcelSheet<TerritoryType>();
        var zoneName = territorySheet.GetRowOrDefault(targetTerritory)?.PlaceName.ValueNullable?.Name.ExtractText();
        var currentTerritory = ctx.Territory;

        var currentTerritoryShards = GetAetherytePoints(currentTerritory, aethernet: true);
        var aethernet = RouteCosting.AethernetCandidate(
            currentTerritoryShards,
            GetAetherytePoints(targetTerritory, aethernet: true),
            ctx.PlayerX,
            ctx.PlayerZ,
            tx,
            tz);

        var sourceLinks = FindEntrances(ctx.MapId, targetMapId);
        var targetLinks = FindEntrances(targetMapId, ctx.MapId);
        var entrance = RouteCosting.EntranceCandidate(sourceLinks, targetLinks, ctx.PlayerX, ctx.PlayerZ, tx, tz);

        // City-network-local teleports are never useful advice — see
        // RouteCosting.TeleportCandidate's doc comment. Both group sets come from raw sheet rows
        // (GetAethernetGroups), NEVER from the position-filtered point lists above — see that
        // method's doc comment for the live bug this fixed.
        var currentTerritoryGroups = GetAethernetGroups(currentTerritory);

        var (aetheryte, aetheryteTerritory, unlocked) = ResolveTargetAetheryte(targetTerritory, tx, tz);
        var aetheryteTerritoryGroups = GetAethernetGroups(aetheryteTerritory);

        var teleport = RouteCosting.TeleportCandidate(
            aetheryte,
            aetheryteTerritory,
            targetTerritory,
            currentTerritory,
            tx,
            tz,
            unlocked,
            currentTerritoryGroups,
            aetheryteTerritoryGroups);

        var chosen = RouteCosting.Choose(aethernet, entrance, teleport);

        // The three-way choice (real route / marker fallback / plain interior message) is a pure
        // Core decision (OtherZoneResolution.Resolve) — see its doc comment.
        return OtherZoneResolution.Resolve(chosen, fallbackWhenNoCandidate is null ? null : FallbackPresent) switch
        {
            OtherZoneOutcome.MarkerFallback => fallbackWhenNoCandidate!,
            OtherZoneOutcome.InteriorMessage => new RouteResult.OtherZone(
                zoneName,
                tx,
                tz,
                Reason: OtherZoneResolution.InteriorMessage(zoneName)),
            _ => new RouteResult.OtherZone(
                zoneName,
                tx,
                tz,
                AetheryteId: chosen?.AetheryteId,
                AetheryteName: chosen?.AetheryteName,
                AetheryteUnlocked: chosen?.AetheryteUnlocked ?? false,
                EntranceName: chosen?.EntranceName,
                EntranceX: chosen?.ArrowX,
                EntranceZ: chosen?.ArrowZ,
                AethernetEntryName: chosen?.AethernetEntryName,
                AethernetExitName: chosen?.AethernetExitName,
                RemainingYalms: chosen?.RemainingYalms),
        };
    }

    /// <summary>Duty guidance for <paramref name="targetTerritory"/> when it is instanced content,
    /// null when it isn't. The identity arguments handed to
    /// <see cref="DutyObjectiveGuidance.TryBuild"/> are the objective's own; only the reason text
    /// and the queueable Duty Finder row are read back, since
    /// <see cref="GuidanceProjection"/> owns identity for every route shape alike.</summary>
    private RouteResult.Duty? DutyRoute(GuidanceObjective objective, uint targetTerritory)
    {
        var built = DutyObjectiveGuidance.TryBuild(
            targetTerritory,
            DutyForTerritory,
            UIState.IsInstanceContentUnlocked,
            objective.QuestId ?? 0,
            objective.Copy.Headline,
            objective.Copy.Detail,
            isPickup: false,
            routeStop: null,
            routeTotal: null);

        return built is { Reason: { } reason }
            ? new RouteResult.Duty(reason, built.DutyContentFinderConditionId)
            : null;
    }

    /// <summary>Every nonzero AethernetGroup homed in <paramref name="territory"/>,
    /// derived from RAW Aetheryte sheet rows — deliberately NOT from
    /// <see cref="GetAetherytePoints"/>, whose point builder drops any row without a
    /// resolvable position. That distinction is the third live "Teleport to Foundation
    /// first" reproduction (2026-08-22): every Ishgard shard row (83–87 The Pillars,
    /// 80–82 Foundation) carries Map=0 and dead Level refs, so the position-filtered
    /// list for The Pillars was always empty, the current-territory group set came out
    /// empty, and RouteCosting.TeleportCandidate's same-network suppression never
    /// fired. Group membership is a pure sheet fact; positions are irrelevant to it
    /// (see AethernetGroups.ForTerritory).</summary>
    private HashSet<uint> GetAethernetGroups(uint territory)
    {
        if (aethernetGroupCache.TryGetValue(territory, out var cached))
        {
            return cached;
        }

        if (aethernetSheetRows == null)
        {
            aethernetSheetRows = [];
            foreach (var a in dataManager.GetExcelSheet<Aetheryte>())
            {
                if (a.AethernetGroup != 0)
                {
                    aethernetSheetRows.Add(new(a.Territory.RowId, a.AethernetGroup));
                }
            }
        }

        var groups = AethernetGroups.ForTerritory(aethernetSheetRows, territory);
        aethernetGroupCache[territory] = groups;
        return groups;
    }

    /// <summary>Resolves the aetheryte to recommend teleporting to for an objective in
    /// <paramref name="targetTerritory"/>: that territory's own nearest (unlocked
    /// preferred) aetheryte, or — for territories that own none, e.g. instanced
    /// interiors — the TerritoryType fallback aetheryte, whose OWN territory can differ
    /// from <paramref name="targetTerritory"/> (verified live: both Ishgard territories'
    /// fallback resolves to the Foundation aetheryte). RouteCosting.TeleportCandidate is
    /// responsible for rejecting a fallback that lands back in the player's own
    /// territory; this method only resolves candidates, it doesn't filter them.</summary>
    private (AetherytePoint? Aetheryte, uint Territory, bool Unlocked) ResolveTargetAetheryte(
        uint targetTerritory, float tx, float tz)
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

        if (AetherytePicker.Nearest(unlockedPts.Count > 0 ? unlockedPts : all, tx, tz) is { } pick)
        {
            return (pick, pick.Territory, unlockedPts.Count > 0);
        }

        if (dataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(targetTerritory) is { } tt
            && tt.Aetheryte.RowId != 0
            && tt.Aetheryte.ValueNullable is { } fallback)
        {
            var name = fallback.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;

            // TeleportCandidate only reads the point's X/Z when its territory matches the
            // target; on a resolution failure, report a territory that can never match so the
            // caller falls back to overhead-only costing instead of distancing from (0, 0).
            var resolved = TryGetAetherytePosition(fallback, out var fx, out var fz);
            var territory = resolved ? fallback.Territory.RowId : uint.MaxValue;

            // AethernetGroup must travel with the fallback point too — this is exactly
            // the live bug case (an interior territory's fallback resolving to a
            // same-network city aetheryte), and TeleportCandidate's group-suppression
            // check can only catch it if the point actually carries its group.
            var point = new AetherytePoint(tt.Aetheryte.RowId, name, fx, fz, territory, fallback.AethernetGroup);
            var ui2 = UIState.Instance();
            var fallbackUnlocked = ui2 != null && ui2->IsAetheryteUnlocked(tt.Aetheryte.RowId);
            return (point, territory, fallbackUnlocked);
        }

        return (null, 0, false);
    }

    /// <summary>Finds every map-link marker (door / zone exit) on <paramref
    /// name="fromMapId"/> that leads to <paramref name="toMapId"/>. DataType 1 =
    /// adjacent map, 2 = interior sub-map; DataKey is the destination Map row for both
    /// (verified against live game data). Split cities have several such doors between
    /// their two maps — all are returned so route costing can pick the nearest.</summary>
    private List<MapLinkPoint> FindEntrances(uint fromMapId, uint toMapId)
    {
        var key = (fromMapId, toMapId);
        if (entranceCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var found = new List<MapLinkPoint>();
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

                    found.Add(new(name!, x, z));
                }
            }
        }

        entranceCache[key] = found;
        return found;
    }

    /// <summary>territoryId → duty, built once and cached at first use. Built from the
    /// InstanceContent sheet (not ContentFinderCondition) because InstanceContent has a
    /// direct, typed <c>ContentFinderCondition</c> RowRef — giving both the duty name
    /// and the InstanceContent row id (what UIState.IsInstanceContentUnlocked expects)
    /// from one pass, with no ContentLinkType byte to decode.</summary>
    private DutyInfo? DutyForTerritory(uint territoryId)
    {
        if (dutyByTerritory == null)
        {
            var map = new Dictionary<uint, DutyInfo>();
            foreach (var ic in dataManager.GetExcelSheet<Lumina.Excel.Sheets.InstanceContent>())
            {
                if (ic.ContentFinderCondition.RowId == 0 || ic.ContentFinderCondition.ValueNullable is not { } cfc
                    || cfc.TerritoryType.RowId == 0)
                {
                    continue;
                }

                var name = cfc.Name.ExtractText();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                map[cfc.TerritoryType.RowId] = new DutyInfo(name, ic.RowId, cfc.RowId);
            }

            dutyByTerritory = map;
        }

        return dutyByTerritory.TryGetValue(territoryId, out var duty) ? duty : null;
    }

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
                list.Add(new(a.RowId, name, x, z, a.Territory.RowId, a.AethernetGroup));
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

        // The row's own Map reference is not reliable: every Ishgard shard row
        // (80–82 Foundation, 83–87 The Pillars) carries Map=0 while the TERRITORY's
        // map (218/219) carries their DataType 4 markers — so fall back to the home
        // territory's map, or intra-Ishgard shard routing never gets a position.
        var mapSheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>();
        var markerSheet = dataManager.GetSubrowExcelSheet<MapMarker>();
        var territoryMap = a.Territory.ValueNullable?.Map.RowId ?? 0;
        foreach (var mapId in AetheryteMapSearch.CandidateMaps(a.Map.RowId, territoryMap))
        {
            if (mapSheet.GetRowOrDefault(mapId) is not { } map || !markerSheet.HasRow(map.MapMarkerRange))
            {
                continue;
            }

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

        return false;
    }
}
