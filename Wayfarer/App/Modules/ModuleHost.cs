using Dalamud.Plugin.Services;

namespace Wayfarer.App.Modules;

/// <summary>Keeps every module in line with its own settings. Nothing here remembers which modules
/// are on, because nothing needs to: a module is on while any of what it offers is switched on,
/// and each module saves its own switches. An enabled set kept beside them would be a second
/// answer to the same question, free to disagree with the first.
///
/// <para>A module that needs the framework thread marshals there itself, as the toolkit's calls
/// do; the host awaits each module plainly so an unload never waits on a tick. A module that
/// throws while coming into line is logged and left as it was. One module's failure never stops
/// the others.</para></summary>
internal sealed class ModuleHost(IEnumerable<IModule> modules, IPluginLog log) : IModuleHost, IAsyncDisposable
{
    private readonly List<IModule> modules = [.. modules];

    /// <inheritdoc/>
    public IReadOnlyList<IModule> Modules => modules;

    /// <inheritdoc/>
    public bool IsOn(IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return module.Settings.Any(setting => setting.Read());
    }

    /// <summary>Brings every module into line with what the player last left switched on.</summary>
    public async Task StartAsync()
    {
        foreach (var module in modules)
        {
            await RefreshAsync(module).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task RefreshAsync(IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        try
        {
            await module.ApplyAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Error(ex, $"the {module.Name} module could not be brought into line with its settings.");
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var module in modules)
        {
            try
            {
                await module.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Error(ex, $"the {module.Name} module failed to stop cleanly.");
            }
        }
    }
}
