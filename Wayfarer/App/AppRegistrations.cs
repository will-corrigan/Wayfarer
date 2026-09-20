using Autofac;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Wayfarer.App.Config;
using Wayfarer.App.Diagnostics;
using Wayfarer.App.Modules;
using Wayfarer.App.Settings;
using Wayfarer.Guidance;
using Wayfarer.Surfaces.ScenarioTree;

namespace Wayfarer.App;

/// <summary>Wires the app into the container: the part every feature module plugs into. Each
/// service the app gains is registered here and nowhere else.</summary>
internal sealed class AppRegistrations : Module
{
    /// <inheritdoc/>
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(c => ShippedRoutingGraph.Load(c.Resolve<IDalamudPluginInterface>(), c.Resolve<IPluginLog>())).SingleInstance();

        // Auto-activated: the frame loop starts when the container is built, not when something
        // first asks for it, and stops when the container is disposed.
        builder.RegisterType<GuidanceService>().As<IGuidance>().SingleInstance().AutoActivate();
        builder.RegisterType<ObjectFinder>().As<IObjectFinder>().SingleInstance();
        builder.RegisterType<Interactions>().As<IInteractions>().SingleInstance();
        builder.RegisterType<Heading>().As<IHeading>().SingleInstance();
        builder.RegisterType<Actions>().As<IActions>().SingleInstance();
        builder.RegisterType<ScenarioTreeStyleStore>().SingleInstance();

        builder.RegisterType<GuidanceReport>().SingleInstance();

        builder.RegisterType<ConfigStore>().As<IConfigStore>().SingleInstance();
        builder.RegisterType<ModuleHost>().As<IModuleHost>().AsSelf().SingleInstance();
        builder.RegisterType<SettingsService>().As<ISettingsWindow>().AsSelf().SingleInstance().AutoActivate();

        // The one surface: the block inside the game's Main Scenario Guide, which draws whatever
        // guidance is published to it.
        builder.RegisterType<ScenarioTreeSurface>().AsSelf().SingleInstance().AutoActivate();
    }
}
