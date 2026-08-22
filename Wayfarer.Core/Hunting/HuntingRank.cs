namespace Wayfarer.Core.Hunting;

/// <summary>One rank page (1-based) of a <see cref="HuntingLog"/> — array position is
/// load-bearing, mirroring <c>MonsterNoteManager</c> rank-page indexing (see
/// <see cref="HuntingProgress.PageState"/>).</summary>
public sealed class HuntingRank
{
    public int Rank { get; set; }

    public List<HuntingTask> Tasks { get; set; } = [];
}
