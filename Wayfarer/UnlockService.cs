using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Unlocks;

namespace Wayfarer;

/// <summary>Loads the wiki unlocks dataset, matches it against the Quest sheet,
/// and computes availability statuses on demand (never per-frame). Framework
/// thread only except where noted. Owned by <see cref="Modules.UnlockChecklistModule"/>,
/// which subscribes <see cref="OnFrameworkUpdate"/> and <see cref="OnPickupAdvanced"/> in
/// <c>Enable()</c> and unsubscribes them in <c>Disable()</c>.</summary>
internal sealed unsafe class UnlockService : IUnlockProvider
{
    private const uint QuestRowIdOffset = 65536;

    /// <summary>How many entries <see cref="GlanceableHere"/> caps at. The readout enforces its own
    /// line budget on top of this (<c>ReadoutComposer.MaxNearbyUnlockLines</c>).</summary>
    private const int GlanceableMax = 3;

    private readonly IPluginLog log;
    private readonly IObjectTable objects;
    private readonly IClientState clientState;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IDataManager dataManager;
    private readonly List<ResolvedUnlock> entries = [];

    /// <summary>(level, territory) as of the last time a recompute was triggered from the
    /// framework-update change detector. Null means "never triggered" — including right after
    /// construction/hot-reload, so the very first tick with a player present always recomputes.</summary>
    private (int Level, uint Territory)? lastChecked;

    // A recompute runs on every zone change, level-up and pickup, so a repeatable fault here would
    // write a line for each. The first one carries the whole story; the rest are noise.
    private bool recomputeFailureLogged;

    // Same reasoning as recomputeFailureLogged, for the same-shaped failure in ResolveGameText: a
    // bad GameTextRef would otherwise spam the log every recompute rather than once.
    private bool gameTextResolveFailureLogged;

    public UnlockService(
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

            // A count, once, at load. It is the first thing worth knowing from a pasted log: an
            // entry that "should be there" and a catalogue that is half the expected size are the
            // same report, and this line tells them apart without any further questions.
            log.Information($"Wayfarer unlocks: catalogue loaded, {entries.Count} entries.");
        }
        catch (Exception ex)
        {
            // Kept, not just logged. The checklist is the whole of this feature, and an empty
            // checklist reads as "you have done everything" — the same lie in a different shape.
            // Every surface that would have shown entries shows this instead.
            LoadError = ex.Message;
            log.Error(ex, "Wayfarer: the unlock catalogue could not be read.");
        }
    }

    public bool Loaded { get; private set; }

    /// <summary>Why the catalogue is not loaded, or null when it is. Shown to the player rather
    /// than left in the log, because the alternative is a checklist that is silently empty.</summary>
    public string? LoadError { get; private set; }

    public IReadOnlyList<ResolvedUnlock> Entries => entries;

    /// <summary>Top <see cref="GlanceableMax"/> Available unlocks in the current zone, nearest-
    /// first from the player position as of the last recompute. This is what the readout's "there
    /// is something here" lines and the info bar's alert marker are both built from, and it is
    /// never rescanned per-frame: it is recomputed only on a zone or level change or an advanced
    /// pickup, and the surfaces refresh only the live distance to each already-selected entry,
    /// which is arithmetic rather than a new scan.</summary>
    public IReadOnlyList<ResolvedUnlock> GlanceableHere { get; private set; } = [];

    public PickupTarget? ToPickupTarget(ResolvedUnlock u) =>
        u.QuestRowId is { } rowId && u.GiverTerritory is { } t && u.GiverMap is { } m
            ? new(u.Def.Unlock, u.Def.Quest ?? "?", rowId, t, m, u.GiverX, u.GiverY, u.GiverZ, u.GiverName)
            : null;

    /// <summary>Lightweight per-tick change detector: two field reads and a tuple comparison,
    /// no allocation. Only calls into <see cref="RecomputeSafe"/> — and therefore only runs the
    /// full status pass — when the player's level or current zone actually changed since the last
    /// check. Covers territory changes, login/logout, level-ups, and hot-reload while logged in
    /// (the initial null baseline forces a recompute on the first tick a player exists).</summary>
    public void OnFrameworkUpdate(IFramework framework)
    {
        var level = (int)(objects.LocalPlayer?.Level ?? 0);
        var territory = clientState.TerritoryType;
        var current = (level, territory);
        if (lastChecked is { } last && last == current)
        {
            return;
        }

        lastChecked = current;
        RecomputeSafe();
    }

    public void OnPickupAdvanced() => RecomputeSafe();

    /// <summary>Full status pass. Framework thread only.</summary>
    public void Recompute()
    {
        if (!Loaded)
        {
            return;
        }

        var level = (int)(objects.LocalPlayer?.Level ?? 0);
        if (level == 0)
        {
            return;
        }

        var qm = QuestManager.Instance();
        var ps = PlayerState.Instance();
        var ui = UIState.Instance();
        var inventory = InventoryManager.Instance();
        var ctx = new UnlockGateContext(
            PlayerLevel: level,
            PlayerGrandCompany: ps != null ? ps->GrandCompany : (byte)0,
            PlayerGrandCompanyRank: ps != null ? ps->GetGrandCompanyRank() : 0,
            IsQuestComplete: QuestManager.IsQuestComplete,
            IsQuestAccepted: id => qm != null && qm->IsQuestAccepted((ushort)(id - QuestRowIdOffset)),
            GetClassJobLevel: jobId => ps != null ? ps->GetClassJobLevel((int)jobId, false) : 0,
            IsInstanceContentCompleted: UIState.IsInstanceContentCompleted,
            IsInstanceContentUnlocked: UIState.IsInstanceContentUnlocked,
            GetBeastTribeRank: tribeId => ps != null ? ps->GetBeastTribeRank(tribeId) : (byte)0,
            IsMountUnlocked: mountId => ps != null && ps->IsMountUnlocked(mountId),
            IsMinionUnlocked: minionId => ui != null && ui->IsCompanionUnlocked(minionId),
            GetOwnedItemCount: itemId => inventory != null ? inventory->GetInventoryItemCount(itemId) : 0,
            GetKeyItemCount: itemId => inventory != null ? KeyItemCount(inventory, itemId) : 0,
            ResolveGameText: ResolveGameText);

        UnlockStatusCalculator.Compute(entries, ctx);
        var territory = clientState.TerritoryType;

        var player = objects.LocalPlayer;
        GlanceableHere = RoutePlanner.TopAvailableHere(
            entries, territory, player?.Position.X ?? 0, player?.Position.Z ?? 0, GlanceableMax);
    }

    /// <summary>Key items live in their own container and are always resident, unlike an ordinary
    /// item that may be sitting in a retainer the game has not loaded — which is why a curated
    /// requirement says which container to look in rather than guessing.</summary>
    private static int KeyItemCount(InventoryManager* inventory, uint itemId) =>
        inventory->GetItemCountInContainer(itemId, InventoryType.KeyItems);

    /// <summary>Which ClassJob abbreviations a <see cref="ClassJobCategory"/> row flags: Lumina
    /// generates one bool property per abbreviation on that struct, so there's no reflection-free
    /// way to look one up by string other than switching on it.</summary>
    private static bool CategoryAllows(ClassJobCategory cat, string abbr) => abbr switch
    {
        "ADV" => cat.ADV,
        "GLA" => cat.GLA,
        "PGL" => cat.PGL,
        "MRD" => cat.MRD,
        "LNC" => cat.LNC,
        "ARC" => cat.ARC,
        "CNJ" => cat.CNJ,
        "THM" => cat.THM,
        "CRP" => cat.CRP,
        "BSM" => cat.BSM,
        "ARM" => cat.ARM,
        "GSM" => cat.GSM,
        "LTW" => cat.LTW,
        "WVR" => cat.WVR,
        "ALC" => cat.ALC,
        "CUL" => cat.CUL,
        "MIN" => cat.MIN,
        "BTN" => cat.BTN,
        "FSH" => cat.FSH,
        "PLD" => cat.PLD,
        "MNK" => cat.MNK,
        "WAR" => cat.WAR,
        "DRG" => cat.DRG,
        "BRD" => cat.BRD,
        "WHM" => cat.WHM,
        "BLM" => cat.BLM,
        "ACN" => cat.ACN,
        "SMN" => cat.SMN,
        "SCH" => cat.SCH,
        "ROG" => cat.ROG,
        "NIN" => cat.NIN,
        "MCH" => cat.MCH,
        "DRK" => cat.DRK,
        "AST" => cat.AST,
        "SAM" => cat.SAM,
        "RDM" => cat.RDM,
        "BLU" => cat.BLU,
        "GNB" => cat.GNB,
        "DNC" => cat.DNC,
        "RPR" => cat.RPR,
        "SGE" => cat.SGE,
        "VPR" => cat.VPR,
        "PCT" => cat.PCT,
        _ => false,
    };

    /// <summary>A <see cref="ClassJobCategory"/> row's own <c>Name</c> — "Disciple of War or Magic",
    /// "Disciple of the Land", or a single job's name on a job quest.
    ///
    /// <para>This is the string the game itself prints for a job gate, and reading it is the whole
    /// of the fix for a requirement line that used to enumerate thirty jobs. Row 0 is the
    /// unrestricted category and names nobody; some rows carry a blank name, which is why the
    /// caller still has the member list to fall back to. See
    /// <see cref="JobGateText"/>.</para></summary>
    private static string? CategoryName(RowRef<ClassJobCategory> categoryRef)
    {
        if (categoryRef.RowId == 0 || categoryRef.ValueNullable is not { } cat)
        {
            return null;
        }

        var name = cat.Name.ExtractText();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>Adds the ClassJob row ids/names a category flags into <paramref name="rowIds"/>/
    /// <paramref name="names"/> (row 0 means unrestricted — nothing to add). Called separately for
    /// <see cref="Quest.ClassJobCategory0"/> and (only when its own level requirement is real,
    /// see <see cref="QuestFacts.From"/>) <see cref="Quest.ClassJobCategory1"/> — the two are
    /// never merged into one list: unioning them would fold the game's "every job" sentinel mask
    /// on <c>ClassJobCategory1</c> into the primary job gate on ordinary single-job quests.</summary>
    private static void CollectAllowedJobs(
        RowRef<ClassJobCategory> categoryRef,
        List<(uint RowId, string Abbr, string Name)> classJobs,
        List<uint> rowIds,
        List<string> names)
    {
        if (categoryRef.RowId == 0 || categoryRef.ValueNullable is not { } cat)
        {
            return;
        }

        foreach (var (jobRowId, abbr, name) in classJobs)
        {
            if (!CategoryAllows(cat, abbr) || rowIds.Contains(jobRowId))
            {
                continue;
            }

            rowIds.Add(jobRowId);
            names.Add(name);
        }
    }

    private static List<(uint RowId, string Abbr, string Name)> LoadClassJobs(IDataManager dataManager)
    {
        var classJobs = new List<(uint RowId, string Abbr, string Name)>();
        foreach (var cj in dataManager.GetExcelSheet<ClassJob>())
        {
            var abbr = cj.Abbreviation.ExtractText();
            if (abbr.Length == 0)
            {
                continue;
            }

            classJobs.Add((cj.RowId, abbr, cj.Name.ExtractText()));
        }

        return classJobs;
    }

    /// <summary>Picks the Quest row an entry's gates are read from, and the set of rows any one of
    /// which would count as having done it.
    ///
    /// <para>Two ways in, in this order. A catalogue <c>questAnyOf</c> is an explicit statement of
    /// the set, re-derived from the guide's own link targets and confirmed against each quest
    /// page's infobox — it beats the name, because the name is what was ambiguous in the first
    /// place. Otherwise the entry names one quest and the name index answers, which may itself
    /// turn out to be ambiguous.</para></summary>
    private static (Quest Row, List<uint> Alternatives)? Bind(
        UnlockDefinition def, ExcelSheet<Quest> sheet, Dictionary<string, List<QuestNameCandidate>> byKey)
    {
        if (def.QuestAnyOf.Count > 0)
        {
            var live = new List<uint>(def.QuestAnyOf.Count);
            foreach (var id in def.QuestAnyOf)
            {
                if (sheet.GetRowOrDefault(id) is not null)
                {
                    live.Add(id);
                }
            }

            // Gates are read off one row and every row in the set is the same quest wearing a
            // different Grand Company's name, so the lowest id is as good as any and is stable.
            return live.Count > 0 && sheet.GetRowOrDefault(live[0]) is { } anyOfRow
                ? (anyOfRow, live)
                : null;
        }

        if (def.Quest is not { } questName
            || !byKey.TryGetValue(QuestNameKey.For(questName), out var candidates))
        {
            return null;
        }

        var match = QuestNameMatch.Resolve(candidates);
        return sheet.GetRowOrDefault(match.Best.RowId) is { } row
            ? (row, match.IsAmbiguous ? [.. match.Alternatives] : [])
            : null;
    }

    /// <summary>Groups every named Quest row under its folded name key, carrying the two facts
    /// that separate a live row from a retired one when a name is duplicated: whether the row is
    /// in the journal, and how many other quests depend on it. Building the inbound-reference
    /// count means one extra pass over the sheet's <c>PreviousQuest</c> columns, paid once at
    /// load.</summary>
    private static Dictionary<string, List<QuestNameCandidate>> BuildNameIndex(ExcelSheet<Quest> sheet)
    {
        var inboundRefs = new Dictionary<uint, int>();
        foreach (var q in sheet)
        {
            foreach (var prev in q.PreviousQuest)
            {
                if (prev.RowId != 0)
                {
                    inboundRefs[prev.RowId] = inboundRefs.GetValueOrDefault(prev.RowId) + 1;
                }
            }
        }

        var byKey = new Dictionary<string, List<QuestNameCandidate>>(StringComparer.Ordinal);
        foreach (var q in sheet)
        {
            var name = q.Name.ExtractText();
            if (name.Length == 0)
            {
                continue;
            }

            var key = QuestNameKey.For(name);
            if (!byKey.TryGetValue(key, out var candidates))
            {
                byKey[key] = candidates = [];
            }

            candidates.Add(new QuestNameCandidate(q.RowId, q.JournalGenre.RowId, inboundRefs.GetValueOrDefault(q.RowId)));
        }

        return byKey;
    }

    /// <summary>Reads a <see cref="GameTextRef"/> against the running client's own sheets, in
    /// whatever client language the player is using — see <see cref="GameTextRef"/> for why a
    /// reference is stored rather than a copy of the text. <c>RawRow</c> is the generic escape
    /// hatch for sheets Lumina has no strongly-typed wrapper for (<c>HowToPage</c> among them);
    /// see <c>tools/Wayfarer.CatalogueGen</c>'s offline use of the same API over sqpack directly.</summary>
    private string? ResolveGameText(GameTextRef reference)
    {
        try
        {
            var sheet = dataManager.Excel.GetSheet<RawRow>(null, reference.Sheet);
            if (!sheet.TryGetRow(reference.Row, out var row))
            {
                return null;
            }

            var text = row.ReadStringColumn(reference.Column).ExtractText();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex)
        {
            LogGameTextResolveFailure(ex, reference);
            return null;
        }
    }

    private void LogGameTextResolveFailure(Exception ex, GameTextRef reference)
    {
        if (gameTextResolveFailureLogged)
        {
            return;
        }

        gameTextResolveFailureLogged = true;
        var where = $"{reference.Sheet}#{reference.Row} col {reference.Column}";
        log.Warning(ex, $"Wayfarer: could not resolve game text {where} — falling back to the curated label.");
    }

    private void RecomputeSafe()
    {
        try
        {
            if (Loaded && clientState.IsLoggedIn)
            {
                Recompute();
            }
        }
        catch (Exception ex)
        {
            if (!recomputeFailureLogged)
            {
                recomputeFailureLogged = true;
                const string message =
                    "Wayfarer unlocks: refreshing the unlocks list failed, so it will keep showing whatever it "
                    + "last worked out until something makes it recompute successfully. Reported once.";
                log.Error(ex, message);
            }
        }
    }

    private void Load()
    {
        var dir = pluginInterface.AssemblyLocation.DirectoryName
            ?? throw new InvalidOperationException("no assembly directory");
        var json = File.ReadAllText(Path.Combine(dir, "unlocks-by-level.json"));
        var defs = UnlockDataset.Parse(json);

        var classJobs = LoadClassJobs(dataManager);
        var enpcSheet = dataManager.GetExcelSheet<ENpcResident>();
        var sheet = dataManager.GetExcelSheet<Quest>();
        var acceptConditions = dataManager.GetExcelSheet<QuestAcceptAdditionCondition>();
        var byKey = BuildNameIndex(sheet);

        entries.Clear();
        foreach (var def in defs)
        {
            var r = new ResolvedUnlock { Def = def };
            if (Bind(def, sheet, byKey) is { } bound)
            {
                QuestFacts.From(bound.Row, classJobs, enpcSheet, sheet, acceptConditions).ApplyTo(r, def.Level ?? 0);
                r.AlternativeQuestRowIds = bound.Alternatives;
            }

            entries.Add(r);
        }
    }

    /// <summary>Everything pulled from a <see cref="Quest"/> sheet row, in one place so
    /// <see cref="Load"/> stays a plain "match by name, apply facts" loop.</summary>
    private sealed record QuestFacts(
        uint RowId,
        int Level,
        List<uint> Prereqs,
        List<string> PrereqNames,
        byte PrereqJoin,
        List<uint> LockoutQuestRowIds,
        List<string> LockoutQuestNames,
        byte LockoutJoin,
        List<uint> RequiredJobRowIds,
        List<string> RequiredJobNames,
        string? RequiredJobCategoryName,
        List<uint> AltRequiredJobRowIds,
        List<string> AltRequiredJobNames,
        string? AltRequiredJobCategoryName,
        int AltRequiredJobLevel,
        List<uint> InstanceContentRowIds,
        List<string> InstanceContentNames,
        byte InstanceContentJoin,
        uint? RequiredGrandCompanyId,
        string? RequiredGrandCompanyName,
        uint? RequiredGrandCompanyRank,
        byte? RequiredBeastTribeId,
        string? RequiredBeastTribeName,
        uint? RequiredBeastTribeRank,
        string? RequiredBeastTribeRankName,
        uint? RequiredMountId,
        string? RequiredMountName,
        bool HasUnmodeledGate,
        uint? HardRequiredJobRowId,
        string? HardRequiredJobName,
        List<uint> AcceptConditionQuestRowIds,
        List<string> AcceptConditionQuestNames,
        bool HasUnresolvedAcceptCondition,
        bool HasNoDiscoverableGate,
        uint? Territory,
        uint? Map,
        float X,
        float Y,
        float Z,
        string? Zone,
        string? GiverName)
    {
        public static QuestFacts From(
            Quest q,
            List<(uint RowId, string Abbr, string Name)> classJobs,
            ExcelSheet<ENpcResident> enpcSheet,
            ExcelSheet<Quest> questSheet,
            ExcelSheet<QuestAcceptAdditionCondition> acceptConditions)
        {
            var prereqs = new List<uint>();
            var prereqNames = new List<string>();
            foreach (var prev in q.PreviousQuest)
            {
                if (prev.RowId == 0)
                {
                    continue;
                }

                prereqs.Add(prev.RowId);
                prereqNames.Add(QuestNameKey.Display(prev.ValueNullable?.Name.ExtractText()) is { Length: > 0 } prevName
                    ? prevName
                    : $"Quest {prev.RowId}");
            }

            var lockoutIds = new List<uint>();
            var lockoutNames = new List<string>();
            foreach (var locked in q.QuestLock)
            {
                if (locked.RowId == 0)
                {
                    continue;
                }

                lockoutIds.Add(locked.RowId);
                lockoutNames.Add(QuestNameKey.Display(locked.ValueNullable?.Name.ExtractText()) is { Length: > 0 } lockedName
                    ? lockedName
                    : $"Quest {locked.RowId}");
            }

            var icIds = new List<uint>();
            var icNames = new List<string>();
            foreach (var ic in q.InstanceContent)
            {
                if (ic.RowId == 0)
                {
                    continue;
                }

                icIds.Add(ic.RowId);
                icNames.Add(ic.ValueNullable?.ContentFinderCondition.ValueNullable?.Name.ExtractText() is { Length: > 0 } n
                    ? n
                    : $"duty {ic.RowId}");
            }

            uint? territory = null, map = null;
            float x = 0, y = 0, z = 0;
            string? zone = null;
            if (q.IssuerLocation.RowId != 0 && q.IssuerLocation.ValueNullable is { } level
                && level.Territory.RowId != 0 && level.Map.RowId != 0)
            {
                territory = level.Territory.RowId;
                map = level.Map.RowId;
                x = level.X;
                y = level.Y;
                z = level.Z;
                zone = level.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ExtractText();
            }

            // ClassJobLevel is a 2-slot collection pairing positionally with ClassJobCategory0/1.
            // Verified against a full live-sheet scan: ClassJobLevel[1] != 0 never co-occurs with
            // ClassJobCategory1 being the "every job" sentinel mask, and the sentinel always pairs
            // with ClassJobLevel[1] == 0 — so the level alone tells us whether category1 is a real,
            // independent alternative gate or just the game's placeholder on an unrestricted slot.
            int lvl0 = 0, lvl1 = 0, idx = 0;
            foreach (var l in q.ClassJobLevel)
            {
                if (idx == 0)
                {
                    lvl0 = l;
                }
                else if (idx == 1)
                {
                    lvl1 = l;
                }

                idx++;
            }

            var jobRowIds = new List<uint>();
            var jobNames = new List<string>();
            CollectAllowedJobs(q.ClassJobCategory0, classJobs, jobRowIds, jobNames);

            var altJobRowIds = new List<uint>();
            var altJobNames = new List<string>();
            if (lvl1 != 0)
            {
                CollectAllowedJobs(q.ClassJobCategory1, classJobs, altJobRowIds, altJobNames);
            }

            // IssuerStart is an untyped RowRef: some quests are issued by objects/eobjects
            // rather than an ENpcResident, so a miss here is expected, not an error — degrade
            // silently to null rather than logging.
            string? giverName = null;
            if (q.IssuerStart.RowId != 0
                && enpcSheet.GetRowOrDefault(q.IssuerStart.RowId)?.Singular.ExtractText() is { Length: > 0 } gn)
            {
                giverName = gn;
            }

            // QuestAcceptAdditionCondition lives in its own sheet, keyed by quest row id, and
            // holds prerequisite quests that never appear in PreviousQuest. Ten catalogue entries
            // depend on one — the Hunt tiers, the Custom Delivery unlocks — and were shown as
            // ready to pick up while their real prerequisite was still incomplete. Some of the
            // sheet's requirement ids don't resolve to a Quest row at all; that is an unknown
            // requirement, not an absent one, and is recorded as such.
            var acceptIds = new List<uint>();
            var acceptNames = new List<string>();
            var unresolvedAccept = false;
            if (acceptConditions.GetRowOrDefault(q.RowId) is { } condition)
            {
                foreach (var requirement in new[] { condition.Requirement0.RowId, condition.Requirement1.RowId, condition.Unknown0 })
                {
                    if (requirement == 0)
                    {
                        continue;
                    }

                    if (questSheet.GetRowOrDefault(requirement)?.Name.ExtractText() is { Length: > 0 } requirementName)
                    {
                        acceptIds.Add(requirement);
                        acceptNames.Add(QuestNameKey.Display(requirementName));
                    }
                    else
                    {
                        unresolvedAccept = true;
                    }
                }
            }

            var hasUnmodeledGate = q.Festival.RowId != 0 || q.IsHouseRequired;

            // ClassJobLevel[0] is only half the required level: QuestLevelOffset carries the rest.
            // Eight catalogue entries were reported 1-9 levels too low, worst of all the two
            // Bozjan-front entries, shown as level 71 when they are level 80.
            var requiredLevel = lvl0 + q.QuestLevelOffset;

            // Nothing in the sheet asks anything of the player. That is not the same fact as
            // "there is nothing to ask": see ResolvedUnlock.HasNoDiscoverableGate.
            var noDiscoverableGate = requiredLevel <= 1
                && prereqs.Count == 0
                && lockoutIds.Count == 0
                && icIds.Count == 0
                && acceptIds.Count == 0
                && !unresolvedAccept
                && !hasUnmodeledGate
                && q.GrandCompany.RowId == 0
                && q.BeastTribe.RowId == 0
                && q.MountRequired.RowId == 0
                && q.ClassJobRequired.RowId == 0;

            return new QuestFacts(
                RowId: q.RowId,
                Level: requiredLevel,
                Prereqs: prereqs,
                PrereqNames: prereqNames,
                PrereqJoin: q.PreviousQuestJoin,
                LockoutQuestRowIds: lockoutIds,
                LockoutQuestNames: lockoutNames,
                LockoutJoin: q.QuestLockJoin,
                RequiredJobRowIds: jobRowIds,
                RequiredJobNames: jobNames,
                RequiredJobCategoryName: CategoryName(q.ClassJobCategory0),
                AltRequiredJobRowIds: altJobRowIds,
                AltRequiredJobNames: altJobNames,
                AltRequiredJobCategoryName: lvl1 != 0 ? CategoryName(q.ClassJobCategory1) : null,
                AltRequiredJobLevel: lvl1,
                InstanceContentRowIds: icIds,
                InstanceContentNames: icNames,
                InstanceContentJoin: q.InstanceContentJoin,
                RequiredGrandCompanyId: q.GrandCompany.RowId != 0 ? q.GrandCompany.RowId : null,
                RequiredGrandCompanyName: q.GrandCompany.RowId != 0 ? q.GrandCompany.ValueNullable?.Name.ExtractText() : null,
                RequiredGrandCompanyRank: q.GrandCompanyRank.RowId != 0 ? q.GrandCompanyRank.RowId : null,
                RequiredBeastTribeId: q.BeastTribe.RowId != 0 ? (byte)q.BeastTribe.RowId : null,
                RequiredBeastTribeName: q.BeastTribe.RowId != 0 ? q.BeastTribe.ValueNullable?.Name.ExtractText() : null,
                RequiredBeastTribeRank: q.BeastReputationRank.RowId != 0 ? q.BeastReputationRank.RowId : null,
                RequiredBeastTribeRankName: q.BeastReputationRank.RowId != 0 ? q.BeastReputationRank.ValueNullable?.Name.ExtractText() : null,
                RequiredMountId: q.MountRequired.RowId != 0 ? q.MountRequired.RowId : null,
                RequiredMountName: q.MountRequired.RowId != 0 ? q.MountRequired.ValueNullable?.Singular.ExtractText() : null,
                HasUnmodeledGate: hasUnmodeledGate,
                HardRequiredJobRowId: q.ClassJobRequired.RowId != 0 ? q.ClassJobRequired.RowId : null,
                HardRequiredJobName: q.ClassJobRequired.RowId != 0 ? q.ClassJobRequired.ValueNullable?.Name.ExtractText() : null,
                AcceptConditionQuestRowIds: acceptIds,
                AcceptConditionQuestNames: acceptNames,
                HasUnresolvedAcceptCondition: unresolvedAccept,
                HasNoDiscoverableGate: noDiscoverableGate,
                Territory: territory,
                Map: map,
                X: x,
                Y: y,
                Z: z,
                Zone: zone,
                GiverName: giverName);
        }

        public void ApplyTo(ResolvedUnlock r, int fallbackLevel)
        {
            r.QuestRowId = RowId;
            r.QuestLevel = Level > 0 ? Level : fallbackLevel;
            r.PrereqRowIds = Prereqs;
            r.PrereqNames = PrereqNames;
            r.PrereqJoin = PrereqJoin;
            r.LockoutQuestRowIds = LockoutQuestRowIds;
            r.LockoutQuestNames = LockoutQuestNames;
            r.LockoutJoin = LockoutJoin;
            r.RequiredJobRowIds = RequiredJobRowIds;
            r.RequiredJobNames = RequiredJobNames;
            r.RequiredJobCategoryName = RequiredJobCategoryName;
            r.AltRequiredJobRowIds = AltRequiredJobRowIds;
            r.AltRequiredJobNames = AltRequiredJobNames;
            r.AltRequiredJobCategoryName = AltRequiredJobCategoryName;
            r.AltRequiredJobLevel = AltRequiredJobLevel;
            r.InstanceContentRowIds = InstanceContentRowIds;
            r.InstanceContentNames = InstanceContentNames;
            r.InstanceContentJoin = InstanceContentJoin;
            r.RequiredGrandCompanyId = RequiredGrandCompanyId;
            r.RequiredGrandCompanyName = RequiredGrandCompanyName;
            r.RequiredGrandCompanyRank = RequiredGrandCompanyRank;
            r.RequiredBeastTribeId = RequiredBeastTribeId;
            r.RequiredBeastTribeName = RequiredBeastTribeName;
            r.RequiredBeastTribeRank = RequiredBeastTribeRank;
            r.RequiredBeastTribeRankName = RequiredBeastTribeRankName;
            r.RequiredMountId = RequiredMountId;
            r.RequiredMountName = RequiredMountName;
            r.HasUnmodeledGate = HasUnmodeledGate;
            r.HardRequiredJobRowId = HardRequiredJobRowId;
            r.HardRequiredJobName = HardRequiredJobName;
            r.AcceptConditionQuestRowIds = AcceptConditionQuestRowIds;
            r.AcceptConditionQuestNames = AcceptConditionQuestNames;
            r.HasUnresolvedAcceptCondition = HasUnresolvedAcceptCondition;
            r.HasNoDiscoverableGate = HasNoDiscoverableGate;
            r.GiverTerritory = Territory;
            r.GiverMap = Map;
            r.GiverX = X;
            r.GiverY = Y;
            r.GiverZ = Z;
            r.ZoneName = Zone;
            r.GiverName = GiverName;
        }
    }
}
