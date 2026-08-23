namespace Wayfarer.Core.Unlocks;

/// <summary>A catalogue entry's curated <c>requires</c> block: the requirements the game does not
/// write down anywhere a plugin can read.
///
/// <para>Quest #67086 ("Fiery Wings, Fiery Hearts", which grants the Firebird mount) has every
/// gate column empty and a recorded level of 1, yet it cannot be accepted without all seven
/// Heavensward Extreme-trial Lanner mounts. That condition lives only in the quest's server-side
/// accept script: an exhaustive scan of every sheet with a Quest reference finds nothing.
/// Curation is the only place such a requirement can exist — and once curated, every part of it
/// (mounts, minions, items, job levels) is checkable live.</para>
///
/// <para>All populated fields are AND-ed. <see cref="Unverifiable"/> is the honest fallback: the
/// requirement is known to exist but cannot be expressed, so the entry reports what it needs and
/// never claims to be available.</para></summary>
public sealed class UnlockRequirement
{
    /// <summary>Human phrasing of the whole requirement, shown when the individual checks can't
    /// be run and used as the summary when several are missing.</summary>
    public string? Label { get; set; }

    /// <summary>The requirement is real but not expressible as any of the lists below. Forces
    /// "requirements unknown" — never Available.</summary>
    public bool Unverifiable { get; set; }

    /// <summary>A character level the entry needs that the Quest sheet doesn't state plainly.
    /// Curated as a second source for the two Bozjan-front entries, whose real level (80) the
    /// sheet splits across <c>ClassJobLevel[0]</c> (71) and <c>QuestLevelOffset</c> (9).</summary>
    public int? MinLevel { get; set; }

    public List<UnlockRequirement.Collectible> Mounts { get; set; } = [];

    public List<UnlockRequirement.Collectible> Minions { get; set; } = [];

    public List<UnlockRequirement.RequiredItem> Items { get; set; } = [];

    public List<UnlockRequirement.RequiredJob> Jobs { get; set; } = [];

    /// <summary>Duties that must have been CLEARED, not merely unlocked. These come from entries
    /// the guide gates on a duty rather than on a quest — the Unreal and Criterion families, whose
    /// catalogue "quest" was a Wandering Minstrel dialogue label that is not a quest at all. The
    /// clear is checkable; whether the player has since taken the unlock itself is not, so these
    /// entries keep <see cref="Unverifiable"/> and gain a real reason instead of a shrug.</summary>
    public List<UnlockRequirement.Collectible> Duties { get; set; } = [];

    /// <summary>True when there is something concrete to check, as opposed to a block that only
    /// carries prose.</summary>
    public bool HasCheckableRequirement =>
        Mounts.Count > 0 || Minions.Count > 0 || Items.Count > 0 || Jobs.Count > 0
        || Duties.Count > 0 || MinLevel is > 0;

    /// <summary>A mount or minion the player must already own.</summary>
    /// <param name="Id">Mount/Companion sheet row id, checked with <c>IsMountUnlocked</c> /
    /// <c>IsCompanionUnlocked</c>.</param>
    /// <param name="Name">What to call it to the player.</param>
    /// <param name="From">Where it comes from, so the player knows what to go and do.</param>
    public sealed record Collectible(uint Id, string Name, string? From)
    {
        public Collectible()
            : this(0, string.Empty, null)
        {
        }
    }

    /// <summary>An item the player must already be carrying.</summary>
    /// <param name="Id">Item sheet row id.</param>
    /// <param name="Name">What to call it to the player.</param>
    /// <param name="Count">How many are needed; 0 and 1 both mean one.</param>
    /// <param name="KeyItem">Look in the key items container rather than the bags — key items are
    /// always resident, ordinary items in a retainer are not.</param>
    public sealed record RequiredItem(uint Id, string Name, int Count, bool KeyItem)
    {
        public RequiredItem()
            : this(0, string.Empty, 1, false)
        {
        }
    }

    /// <summary>A specific job the player must have levelled, independent of whichever job the
    /// quest's own category mask lets accept it.</summary>
    /// <param name="Id">ClassJob sheet row id.</param>
    /// <param name="Name">What to call it to the player.</param>
    /// <param name="Level">The level that job must be at.</param>
    public sealed record RequiredJob(uint Id, string Name, int Level)
    {
        public RequiredJob()
            : this(0, string.Empty, 1)
        {
        }
    }
}
