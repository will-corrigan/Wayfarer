namespace Wayfarer.Core.Hunting;

/// <summary>One hunting-log target within a <see cref="HuntingTask"/>. <see cref="MonsterIndex"/>
/// is positional and load-bearing: live kill counts are read from
/// <c>MonsterNoteManager</c>'s per-task <c>Counts[]</c> array by this index, not by
/// <see cref="BNpcNameId"/> — do not re-sort <see cref="HuntingTask.Monsters"/>.</summary>
public sealed class HuntingMonster
{
    public int MonsterIndex { get; set; }

    /// <summary>FK into the BNpcName sheet — resolve display name/icon from Lumina at load,
    /// never carried in this data file. Also what live in-zone tracking (IObjectTable,
    /// <c>ObjectKind.BattleNpc</c>, <c>DataId == BNpcNameId</c>) matches against.</summary>
    public uint BNpcNameId { get; set; }

    /// <summary>Curated by Hunty; provisional pending a live-sheet-derived count per the design
    /// spec — treat as a fallback, not authoritative, if a sheet-derived count becomes
    /// available.</summary>
    public int RequiredKills { get; set; }

    public List<HuntingLocation> Locations { get; set; } = [];
}
