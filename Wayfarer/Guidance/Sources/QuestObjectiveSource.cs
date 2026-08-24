using System.Globalization;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;
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

    /// <summary>What this module calls itself on the readout's banner, which prints "Current" in
    /// front of it — "Current Quest". The same word whether the quest is the player's own pick or
    /// the head of the main scenario: which of the two it is belongs to the mode label below, not to
    /// the module's name. See <see cref="ObjectiveCopy.SourceName"/>.</summary>
    private const string ModuleName = "Quest";

    /// <summary>Macro payload codes that ExtractText always resolves fully, regardless of runtime
    /// state — a button glyph, a line break, a colour tag. Anything else (a player-name insert, an
    /// item-name sheet reference, an if/switch branch) needs live game state Lumina does not have,
    /// and silently drops out of the extracted string instead of erroring — verified live: a
    /// "Sheet" code left "Deliver a suit of steel chainmail  to Blanstyr." (the item name gap) and
    /// an "If" code left a bare "." (the whole branch resolved empty). See
    /// <see cref="ReadQuestStepTexts"/>, the only place this is consulted.</summary>
    private static readonly HashSet<MacroCode> PresentationalMacroCodes =
    [
        MacroCode.NewLine, MacroCode.Wait, MacroCode.Icon, MacroCode.Color, MacroCode.EdgeColor,
        MacroCode.ShadowColor, MacroCode.SoftHyphen, MacroCode.Key, MacroCode.Scale, MacroCode.Bold,
        MacroCode.Italic, MacroCode.Edge, MacroCode.Shadow, MacroCode.NonBreakingSpace, MacroCode.Icon2,
        MacroCode.Hyphen, MacroCode.Link, MacroCode.Caps, MacroCode.Head, MacroCode.Split,
        MacroCode.HeadAll, MacroCode.Fixed, MacroCode.Lower, MacroCode.LowerHead, MacroCode.ColorType,
        MacroCode.EdgeColorType, MacroCode.Ruby, MacroCode.Sound, MacroCode.LevelPos,
        MacroCode.SetResetTime, MacroCode.SetTime,
    ];

    /// <summary>Per-quest ToDo text, read once and kept forever: the sheet is static game data (only
    /// the runtime SEQUENCE that selects among its entries changes tick to tick), so re-scanning the
    /// raw text sheet every <see cref="Poll"/> would be pure waste on the "must be cheap, runs every
    /// tick" path. See <see cref="ReadQuestStepTexts"/>.</summary>
    private readonly Dictionary<ushort, List<QuestStepText>> stepTextCache = [];

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

        var (markerLabel, markers) = ReadQuestMarkers(questId);
        var sequence = QuestManager.GetQuestSequence(questId);
        var stepTexts = ReadQuestStepTexts(questId);
        var stepLabel = QuestStepTextSelection.SelectCurrentStepText(stepTexts, sequence, markerLabel);
        var objective = new GuidanceObjective(
            new ObjectiveKey(SourceId, questId.ToString(CultureInfo.InvariantCulture)),
            ResolveDestination(questId, markers, ctx),
            new ObjectiveCopy(
                QuestName(questId),
                stepLabel,
                FollowedQuest is null ? "Main Scenario" : "Followed quest",
                ModuleName),
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

    /// <summary>Live current-objective label for any accepted quest, keyed by its raw id. Marker-only
    /// — deliberately NOT the sheet-first read <see cref="Poll"/> uses for the followed quest (see
    /// <see cref="ReadQuestStepTexts"/>), since this runs once per row in a popup that can list every
    /// accepted quest at once and a raw-sheet open per row per frame is not "cheap". Standalone so
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

    /// <summary>The game's own live quest markers — every marker position for this quest, plus
    /// whatever label the FIRST one happens to carry. That label is now the FALLBACK for the step
    /// text (see <see cref="ReadQuestStepTexts"/> and <see cref="QuestStepTextSelection"/>), not
    /// the primary source — a marker very often carries no label at all (the exact readout gap
    /// this file exists to close), while the quest's own ToDo text sheet almost always has
    /// one.</summary>
    private static (string? MarkerLabel, List<MarkerPoint> Markers) ReadQuestMarkers(ushort questId)
    {
        string? markerLabel = null;
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

            if (markerLabel == null)
            {
                var label = mi.Label.ToString();
                if (label.Length > 0)
                {
                    markerLabel = label;
                }
            }

            for (var i = 0; i < (int)mi.MarkerData.LongCount; i++)
            {
                var md = mi.MarkerData[i];

                // md.Radius: the game's own live marker radius, in yalms. Verified against the
                // installed FFXIVClientStructs.dll (MapMarkerData, field offset 0x28, float) — the
                // same field name and type as the static Level sheet's Radius column used to
                // measure SearchAreaRadius.ThresholdYalms, so that threshold applies here unchanged.
                // Dropping this field entirely is the exact defect that sent a player an arrow with
                // a precise-looking distance to the CENTRE of a search-area step instead of telling
                // them it was an area to search — see MarkerPoint.Radius and everything downstream.
                markers.Add(new(md.Position.X, md.Position.Y, md.Position.Z, md.TerritoryTypeId, md.MapId, md.Radius));
            }
        }

        return (markerLabel, markers);
    }

    /// <summary>Every <c>TEXT_&lt;InternalName&gt;_TODO_&lt;nn&gt;</c> row in <paramref
    /// name="raw"/>, keyed by its <c>nn</c> index (the same index as <c>Quest.TodoParams</c>) —
    /// matched by string key rather than a fixed row offset, since nothing guarantees the TODO
    /// block sits at the same offset in every quest's sheet.</summary>
    private static Dictionary<int, (string Text, bool HasPlaceholder)> ParseTodoRows(
        ExcelSheet<RawRow> raw, string internalName)
    {
        var keyPrefix = $"TEXT_{internalName.ToUpperInvariant()}_TODO_";
        var byIndex = new Dictionary<int, (string Text, bool HasPlaceholder)>();
        foreach (var row in raw)
        {
            var key = row.ReadStringColumn(0).ExtractText();
            if (!key.StartsWith(keyPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (!int.TryParse(
                    key.AsSpan(keyPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                continue;
            }

            var seString = row.ReadStringColumn(1);
            byIndex[index] = (seString.ExtractText(), HasUnresolvedPlaceholder(seString));
        }

        return byIndex;
    }

    /// <summary>True when <paramref name="seString"/> carries a macro payload outside
    /// <see cref="PresentationalMacroCodes"/> — a value only the live client can fill in.</summary>
    private static bool HasUnresolvedPlaceholder(ReadOnlySeString seString)
    {
        foreach (var payload in seString)
        {
            if (payload.Type == ReadOnlySePayloadType.Macro && !PresentationalMacroCodes.Contains(payload.MacroCode))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The quest's own ToDo text sheet — <c>TEXT_&lt;InternalName&gt;_TODO_&lt;nn&gt;</c>,
    /// the exact strings the game's own quest tracker prints, one entry per
    /// <c>Quest.TodoParams</c> index and keyed by the same sequence byte
    /// <c>QuestManager.GetQuestSequence</c> returns at runtime — verified live against "Heroes of
    /// the Hour" (quest 67782): sequence 1's entry reads "Speak with Lucia.", the exact text the
    /// game's tracker shows, for a quest whose marker carries no label at all.
    ///
    /// <para>The per-quest sheet lives at <c>quest/&lt;first 3 digits of the internal row
    /// number&gt;/&lt;InternalName&gt;</c> (e.g. quest 67782's internal id <c>HeaVnd105_02246</c>
    /// resolves to <c>quest/022/HeaVnd105_02246</c>) — there is no strongly-typed sheet for it, so
    /// this reads it as <see cref="RawRow"/> and matches rows by their string key rather than by a
    /// fixed row offset, since nothing guarantees the TODO block sits at the same offset in every
    /// quest's sheet.</para>
    ///
    /// <para>Cached forever per quest id in <see cref="stepTextCache"/> — this is static game data,
    /// so unlike the live marker scan it never needs a fresh read.</para></summary>
    private List<QuestStepText> ReadQuestStepTexts(ushort questId)
    {
        if (!stepTextCache.TryGetValue(questId, out var cached))
        {
            cached = ReadQuestStepTextsUncached(questId);
            stepTextCache[questId] = cached;
        }

        return cached;
    }

    private List<QuestStepText> ReadQuestStepTextsUncached(ushort questId)
    {
        var result = new List<QuestStepText>();
        var questSheet = dataManager.GetExcelSheet<Quest>();
        if (questSheet.GetRowOrDefault(questId + QuestRowIdOffset) is not { } q || q.TodoParams.Count == 0)
        {
            return result;
        }

        var internalName = q.Id.ExtractText();
        var raw = OpenQuestTextSheet(internalName);
        if (raw == null)
        {
            return result;
        }

        var byIndex = ParseTodoRows(raw, internalName);
        for (var i = 0; i < q.TodoParams.Count; i++)
        {
            var seq = q.TodoParams[i].ToDoCompleteSeq;
            if (seq == 0 || !byIndex.TryGetValue(i, out var entry) || entry.Text.Length == 0)
            {
                continue;
            }

            result.Add(new QuestStepText(seq, entry.Text, entry.HasPlaceholder));
        }

        return result;
    }

    /// <summary>The per-quest raw text sheet at <c>quest/&lt;first 3 digits of the internal row
    /// number&gt;/&lt;InternalName&gt;</c> (e.g. quest 67782's internal id
    /// <c>HeaVnd105_02246</c> resolves to <c>quest/022/HeaVnd105_02246</c>), or null when
    /// <paramref name="internalName"/> is too short for that split or the sheet does not
    /// exist.</summary>
    private ExcelSheet<RawRow>? OpenQuestTextSheet(string internalName)
    {
        if (internalName.Length < 4)
        {
            return null;
        }

        var numPart = internalName.Split('_')[^1];
        if (numPart.Length < 3)
        {
            return null;
        }

        try
        {
            return dataManager.Excel.GetSheet<RawRow>(name: $"quest/{numPart[..3]}/{internalName}");
        }
        catch (Exception)
        {
            // No text sheet for this quest, or the naming convention doesn't hold for it — the
            // marker-label fallback in QuestStepTextSelection covers this case.
            return null;
        }
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

                    // level.Radius: this branch only runs when the game has no live marker at all,
                    // so this IS the primary read here, not a proxy for one — the same field
                    // SearchAreaRadius.ThresholdYalms was measured against.
                    return new ObjectiveDestination.WorldPoint(
                        level.Territory.RowId, level.Map.RowId, level.X, level.Y, level.Z, Radius: level.Radius);
                }

                break;
            }
        }

        return new ObjectiveDestination.Unresolved(
            "no map location for this step");
    }

    private string QuestName(ushort id) =>
        dataManager.GetExcelSheet<Quest>().GetRowOrDefault(id + QuestRowIdOffset)?.Name.ExtractText()
        ?? $"Quest {id}";
}
