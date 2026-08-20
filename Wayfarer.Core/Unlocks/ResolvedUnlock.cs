namespace Wayfarer.Core.Unlocks;

public enum UnlockStatus
{
    Unverified,
    Done,
    Accepted,
    Available,
    LevelLocked,
    QuestLocked,
}

/// <summary>An unlock entry after the plugin has matched it against game data.
/// Core computes Status/LockReason; the plugin fills everything else.</summary>
public sealed class ResolvedUnlock
{
    public UnlockDefinition Def { get; set; } = new();

    public uint? QuestRowId { get; set; }

    public int QuestLevel { get; set; }

    public List<uint> PrereqRowIds { get; set; } = [];

    public List<string> PrereqNames { get; set; } = [];

    public uint? GiverTerritory { get; set; }

    public uint? GiverMap { get; set; }

    public float GiverX { get; set; }

    public float GiverY { get; set; }

    public float GiverZ { get; set; }

    public string? ZoneName { get; set; }

    public UnlockStatus Status { get; set; }

    public string? LockReason { get; set; }

    /// <summary>Member-wise copy for cross-thread hand-off (e.g. MCP serialization while
    /// the framework thread may concurrently call <c>UnlockStatusCalculator.Compute</c> on
    /// the live instance). <see cref="Def"/> and the two prereq lists are shared, not
    /// deep-copied — both are immutable after load; every scalar (including Status and
    /// LockReason) is copied by value so mutations to the original never show up here.</summary>
    public ResolvedUnlock Snapshot() => new()
    {
        Def = Def,
        QuestRowId = QuestRowId,
        QuestLevel = QuestLevel,
        PrereqRowIds = PrereqRowIds,
        PrereqNames = PrereqNames,
        GiverTerritory = GiverTerritory,
        GiverMap = GiverMap,
        GiverX = GiverX,
        GiverY = GiverY,
        GiverZ = GiverZ,
        ZoneName = ZoneName,
        Status = Status,
        LockReason = LockReason,
    };
}
