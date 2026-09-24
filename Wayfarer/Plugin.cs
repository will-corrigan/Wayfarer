using Autofac;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;
using Wayfarer.App;
using Wayfarer.App.Modules;
using Wayfarer.Modules.Hunting;
using Wayfarer.Modules.Quests;

namespace Wayfarer;

/// <summary>The plugin's entry point and its only composition root.
///
/// <para>Dalamud constructs this class with the one thing it needs to reach everything else, then
/// calls <see cref="LoadAsync"/>, which is where the container is built and anything that has to
/// start does so — asynchronously, which is what a toolkit with an async initialisation needs.
/// Everything below this class is built by the container: the app's own services from
/// <see cref="AppRegistrations"/>, and one Autofac module per feature module, each registering what it
/// owns. This class never constructs anything itself.</para>
///
/// <para>The game's own services are not listed here. <see cref="GameServices"/> fetches whichever
/// of them something asks for, so a class that wants one takes it and nothing else has to be told;
/// a list kept here instead would be a second copy to keep in step, and the plugin would fail to
/// load the first time the two disagreed.</para>
///
/// <para>Everything the container creates, it disposes, in reverse order of creation, when the
/// plugin is unloaded — asynchronously, so a service that has to finish on the framework thread
/// can await its way there instead of blocking the unload. Dalamud's own services are excepted:
/// Dalamud made them and Dalamud disposes them.</para></summary>
public sealed class Plugin(IDalamudPluginInterface pluginInterface) : IAsyncDalamudPlugin
{
    private IContainer? container;

    /// <inheritdoc/>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        // Required before any KamiToolKit type (native windows, nodes) is touched.
        await KamiToolKitLibrary.InitializeAsync(pluginInterface, "Wayfarer").ConfigureAwait(false);

        // Dalamud gives up on a load after a while and asks it to stop; a load that stops here is
        // one Dalamud knows did not finish, and DisposeAsync tolerates the container not being built.
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ContainerBuilder();

        builder.RegisterInstance(pluginInterface).ExternallyOwned();
        builder.RegisterSource(new GameServices(pluginInterface));

        builder.RegisterModule<AppRegistrations>();
        builder.RegisterModule<QuestsRegistrations>();
        builder.RegisterModule<HuntingRegistrations>();

        container = builder.Build();
        cancellationToken.ThrowIfCancellationRequested();
        await container.Resolve<ModuleHost>().StartAsync().ConfigureAwait(false);

        // The version belongs in this line: it is the first question asked of every pasted log.
        container.Resolve<IPluginLog>().Information($"{typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "?"} loaded.");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (container is { } owned)
        {
            container = null;
            await owned.DisposeAsync().ConfigureAwait(false);
        }

        await KamiToolKitLibrary.DisposeAsync().ConfigureAwait(false);
    }
}
