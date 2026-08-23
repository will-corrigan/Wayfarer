using Dalamud.Plugin.Services;
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

    /// <summary>The universal exit: whichever explicit mode is engaged, this ends it and drops the
    /// player back to the quest they were following.</summary>
    public void ClearPickup() => guidance.Arbiter.ReleaseAll();

    public void OnUpdate(IFramework framework) => guidance.OnUpdate(framework);

    public List<(ushort Id, string Name)> GetAcceptedQuests() => questSource.GetAcceptedQuests();

    public string? GetAcceptedQuestObjective(uint rawQuestId) => questSource.GetAcceptedQuestObjective(rawQuestId);
}
