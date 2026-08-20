using Wayfarer.Core.Unlocks;

namespace Wayfarer;

/// <summary>Consumer-shaped seam over <see cref="UnlockService"/>: the resolved unlock checklist
/// and status queries used by <see cref="Windows.UnlockWindow"/>, the unlocks button in
/// <see cref="Windows.ArrowWindow"/>, and <see cref="WayfarerIpcProvider"/>.
/// <see cref="Modules.UnlockChecklistModule"/>, which owns <see cref="UnlockService"/>'s lifecycle,
/// keeps the concrete type instead of this interface — it needs <see cref="UnlockService.OnFrameworkUpdate"/>
/// and <see cref="UnlockService.OnPickupAdvanced"/>, neither of which is part of this contract because
/// none of the three consumers above call them.</summary>
internal interface IUnlockProvider
{
    /// <summary>Whether the wiki unlocks dataset loaded successfully at startup.</summary>
    bool Loaded { get; }

    /// <summary>Every resolved unlock entry, with its current status as of the last
    /// <see cref="Recompute"/>.</summary>
    IReadOnlyList<ResolvedUnlock> Entries { get; }

    /// <summary>Count of unlocks with an available pickup in the player's current territory,
    /// as of the last <see cref="Recompute"/>.</summary>
    int AvailableHereCount { get; }

    /// <summary>Runs a full status pass over <see cref="Entries"/>. Framework thread only.</summary>
    void Recompute();

    /// <summary>Converts a resolved unlock into a navigable pickup target, or <see langword="null"/>
    /// when its quest giver's location isn't known.</summary>
    PickupTarget? ToPickupTarget(ResolvedUnlock u);
}
