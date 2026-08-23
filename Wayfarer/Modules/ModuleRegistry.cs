using Dalamud.Plugin.Services;

namespace Wayfarer.Modules;

/// <summary>Owns every <see cref="IModule"/> the plugin hosts: applies the saved enabled/disabled
/// flags on registration, guards <see cref="IModule.Enable"/>/<see cref="IModule.Disable"/> against
/// a misbehaving module taking the whole plugin down, and disposes modules in reverse registration
/// order when the plugin unloads.</summary>
public sealed class ModuleRegistry(IPluginLog log, Configuration config) : IDisposable
{
    private readonly List<IModule> modules = [];

    /// <summary>Every registered module, in registration order.</summary>
    public IReadOnlyList<IModule> Modules => modules;

    /// <summary>Adds a module to the registry and activates it per the saved config — a missing
    /// entry in <see cref="Configuration.ModuleEnabled"/> falls back to
    /// <paramref name="enabledByDefault"/> rather than the module's own <see cref="IModule.Enabled"/>,
    /// since modules are constructed inactive (see <see cref="IModule.Enabled"/>) and the registry
    /// is what decides whether to call <see cref="IModule.Enable"/>.</summary>
    /// <param name="module">The module to register. Must be constructed with <c>Enabled == false</c>.</param>
    /// <param name="enabledByDefault">The state to use when no saved config entry exists for
    /// <see cref="IModule.Name"/>.</param>
    public void Register(IModule module, bool enabledByDefault)
    {
        modules.Add(module);

        var desired = config.ModuleEnabled.TryGetValue(module.Name, out var stored) ? stored : enabledByDefault;
        SetEnabled(module, desired);
    }

    /// <summary>Enables or disables <paramref name="module"/>, catching and logging any exception
    /// the module throws so one broken module can't take the rest of the plugin down. A throwing
    /// <see cref="IModule.Enable"/> call is followed by a best-effort <see cref="IModule.Disable"/>
    /// to force the module back to a known-safe state; that follow-up call's own exceptions (if
    /// any) are swallowed so only a single error is ever logged per failure.</summary>
    public void SetEnabled(IModule module, bool enabled)
    {
        if (module.Enabled == enabled)
        {
            return;
        }

        try
        {
            if (enabled)
            {
                module.Enable();
            }
            else
            {
                module.Disable();
            }
        }
        catch (Exception ex)
        {
            var message =
                $"Wayfarer: the {module.Name} feature threw while {(enabled ? "starting" : "stopping")}, so it "
                + "has been switched off. Everything else keeps running; turn it back on in Settings to retry.";
            log.Error(ex, message);
            if (enabled)
            {
                try
                {
                    module.Disable();
                }
                catch (Exception)
                {
                    // Module is already broken; a second log line or a throw from the guard
                    // itself would not help the user, so this is intentionally swallowed.
                }
            }
        }
    }

    /// <summary>Returns the registered module of type <typeparamref name="T"/>, or <see langword="null"/>
    /// if none is registered.</summary>
    /// <typeparam name="T">The module type to look up.</typeparam>
    public T? Get<T>()
        where T : class, IModule =>
        modules.OfType<T>().FirstOrDefault();

    /// <summary>Disposes every module in reverse registration order, guarding each one with its own
    /// try/catch (spec: controller wave task 2b) so a single module throwing during teardown — e.g.
    /// a native window's <see cref="IDisposable.Dispose"/> asserting the main thread when Dalamud
    /// unloads plugins from a thread-pool thread — can't abort the rest of the chain and strand
    /// <see cref="Plugin"/> before it reaches <c>KamiToolKitLibrary.Cleanup()</c>.</summary>
    public void Dispose()
    {
        for (var i = modules.Count - 1; i >= 0; i--)
        {
            try
            {
                modules[i].Dispose();
            }
            catch (Exception ex)
            {
                var message =
                    $"Wayfarer: the {modules[i].Name} feature threw while shutting down, so whatever it owns may "
                    + "be leaked until the game is restarted. The remaining features are still being shut down.";
                log.Error(ex, message);
            }
        }
    }
}
