namespace Wayfarer.Core.Hunting;

/// <summary>One hunting-log task (a single kill-count line within a <see cref="HuntingRank"/>
/// page). <see cref="TaskIndex"/> is positional and load-bearing — see
/// <see cref="HuntingMonster"/>.</summary>
public sealed class HuntingTask
{
    public int TaskIndex { get; set; }

    /// <summary>Hunty's own label (e.g. "Gladiator 01") — documentary/debug only, not
    /// sheet-derived. Do not surface verbatim in the UI.</summary>
    public string Label { get; set; } = string.Empty;

    public List<HuntingMonster> Monsters { get; set; } = [];
}
