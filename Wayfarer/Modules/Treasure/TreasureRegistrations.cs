using Autofac;
using Wayfarer.App.Modules;

namespace Wayfarer.Modules.Treasure;

/// <summary>Wires the treasure folder into the container: everything in this folder, and nothing
/// outside it. The app learns of the module through the <see cref="IModule"/> registered here.</summary>
internal sealed class TreasureRegistrations : Module
{
    /// <inheritdoc/>
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<TreasureSettings>().SingleInstance();
        builder.RegisterType<TreasureSpots>().SingleInstance();
        builder.RegisterType<LiveTreasure>().SingleInstance();
        builder.RegisterType<TreasureMap>().SingleInstance();
        builder.RegisterType<TreasureModule>().As<IModule>().SingleInstance();
    }
}
