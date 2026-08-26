namespace Wayfarer.Core.Unlocks;

/// <summary>Groups a computed <see cref="UnlockStatus"/> into the band its row is listed under, and
/// names the band.
///
/// <para><b>Why three bands and not thirteen sort keys.</b> The list previously ordered rows by
/// status with a thirteen-way sort and no headings, so a player scrolling a domain saw the
/// actionable rows run into the blocked ones with nothing marking the transition. The status word
/// was on each row, but "where does the part I can do end" is a question about the LIST, and only a
/// heading answers it.</para>
///
/// <para><b>Why "Not known" is a band and not a footnote.</b> Some entries have no quest, no reward
/// identity and no readable gate — the game states nothing a plugin can grade them on. Sorting
/// those in among the blocked ones implies Wayfarer checked and found a lock; dropping them implies
/// they do not exist; calling them available would be a straight lie. A band that says "not known"
/// is the only one of the four that is true, and it is deliberately visible rather than folded away,
/// because the size of what we cannot answer is itself something the player is owed.</para>
///
/// <para><b>Why Complete is here at all.</b> The player can turn finished entries back on, and when
/// they do those rows need a band. Last, because it is the one band with nothing to act on — and
/// separate, because putting a finished entry in <see cref="UnlockBand.Available"/> would make the
/// one band that promises "you can go and get this" the one band that also contains things you
/// already have.</para></summary>
public static class UnlockBands
{
    /// <summary>Bands in presentation order.</summary>
    public static IReadOnlyList<UnlockBand> All { get; } =
        [UnlockBand.Available, UnlockBand.Blocked, UnlockBand.NotKnown, UnlockBand.Complete];

    /// <summary>Which band a status is listed under. Total over the enum on purpose: a status added
    /// later lands in <see cref="UnlockBand.NotKnown"/>, which is the only honest default — an
    /// unrecognised state is by definition one this code cannot vouch for, so it must not be able to
    /// fall into <see cref="UnlockBand.Available"/>.</summary>
    public static UnlockBand Of(UnlockStatus status) => status switch
    {
        // In progress belongs with available rather than in a band of its own: the quest is taken,
        // the route to it is the route to finishing it, and it is the same "go and do this" answer.
        UnlockStatus.Available or UnlockStatus.Accepted => UnlockBand.Available,

        UnlockStatus.Done => UnlockBand.Complete,

        // Missed is a lock like any other from the list's point of view — the reason is on the row,
        // and "no longer obtainable" is as specific a reason as any gate gives.
        UnlockStatus.LevelLocked
            or UnlockStatus.QuestLocked
            or UnlockStatus.LockedOut
            or UnlockStatus.InstanceLocked
            or UnlockStatus.GrandCompanyLocked
            or UnlockStatus.BeastTribeLocked
            or UnlockStatus.MountLocked
            or UnlockStatus.CollectionLocked => UnlockBand.Blocked,

        _ => UnlockBand.NotKnown,
    };

    /// <summary>The heading the band is drawn under. <c>Not known</c> is spelled out rather than
    /// abbreviated to "Unknown", which reads as a property of the unlock; this is a statement about
    /// what Wayfarer knows.</summary>
    public static string Label(UnlockBand band) => band switch
    {
        UnlockBand.Available => "Available",
        UnlockBand.Blocked => "Blocked",
        UnlockBand.NotKnown => "Not known",
        _ => "Complete",
    };

    /// <summary>The one line under the heading saying what the band means, for the detail pane.
    /// Every band gets one — a heading whose meaning is obvious still needs the same affordance as
    /// one whose meaning is not, or the pane goes blank on some rows and not others.</summary>
    public static string Explanation(UnlockBand band) => band switch
    {
        UnlockBand.Available => "Every requirement Wayfarer can check is met. Route Me will walk these.",
        UnlockBand.Blocked => "Something specific is in the way. Each row says what it is.",

        // Two kinds of row land here and the sentence has to cover both, or it is false about
        // whichever it leaves out. Most are entries the game states nothing checkable about at all.
        // The rest are entries whose proof is request-gated — a title's, until the achievement table
        // arrives — where the game states plenty and Wayfarer has simply not read it yet. Each row
        // says which; this says that the band is the honest place for both.
        UnlockBand.NotKnown =>
            "Either the game states nothing Wayfarer can check these against, or the reading that "
            + "would settle them has not arrived yet. Each row says which. Listed rather than "
            + "hidden, and never reported as available.",
        _ => "Already unlocked on this character.",
    };

    /// <summary>Sort position, so a caller ordering groups does not depend on the enum's own
    /// numbering staying as written.</summary>
    public static int Rank(UnlockBand band)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i] == band)
            {
                return i;
            }
        }

        return All.Count;
    }
}
