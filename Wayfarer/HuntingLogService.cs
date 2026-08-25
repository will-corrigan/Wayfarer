using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Hunting;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer;

/// <summary>Loads the curated hunting-log dataset, reads live progress from
/// <c>MonsterNoteManager</c> per <c>HuntingSlotTable</c>'s job→slot mapping and
/// <c>HuntingProgress</c>'s page semantics — both consumed here, neither rebuilt — and resolves the
/// current page's remaining targets to world positions/live mob tracking for
/// <see cref="Modules.HuntingLogModule"/>. Framework thread only except where noted. Owned by
/// <see cref="Modules.HuntingLogModule"/>, which subscribes <see cref="OnFrameworkUpdate"/> in
/// <c>Enable()</c> and unsubscribes it in <c>Disable()</c> — mirrors <see cref="UnlockService"/>'s
/// ownership split.</summary>
internal sealed unsafe class HuntingLogService
{
    private readonly IPluginLog log;
    private readonly IObjectTable objects;
    private readonly IClientState clientState;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IDataManager dataManager;

    /// <summary>(currentClassJobId, territory, live-signature) as of the last tick that triggered
    /// a full <see cref="Recompute"/> — see <see cref="ReadLiveSignature"/> for what the signature
    /// folds in. Null means "never triggered", forcing a recompute on the first tick a player
    /// exists.</summary>
    private (uint ClassJobId, uint Territory, int Signature)? lastChecked;

    // A recompute runs on every kill, job swap and zone change, so a repeatable fault here would
    // write a line for each. The first one carries the whole story; the rest are noise.
    private bool recomputeFailureLogged;

    private Dictionary<uint, (string Name, uint ContentFinderConditionId)>? dutyByTerritory;

    /// <summary>BNpcName row id to the Hunting Log's own creature icon, built once on first use.
    /// Lazy rather than eager because the sheet is 850 rows and a player who never opens the log
    /// should not pay for it at load.</summary>
    private Dictionary<uint, uint>? monsterIcons;

    private HuntingDataset? dataset;

    /// <summary>Live kill count reader and the page it belongs to, as of the last
    /// <see cref="RecomputeCore"/>. Kept so <see cref="KilledFor"/> can answer for any monster on
    /// demand — the guidance source asks per tick, and the answer must not require a full
    /// recompute. Safe to hold: every kill changes the live signature, so a recompute has already
    /// run by the time a count matters.</summary>
    private Func<int, int, int>? killedCount;
    private HuntingRank? currentPageRank;

    public HuntingLogService(
        IPluginLog log,
        IObjectTable objects,
        IClientState clientState,
        IDalamudPluginInterface pluginInterface,
        IDataManager dataManager)
    {
        this.log = log;
        this.objects = objects;
        this.clientState = clientState;
        this.pluginInterface = pluginInterface;
        this.dataManager = dataManager;
        try
        {
            Load();
            Loaded = true;

            // Same reasoning as the unlock catalogue: one count, once, so a "my hunting log is
            // empty" report arrives with the answer already in it.
            log.Information($"Wayfarer hunting: dataset loaded, {dataset?.Logs.Count ?? 0} logs.");
        }
        catch (Exception ex)
        {
            const string message =
                "Wayfarer hunting: the hunting dataset could not be read, so hunting mode is off for this "
                + "session — the readout and the window's Hunting tab will show nothing.";
            log.Error(ex, message);
        }
    }

    public bool Loaded { get; private set; }

    /// <summary>Display label for whichever log is active right now ("Gladiator", "Maelstrom
    /// Elite", ...), or <see langword="null"/> when nothing is active — see
    /// <see cref="NoLogReason"/> for why.</summary>
    public string? ActiveLogLabel { get; private set; }

    /// <summary>Explains why nothing is active: no dataset, a post-Stormblood job with no class
    /// log and no Grand Company joined, an Elite log not yet unlocked, or the active log fully
    /// completed. Null exactly when <see cref="ActiveLogLabel"/> is set.</summary>
    public string? NoLogReason { get; private set; }

    public int? CurrentRank { get; private set; }

    /// <summary>Remaining (not-yet-fully-killed) monsters on the current page, dataset positional
    /// order — see <see cref="HuntingProgress.RemainingForCurrentPage"/>.</summary>
    public IReadOnlyList<HuntingMonster> RemainingOnPage { get; private set; } = [];

    /// <summary>The guidance target: nearest remaining monster in the player's current zone if one
    /// exists there (chained via <see cref="HuntingChaining"/>), else the dataset-first remaining
    /// monster elsewhere, else a duty-gated (non-routable) remaining monster's duty affordance, or
    /// <see langword="null"/> when nothing remains. Refreshed with a live <c>IObjectTable</c>
    /// position every tick while the player stands in its territory (see
    /// <see cref="RefreshLiveTracking"/>) — everything else about it only changes on
    /// <see cref="Recompute"/>.</summary>
    public HuntingTargetView? CurrentTarget { get; private set; }

    /// <summary>Every remaining, routable target in the player's current zone, nearest-first —
    /// the "hunt here" route-chaining order. Empty whenever <see cref="CurrentTarget"/>
    /// is null, is a duty affordance, or is in a different zone.</summary>
    public IReadOnlyList<HuntingTargetView> HuntHereOrder { get; private set; } = [];

    /// <summary>Every remaining target on the current page resolved to a view, in dataset order —
    /// including the ones outside the player's zone and the duty-gated ones, both of which
    /// <see cref="HuntHereOrder"/> drops. This is what a hunting plan is built from: a chain that
    /// only ever contained this zone's targets would stop the moment you cleared it.</summary>
    public IReadOnlyList<HuntingTargetView> RemainingTargets { get; private set; } = [];

    /// <summary>Transitional bridge to the old pickup shape, kept only so presentations that still
    /// speak in pickups keep working. <c>QuestRowId</c> is meaningless here — which is exactly why
    /// <see cref="HuntingTarget"/> carries the real selection: nothing may infer a hunting target's
    /// completion from a quest row again.</summary>
    public PickupTarget? ToPickupTarget(HuntingTargetView v) =>
        v.IsRoutable
            ? new PickupTarget(v.MonsterName, ActiveLogLabel ?? "Hunting log", QuestRowId: 0, v.TerritoryTypeId, v.MapId, v.WorldX, v.WorldY, v.WorldZ, v.MonsterName)
            { HuntingTarget = v }
            : null;

    /// <summary>Live kill count for a monster on the current page, 0 when it cannot be read. The
    /// hunting guidance source's completion signal, and the only one it has.</summary>
    public int KilledFor(HuntingMonster monster) =>
        killedCount is { } killed && currentPageRank is { } rank && TaskIndexOf(rank, monster) is var task and >= 0
            ? killed(task, monster.MonsterIndex)
            : 0;

    /// <summary>Whether this monster still belongs to the page the game is tracking. False after a
    /// rank-up, which is what lets a plan built on the old page finish instead of stalling on a
    /// target whose kill count no longer exists.</summary>
    public bool IsTracked(HuntingMonster monster) =>
        currentPageRank is { } rank && TaskIndexOf(rank, monster) >= 0;

    /// <summary>The freshest view of a target: current kill count, plus a live object-table position
    /// when the player is standing in its zone. Framework thread only.</summary>
    public HuntingTargetView LiveView(HuntingTargetView view) =>
        WithLivePosition(view with { Killed = KilledFor(view.Monster) }, clientState.TerritoryType);

    /// <summary>Lightweight per-tick change detector (mirrors <see cref="UnlockService.OnFrameworkUpdate"/>):
    /// cheap current-job/territory/live-signature reads, only running the full <see cref="Recompute"/>
    /// pass when one of them actually changed. Also refreshes the live in-zone tracking position
    /// every tick regardless (cheap: a single-NameId
    /// <c>IObjectTable</c> filter, not gated the way the heavier recompute is).</summary>
    public void OnFrameworkUpdate(IFramework framework)
    {
        if (!Loaded || !clientState.IsLoggedIn)
        {
            return;
        }

        var ps = PlayerState.Instance();
        var classJobId = ps != null ? ps->CurrentClassJobId : 0u;
        if (classJobId == 0)
        {
            return;
        }

        var territory = clientState.TerritoryType;
        var signature = ReadLiveSignature(classJobId, ps);
        var current = (classJobId, territory, signature);
        if (lastChecked is not { } last || last != current)
        {
            lastChecked = current;
            RecomputeSafe(classJobId, territory);
        }

        RefreshLiveTracking(territory);
    }

    /// <summary>Full progress pass: resolves the active log/slot, live rank, remaining targets on
    /// the current page, and the guidance target. Framework thread only.</summary>
    public void Recompute()
    {
        var ps = PlayerState.Instance();
        RecomputeSafe(ps != null ? ps->CurrentClassJobId : 0u, clientState.TerritoryType);
    }

    /// <summary>Cheap per-tick fold of the live rank + every task's kill-count bytes for
    /// <paramref name="classJobId"/>'s slot — used only as a change-detector signature (see
    /// <see cref="OnFrameworkUpdate"/>), never to derive actual progress (that's
    /// <see cref="RecomputeCore"/>'s job). Returns 0 when the job has no class log and no Grand
    /// Company is joined (nothing to detect changes on).</summary>
    private static int ReadLiveSignature(uint classJobId, PlayerState* ps)
    {
        var slot = HuntingSlotTable.SlotForClassJob(classJobId);
        if (slot is null)
        {
            // Range-checked rather than fed to EliteSlotForGrandCompany (which throws on
            // out-of-range): this runs on every framework tick outside any try/catch, so a
            // corrupt GC byte must degrade to "no signal", not throw every frame.
            var gc = ps != null ? ps->GrandCompany : (byte)0;
            if (gc is 0 or > 3)
            {
                return 0;
            }

            slot = HuntingSlotTable.EliteSlotForGrandCompany(gc);
        }

        var mgr = MonsterNoteManager.Instance();
        if (mgr == null)
        {
            return 0;
        }

        ref var rankInfo = ref mgr->RankData[slot.Value];
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + rankInfo.Rank;
            var tasks = rankInfo.RankData;
            for (var i = 0; i < tasks.Length; i++)
            {
                var counts = tasks[i].Counts;
                for (var j = 0; j < counts.Length; j++)
                {
                    hash = (hash * 31) + counts[j];
                }
            }

            return hash;
        }
    }

    /// <summary>Copies the current page's live per-task kill-count bytes into plain managed arrays
    /// so <see cref="RecomputeCore"/>'s <c>Killed</c> delegate doesn't need to capture the native
    /// <c>Span</c> (ref structs can't be captured by a delegate).</summary>
    private static byte[][] ReadTaskCounts(MonsterNoteRankInfo rankInfo)
    {
        var tasks = rankInfo.RankData;
        var result = new byte[tasks.Length][];
        for (var i = 0; i < tasks.Length; i++)
        {
            result[i] = tasks[i].Counts.ToArray();
        }

        return result;
    }

    private static int TaskIndexOf(HuntingRank pageRank, HuntingMonster monster)
    {
        foreach (var task in pageRank.Tasks)
        {
            if (task.Monsters.Contains(monster))
            {
                return task.TaskIndex;
            }
        }

        return -1;
    }

    private void RecomputeSafe(uint classJobId, uint territory)
    {
        try
        {
            RecomputeCore(classJobId, territory);
        }
        catch (Exception ex)
        {
            if (!recomputeFailureLogged)
            {
                recomputeFailureLogged = true;
                const string message =
                    "Wayfarer hunting: refreshing hunting progress failed, so the remaining-target list and "
                    + "any running hunt will be stale until it recomputes successfully. Reported once.";
                log.Error(ex, message);
            }
        }
    }

    private void RecomputeCore(uint classJobId, uint territory)
    {
        if (!Loaded || dataset is not { } ds || classJobId == 0)
        {
            return;
        }

        if (ResolveActiveLog(classJobId, ds) is not { } active)
        {
            return;
        }

        var (huntingLog, slot, isElite, grandCompanyId) = active;

        var mgr = MonsterNoteManager.Instance();
        if (mgr == null)
        {
            return;
        }

        ref var rankInfo = ref mgr->RankData[slot];

        // The memory Rank is 0-based; the dataset (and PageState) are 1-based. This is the one
        // read boundary where the conversion happens — see HuntingProgress.CurrentRankFromMemory.
        // No separate elite-unlock gate is needed here: joining a Grand Company unlocks its
        // hunting log's first page outright (no unlock quest; higher pages gate on GC-rank
        // promotions which the game folds into the memory Rank itself), so membership — already
        // checked in ResolveActiveLog — IS the unlock signal. The previous "Rank <= 0 means
        // locked" check misread the 0-based field: Rank 0 is a freshly unlocked log at page 1.
        var liveRank = HuntingProgress.CurrentRankFromMemory(rankInfo.Rank);

        ActiveLogLabel = isElite ? $"{GrandCompanyName(grandCompanyId)} Elite" : ClassJobName(classJobId);
        CurrentRank = liveRank;

        var pageRank = huntingLog.Ranks.Find(r => HuntingProgress.PageState(r.Rank, liveRank) == HuntingPageState.Current);
        currentPageRank = pageRank;
        if (pageRank is null)
        {
            NoLogReason = null; // still "active", just nothing left to show
            RemainingOnPage = [];
            HuntHereOrder = [];
            RemainingTargets = [];
            CurrentTarget = null;
            return;
        }

        var counts = ReadTaskCounts(rankInfo);
        int Killed(int taskIndex, int monsterIndex) =>
            taskIndex >= 0 && taskIndex < counts.Length && monsterIndex >= 0 && monsterIndex < counts[taskIndex].Length
                ? counts[taskIndex][monsterIndex]
                : 0;

        killedCount = Killed;
        var remaining = HuntingProgress.RemainingForCurrentPage(pageRank, Killed);
        RemainingOnPage = remaining;
        NoLogReason = null;

        BuildTargets(remaining, territory, Killed, pageRank);
    }

    /// <summary>Resolves which log is active for <paramref name="classJobId"/>: the job's own
    /// class log, or — for a post-Stormblood job with none — one of the shared Grand Company Elite
    /// logs, gated on Grand Company membership. Membership is
    /// the complete unlock signal for the Elite logs: enlisting grants the log's first page with
    /// no separate unlock quest, and later pages gate on GC-rank promotions the game reflects in
    /// the memory Rank itself. Calls <see cref="SetNoLog"/> and returns null on any failure.</summary>
    private (HuntingLog Log, int Slot, bool IsElite, uint GrandCompanyId)? ResolveActiveLog(uint classJobId, HuntingDataset ds)
    {
        var slot = HuntingSlotTable.SlotForClassJob(classJobId);
        var isElite = false;
        uint grandCompanyId = 0;
        string jobKey;
        if (slot is null)
        {
            var ps = PlayerState.Instance();
            grandCompanyId = ps != null ? ps->GrandCompany : (byte)0;
            if (grandCompanyId == 0)
            {
                SetNoLog("No hunting log for this job, and no Grand Company yet.");
                return null;
            }

            slot = HuntingSlotTable.EliteSlotForGrandCompany(grandCompanyId);
            isElite = true;
            jobKey = (10000 + grandCompanyId).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            // Evolved jobs (PLD/WAR/NIN/...) share their base class's dataset row — the base-class
            // mapping is HuntingSlotTable's, reused here rather than re-derived, so the dataset key
            // always matches the class log HuntingSlotTable already resolved the slot for.
            jobKey = HuntingSlotTable.BaseClassFor(classJobId).ToString(CultureInfo.InvariantCulture);
        }

        if (!ds.Logs.TryGetValue(jobKey, out var huntingLog))
        {
            SetNoLog("No hunting log data for this job.");
            return null;
        }

        return (huntingLog, slot.Value, isElite, grandCompanyId);
    }

    private void SetNoLog(string reason)
    {
        ActiveLogLabel = null;
        NoLogReason = reason;
        CurrentRank = null;

        // Nothing is tracked any more (a job change to a job with no log, a missing dataset row):
        // clearing the page is what lets an active hunting plan finish instead of stalling on
        // targets whose kill counts no longer mean anything.
        currentPageRank = null;
        killedCount = null;
        RemainingOnPage = [];
        HuntHereOrder = [];
        RemainingTargets = [];
        CurrentTarget = null;
    }

    private void BuildTargets(List<HuntingMonster> remaining, uint territory, Func<int, int, int> killedCount, HuntingRank pageRank)
    {
        var mapSheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>();
        var chainTargets = new List<HuntingChainTarget>();
        var byMonster = new Dictionary<HuntingMonster, (uint TerritoryTypeId, uint MapId, float X, float Y, float Z)>();
        var allRemaining = new List<HuntingTargetView>();
        HuntingMonster? dutyTarget = null;

        foreach (var monster in remaining)
        {
            var loc = monster.Locations.Find(l => l.IsPrimary) ?? (monster.Locations.Count > 0 ? monster.Locations[0] : null);
            if (loc is null)
            {
                continue;
            }

            if (!loc.Routable)
            {
                dutyTarget ??= monster;

                // Duty-gated targets stay in the plan as duty objectives rather than being dropped
                // — the 25 Grand-Company-Elite targets were unreachable while a coordinate was the
                // only thing a selection could carry.
                allRemaining.Add(BuildDutyView(monster, killedCount, pageRank));
                continue;
            }

            if (mapSheet.GetRowOrDefault(loc.MapId) is not { } mapRow)
            {
                continue;
            }

            var (wx, wz) = MapCoords.MapToWorld(loc.X, loc.Y, mapRow.SizeFactor, mapRow.OffsetX, mapRow.OffsetY);
            var wy = objects.LocalPlayer?.Position.Y ?? 0f; // map coords carry no vertical axis — see MapCoords.MapToWorld
            byMonster[monster] = (loc.TerritoryTypeId, loc.MapId, wx, wy, wz);
            chainTargets.Add(new HuntingChainTarget(monster, loc.TerritoryTypeId, wx, wz));
            allRemaining.Add(ToView(monster, byMonster[monster], killedCount, pageRank));
        }

        RemainingTargets = allRemaining;
        SelectCurrentTarget(chainTargets, byMonster, dutyTarget, territory, killedCount, pageRank);
    }

    /// <summary>The service's own "what would you show if nobody chose anything" pick, kept for the
    /// glanceable widget line and the hunting windows: nearest remaining target in this zone, else
    /// the first elsewhere, else a duty-gated one. Guidance no longer depends on it — a chosen
    /// target is owned by the hunting guidance source, which is why selecting one now survives.</summary>
    private void SelectCurrentTarget(
        List<HuntingChainTarget> chainTargets,
        Dictionary<HuntingMonster, (uint TerritoryTypeId, uint MapId, float X, float Y, float Z)> byMonster,
        HuntingMonster? dutyTarget,
        uint territory,
        Func<int, int, int> killedCount,
        HuntingRank pageRank)
    {
        var player = objects.LocalPlayer;
        var ordered = HuntingChaining.OrderNearestFirst(chainTargets, territory, player?.Position.X ?? 0f, player?.Position.Z ?? 0f);

        HuntHereOrder = [.. ordered.Select(t => ToView(t.Monster, byMonster[t.Monster], killedCount, pageRank))];

        if (HuntHereOrder.Count > 0)
        {
            CurrentTarget = HuntHereOrder[0];
            return;
        }

        // Nothing in the current zone: fall back to the dataset-first routable target elsewhere,
        // then finally a duty-gated one.
        var elsewhere = chainTargets.Find(t => byMonster.ContainsKey(t.Monster));
        if (elsewhere is not null)
        {
            CurrentTarget = ToView(elsewhere.Monster, byMonster[elsewhere.Monster], killedCount, pageRank);
            return;
        }

        CurrentTarget = dutyTarget is { } duty ? BuildDutyView(duty, killedCount, pageRank) : null;
    }

    private HuntingTargetView ToView(
        HuntingMonster monster,
        (uint TerritoryTypeId, uint MapId, float X, float Y, float Z) loc,
        Func<int, int, int> killedCount,
        HuntingRank pageRank)
    {
        var taskIndex = TaskIndexOf(pageRank, monster);
        var killed = killedCount(taskIndex, monster.MonsterIndex);
        return new HuntingTargetView(
            Monster: monster,
            MonsterName: MonsterName(monster.BNpcNameId),
            Killed: killed,
            Required: monster.RequiredKills,
            TerritoryTypeId: loc.TerritoryTypeId,
            MapId: loc.MapId,
            WorldX: loc.X,
            WorldY: loc.Y,
            WorldZ: loc.Z,
            IsLivePosition: false,
            DutyName: null,
            DutyContentFinderConditionId: null,
            ZoneName: ZoneName(loc.TerritoryTypeId),
            IconId: IconFor(monster.BNpcNameId));
    }

    private HuntingTargetView BuildDutyView(HuntingMonster monster, Func<int, int, int> killedCount, HuntingRank pageRank)
    {
        var taskIndex = TaskIndexOf(pageRank, monster);
        var killed = killedCount(taskIndex, monster.MonsterIndex);
        var loc = monster.Locations.Find(l => !l.Routable);
        var duty = loc?.DutyTerritoryTypeId is { } dt ? DutyForTerritory(dt) : null;
        return new HuntingTargetView(
            Monster: monster,
            MonsterName: MonsterName(monster.BNpcNameId),
            Killed: killed,
            Required: monster.RequiredKills,
            TerritoryTypeId: 0,
            MapId: 0,
            WorldX: 0,
            WorldY: 0,
            WorldZ: 0,
            IsLivePosition: false,
            DutyName: duty?.Name ?? "an instanced Grand Company duty",
            DutyContentFinderConditionId: duty?.ContentFinderConditionId,
            ZoneName: loc?.DutyTerritoryTypeId is { } dutyTerritory ? ZoneName(dutyTerritory) : null,
            IconId: IconFor(monster.BNpcNameId));
    }

    /// <summary>Refreshes <see cref="CurrentTarget"/>'s position from a live <c>IObjectTable</c>
    /// scan when the player stands in its territory: nearest
    /// alive, targetable <c>IBattleNpc</c> whose <c>NameId</c> (the BNpcName row id, on
    /// <c>ICharacter</c>) matches the target's <c>BNpcNameId</c> replaces the curated coordinate;
    /// falls back to the curated coordinate (clearing
    /// <see cref="HuntingTargetView.IsLivePosition"/>) when none is visible. NOT
    /// <c>IGameObject.BaseId</c>/<c>DataId</c> — for a battle NPC that is the BNpcBase row id, a
    /// different id space (see <see cref="HuntingLiveTracking"/>). A single-id filter over the
    /// object table is cheap enough to run every tick unconditionally — no separate throttle
    /// needed (mirrors the "no Stopwatch/frame-counter throttling" idiom already used elsewhere in
    /// this codebase).</summary>
    private void RefreshLiveTracking(uint territory)
    {
        if (CurrentTarget is { } target)
        {
            CurrentTarget = WithLivePosition(target, territory);
        }
    }

    /// <summary>The live scan itself, callable for any target rather than only
    /// <see cref="CurrentTarget"/> — the hunting guidance source needs it for whichever leg it is
    /// on. Returns <paramref name="target"/> unchanged when it has no world position or the player
    /// is not in its zone.</summary>
    private HuntingTargetView WithLivePosition(HuntingTargetView target, uint territory)
    {
        if (!target.IsRoutable || target.TerritoryTypeId != territory)
        {
            return target;
        }

        var player = objects.LocalPlayer;
        Vector3? nearest = null;
        var bestSq = float.MaxValue;
        foreach (var obj in objects)
        {
            if (obj is not IBattleNpc bnpc
                || !HuntingLiveTracking.IsCandidate(bnpc.NameId, target.Monster.BNpcNameId, bnpc.IsDead, bnpc.IsTargetable))
            {
                continue;
            }

            var dx = obj.Position.X - (player?.Position.X ?? obj.Position.X);
            var dz = obj.Position.Z - (player?.Position.Z ?? obj.Position.Z);
            var sq = (dx * dx) + (dz * dz);
            if (sq < bestSq)
            {
                bestSq = sq;
                nearest = obj.Position;
            }
        }

        return nearest is { } n
            ? target with { WorldX = n.X, WorldY = n.Y, WorldZ = n.Z, IsLivePosition = true }
            : target with { IsLivePosition = false };
    }

    // Title-cased here rather than at each surface, because this is the one place a monster name
    // enters the plugin: the hub rows, the readout's hunting line, the nameplate marker match and
    // the info-bar entry all read the name this method resolved. The sheet stores "dragonfly"; the
    // game's own Hunting Log shows "Dragonfly" — see DisplayNames.

    /// <summary>The Hunting Log's own creature art, keyed the way the dataset is keyed.
    ///
    /// <para><c>MonsterNoteTarget</c> is the sheet the vanilla Hunting Log draws its pictures from,
    /// and it exists for no other purpose: every one of the 362 rows a <c>MonsterNote</c> actually
    /// references carries a non-zero icon, in the 63xxx block, at 48x48 with a 96x96 high-resolution
    /// variant beside it. So "can we show the images like the actual hunting log?" needs no fallback
    /// design — there is no entry without art. One is shipped anyway, because a future patch can add
    /// a row before it adds a picture.</para>
    ///
    /// <para><b>The honest caveat.</b> These are creature-<i>family</i> icons: about a hundred
    /// distinct pictures cover all 362 entries, and a grounded pirate and a grounded raider share
    /// one. That is not a limitation being imposed here — it is what the vanilla log shows, because
    /// it is the only art the sheet has. Two rows on the same rank can legitimately carry the same
    /// picture, and inventing per-monster art to "fix" that would be inventing something the game
    /// does not have.</para></summary>
    private uint IconFor(uint bNpcNameId)
    {
        monsterIcons ??= BuildMonsterIcons();
        return monsterIcons.GetValueOrDefault(bNpcNameId, 0u);
    }

    private Dictionary<uint, uint> BuildMonsterIcons()
    {
        var icons = new Dictionary<uint, uint>();
        try
        {
            foreach (var row in dataManager.GetExcelSheet<MonsterNoteTarget>())
            {
                var nameId = row.BNpcName.RowId;
                if (nameId != 0 && row.Icon != 0)
                {
                    // First win: the sheet can name the same creature twice and the pictures agree,
                    // so re-reading a later row would cost a write for no change.
                    icons.TryAdd(nameId, (uint)row.Icon);
                }
            }
        }
        catch (Exception ex)
        {
            // Built once, so one line. The log still works without pictures; it looks like it did
            // before, which is a worse log rather than a broken one.
            log.Warning(ex, "Wayfarer hunting: the monster art sheet could not be read, so the log's rows will have no pictures.");
        }

        return icons;
    }

    /// <summary>The target's territory as the game names it. Null rather than a placeholder when
    /// the row does not resolve: the row's second line simply carries less, which is better than
    /// carrying "Territory 148".</summary>
    private string? ZoneName(uint territoryTypeId) =>
        territoryTypeId == 0
            ? null
            : dataManager.GetExcelSheet<TerritoryType>()
                         .GetRowOrDefault(territoryTypeId)?.PlaceName.ValueNullable?.Name.ExtractText()
              is { Length: > 0 } name
                ? name
                : null;

    private string MonsterName(uint bNpcNameId) =>
        dataManager.GetExcelSheet<BNpcName>().GetRowOrDefault(bNpcNameId)?.Singular.ExtractText() is { Length: > 0 } n
            ? DisplayNames.TitleCase(n)
            : $"Monster {bNpcNameId}";

    // Title-cased for the same reason MonsterName above is, and it is the same defect: the ClassJob
    // sheet stores "warrior" and the game title-cases it at draw time, so the readout's heading read
    // "Hunting Log tt warrior" while every game window beside it said "Warrior". This is the one
    // place the log's name enters the plugin — the readout heading, the hub's Hunting Log tab and
    // the ImGui window all read what this returns.
    private string ClassJobName(uint classJobId) =>
        dataManager.GetExcelSheet<ClassJob>().GetRowOrDefault(classJobId)?.Name.ExtractText() is { Length: > 0 } n
            ? DisplayNames.TitleCase(n)
            : $"Job {classJobId}";

    private string GrandCompanyName(uint grandCompanyId) =>
        dataManager.GetExcelSheet<GrandCompany>().GetRowOrDefault(grandCompanyId)?.Name.ExtractText() is { Length: > 0 } n
            ? DisplayNames.TitleCase(n)
            : $"Grand Company {grandCompanyId}";

    /// <summary>Territory → duty name/CFC id, built once from the <c>InstanceContent</c> sheet
    /// (not <c>ContentFinderCondition</c>, which has no typed route back to the territory).
    ///
    /// <para>This is a second copy of <see cref="Guidance.GuidanceRouter"/>'s own
    /// <c>DutyForTerritory</c>, differing only in that this one drops the InstanceContent row id
    /// the router needs. The two have no owner in common, which is the only reason they are not
    /// one method — worth folding together the next time either is touched.</para></summary>
    private (string Name, uint ContentFinderConditionId)? DutyForTerritory(uint territoryId)
    {
        if (dutyByTerritory == null)
        {
            var map = new Dictionary<uint, (string, uint)>();
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

                map[cfc.TerritoryType.RowId] = (name, cfc.RowId);
            }

            dutyByTerritory = map;
        }

        return dutyByTerritory.TryGetValue(territoryId, out var duty) ? duty : null;
    }

    private void Load()
    {
        var dir = pluginInterface.AssemblyLocation.DirectoryName
            ?? throw new InvalidOperationException("no assembly directory");
        var json = File.ReadAllText(Path.Combine(dir, "hunting-targets.json"));
        dataset = HuntingDataset.Parse(json);
    }
}
