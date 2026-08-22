namespace Wayfarer.Core.Hunting;

/// <summary>One hunting log — either a base class's 5-rank class log or one of the 3 shared
/// Grand Company Elite logs (3 ranks each). Keyed in <see cref="HuntingDataset.Logs"/> by
/// <see cref="ClassJobId"/> for the former, or a synthetic <c>10000 + GrandCompanyId</c> string
/// for the latter (mirrors Hunty's own convention).</summary>
public sealed class HuntingLog
{
    /// <summary>"classJob" or "grandCompanyElite".</summary>
    public string Kind { get; set; } = string.Empty;

    public uint? ClassJobId { get; set; }

    public uint? GrandCompanyId { get; set; }

    /// <summary>Documentary only, not authoritative — resolve display names from sheets.</summary>
    public string Label { get; set; } = string.Empty;

    public List<HuntingRank> Ranks { get; set; } = [];
}
