using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Wayfarer.Api;
using Wayfarer.Api.Dto;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Unlocks;
using Wayfarer.Modules;

namespace Wayfarer;

/// <summary>Registers Wayfarer's Dalamud IPC gates (see <see cref="WayfarerIpc"/>) and serves
/// them off live module state. Plugin-owned rather than module-owned: the gates must always
/// exist for a consumer plugin to call, even while a module is disabled — they degrade to a
/// Hidden navigation state / an empty unlocks list rather than the gate disappearing
/// (task-5-brief.md delta 4).</summary>
internal sealed class WayfarerIpcProvider : IDisposable
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly ModuleRegistry modules;
    private readonly IClientState clientState;
    private readonly ICallGateProvider<int> versionGate;
    private readonly ICallGateProvider<string> navigationGate;
    private readonly ICallGateProvider<string, int, string> unlocksGate;

    public WayfarerIpcProvider(IDalamudPluginInterface pluginInterface, ModuleRegistry modules, IClientState clientState)
    {
        this.modules = modules;
        this.clientState = clientState;

        versionGate = pluginInterface.GetIpcProvider<int>(WayfarerIpc.VersionGate);
        versionGate.RegisterFunc(() => WayfarerIpc.ApiVersion);

        navigationGate = pluginInterface.GetIpcProvider<string>(WayfarerIpc.NavigationGate);
        navigationGate.RegisterFunc(GetNavigationJson);

        unlocksGate = pluginInterface.GetIpcProvider<string, int, string>(WayfarerIpc.UnlocksGate);
        unlocksGate.RegisterFunc(GetUnlocksJson);
    }

    public void Dispose()
    {
        versionGate.UnregisterFunc();
        navigationGate.UnregisterFunc();
        unlocksGate.UnregisterFunc();
    }

    private string GetNavigationJson()
    {
        var module = modules.Get<QuestHelperModule>();
        var state = module is { Enabled: true } ? module.Navigator.Current : new NavigationState();
        return JsonSerializer.Serialize(state, Options);
    }

    /// <summary>Ported from the private FFXIVInventory.Core.ToolService.GetUnlocks filter
    /// (task-5-brief.md delta 4): scope "all" keeps every status, otherwise only Available;
    /// scope "here" additionally restricts to the player's current territory; maxLevel &gt; 0
    /// caps the quest level. Unlike the source, an unrecognized scope value degrades to the
    /// same behavior as "available" rather than throwing — this is a wire boundary, not a tool
    /// call that can surface a validation error to the caller.</summary>
    private string GetUnlocksJson(string scope, int maxLevel)
    {
        if (modules.Get<UnlockChecklistModule>() is not { Enabled: true } module)
        {
            return JsonSerializer.Serialize(Array.Empty<UnlockRowDto>(), Options);
        }

        var here = clientState.TerritoryType;
        var rows = new List<UnlockRowDto>();
        foreach (var live in module.Unlocks.Entries)
        {
            // Snapshot before reading: this can run off the framework thread while Recompute
            // mutates Status/LockReason on the live instance concurrently.
            var u = live.Snapshot();
            if (!string.Equals(scope, "all", StringComparison.Ordinal) && u.Status != UnlockStatus.Available)
            {
                continue;
            }

            if (string.Equals(scope, "here", StringComparison.Ordinal) && u.GiverTerritory != here)
            {
                continue;
            }

            if (maxLevel > 0 && u.QuestLevel > maxLevel)
            {
                continue;
            }

            rows.Add(new()
            {
                Unlock = u.Def.Unlock,
                Status = u.Status.ToString(),
                LockReason = u.LockReason,
                Quest = u.Def.Quest,
                Giver = u.GiverName,
                Level = u.QuestLevel,
                Zone = u.ZoneName,
                Priority = u.Def.Priority,
                Category = UnlockFilters.Category(u.Def),
                Description = u.Def.Description,
            });
        }

        return JsonSerializer.Serialize(rows, Options);
    }
}
