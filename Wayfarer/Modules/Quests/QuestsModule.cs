using Wayfarer.App;
using Wayfarer.App.Guidance;
using Wayfarer.App.Modules;

namespace Wayfarer.Modules.Quests;

/// <summary>The quests module: what the settings checkbox switches. Up means
/// <see cref="QuestObjectives"/> holds guidance focus and the journal offers its follow button;
/// down means both have let go.</summary>
internal sealed class QuestsModule(QuestObjectives objectives, JournalFollowButton followButton, IGuidance guidance) : IModule
{
    /// <summary>What the module is called, everywhere: the checkbox, the guidance it publishes, and
    /// the enabled set saved in <c>app.json</c>, which is keyed by this.</summary>
    public const string ModuleName = "Quests";

    /// <inheritdoc/>
    public string Name => ModuleName;

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
