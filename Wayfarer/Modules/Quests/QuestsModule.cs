using Dalamud.Plugin.Services;
using Wayfarer.App.Modules;
using Wayfarer.Guidance;

namespace Wayfarer.Modules.Quests;

/// <summary>The quests module: everything Wayfarer does about the quest you are on. Each part is
/// a switch of its own — the guiding itself, the journal's button, the Duty Finder's marks — and
/// the module is whatever those switches add up to.</summary>
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

    /// <summary>The game's own mark for a quest worth taking.</summary>
    private const uint QuestMark = 71221;

    private const string GuidingName = "Guide me through my quests";
    private const string GuidingDescription = "Follows the main scenario, or whichever quest you chose, and shows the way to its next step in the Main Scenario Guide.";
    private const string JournalButtonName = "Follow quests from the journal";
    private const string JournalButtonDescription = "Puts a button in the quest journal that follows the quest on show, so the guide leads to it instead of the main scenario.";
    private const string DutyMarksName = "Mark duties your quests lead to";
    private const string DutyMarksDescription = "Puts a quest's own mark beside its duty in the Duty Finder, so a duty an accepted quest is waiting behind is told apart from one that is not.";

    /// <inheritdoc/>
    public string Name => ModuleName;

    /// <inheritdoc/>
    /// <remarks>The mark the game itself draws over someone with a quest to give.</remarks>
    public uint Icon => QuestMark;

    /// <inheritdoc/>
    public string Description => "Follows the main scenario, or any quest you choose from the journal, in the Main Scenario Guide.";

    /// <inheritdoc/>
    public IReadOnlyList<ModuleSetting> Settings =>
    [
        new ModuleSetting(GuidingName, GuidingDescription, () => following.Guiding, on => following.Guiding = on),
        new ModuleSetting(JournalButtonName, JournalButtonDescription, () => following.FromJournal, on => following.FromJournal = on),
        new ModuleSetting(DutyMarksName, DutyMarksDescription, () => following.MarkDuties, on => following.MarkDuties = on),
    ];

    /// <inheritdoc/>
    /// <remarks>Each part is started or stopped on its own, and each is safe to start again while
    /// it is already running, so this says what the module should look like rather than what has
    /// changed since last time.</remarks>
    public async Task ApplyAsync()
    {
        if (following.Guiding)
        {
            await WarmAsync().ConfigureAwait(false);
            guidance.Claim(objectives);
        }
        else
        {
            guidance.Yield(objectives);
        }

        if (following.FromJournal)
        {
            await followButton.StartAsync().ConfigureAwait(false);
        }
        else
        {
            await followButton.StopAsync().ConfigureAwait(false);
        }

        if (following.MarkDuties)
        {
            await dutyMarks.StartAsync().ConfigureAwait(false);
        }
        else
        {
            await dutyMarks.StopAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync()
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
}
