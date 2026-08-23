using System.Runtime.InteropServices;

namespace Wayfarer.Core.Unlocks;

/// <summary>The facts that decide which Quest row a duplicated name binds to. The sheet ships 69
/// duplicate names across 81 rows, and they are not interchangeable: for three of them the lowest
/// row id is a retired pre-6.1 row that no live character can ever complete (#66060
/// <c>The Ultimate Weapon</c>, #66672 <c>Rock the Castrum</c>, #66988 <c>Levin an Impression</c>),
/// so the old "first row wins" bound five catalogue entries to dead rows and told players who
/// finished A Realm Reborn years ago that they had not.</summary>
/// <param name="RowId">The Quest sheet row id.</param>
/// <param name="JournalGenreRowId">Zero when the row has no journal entry. Retired rows are
/// stripped of their genre; a live row keeps it. Not decisive on its own — 12 legitimately hidden
/// system quests also have none — but decisive when a same-named sibling does have one.</param>
/// <param name="InboundPrereqReferences">How many other quests name this row in their
/// <c>PreviousQuest</c> list. The live prerequisite graph walks through the live row: #70058 is
/// reached from Heavensward, #66060 from nothing that leads anywhere.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct QuestNameCandidate(uint RowId, uint JournalGenreRowId, int InboundPrereqReferences)
{
    /// <summary>Ranks candidates best-first: in the journal before not, then most depended upon,
    /// then lowest row id purely so the order is total and stable.</summary>
    public static int Compare(QuestNameCandidate a, QuestNameCandidate b)
    {
        var byGenre = (b.JournalGenreRowId != 0).CompareTo(a.JournalGenreRowId != 0);
        if (byGenre != 0)
        {
            return byGenre;
        }

        var byRefs = b.InboundPrereqReferences.CompareTo(a.InboundPrereqReferences);
        return byRefs != 0 ? byRefs : a.RowId.CompareTo(b.RowId);
    }

    /// <summary>True when two candidates are indistinguishable on the evidence — same journal
    /// presence, same inbound dependency count. The three <c>Simply the Hest</c> rows (one per
    /// starting city) tie here, and a character completes exactly one of them depending on where
    /// they started: no tiebreak can pick correctly, so the caller must treat the group as
    /// alternatives rather than choosing.</summary>
    public static bool Indistinguishable(QuestNameCandidate a, QuestNameCandidate b) =>
        (a.JournalGenreRowId != 0) == (b.JournalGenreRowId != 0)
        && a.InboundPrereqReferences == b.InboundPrereqReferences;
}
