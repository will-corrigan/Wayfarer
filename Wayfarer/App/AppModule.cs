using Autofac;
using Wayfarer.Core.Routing;

namespace Wayfarer.App;

/// <summary>Registers the app: the part every feature module plugs into. Each service the app
/// gains is registered here and nowhere else.</summary>
internal sealed class AppModule : Module
{
    /// <inheritdoc/>
    protected override void Load(ContainerBuilder builder)
    {
        // Empty until the generator writes the routing data file: with no aetherytes, shards or
        // doors, every route is a walk on the current map, which is still honest guidance.
        builder.Register(_ => new RouteGraph([], [])).SingleInstance();

        // Auto-activated: the frame loop starts when the container is built, not when something
        // first asks for it, and stops when the container is disposed.
        builder.RegisterType<GuidanceService>().As<IGuidance>().SingleInstance().AutoActivate();
        builder.RegisterType<Heading>().As<IHeading>().SingleInstance();
    }
}
