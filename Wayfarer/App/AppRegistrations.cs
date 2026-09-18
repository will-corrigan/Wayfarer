using Autofac;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Wayfarer.App.Settings;
using Wayfarer.Surfaces.ScenarioTree;

namespace Wayfarer.App;

/// <summary>Wires the app into the container: the part every feature module plugs into. Each
/// service the app gains is registered here and nowhere else.</summary>
internal sealed class AppRegistrations : Module
{
    /// <inheritdoc/>
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(c => ShippedRoutingGraph.Load(c.Resolve<IDalamudPluginInterface>(), c.Resolve<ILog>())).SingleInstance();

        // Auto-activated: the frame loop starts when the container is built, not when something
        // first asks for it, and stops when the container is disposed.
        builder.RegisterType<GuidanceService>().As<IGuidance>().SingleInstance().AutoActivate();
        builder.RegisterType<Heading>().As<IHeading>().SingleInstance();
        builder.RegisterType<Actions>().As<IActions>().SingleInstance();

        builder.RegisterType<RemoteLogSink>().SingleInstance();
        builder.RegisterType<Log>().As<ILog>().SingleInstance();
        builder.RegisterType<ConfigStore>().As<IConfigStore>().SingleInstance();
        builder.RegisterType<ModuleHost>().As<IModuleHost>().AsSelf().SingleInstance();
        builder.RegisterType<SettingsService>().SingleInstance().AutoActivate();

        // The one surface for now: the block inside the game's Main Scenario Guide.
        builder.RegisterType<ScenarioTreeSurface>().AsSelf().As<IDiagnostics>().SingleInstance().AutoActivate();
    }
}
