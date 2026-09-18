using Wayfarer.App;

namespace Wayfarer.Modules.Quests;

/// <summary>The quests module as the app sees it: a name, a line, and up and down. Up means
/// <see cref="QuestObjectives"/> holds guidance focus; down means it has let go.</summary>
internal sealed class QuestsFeature(QuestObjectives objectives, IGuidance guidance) : IModule
{
    /// <inheritdoc/>
    public string Name => "Quests";

    /// <inheritdoc/>
    public string Description => "Follows the main scenario in the Main Scenario Guide.";

    /// <inheritdoc/>
    public Task EnableAsync()
    {
        guidance.Claim(objectives);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DisableAsync()
    {
        guidance.Yield(objectives);
        return Task.CompletedTask;
    }
}
