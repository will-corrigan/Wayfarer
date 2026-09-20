using Wayfarer.App.Modules;
using Wayfarer.Guidance;
using Wayfarer.Surfaces.DutyFinder;

namespace Wayfarer.Modules.Quests;

/// <summary>The quests module: what the settings checkbox switches. Up means
/// <see cref="QuestObjectives"/> holds guidance focus and the journal offers its follow button;
/// down means both have let go.</summary>
internal sealed class QuestsModule(
    QuestObjectives objectives,
    QuestFollowing following,
    JournalFollowButton followButton,
    QuestDutyMarks dutyMarks,
    IDutyFinder dutyFinder,
    IGuidance guidance) : IModule
{
    /// <summary>What the module is called, everywhere: the checkbox, the guidance it publishes, and
    /// the enabled set saved in <c>app.json</c>, which is keyed by this.</summary>
    public const string ModuleName = "Quests";

    private const string JournalButtonName = "Follow quests from the journal";
    private const string JournalButtonDescription = "Puts a button in the quest journal that follows the quest on show, so the guide leads to it instead of the main scenario.";

    private const string DutyMarksName = "Mark duties in the Duty Finder";
    private const string DutyMarksDescription = "Puts a quest mark on every duty in the Duty Finder that a quest in your journal still leads to.";

    private IDisposable? marking;

    /// <inheritdoc/>
    public string Name => ModuleName;

    /// <inheritdoc/>
    public string Description => "Follows the main scenario, or any quest you choose from the journal, in the Main Scenario Guide.";

    /// <inheritdoc/>
    public IReadOnlyList<ModuleSetting> Settings =>
    [
        new ModuleSetting(JournalButtonName, JournalButtonDescription, () => following.FromJournal, SetJournalButton),
        new ModuleSetting(DutyMarksName, DutyMarksDescription, () => following.MarkDutyFinder, SetDutyMarks),
    ];

    /// <inheritdoc/>
    public Task EnableAsync()
    {
        guidance.Claim(objectives);
        if (following.FromJournal)
        {
            followButton.Start();
        }

        MarkDuties(following.MarkDutyFinder);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task DisableAsync()
    {
        guidance.Yield(objectives);
        MarkDuties(false);
        await followButton.StopAsync().ConfigureAwait(false);
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

    /// <summary>Switches the Duty Finder's marks, and puts them there or takes them away at once
    /// rather than waiting for the module to come round again.</summary>
    private void SetDutyMarks(bool wanted)
    {
        following.MarkDutyFinder = wanted;
        MarkDuties(wanted);
    }

    /// <summary>Asks the Duty Finder to draw this module's marks, or stops asking. What it hands
    /// back is the asking: letting go of it is how the marks come off, and the window stops being
    /// watched at all once no module is asking.</summary>
    private void MarkDuties(bool wanted)
    {
        if (wanted)
        {
            marking ??= dutyFinder.Mark(dutyMarks);
            return;
        }

        marking?.Dispose();
        marking = null;
    }
}
