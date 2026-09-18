using Autofac;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;
using Wayfarer.App;
using Wayfarer.Modules.Quests;

namespace Wayfarer;

/// <summary>The plugin's entry point and its only composition root.
///
/// <para>Dalamud constructs this class with the game services its constructor asks for, then
/// calls <see cref="LoadAsync"/>, which is where the container is built and anything that has to
/// start does so — asynchronously, which is what a toolkit with an async initialisation needs.
/// Everything below this class is built by the container: the app's own services from
/// <see cref="AppRegistrations"/>, and one Autofac module per feature module, each registering what it
/// owns. This class never constructs anything itself.</para>
///
/// <para>Dalamud's services are registered as externally owned: Dalamud created them and Dalamud
/// disposes them, so the container must never do so. Everything the container creates, it
/// disposes, in reverse order of creation, when the plugin is unloaded — asynchronously, so a
/// service that has to finish on the framework thread can await its way there instead of
/// blocking the unload.</para></summary>
public sealed class Plugin(
    IDalamudPluginInterface pluginInterface,
    IFramework framework,
    IClientState clientState,
    IObjectTable objects,
    IDataManager dataManager,
    ICommandManager commands,
    IAddonLifecycle addonLifecycle,
    ITextureProvider textures,
    IGameGui gameGui,
    IPluginLog log) : IAsyncDalamudPlugin
{
    private IContainer? container;

    /// <inheritdoc/>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        // Required before any KamiToolKit type (native windows, nodes) is touched.
        await KamiToolKitLibrary.InitializeAsync(pluginInterface, "Wayfarer").ConfigureAwait(false);

        var builder = new ContainerBuilder();

        builder.RegisterInstance(pluginInterface).ExternallyOwned();
        builder.RegisterInstance(framework).ExternallyOwned();
        builder.RegisterInstance(clientState).ExternallyOwned();
        builder.RegisterInstance(objects).ExternallyOwned();
        builder.RegisterInstance(dataManager).ExternallyOwned();
        builder.RegisterInstance(commands).ExternallyOwned();
        builder.RegisterInstance(addonLifecycle).ExternallyOwned();
        builder.RegisterInstance(textures).ExternallyOwned();
        builder.RegisterInstance(gameGui).ExternallyOwned();
        builder.RegisterInstance(log).ExternallyOwned();

        builder.RegisterModule<AppRegistrations>();
        builder.RegisterModule<QuestsRegistrations>();

        container = builder.Build();
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
