using Autofac;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Wayfarer.App.Config;
using Wayfarer.App.Modules;
using Wayfarer.App.Settings;
using Wayfarer.Guidance;
using Wayfarer.Surfaces.DutyFinder;
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

        builder.RegisterType<ConfigStore>().As<IConfigStore>().SingleInstance();
        builder.RegisterType<ModuleHost>().As<IModuleHost>().AsSelf().SingleInstance();
        builder.RegisterType<SettingsService>().As<ISettingsWindow>().AsSelf().SingleInstance().AutoActivate();

        // The surfaces: game windows more than one module can draw in, which own the window and
        // settle what goes where. A window only one module will ever draw in belongs to that
        // module instead. The Duty Finder watches nothing until a module asks it to, so it is not
        // activated with the container the way one that draws of its own accord is.
        builder.RegisterType<ScenarioTreeSurface>().AsSelf().SingleInstance().AutoActivate();
        builder.RegisterType<DutyFinderSurface>().As<IDutyFinder>().SingleInstance();
    }
}
