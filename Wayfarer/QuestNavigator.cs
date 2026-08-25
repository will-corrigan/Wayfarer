using Dalamud.Plugin.Services;
using Wayfarer.Core.Guidance;
using Wayfarer.Core.Navigation;
using Wayfarer.Guidance;
using Wayfarer.Guidance.Sources;

namespace Wayfarer;

/// <summary>A single selection the arrow can be pointed at: historically an unlock-quest pickup
/// (walk here and accept this quest), later reused — badly — for hunting targets too, which is how
/// a monster ended up carrying a quest row id of 0.
///
/// TRANSITIONAL. It survives only so the windows and the context menu keep compiling unchanged
/// while guidance moves underneath them; each feature now owns a properly typed selection, and
/// <see cref="HuntingTarget"/> is what tells the adapter which source a selection really belongs
/// to. This type disappears when the presentations are moved onto the typed source APIs.</summary>
internal sealed record PickupTarget(
    string UnlockName, string QuestName, uint QuestRowId,
    uint Territory, uint MapId, float X, float Y, float Z, string? GiverName = null)
{
    /// <summary>Set when this "pickup" is really a hunting target, so
    /// <see cref="QuestNavigator.SetPickup"/> can hand it to the source that knows how to tell when
    /// it is finished. Nothing may infer that from <see cref="QuestRowId"/> — for a monster it is
    /// meaningless, and reading it anyway is the defect this whole architecture removes.</summary>
    public HuntingTargetView? HuntingTarget { get; init; }
}

/// <summary>Thin adapter over the guidance framework, keeping the shape the windows, the context
/// menu and the IPC provider already speak. Every method here is a redirect: guidance itself lives
/// in <see cref="GuidanceService"/> (the per-frame loop), the sources (what to guide to and when it
/// is done) and <see cref="GuidanceRouter"/> (how to get there).
///
/// Owned by <see cref="Modules.QuestHelperModule"/>, which subscribes <see cref="OnUpdate"/> to
/// <c>Framework.Update</c> in <c>Enable()</c> and unsubscribes in <c>Disable()</c>.</summary>
internal sealed class QuestNavigator(
    GuidanceService guidance,
    QuestObjectiveSource questSource,
    UnlockRouteSource unlockSource,
    HuntingSource huntingSource) : INavigationProvider
{
    public NavigationState Current => guidance.Current;

    /// <summary>Overrides the followed quest with a specific accepted quest id, or clears the
    /// override — falling back to following the main scenario — when set to null. Written by the
    /// window's Quests tab and by the context menu's "Follow MSQ"; read only by
    /// <see cref="QuestObjectiveSource"/>.</summary>
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

    /// <summary>Which of the four follow modes is running — see <see cref="MainScenarioReturn"/>.
    /// Exactly one, always, and "nothing in particular" is <see cref="Core.Guidance.FollowMode.MainScenario"/>.
    ///
    /// <para>The engaged source is identified by asking each source for its own <c>SourceId</c> rather
    /// than by comparing against a written-down string, so a renamed source cannot leave a surface
    /// silently reporting the wrong mode.</para></summary>
    public FollowMode FollowMode =>
        MainScenarioReturn.ModeOf(EngagedMode(), FollowedOverride is not null);

    /// <summary>The active explicit selection, whichever feature owns it — an unlock stop, or a
    /// hunting target rendered back into the old shape. Transitional, like
    /// <see cref="PickupTarget"/> itself.</summary>
    public PickupTarget? Pickup => unlockSource.CurrentLeg ?? huntingSource.CurrentPickup;

    /// <summary>Routes the selection to the source that owns it. A hunting target's completion is a
    /// kill count and an unlock stop's is a quest accept; sending either to the wrong owner is what
    /// made a selected monster vanish a frame later.</summary>
    public void SetPickup(PickupTarget t)
    {
        if (t.HuntingTarget is { } target)
        {
            huntingSource.GoTo(target);
            return;
        }

        unlockSource.GoTo(t);
    }

    public void SetRoute(List<PickupTarget> route)
    {
        if (route.Count == 0)
        {
            return;
        }

        if (route[0].HuntingTarget is not null)
        {
            huntingSource.StartHunt();
            return;
        }

        unlockSource.StartRoute(route);
    }

    /// <summary>Starts a hunt through every remaining target on the current log page.
    ///
    /// <para>The direct call, because the pickup shape cannot express this plan. The three surfaces
    /// that offer "Start Hunting" used to build a list of <see cref="PickupTarget"/>s out of the
    /// targets in the player's current zone and hand it to <see cref="SetRoute"/>, which recognised
    /// them as hunting targets and threw the list away in favour of the whole rank — so the list was
    /// a fiction, and worse than a fiction: an empty one (every remaining target in another zone, or
    /// every one of them duty-gated) made <see cref="SetRoute"/> return without starting anything,
    /// which is a press that does nothing.</para></summary>
    public void StartHunt() => huntingSource.StartHunt();

    /// <summary>The universal exit: whichever explicit mode is engaged, this ends it and drops the
    /// player back to the quest they were following.</summary>
    public void ClearPickup() => guidance.Arbiter.ReleaseAll();

    public void OnUpdate(IFramework framework) => guidance.OnUpdate(framework);

    public List<(ushort Id, string Name)> GetAcceptedQuests() => questSource.GetAcceptedQuests();

    public string? GetAcceptedQuestObjective(uint rawQuestId) => questSource.GetAcceptedQuestObjective(rawQuestId);

    /// <summary>The mode the engaged source stands for, or null when nothing is engaged. The one
    /// place a source id is turned into a follow mode, and it does it by asking the sources.</summary>
    private FollowMode? EngagedMode()
    {
        if (Current is not { Engaged: true, SourceId: { } sourceId })
        {
            return null;
        }

        if (string.Equals(sourceId, huntingSource.SourceId, StringComparison.Ordinal))
        {
            return Core.Guidance.FollowMode.Hunting;
        }

        return string.Equals(sourceId, unlockSource.SourceId, StringComparison.Ordinal)
            ? Core.Guidance.FollowMode.UnlockRoute
            : null;
    }
}
