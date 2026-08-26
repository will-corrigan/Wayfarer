using Wayfarer.Core.Unlocks.Gates;

namespace Wayfarer.Core.Unlocks;

/// <summary>What the checklist says about one entry. Everything except <see cref="Available"/>
/// and <see cref="Done"/> is a specific reason the player is not going anywhere yet, and the
/// several flavours of "unknown" are deliberately distinct: they are the difference between a gate
/// this plugin can see and one it merely suspects.
///
/// <para>There is no status for "known but unverifiable" (a partner, or a future requirement of the
/// same shape) — that is not a reason to withhold Available, it is a fact to state alongside it.
/// An entry with every checkable gate met reports <see cref="Available"/> and carries the condition
/// in <see cref="ResolvedUnlock.AvailableCondition"/> / <see cref="ResolvedUnlock.AvailableConditionDetail"/>
/// instead. That keeps it distinct from <see cref="RequirementsUnknown"/>, which means the opposite
/// thing: "we cannot even say what this needs", not "we cannot see whether you meet the stated
/// condition".</para></summary>
public enum UnlockStatus
{
    Unverified,
    Done,
    Accepted,
    Available,
    LevelLocked,
    QuestLocked,

    /// <summary>Permanently unobtainable: a <c>QuestLock</c> quest was completed.</summary>
    LockedOut,

    /// <summary>Gated behind an <c>InstanceContent</c> entry that isn't cleared (or isn't
    /// even unlocked yet).</summary>
    InstanceLocked,

    /// <summary>Gated behind Grand Company membership and/or rank.</summary>
    GrandCompanyLocked,

    /// <summary>Gated behind beast tribe reputation rank.</summary>
    BeastTribeLocked,

    /// <summary>Gated behind an unlocked mount.</summary>
    MountLocked,

    /// <summary>Gated behind a requirement this plugin doesn't model (festival window,
    /// personal house ownership, ...). Never reported as Available.</summary>
    UnknownGate,

    /// <summary>Gated behind owning a curated set of collectibles — Extreme-trial trophy mounts,
    /// minions, key items — that the Quest sheet does not record. The player is told exactly what
    /// is missing; see <see cref="UnlockDefinition.Requires"/>.</summary>
    CollectionLocked,

    /// <summary>The plugin has a quest row for this entry but cannot establish what it takes to
    /// get it: the row carries no gate at all and nothing is curated, or several rows share the
    /// name and only the character knows which is theirs, or a requirement id doesn't resolve.
    /// This is the honest answer, and it exists because "I found no gate" was previously reported
    /// as "go and get it".</summary>
    RequirementsUnknown,
}

/// <summary>An unlock entry after the plugin has matched it against game data.
/// Core computes Status/LockReason; the plugin fills everything else.</summary>
public sealed class ResolvedUnlock
{
    public UnlockDefinition Def { get; set; } = new();

    /// <summary>A gate that reads the entry's OWN identity rather than a prerequisite for it —
    /// "is this duty open to you", asked of the very duty the entry is about.
    ///
    /// <para>This is what closed the largest class of "requirements unknown" in the catalogue. A
    /// third of the ungradeable entries were duty access: the guide says the Ultimate opens after
    /// clearing the Savage tier, the clear is readable, but whether the player then went and took
    /// the unlock was recorded as unknowable. It is not — the client keeps a bit per duty saying
    /// exactly that, and this is it. Satisfied means the player already has the thing, which is
    /// <see cref="UnlockStatus.Done"/>; blocked means they demonstrably do not, which is what lets
    /// the rest of the chain grade the entry instead of shrugging.</para>
    ///
    /// <para>Derived from <see cref="UnlockDefinition.Reward"/> by the host at load time, generically
    /// from the reward's sheet kind. Nothing here or downstream knows which entry it belongs to.</para></summary>
    public GateNode? IdentityGate { get; set; }

    public uint? QuestRowId { get; set; }

    /// <summary>Every Quest row the catalogue's name could equally well mean, when the game ships
    /// several the evidence can't separate — the three <c>Simply the Hest</c> rows, one per
    /// starting city, are the canonical case. Empty when the match was unambiguous.
    /// <see cref="QuestRowId"/> is one of these; completing any of them completes the unlock, and
    /// when none is complete the plugin does not know which one this character was given.</summary>
    public List<uint> AlternativeQuestRowIds { get; set; } = [];

    public int QuestLevel { get; set; }

    public List<uint> PrereqRowIds { get; set; } = [];

    public List<string> PrereqNames { get; set; } = [];

    /// <summary><c>PreviousQuestJoin</c>: 1 = AND (every prereq must be complete), 2 = OR (any
    /// one suffices). Any other value (including the unset default 0) behaves as AND.</summary>
    public byte PrereqJoin { get; set; }

    /// <summary><c>QuestLock</c>: quests whose completion permanently locks this one out.</summary>
    public List<uint> LockoutQuestRowIds { get; set; } = [];

    public List<string> LockoutQuestNames { get; set; } = [];

    /// <summary><c>QuestLockJoin</c>: 1 = AND (every listed quest must be complete to lock out),
    /// 2 = OR (any one suffices). Only 2 has been observed in game data.</summary>
    public byte LockoutJoin { get; set; }

    /// <summary>ClassJob row ids allowed by <c>ClassJobCategory0</c>, checked against
    /// <see cref="QuestLevel"/> (<c>ClassJobLevel[0]</c>). Empty means unrestricted — any job
    /// qualifies and <see cref="QuestLevel"/> is checked against the player's currently active
    /// job instead. This is the primary, always-real job/level gate.</summary>
    public List<uint> RequiredJobRowIds { get; set; } = [];

    public List<string> RequiredJobNames { get; set; } = [];

    /// <summary>The <c>ClassJobCategory0</c> row's own <c>Name</c> — "Disciple of War or Magic",
    /// "Disciple of the Land", or a single job's name on a job quest.
    ///
    /// <para>This is what the gate is <b>called</b>, as against
    /// <see cref="RequiredJobNames"/>, which is what it is <b>made of</b>. The game prints the
    /// name; printing the members instead is how a level-70 combat gate came to be said as a
    /// thirty-job sentence. See <see cref="JobGateText"/>.</para></summary>
    public string? RequiredJobCategoryName { get; set; }

    /// <summary>ClassJob row ids allowed by <c>ClassJobCategory1</c> — a genuine alternative to
    /// <see cref="RequiredJobRowIds"/>, checked against <see cref="AltRequiredJobLevel"/>
    /// (<c>ClassJobLevel[1]</c>). Populated only when <see cref="AltRequiredJobLevel"/> is
    /// nonzero: the game reuses <c>ClassJobCategory1</c> as an "every job" sentinel mask on
    /// ordinary single-category quests, always paired with <c>ClassJobLevel[1] == 0</c> — that
    /// sentinel must never be treated as an eligible job set, or every job-restricted quest that
    /// carries it becomes wrongly available to every job.</summary>
    public List<uint> AltRequiredJobRowIds { get; set; } = [];

    public List<string> AltRequiredJobNames { get; set; } = [];

    /// <inheritdoc cref="RequiredJobCategoryName"/>
    public string? AltRequiredJobCategoryName { get; set; }

    /// <summary><c>ClassJobLevel[1]</c>. Zero means <see cref="AltRequiredJobRowIds"/> is empty/
    /// unused — there is no genuine category1 alternative for this quest.</summary>
    public int AltRequiredJobLevel { get; set; }

    /// <summary><c>InstanceContent</c> row ids this quest requires progress on.</summary>
    public List<uint> InstanceContentRowIds { get; set; } = [];

    public List<string> InstanceContentNames { get; set; } = [];

    /// <summary><c>InstanceContentJoin</c>: 1 = AND (every listed duty must be cleared), anything
    /// else = OR (any one suffices).</summary>
    public byte InstanceContentJoin { get; set; }

    public uint? RequiredGrandCompanyId { get; set; }

    public string? RequiredGrandCompanyName { get; set; }

    public uint? RequiredGrandCompanyRank { get; set; }

    public byte? RequiredBeastTribeId { get; set; }

    public string? RequiredBeastTribeName { get; set; }

    public uint? RequiredBeastTribeRank { get; set; }

    public string? RequiredBeastTribeRankName { get; set; }

    public uint? RequiredMountId { get; set; }

    public string? RequiredMountName { get; set; }

    /// <summary>True when the sheet carries a gate this plugin doesn't model (Festival window,
    /// IsHouseRequired, ...). Forces <see cref="UnlockStatus.UnknownGate"/> instead of a false
    /// Available.</summary>
    public bool HasUnmodeledGate { get; set; }

    /// <summary><c>ClassJobRequired</c>: one job that must be levelled to <see cref="QuestLevel"/>
    /// whatever the <c>ClassJobCategory0</c> mask allows. Only one catalogue entry uses it
    /// (Spearfishing needs Fisher), and ignoring it showed that entry as available to a character
    /// who had never touched Fisher.</summary>
    public uint? HardRequiredJobRowId { get; set; }

    public string? HardRequiredJobName { get; set; }

    /// <summary><c>QuestAcceptAdditionCondition</c>: extra accept-time prerequisite quests that
    /// live in their own sheet rather than in <c>PreviousQuest</c>. Always AND — the sheet has no
    /// join column and none of its 58 rows imply otherwise.</summary>
    public List<uint> AcceptConditionQuestRowIds { get; set; } = [];

    public List<string> AcceptConditionQuestNames { get; set; } = [];

    /// <summary>True when an accept condition names an id that isn't a Quest row. That is an
    /// unknown requirement, not an absent one.</summary>
    public bool HasUnresolvedAcceptCondition { get; set; }

    /// <summary>True when the matched Quest row records no requirement of any kind — no level
    /// above 1, no prerequisite, no duty, no job, nothing. Quest row 67086 looks exactly like
    /// this and still needs seven Extreme-trial mounts, because its real condition lives in a
    /// server-side accept script. An entry in this state must never be called available on the
    /// strength of the absence: without curated <see cref="UnlockDefinition.Requires"/> data it
    /// reports <see cref="UnlockStatus.RequirementsUnknown"/>.</summary>
    public bool HasNoDiscoverableGate { get; set; }

    /// <summary>Everything a curated requirement found missing, in order, phrased for the player
    /// ("Rose Lanner — Thok ast Thok (Extreme)"). The lock reason names the first; the window
    /// shows this whole list on demand, which is the entire point of curating it.</summary>
    public List<string> MissingRequirements { get; set; } = [];

    public uint? GiverTerritory { get; set; }

    public uint? GiverMap { get; set; }

    public float GiverX { get; set; }

    public float GiverY { get; set; }

    public float GiverZ { get; set; }

    public string? ZoneName { get; set; }

    /// <summary>The bound quest's <c>Expansion</c> name — "A Realm Reborn", "Shadowbringers" — or
    /// null when no quest is bound. Read for one purpose: telling apart the entries that share a
    /// name. See <see cref="UnlockDisambiguation"/>.</summary>
    public string? QuestExpansion { get; set; }

    /// <summary>The bound quest's own <c>PlaceName</c> — the journal's place for it, which is the
    /// city for the per-city unlocks. Distinct from <see cref="ZoneName"/>, which is the territory the
    /// GIVER stands in: the two usually agree, and the quest's own is the one the game prints in the
    /// Journal, so it is the one a qualifier should read.</summary>
    public string? QuestPlaceName { get; set; }

    /// <summary>What tells this entry apart from the others of the same name — an expansion or a
    /// city, taken from its own bound quest. Null on all but thirty-five of the catalogue's entries,
    /// and on any of those that nothing on the quest distinguishes.
    ///
    /// <para>Computed by <see cref="UnlockDisambiguation.Apply"/> rather than carried in the data,
    /// because whether a name needs qualifying is a property of the catalogue as a whole and the
    /// qualifier itself lives in the game's own sheets, already localised.</para></summary>
    public string? Qualifier { get; set; }

    /// <summary>The game's own sentence about this unlock, read at load from the sheet cell
    /// <see cref="UnlockDefinition.DescriptionSource"/> names — or null when the entry cites none, or
    /// when the cell could not be read against the installed patch.
    ///
    /// <para>Here rather than on the definition because the definition is what the committed file
    /// says and this is what the running client says. It is resolved once, at load, in the player's
    /// own client language; see <see cref="GameTextRef"/> for why the reference and not the text is
    /// what gets committed, and <c>Wayfarer.Core/Ui/UnlockRowText.Description</c> for where it sits in
    /// the fallback order.</para></summary>
    public string? GameDescription { get; set; }

    /// <summary><c>IssuerStart</c> resolved against the ENpcResident sheet's
    /// <c>Singular</c> name. Null when the issuer isn't an ENpcResident (some quests are
    /// issued by objects/eobjects) or has no name — degrades silently, no logging.</summary>
    public string? GiverName { get; set; }

    public UnlockStatus Status { get; set; }

    public string? LockReason { get; set; }

    /// <summary>Set only when <see cref="Status"/> is <see cref="UnlockStatus.Available"/> and the
    /// curated requirement still carries a knowable-but-unverifiable condition (see
    /// <see cref="UnlockRequirement.RequiresAnotherPlayer"/>) — a short, terse phrase in the game's
    /// own register ("needs a partner"), for the list row: "Available — needs a partner." Null for
    /// every ordinary Available entry, where nothing is left to say.</summary>
    public string? AvailableCondition { get; set; }

    /// <summary>The full statement of <see cref="AvailableCondition"/>, for the detail pane /
    /// journal page where the requirement list lives. Preferably the game's own words, resolved at
    /// runtime through <see cref="UnlockGateContext.ResolveGameText"/> from
    /// <see cref="UnlockRequirement.ConditionSource"/>; falls back to the curated
    /// <see cref="UnlockRequirement.Label"/> when that lookup misses, and to a plain admission that
    /// the game does not say more when even that is absent. Null whenever
    /// <see cref="AvailableCondition"/> is.</summary>
    public string? AvailableConditionDetail { get; set; }

    /// <summary>Member-wise copy for cross-thread hand-off (e.g. MCP serialization while
    /// the framework thread may concurrently call <c>UnlockStatusCalculator.Compute</c> on
    /// the live instance). <see cref="Def"/> and the gate lists are shared, not deep-copied —
    /// all are immutable after load; every scalar (including Status and LockReason) is copied
    /// by value so mutations to the original never show up here.</summary>
    public ResolvedUnlock Snapshot() => new()
    {
        Def = Def,
        IdentityGate = IdentityGate,
        QuestRowId = QuestRowId,
        AlternativeQuestRowIds = AlternativeQuestRowIds,
        QuestLevel = QuestLevel,
        PrereqRowIds = PrereqRowIds,
        PrereqNames = PrereqNames,
        PrereqJoin = PrereqJoin,
        LockoutQuestRowIds = LockoutQuestRowIds,
        LockoutQuestNames = LockoutQuestNames,
        LockoutJoin = LockoutJoin,
        RequiredJobRowIds = RequiredJobRowIds,
        RequiredJobNames = RequiredJobNames,
        RequiredJobCategoryName = RequiredJobCategoryName,
        AltRequiredJobRowIds = AltRequiredJobRowIds,
        AltRequiredJobNames = AltRequiredJobNames,
        AltRequiredJobCategoryName = AltRequiredJobCategoryName,
        AltRequiredJobLevel = AltRequiredJobLevel,
        InstanceContentRowIds = InstanceContentRowIds,
        InstanceContentNames = InstanceContentNames,
        InstanceContentJoin = InstanceContentJoin,
        RequiredGrandCompanyId = RequiredGrandCompanyId,
        RequiredGrandCompanyName = RequiredGrandCompanyName,
        RequiredGrandCompanyRank = RequiredGrandCompanyRank,
        RequiredBeastTribeId = RequiredBeastTribeId,
        RequiredBeastTribeName = RequiredBeastTribeName,
        RequiredBeastTribeRank = RequiredBeastTribeRank,
        RequiredBeastTribeRankName = RequiredBeastTribeRankName,
        RequiredMountId = RequiredMountId,
        RequiredMountName = RequiredMountName,
        HasUnmodeledGate = HasUnmodeledGate,
        HardRequiredJobRowId = HardRequiredJobRowId,
        HardRequiredJobName = HardRequiredJobName,
        AcceptConditionQuestRowIds = AcceptConditionQuestRowIds,
        AcceptConditionQuestNames = AcceptConditionQuestNames,
        HasUnresolvedAcceptCondition = HasUnresolvedAcceptCondition,
        HasNoDiscoverableGate = HasNoDiscoverableGate,
        MissingRequirements = MissingRequirements,
        GiverTerritory = GiverTerritory,
        GiverMap = GiverMap,
        GiverX = GiverX,
        GiverY = GiverY,
        GiverZ = GiverZ,
        ZoneName = ZoneName,
        GameDescription = GameDescription,
        GiverName = GiverName,
        Status = Status,
        LockReason = LockReason,
        AvailableCondition = AvailableCondition,
        AvailableConditionDetail = AvailableConditionDetail,
    };
}
