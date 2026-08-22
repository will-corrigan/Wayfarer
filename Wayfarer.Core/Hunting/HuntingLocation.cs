namespace Wayfarer.Core.Hunting;

/// <summary>One curated spawn point for a <see cref="HuntingMonster"/>. Coordinates are FFXIV
/// "map coordinates" (~1.0-42.0, the flag/minimap scale) — convert via
/// <see cref="Navigation.MapCoords.MapToWorld"/> before feeding a world-space router.</summary>
public sealed class HuntingLocation
{
    public uint TerritoryTypeId { get; set; }

    public uint MapId { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>True only at index 0 of the owning <see cref="HuntingMonster"/>'s
    /// <see cref="HuntingMonster.Locations"/> — Hunty's own "the" location convention.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>False for the 25 Grand-Company-Elite targets that live inside an instanced duty
    /// (Hunty records these as x=0,y=0 — no usable overworld coordinate). When false,
    /// <see cref="DutyTerritoryTypeId"/> is set and the UI should offer "queue via Duty Finder"
    /// instead of a walking route.</summary>
    public bool Routable { get; set; }

    /// <summary>Set only when <see cref="Routable"/> is false: the duty's instance territory,
    /// resolved via <c>ContentFinderCondition</c> where <c>TerritoryType.RowId</c> equals this
    /// value to get the duty-finder key.</summary>
    public uint? DutyTerritoryTypeId { get; set; }
}
