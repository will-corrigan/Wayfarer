using System.Runtime.InteropServices;

namespace Wayfarer.Core.Unlocks;

/// <summary>A resolved name lookup: the row the matcher binds, plus every equally-plausible
/// alternative when the evidence cannot separate them.</summary>
/// <param name="Best">The row to read quest facts from.</param>
/// <param name="Alternatives">Every row (including <see cref="Best"/>) that ties with it, empty
/// when the choice was unambiguous. Completing any one of these counts as completing the unlock;
/// when none is complete, the plugin does not know which one this character was given and must
/// say so rather than guess.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct QuestNameMatch(QuestNameCandidate Best, IReadOnlyList<uint> Alternatives)
{
    public bool IsAmbiguous => Alternatives.Count > 1;

    /// <summary>Picks from every row sharing a key.</summary>
    public static QuestNameMatch Resolve(IReadOnlyList<QuestNameCandidate> candidates)
    {
        if (candidates.Count == 1)
        {
            return new(candidates[0], []);
        }

        var ordered = new List<QuestNameCandidate>(candidates);
        ordered.Sort(QuestNameCandidate.Compare);
        var best = ordered[0];
        var tied = new List<uint>();
        foreach (var c in ordered)
        {
            if (QuestNameCandidate.Indistinguishable(best, c))
            {
                tied.Add(c.RowId);
            }
        }

        return new(best, tied.Count > 1 ? tied : []);
    }
}
