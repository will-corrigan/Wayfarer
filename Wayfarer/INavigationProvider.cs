using Wayfarer.Core.Navigation;

namespace Wayfarer;

/// <summary>Consumer-shaped seam over <see cref="QuestNavigator"/>: the navigation state and
/// pickup-routing operations used by <see cref="Windows.ArrowWindow"/>, <see cref="Windows.UnlockWindow"/>
/// and <see cref="Modules.UnlockChecklistModule"/>. <see cref="WayfarerIpcProvider"/> uses the concrete
/// <see cref="QuestNavigator"/> type instead of this interface. <see cref="Modules.QuestHelperModule"/>,
/// which owns <see cref="QuestNavigator"/>'s lifecycle, also keeps the concrete type — it needs
/// <see cref="QuestNavigator.OnUpdate"/>, which is not part of this contract because none of the three
/// consumers above call it.</summary>
internal interface INavigationProvider
{
    /// <summary>Raised after a pickup is accepted or completed and the navigator advances to the
    /// next queued pickup (or back to the followed quest).</summary>
    event Action? OnPickupAdvanced;

    /// <summary>The current navigation target, recomputed once per framework tick. Safe to read
    /// from any thread; only the reference is swapped.</summary>
    NavigationState Current { get; }

    /// <summary>Overrides the followed quest with a specific accepted quest id, or clears the
    /// override (falling back to following the MSQ) when set to <see langword="null"/>. Write-only
    /// in this contract: only the quest picker popup in <see cref="Windows.ArrowWindow"/> sets it —
    /// <see cref="QuestNavigator"/> itself is the only reader.</summary>
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

    /// <summary>Accepted quests for the quest picker popup. Framework thread only.</summary>
    List<(ushort Id, string Name)> GetAcceptedQuests();

    /// <summary>Live current-objective label for an accepted quest, keyed by its raw (unoffset)
    /// quest id — the same <c>Map.Instance()-&gt;QuestMarkers</c> scan <see cref="QuestNavigator"/>
    /// uses internally to compute the followed quest's step label, but callable for any accepted
    /// quest rather than only the followed one. Framework thread only. Null when the game has no
    /// marker for this quest right now (not every step/zone has one) or the marker's label text
    /// is empty.</summary>
    string? GetAcceptedQuestObjective(uint rawQuestId);
}
