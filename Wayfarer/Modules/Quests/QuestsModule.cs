using Dalamud.Plugin.Services;
using Wayfarer.App.Modules;
using Wayfarer.Guidance;

namespace Wayfarer.Modules.Quests;

/// <summary>The quests module: what the settings checkbox switches. Up means
/// <see cref="QuestObjectives"/> holds guidance focus and the journal offers its follow button;
/// down means both have let go.</summary>
internal sealed class QuestsModule(
    QuestObjectives objectives,
    QuestFollowing following,
    QuestReader reader,
    JournalFollowButton followButton,
    DutyMarking dutyMarks,
    IFramework framework,
    IGuidance guidance) : IModule
{
    /// <summary>What the module is called, everywhere: the checkbox, the guidance it publishes, and
    /// the enabled set saved in <c>app.json</c>, which is keyed by this.</summary>
    public const string ModuleName = "Quests";

    private const string JournalButtonName = "Follow quests from the journal";
    private const string JournalButtonDescription = "Puts a button in the quest journal that follows the quest on show, so the guide leads to it instead of the main scenario.";
    private const string DutyMarksName = "Mark duties your quests lead to";
    private const string DutyMarksDescription = "Puts a quest's own mark beside its duty in the Duty Finder, so a duty an accepted quest is waiting behind is told apart from one that is not.";

    /// <inheritdoc/>
    public string Name => ModuleName;

    /// <inheritdoc/>
    public string Description => "Follows the main scenario, or any quest you choose from the journal, in the Main Scenario Guide.";

    /// <inheritdoc/>
    public IReadOnlyList<ModuleSetting> Settings =>
    [
        new ModuleSetting(JournalButtonName, JournalButtonDescription, () => following.FromJournal, SetJournalButton),
        new ModuleSetting(DutyMarksName, DutyMarksDescription, () => following.MarkDuties, SetDutyMarks),
    ];

    /// <inheritdoc/>
    public async Task EnableAsync()
    {
        await WarmAsync().ConfigureAwait(false);

        guidance.Claim(objectives);
        if (following.FromJournal)
        {
            followButton.Start();
        }

        if (following.MarkDuties)
        {
            dutyMarks.Start();
        }
    }

    /// <inheritdoc/>
    public async Task DisableAsync()
    {
        guidance.Yield(objectives);
        await dutyMarks.StopAsync().ConfigureAwait(false);
        await followButton.StopAsync().ConfigureAwait(false);
    }

    /// <summary>Reads what the first frame of guidance would otherwise read, away from that frame.
    ///
    /// <para>Which quest is being guided is the game's own answer and has to be asked for on its
    /// thread; everything read about that quest is sheets, and is read off it. A quest that cannot
    /// be named yet -- the player is not in the world -- is no reason to hold the module up, and
    /// the frame that wants it will read it as it always did.</para></summary>
    private async Task WarmAsync()
    {
        reader.Warm();

        var guided = await framework.RunOnFrameworkThread(
            () => following.Followed ?? reader.CurrentMainScenarioQuest()).ConfigureAwait(false);

        if (guided is { } questId)
        {
            reader.Warm(questId);
        }
    }

    /// <summary>Switches the Duty Finder's marks, and puts them there or takes them away at once.
    /// </summary>
    private void SetDutyMarks(bool wanted)
    {
        following.MarkDuties = wanted;
        if (wanted)
        {
            dutyMarks.Start();
        }
        else
        {
            _ = dutyMarks.StopAsync();
        }
    }

    /// <summary>Switches the journal's button, and puts it there or takes it away at once rather
    /// than waiting for the module to come round again.</summary>
    private void SetJournalButton(bool wanted)
    {
        following.FromJournal = wanted;
        if (wanted)
        {
            followButton.Start();
        }
        else
        {
            _ = followButton.StopAsync();
        }
    }
}
