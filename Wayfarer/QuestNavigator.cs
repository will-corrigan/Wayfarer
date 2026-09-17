using Dalamud.Plugin.Services;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Navigation;
using Wayfarer.Guidance;
using Wayfarer.Guidance.Sources;

namespace Wayfarer;

/// <summary>Thin adapter over the guidance framework, keeping the shape the windows and the context
/// menu already speak. Every method here is a redirect: guidance itself lives
/// in <see cref="GuidanceService"/> (the per-frame loop), the sources (what to guide to and when it
/// is done) and <see cref="GuidanceRouter"/> (how to get there).
///
/// Owned by <see cref="Modules.QuestHelperModule"/>, which subscribes <see cref="OnUpdate"/> to
/// <c>Framework.Update</c> in <c>Enable()</c> and unsubscribes in <c>Disable()</c>.</summary>
internal sealed class QuestNavigator(
    GuidanceService guidance,
    QuestObjectiveSource questSource) : INavigationProvider
{
    public NavigationState Current => guidance.Current;

    /// <summary>Overrides the followed quest with a specific accepted quest id, or clears the
    /// override — falling back to following the main scenario — when set to null. Written by the
    /// context menu's "Follow MSQ"; read only by <see cref="QuestObjectiveSource"/>.</summary>
    public ushort? FollowedOverride
    {
        get => questSource.FollowedQuest;
        set => questSource.FollowedQuest = value;
    }

    /// <summary>What returning to the main scenario has to do from here — see
    /// <see cref="MainScenarioReturn"/>. Read by every surface that offers that return, so the
    /// condition deciding whether the control is live and the operations the control performs are one
    /// thing rather than four independent readings of two fields.</summary>
    public FollowReset MainScenarioReset =>
        MainScenarioReturn.From(Current.Engaged, FollowedOverride is not null);

    /// <summary>Which follow mode is running — see <see cref="MainScenarioReturn"/>. Exactly one,
    /// always, and "nothing in particular" is
    /// <see cref="Core.Guidance.FollowMode.MainScenario"/>.</summary>
    public FollowMode FollowMode =>
        MainScenarioReturn.ModeOf(FollowedOverride is not null);

    /// <summary>The universal exit: whichever explicit mode is engaged, this ends it and drops the
    /// player back to the quest they were following.</summary>
    public void ClearPickup() => guidance.Arbiter.ReleaseAll();

    public void OnUpdate(IFramework framework) => guidance.OnUpdate(framework);

    public List<(ushort Id, string Name)> GetAcceptedQuests() => questSource.GetAcceptedQuests();

    public string? GetAcceptedQuestObjective(uint rawQuestId) => questSource.GetAcceptedQuestObjective(rawQuestId);
}
