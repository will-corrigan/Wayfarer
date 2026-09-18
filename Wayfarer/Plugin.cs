using Autofac;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Wayfarer.App;
using Wayfarer.Modules.Quests;

namespace Wayfarer;

/// <summary>The plugin's entry point and its only composition root.
///
/// <para>Dalamud constructs this class with the game services its constructor asks for, then
/// calls <see cref="LoadAsync"/>, which is where the container is built and anything that has to
/// start does so — asynchronously, which is what a toolkit with an async initialisation needs.
/// Everything below this class is built by the container: the app's own services from
/// <see cref="AppModule"/>, and one Autofac module per feature module, each registering what it
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
    IPluginLog log) : IAsyncDalamudPlugin
{
    private IContainer? container;

    /// <inheritdoc/>
    public Task LoadAsync(CancellationToken cancellationToken)
    {
        var builder = new ContainerBuilder();

        builder.RegisterInstance(pluginInterface).ExternallyOwned();
        builder.RegisterInstance(framework).ExternallyOwned();
        builder.RegisterInstance(clientState).ExternallyOwned();
        builder.RegisterInstance(objects).ExternallyOwned();
        builder.RegisterInstance(dataManager).ExternallyOwned();
        builder.RegisterInstance(log).ExternallyOwned();

        builder.RegisterModule<AppModule>();
        builder.RegisterModule<QuestsModule>();

        container = builder.Build();

        // The version belongs in this line: it is the first question asked of every pasted log.
        log.Information($"Wayfarer {typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "?"} loaded.");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => container?.DisposeAsync() ?? ValueTask.CompletedTask;
}
