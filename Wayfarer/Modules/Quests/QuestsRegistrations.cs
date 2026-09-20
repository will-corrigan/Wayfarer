using Autofac;
using Wayfarer.App.Modules;
using Wayfarer.Guidance;

namespace Wayfarer.Modules.Quests;

/// <summary>Wires the quests folder into the container: everything in this folder, and nothing
/// outside it. The app learns of the module through the <see cref="IModule"/> registered here.</summary>
internal sealed class QuestsRegistrations : Module
{
    /// <inheritdoc/>
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<QuestJournal>().SingleInstance();
        builder.RegisterType<QuestReader>().SingleInstance();
        builder.RegisterType<QuestFollowing>().SingleInstance();
        builder.RegisterType<JournalFollowButton>().SingleInstance();
        builder.RegisterType<QuestDutyBadges>().SingleInstance();
        builder.RegisterType<QuestObjectives>().As<IObjectiveSource>().AsSelf().SingleInstance();
        builder.RegisterType<QuestsModule>().As<IModule>().SingleInstance();
    }
}
