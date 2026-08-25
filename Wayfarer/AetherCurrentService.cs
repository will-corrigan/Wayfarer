using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Navigation;

namespace Wayfarer;

/// <summary>Where a zone's aether currents are and which of them this character has.
///
/// <para><b>Why this is a small class.</b> Aether currents are the easiest thing in the game to be
/// truthful about. Every position is fixed in the sheets, so nothing has to be live-tracked; and
/// attunement is a bit in a local bitfield, so nothing has to be requested from the server and no
/// window has to be opened first. There is no <c>ObservationStore</c> floor here for exactly that
/// reason — a remembered lower bound exists to cover a value that is only readable in some rare
/// context, and this one is readable whenever the player is logged in. Monotonicity is beside the
/// point: nothing is ever cached, so nothing can go stale.</para>
///
/// <para><b>Both live reads go through Dalamud's own <see cref="IUnlockState"/></b> rather than
/// through the client structures underneath it. That is not just taste: the per-current read needs a
/// base subtracted from the row id before it indexes a fixed-width bitfield, and the zone read is a
/// signature-bound game function that throws when a patch moves it. Dalamud carries both, and its
/// versions are the ones that get fixed when the game changes. What it does NOT do is distinguish
/// "not unlocked" from "could not be read" — it answers false for both — so that distinction is
/// recovered here from login state, because a zone reported as entirely unattuned mid-loading-screen
/// would send the player back to currents they already have.</para>
///
/// <para>Framework thread only: it reads Lumina sheets and live unlock state.</para></summary>
internal sealed unsafe class AetherCurrentService(
    IPluginLog log, IDataManager dataManager, IClientState clientState, IUnlockState unlocks)
{
    private const uint QuestRowIdOffset = 65536;

    private Dictionary<uint, AetherCurrentZone>? byTerritory;
    private bool loggedTableFailure;

    /// <summary>The zone's currents, or null when this territory has none — which is most of them:
    /// only 31 territories in the game carry a set.</summary>
    public AetherCurrentZone? ZoneFor(uint territory) =>
        Table().TryGetValue(territory, out var zone) ? zone : null;

    /// <summary>Whether this character has attuned to one current, or null when there was nobody to
    /// ask about. Null is "we cannot tell" and is never flattened to false.</summary>
    public bool? IsAttuned(uint currentRowId)
    {
        if (!clientState.IsLoggedIn
            || dataManager.GetExcelSheet<AetherCurrent>().GetRowOrDefault(currentRowId) is not { } row)
        {
            return null;
        }

        return unlocks.IsAetherCurrentUnlocked(row);
    }

    /// <summary>The client's own verdict on whether a zone is finished, or null when it could not be
    /// asked. Its only job is to cross-check the total we would otherwise print — see
    /// <see cref="AetherCurrentPlan.Tally"/> — so losing it costs a number, never the route.</summary>
    public bool? ZoneComplete(uint compFlgSetId)
    {
        if (!clientState.IsLoggedIn
            || dataManager.GetExcelSheet<AetherCurrentCompFlgSet>().GetRowOrDefault(compFlgSetId)
                is not { } row)
        {
            return null;
        }

        return unlocks.IsAetherCurrentCompFlgSetUnlocked(row);
    }

    /// <summary>How the zone's currents stand, or null when the territory has none. The total inside
    /// is nullable and the decision about it is <see cref="AetherCurrentPlan.Tally"/>'s — this only
    /// supplies the two counts and the game's own verdict for it to cross-check.</summary>
    public AetherCurrentTally? TallyFor(uint territory)
    {
        if (ZoneFor(territory) is not { } zone)
        {
            return null;
        }

        var attuned = 0;
        foreach (var point in zone.Points)
        {
            if (IsAttuned(point.CurrentRowId) is true)
            {
                attuned++;
            }
        }

        return AetherCurrentPlan.Tally(zone.Points.Count, attuned, ZoneComplete(zone.CompFlgSetId));
    }

    /// <summary>The zone's currents this character has not reached yet, in the set row's own order —
    /// the caller orders them for travel. A current whose attunement cannot be read is treated as
    /// NOT remaining: with nothing readable the honest plan is an empty one, not a route through
    /// everything the player may already have.</summary>
    public List<AetherCurrentPoint> RemainingIn(uint territory)
    {
        var remaining = new List<AetherCurrentPoint>();
        if (ZoneFor(territory) is not { } zone)
        {
            return remaining;
        }

        foreach (var point in zone.Points)
        {
            if (IsAttuned(point.CurrentRowId) is false && !IsReached(point))
            {
                remaining.Add(point);
            }
        }

        return remaining;
    }

    /// <summary>Whether there is anything left to do about one current, against live state. The rule
    /// itself is <see cref="AetherCurrentPlan.IsReached"/>'s — this only supplies the three game reads
    /// it needs, so the plan that builds the route and the predicate that advances it can never
    /// disagree about what "done" means.
    ///
    /// <para>The two quest reads go to <c>QuestManager</c> rather than to <see cref="IUnlockState"/>,
    /// which is the exception to this file's own rule and deliberate: the supported service answers
    /// "completed" but has nothing for "accepted", and accepted is the half that matters here — the
    /// route walks to a giver, so taking the quest ends the walk. Splitting one predicate across two
    /// APIs to use the supported one for half of it would make the two halves able to disagree. This
    /// is the same call <c>UnlockRouteSource</c> already makes, for the same reason.</para></summary>
    public bool IsReached(AetherCurrentPoint point)
    {
        var manager = FFXIVClientStructs.FFXIV.Client.Game.QuestManager.Instance();
        var quest = point.QuestRowId;
        var accepted = quest != 0 && manager != null
            && manager->IsQuestAccepted((ushort)(quest - QuestRowIdOffset));
        var complete = quest != 0 && FFXIVClientStructs.FFXIV.Client.Game.QuestManager.IsQuestComplete(quest);

        return AetherCurrentPlan.IsReached(
            point.Kind, IsAttuned(point.CurrentRowId) is true, accepted, complete);
    }

    private static Dictionary<uint, AetherCurrentZone> BuildTable(IDataManager dataManager)
    {
        var currents = dataManager.GetExcelSheet<AetherCurrent>();
        var quests = dataManager.GetExcelSheet<Quest>();
        var givers = dataManager.GetExcelSheet<ENpcResident>();
        var positions = PlacedCurrentPositions(dataManager, currents);

        var table = new Dictionary<uint, AetherCurrentZone>();
        foreach (var set in dataManager.GetExcelSheet<AetherCurrentCompFlgSet>())
        {
            // Two of the 33 rows carry no territory and no currents at all, which is why the client's
            // own completion bitfield is 31 bits wide rather than 33. They are placeholders, not a
            // discrepancy: skipping anything with no territory covers them without naming them.
            if (set.Territory.RowId == 0)
            {
                continue;
            }

            var zoneName =
                set.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            var points = PointsIn(set, zoneName, currents, quests, givers, positions);
            if (points.Count > 0)
            {
                table[set.Territory.RowId] =
                    new AetherCurrentZone(set.RowId, set.Territory.RowId, zoneName, points);
            }
        }

        return table;
    }

    private static List<AetherCurrentPoint> PointsIn(
        AetherCurrentCompFlgSet set,
        string zoneName,
        ExcelSheet<AetherCurrent> currents,
        ExcelSheet<Quest> quests,
        ExcelSheet<ENpcResident> givers,
        Dictionary<uint, Level> positions)
    {
        // The AetherCurrents column is a fixed 15 wide on EVERY row and is SPARSE — Coerthas Western
        // Highlands fills nine of the fifteen slots, with gaps in the middle. Empty slots are skipped
        // and duplicates guarded against, so the resulting count is the zone's real requirement.
        var points = new List<AetherCurrentPoint>();
        var seen = new HashSet<uint>();
        foreach (var reference in set.AetherCurrents)
        {
            if (reference.RowId == 0 || !seen.Add(reference.RowId)
                || currents.GetRowOrDefault(reference.RowId) is not { } current)
            {
                continue;
            }

            points.Add(current.Quest.RowId != 0
                ? QuestPoint(set, zoneName, reference.RowId, current.Quest.RowId, quests, givers)
                : PlacedPoint(set, zoneName, reference.RowId, positions));
        }

        return points;
    }

    /// <summary>A current earned from a quest, routed to the quest's ISSUER LOCATION — a
    /// <c>Level</c> row on the <c>Quest</c> sheet, the same field the unlock catalogue already walks
    /// to. Nine of the game's quest currents are handed out in a neighbouring city rather than in the
    /// zone they unlock, so the giver's territory is kept rather than the set's.</summary>
    private static AetherCurrentPoint QuestPoint(
        AetherCurrentCompFlgSet set,
        string zoneName,
        uint currentRowId,
        uint questRowId,
        ExcelSheet<Quest> quests,
        ExcelSheet<ENpcResident> givers)
    {
        var quest = quests.GetRowOrDefault(questRowId);
        var location = quest?.IssuerLocation.ValueNullable;
        var giverName = quest is { IssuerStart.RowId: not 0 }
            ? givers.GetRowOrDefault(quest.Value.IssuerStart.RowId)?.Singular.ExtractText()
            : null;

        return new AetherCurrentPoint(
            currentRowId,
            AetherCurrentKind.Quest,
            set.RowId,
            zoneName,
            location?.Territory.RowId ?? 0,
            location?.Map.RowId ?? 0,
            location?.X ?? 0f,
            location?.Y ?? 0f,
            location?.Z ?? 0f,
            questRowId,
            quest?.Name.ExtractText(),
            giverName is { Length: > 0 } ? giverName : null);
    }

    private static AetherCurrentPoint PlacedPoint(
        AetherCurrentCompFlgSet set,
        string zoneName,
        uint currentRowId,
        Dictionary<uint, Level> positions)
    {
        var level = positions.TryGetValue(currentRowId, out var found) ? found : default;
        return new AetherCurrentPoint(
            currentRowId,
            AetherCurrentKind.Attunable,
            set.RowId,
            zoneName,
            level.Territory.RowId,
            level.Map.RowId,
            level.X,
            level.Y,
            level.Z);
    }

    /// <summary>Where every placed aether current is, by <c>AetherCurrent</c> row id.
    ///
    /// <para>The join is <c>EObj.Data</c> → <c>AetherCurrent</c>, then <c>Level.Object</c> →
    /// <c>EObj</c>: an <c>EObj</c> row whose untyped <c>Data</c> reference lands inside the
    /// <c>AetherCurrent</c> sheet IS an aether current object, and the <c>Level</c> row pointing back
    /// at it carries the coordinates, the territory and the map. Both sheets have to be walked once
    /// because neither indexes the other in the direction needed.</para>
    ///
    /// <para>Matching on <c>Data</c> rather than on the object's NAME is deliberate. Labyrinthos
    /// holds an object called an "artificial wind-aspected aether current" which is a quest prop —
    /// its <c>Data</c> points into the <c>Quest</c> sheet, not this one — and a name match would put a
    /// stop on the route that can never be attuned.</para></summary>
    private static Dictionary<uint, Level> PlacedCurrentPositions(
        IDataManager dataManager, ExcelSheet<AetherCurrent> currents)
    {
        var objectToCurrent = new Dictionary<uint, uint>();
        foreach (var obj in dataManager.GetExcelSheet<EObj>())
        {
            if (obj.Data.RowId != 0 && currents.HasRow(obj.Data.RowId))
            {
                objectToCurrent[obj.RowId] = obj.Data.RowId;
            }
        }

        var positions = new Dictionary<uint, Level>();
        foreach (var level in dataManager.GetExcelSheet<Level>())
        {
            if (level.Object.RowId != 0
                && objectToCurrent.TryGetValue(level.Object.RowId, out var currentRowId)
                && level.Territory.RowId != 0)
            {
                // First placement wins. Every current has exactly one today; a second would be a
                // duplicate of the same object rather than a different current.
                positions.TryAdd(currentRowId, level);
            }
        }

        return positions;
    }

    /// <summary>Every zone's currents, built once from the sheets and never rebuilt: none of it
    /// changes while the game is running.
    ///
    /// <para>A failure here disables the feature and says so rather than throwing into whatever
    /// asked. An empty table simply means no territory has currents, which every caller already
    /// handles.</para>
    ///
    /// <para>Last in the file only because it is the one private member that has to be an instance
    /// method — it owns the cache — and the analyzer wants the statics above it.</para></summary>
    private Dictionary<uint, AetherCurrentZone> Table()
    {
        if (byTerritory is { } built)
        {
            return built;
        }

        try
        {
            byTerritory = BuildTable(dataManager);
        }
        catch (Exception ex)
        {
            if (!loggedTableFailure)
            {
                loggedTableFailure = true;
                log.Error(ex, "Wayfarer: the aether-current tables could not be read, so that feature is off.");
            }

            byTerritory = [];
        }

        return byTerritory;
    }
}
