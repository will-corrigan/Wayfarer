using Autofac;
using Wayfarer.App;
using Wayfarer.Core.Guidance;

namespace Wayfarer.Modules.Quests;

/// <summary>Registers the quests module: everything in this folder, and nothing outside it. The
/// app learns of the module through the <see cref="IModule"/> registered here, and switches it
/// on and off through that.</summary>
internal sealed class QuestsModule : Module
{
    /// <inheritdoc/>
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<QuestReader>().SingleInstance();
        builder.RegisterType<QuestObjectives>().As<IObjectiveSource>().AsSelf().SingleInstance();
        builder.RegisterType<QuestsFeature>().As<IModule>().SingleInstance();
    }
}
