using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
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
        }
        catch (Exception ex)
        {
            log.Error(ex, "UnlockService: dataset load failed — unlocks feature disabled");
        }
    }

    public bool Loaded { get; private set; }

    public IReadOnlyList<ResolvedUnlock> Entries => entries;

    public int AvailableHereCount { get; private set; }

    public PickupTarget? ToPickupTarget(ResolvedUnlock u) =>
        u.QuestRowId is { } rowId && u.GiverTerritory is { } t && u.GiverMap is { } m
            ? new PickupTarget(u.Def.Unlock, u.Def.Quest ?? "?", rowId, t, m, u.GiverX, u.GiverY, u.GiverZ)
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
        UnlockStatusCalculator.Compute(
            entries,
            level,
            QuestManager.IsQuestComplete,
            id => qm != null && qm->IsQuestAccepted((ushort)(id - QuestRowIdOffset)));
        AvailableHereCount = UnlockStatusCalculator.CountAvailableIn(entries, clientState.TerritoryType);
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
            log.Error(ex, "UnlockService: recompute failed");
        }
    }

    private void Load()
    {
        var dir = pluginInterface.AssemblyLocation.DirectoryName
            ?? throw new InvalidOperationException("no assembly directory");
        var json = File.ReadAllText(Path.Combine(dir, "unlocks-by-level.json"));
        var defs = UnlockDataset.Parse(json);

        // One pass over the Quest sheet: name (lowercase) → row facts.
        var byName = new Dictionary<string, (uint RowId, int Level, List<uint> Prereqs, List<string> PrereqNames, uint? Territory, uint? Map, float X, float Y, float Z, string? Zone)>(StringComparer.Ordinal);
        var sheet = dataManager.GetExcelSheet<Quest>();
        foreach (var q in sheet)
        {
            var name = q.Name.ExtractText();
            if (name.Length == 0)
            {
                continue;
            }

            var key = name.ToLowerInvariant();
            if (byName.ContainsKey(key))
            {
                continue; // first wins; duplicates are rare and equivalent for our purpose
            }

            var prereqs = new List<uint>();
            var prereqNames = new List<string>();
            foreach (var prev in q.PreviousQuest)
            {
                if (prev.RowId == 0)
                {
                    continue;
                }

                prereqs.Add(prev.RowId);
                prereqNames.Add(prev.ValueNullable?.Name.ExtractText() ?? $"Quest {prev.RowId}");
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

            var lvl = 0;
            foreach (var l in q.ClassJobLevel)
            {
                lvl = l;
                break;
            }

            byName[key] = (q.RowId, lvl, prereqs, prereqNames, territory, map, x, y, z, zone);
        }

        entries.Clear();
        foreach (var def in defs)
        {
            var r = new ResolvedUnlock { Def = def };
            if (def.Quest is { } questName && byName.TryGetValue(questName.ToLowerInvariant(), out var m))
            {
                r.QuestRowId = m.RowId;
                r.QuestLevel = m.Level > 0 ? m.Level : def.Level;
                r.PrereqRowIds = m.Prereqs;
                r.PrereqNames = m.PrereqNames;
                r.GiverTerritory = m.Territory;
                r.GiverMap = m.Map;
                r.GiverX = m.X;
                r.GiverY = m.Y;
                r.GiverZ = m.Z;
                r.ZoneName = m.Zone;
            }

            entries.Add(r);
        }
    }
}
