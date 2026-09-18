using Dalamud.Plugin.Services;

namespace Wayfarer.App;

/// <summary>Owns the enabled set and the modules' up/down state. Brings the enabled modules up on
/// <see cref="StartAsync"/> and takes every module that is up down when disposed.
///
/// <para>A module that needs the framework thread marshals there itself, as the toolkit's calls
/// do; the host awaits each module plainly so an unload never waits on a tick. A module that
/// throws while coming up is logged and left down; one that throws while going down is logged and
/// treated as down. Neither stops the other modules.</para></summary>
internal sealed class ModuleHost(IEnumerable<IModule> modules, IConfigStore configs, RemoteLogSink remoteLog, IPluginLog log) : IModuleHost, IAsyncDisposable
{
    private const string ConfigName = "app";

    private readonly List<IModule> modules = [.. modules];
    private readonly HashSet<IModule> up = [];
    private AppConfig config = new();

    /// <inheritdoc/>
    public IReadOnlyList<IModule> Modules => modules;

    /// <inheritdoc/>
    public bool IsEnabled(IModule module) => up.Contains(module);

    /// <summary>Loads the enabled set and brings those modules up. On a first run every module is
    /// on, so a fresh install guides from the moment it loads.</summary>
    public async Task StartAsync()
    {
        config = configs.Load<AppConfig>(ConfigName);
        remoteLog.Start(config.RemoteLogUrl);
        if (!config.Initialised)
        {
            config.Initialised = true;
            config.EnabledModules = new HashSet<string>(modules.Select(m => m.Name), StringComparer.Ordinal);
            configs.Save(ConfigName, config);
        }

        foreach (var module in modules.Where(m => config.EnabledModules.Contains(m.Name)))
        {
            await BringUpAsync(module).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task SetEnabledAsync(IModule module, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(module);

        var changed = enabled ? config.EnabledModules.Add(module.Name) : config.EnabledModules.Remove(module.Name);
        if (changed)
        {
            configs.Save(ConfigName, config);
        }

        if (enabled && !up.Contains(module))
        {
            await BringUpAsync(module).ConfigureAwait(false);
        }
        else if (!enabled && up.Contains(module))
        {
            await TakeDownAsync(module).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var module in modules.Where(up.Contains).ToList())
        {
            await TakeDownAsync(module).ConfigureAwait(false);
        }
    }

    private async Task BringUpAsync(IModule module)
    {
        try
        {
            await module.EnableAsync().ConfigureAwait(false);
            up.Add(module);
        }
        catch (Exception ex)
        {
            log.Error(ex, $"the {module.Name} module failed to start and is off for this session.");
        }
    }

    private async Task TakeDownAsync(IModule module)
    {
        up.Remove(module);
        try
        {
            await module.DisableAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Error(ex, $"the {module.Name} module failed to stop cleanly.");
        }
    }
}
