using Wayfarer.App;
using Wayfarer.App.Guidance;
using Wayfarer.App.Modules;

namespace Wayfarer.Modules.Quests;

/// <summary>The quests module: what the settings checkbox switches. Up means
/// <see cref="QuestObjectives"/> holds guidance focus and the journal offers its follow button;
/// down means both have let go.</summary>
internal sealed class QuestsModule(QuestObjectives objectives, JournalFollowButton followButton, IGuidance guidance) : IModule
{
    /// <inheritdoc/>
    public string Name => "Quests";

    /// <inheritdoc/>
    public string Description => "Follows the main scenario, or any quest you choose from the journal, in the Main Scenario Guide.";

    /// <inheritdoc/>
    public Task EnableAsync()
    {
        guidance.Claim(objectives);
        followButton.Start();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task DisableAsync()
    {
        guidance.Yield(objectives);
        await followButton.StopAsync().ConfigureAwait(false);
    }
}
