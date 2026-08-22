namespace Wayfarer.Core.Hunting;

/// <summary>Hardcoded ClassJob → <c>MonsterNoteManager</c> slot table (0-11). NOT derivable from
/// Lumina — mirrors Hunty's <c>JobInMemory</c> table. Base-class slots verified against live
/// <c>MonsterNote</c> sheet data 2026-08-22 (RowId = ClassJob.RowId*10000 + overall task number
/// resolves correctly for all 9 for these ids; see task-C1-report.md). Evolved-job pairings
/// verified the same day via <c>ClassJob.MonsterNote</c> (a reward-catalog FK): every evolved
/// job below shares its base class's <c>MonsterNote</c> row (MNK/PGL, WAR/MRD, DRG/LNC, BRD/ARC,
/// WHM/CNJ, BLM/THM, SMN+SCH/ACN, NIN/ROG) except GLA/PLD, which both read the FK as unset
/// (RowId 0) — that pairing rests on standard, invariant FFXIV job-list knowledge rather than an
/// independent sheet cross-check. Post-Stormblood jobs (MCH, DRK, AST, SAM, RDM, BLU, GNB, DNC,
/// RPR, SGE, VPR, PCT, BST) confirmed to have no class hunting log: every one of them reads
/// <c>ClassJob.MonsterNote</c> as the invalid-RowRef sentinel.</summary>
public static class HuntingSlotTable
{
    public const int EliteSlotMaelstrom = 8;
    public const int EliteSlotTwinAdder = 9;
    public const int EliteSlotImmortalFlames = 10;

    private static readonly Dictionary<uint, int> BaseClassSlots = new()
    {
        [1] = 0,   // GLA
        [2] = 1,   // PGL
        [3] = 2,   // MRD
        [4] = 3,   // LNC
        [5] = 4,   // ARC
        [6] = 5,   // CNJ
        [7] = 6,   // THM
        [26] = 7,  // ACN
        [29] = 11, // ROG
    };

    /// <summary>Evolved job ClassJobId → its base class's ClassJobId.</summary>
    private static readonly Dictionary<uint, uint> EvolvedToBaseClass = new()
    {
        [19] = 1,  // PLD <- GLA
        [20] = 2,  // MNK <- PGL
        [21] = 3,  // WAR <- MRD
        [22] = 4,  // DRG <- LNC
        [23] = 5,  // BRD <- ARC
        [24] = 6,  // WHM <- CNJ
        [25] = 7,  // BLM <- THM
        [27] = 26, // SMN <- ACN
        [28] = 26, // SCH <- ACN
        [30] = 29, // NIN <- ROG
    };

    /// <summary>Slot 0-11 for the given ClassJobId's class hunting log, or <see langword="null"/>
    /// if this job has none (a post-Stormblood job — the module should fall back to offering the
    /// Elite logs via <see cref="EliteSlotForGrandCompany"/>).</summary>
    public static int? SlotForClassJob(uint classJobId)
    {
        if (BaseClassSlots.TryGetValue(classJobId, out var slot))
        {
            return slot;
        }

        return EvolvedToBaseClass.TryGetValue(classJobId, out var baseClassJobId)
            ? BaseClassSlots[baseClassJobId]
            : null;
    }

    /// <summary>Resolves <paramref name="classJobId"/> to the ClassJobId whose hunting-log dataset
    /// key should be used to look up a class log: the job itself if it already IS a base class (or
    /// has no class log at all), or its base class if it's one of the ten evolved jobs. This is the
    /// single source of truth for that mapping — <see cref="HuntingLogService.ResolveActiveLog"/>
    /// must derive its dataset <c>jobKey</c> from this, not from the raw <paramref name="classJobId"/>,
    /// since <c>data/hunting-targets.json</c> only has base-class keys (see that method's doc
    /// comment for the bug this fixes).</summary>
    public static uint BaseClassFor(uint classJobId) =>
        EvolvedToBaseClass.TryGetValue(classJobId, out var baseClassJobId) ? baseClassJobId : classJobId;

    /// <summary>Slot 8-10 for a Grand Company's shared Elite log. <paramref name="grandCompanyId"/>
    /// is the GrandCompany sheet RowId (1-3) — the same convention as this data file's synthetic
    /// <c>10000+grandCompanyId</c> jobKeys.</summary>
    public static int EliteSlotForGrandCompany(uint grandCompanyId) => grandCompanyId switch
    {
        1 => EliteSlotMaelstrom,
        2 => EliteSlotTwinAdder,
        3 => EliteSlotImmortalFlames,
        _ => throw new ArgumentOutOfRangeException(nameof(grandCompanyId), grandCompanyId, "Grand Company id must be 1-3"),
    };
}
