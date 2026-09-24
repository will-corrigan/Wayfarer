using Autofac;
using Wayfarer.App.Modules;

namespace Wayfarer.Modules.Hunting;

/// <summary>Wires the hunting folder into the container: everything in this folder, and nothing
/// outside it. The app learns of the module through the <see cref="IModule"/> registered here.</summary>
internal sealed class HuntingRegistrations : Module
{
    /// <inheritdoc/>
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<MonsterPositions>().SingleInstance();
        builder.RegisterType<HuntReader>().SingleInstance();
        builder.RegisterType<HuntFollowing>().SingleInstance();
        builder.RegisterType<HuntObjectives>().SingleInstance();
        builder.RegisterType<HuntingLogButton>().SingleInstance();
        builder.RegisterType<MarkBillButtons>().SingleInstance();
        builder.RegisterType<HuntingModule>().As<IModule>().SingleInstance();
    }
}
