using System.Globalization;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Navigation;
using GameMap = FFXIVClientStructs.FFXIV.Client.Game.UI.Map;

namespace Wayfarer.Guidance.Sources;

/// <summary>The AMBIENT guidance source: whatever quest the player is following when no explicit
/// mode is engaged — their own chosen quest if they picked one, otherwise the head of the main
/// scenario. It never engages, so it can never take the arrow away from a route or a hunt; it is
/// simply what the arrow falls back to, which is also why ending a hunt returns the player to THEIR
/// quest rather than to the MSQ.
///
/// Completion is the quest system's business and is answered here: a quest that is no longer
/// accepted stops being offered. Nothing outside this class asks that question.</summary>
internal sealed unsafe class QuestObjectiveSource(IDataManager dataManager) : IGuidanceSource
{
    private const uint QuestRowIdOffset = 65536;

    public string SourceId => "quest";

    /// <summary>The player's explicit quest pick, or null to follow the main scenario. Cleared
    /// automatically once the game confirms that quest is no longer accepted.</summary>
    public ushort? FollowedQuest { get; set; }

    public GuidanceOffer? Poll(GuidanceContext ctx)
    {
        var qm = QuestManager.Instance();
        var outcome = QuestFollowResolution.Resolve(
            FollowedQuest,
            qm != null,
            id => qm != null && qm->IsQuestAccepted(id),
            ReadMainScenarioQuestIds());

        if (outcome.ClearOverride)
        {
            FollowedQuest = null;
        }

        if (outcome.QuestId is not { } questId)
        {
            return null;
        }

        var (stepLabel, markers) = ReadQuestMarkers(questId);
        var objective = new GuidanceObjective(
            new ObjectiveKey(SourceId, questId.ToString(CultureInfo.InvariantCulture)),
            ResolveDestination(questId, markers, ctx),
            new ObjectiveCopy(
                QuestName(questId),
                stepLabel,
                FollowedQuest is null ? "Main Scenario" : "Followed quest"),
            QuestId: questId + QuestRowIdOffset);

        return new GuidanceOffer(objective, GuidanceEngagement.Ambient);
    }

    /// <summary>Ambient sources never hold the token, so this cannot be reached in practice — the
    /// followed quest deliberately survives every mode change, which is what makes exiting a hunt
    /// return the player to their own quest.</summary>
    public void OnDisengaged(DisengageReason reason)
    {
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

    /// <summary>Live current-objective label for any accepted quest, keyed by its raw id. Same scan
    /// as the followed quest's step label (see <see cref="ReadQuestMarkers"/>), but standalone so
    /// the checklist can show an accepted row's objective without following it first. Framework
    /// thread only. Null when the game has no marker for this quest right now (not every step/zone
    /// has one) or the marker's label text is empty.</summary>
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

    private static List<ushort> ReadMainScenarioQuestIds()
    {
        var result = new List<ushort>();
        var tree = AgentScenarioTree.Instance();
        if (tree == null || tree->Data == null)
        {
            return result;
        }

        var ids = tree->Data->MainScenarioQuestIds;
        for (var i = 0; i < 3 && i < ids.Length; i++)
        {
            result.Add(ids[i]);
        }

        return result;
    }

    /// <summary>The game's own live quest markers — authoritative for the current step. Returns the
    /// step label (the first non-empty marker label) and every marker position for this quest.</summary>
    private static (string? StepLabel, List<MarkerPoint> Markers) ReadQuestMarkers(ushort questId)
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

                // md.Radius: the game's own "search this area" circle radius, in yalms — 0 for an
                // ordinary point objective. Verified against the installed FFXIVClientStructs.dll
                // (MapMarkerData, field offset 0x28, float). Dropping it here is the exact defect
                // that sent a player an arrow with a precise-looking distance to the CENTRE of a
                // search-area step instead of telling them it was an area to search — see
                // MarkerPoint.Radius and everything downstream of it.
                markers.Add(new(md.Position.X, md.Position.Y, md.Position.Z, md.TerritoryTypeId, md.MapId, md.Radius));
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
            return new ObjectiveDestination.WorldPoint(ctx.Territory, mk.MapId, mk.X, mk.Y, mk.Z, Radius: mk.Radius);
        }

        if (markers.Count > 0)
        {
            var m = markers[0];
            return new ObjectiveDestination.WorldPoint(m.TerritoryId, m.MapId, m.X, m.Y, m.Z, Radius: m.Radius);
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

    private string QuestName(ushort id) =>
        dataManager.GetExcelSheet<Quest>().GetRowOrDefault(id + QuestRowIdOffset)?.Name.ExtractText()
        ?? $"Quest {id}";
}
