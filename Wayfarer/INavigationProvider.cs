using Wayfarer.Core.Navigation;

namespace Wayfarer;

/// <summary>Consumer-shaped seam over <see cref="QuestNavigator"/>: the navigation state and
/// pickup-routing operations used by <see cref="Windows.ArrowWindow"/>, <see cref="Windows.UnlockWindow"/>,
/// <see cref="Windows.ReadoutFeed"/> and <see cref="Modules.UnlockChecklistModule"/>.
///
/// Surfaces that choose which quest is followed — <see cref="Windows.NativeHubWindow"/>'s Quests tab
/// and <see cref="ContextMenuActions"/> — hold the concrete <see cref="QuestNavigator"/> instead,
/// because that is a narrower audience than this contract, and so do
/// <see cref="WayfarerIpcProvider"/> and <see cref="Modules.QuestHelperModule"/>, which needs
/// <see cref="QuestNavigator.OnUpdate"/>.</summary>
internal interface INavigationProvider
{
    /// <summary>The current navigation target, recomputed once per framework tick. Safe to read
    /// from any thread; only the reference is swapped.</summary>
    NavigationState Current { get; }

    /// <summary>Overrides the followed quest with a specific accepted quest id, or clears the
    /// override — falling back to following the main scenario — when set to <see langword="null"/>.
    /// Write-only in this contract: <see cref="Windows.UnlockWindow"/> sets it when an accepted
    /// unlock quest is clicked, and <see cref="QuestNavigator"/> itself is the only reader. The
    /// quest picker on <see cref="Windows.NativeHubWindow"/>'s Quests tab needs to read it back, so
    /// it holds the concrete navigator instead.</summary>
    ushort? FollowedOverride { set; }

    /// <summary>Sets a single unlock-quest pickup as the active navigation target, replacing any
    /// queued route.</summary>
    void SetPickup(PickupTarget t);

    /// <summary>Queues a multi-stop pickup route, making its first entry the active navigation
    /// target.</summary>
    void SetRoute(List<PickupTarget> route);

    /// <summary>Clears the active pickup and any queued route, returning navigation to the
    /// followed quest.</summary>
    void ClearPickup();

    /// <summary>Live current-objective label for an accepted quest, keyed by its raw (unoffset)
    /// quest id — the same <c>Map.Instance()-&gt;QuestMarkers</c> scan <see cref="QuestNavigator"/>
    /// uses internally to compute the followed quest's step label, but callable for any accepted
    /// quest rather than only the followed one. Framework thread only. Null when the game has no
    /// marker for this quest right now (not every step/zone has one) or the marker's label text
    /// is empty.</summary>
    string? GetAcceptedQuestObjective(uint rawQuestId);
}
