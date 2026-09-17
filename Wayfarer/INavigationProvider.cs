using Wayfarer.Core.Navigation;

namespace Wayfarer;

/// <summary>Consumer-shaped seam over <see cref="QuestNavigator"/>: the navigation state and the
/// operations used by <see cref="Windows.ReadoutFeed"/>.
///
/// Surfaces that choose which quest is followed — <see cref="ContextMenuActions"/> — hold the
/// concrete <see cref="QuestNavigator"/> instead, because that is a narrower audience than this
/// contract, and so does <see cref="Modules.QuestHelperModule"/>, which needs
/// <see cref="QuestNavigator.OnUpdate"/>.</summary>
internal interface INavigationProvider
{
    /// <summary>The current navigation target, recomputed once per framework tick. Safe to read
    /// from any thread; only the reference is swapped.</summary>
    NavigationState Current { get; }

    /// <summary>Overrides the followed quest with a specific accepted quest id, or clears the
    /// override — falling back to following the main scenario — when set to <see langword="null"/>.
    /// Write-only in this contract; <see cref="QuestNavigator"/> itself is the only reader.</summary>
    ushort? FollowedOverride { set; }

    /// <summary>Clears the active selection, returning navigation to the followed quest.</summary>
    void ClearPickup();

    /// <summary>Live current-objective label for an accepted quest, keyed by its raw (unoffset)
    /// quest id — the same <c>Map.Instance()-&gt;QuestMarkers</c> scan <see cref="QuestNavigator"/>
    /// uses internally to compute the followed quest's step label, but callable for any accepted
    /// quest rather than only the followed one. Framework thread only. Null when the game has no
    /// marker for this quest right now (not every step/zone has one) or the marker's label text
    /// is empty.</summary>
    string? GetAcceptedQuestObjective(uint rawQuestId);
}
