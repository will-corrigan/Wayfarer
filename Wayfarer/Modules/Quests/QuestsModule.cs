using Autofac;
using Wayfarer.Core.Guidance;

namespace Wayfarer.Modules.Quests;

/// <summary>Registers the quests module: everything in this folder, and nothing outside it.
/// Loading this module is what enables it; not loading it is what disables it.</summary>
internal sealed class QuestsModule : Module
{
    /// <inheritdoc/>
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<QuestObjectives>().As<IObjectiveSource>().AsSelf().SingleInstance();
    }
}
