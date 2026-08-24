namespace Wayfarer.Api.Dto;

/// <summary>Wire shape for a single row returned by the unlocks IPC gate, mirroring the
/// private ToolService.GetUnlocks row shape.</summary>
public sealed class UnlockRowDto
{
    public string Unlock { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? LockReason { get; init; }

    /// <summary>Set only when <c>Status</c> is <c>Available</c> and a knowable-but-unverifiable
    /// condition is still outstanding (a partner, or a future requirement of the same shape) — the
    /// terse note a consumer would put next to "Available", e.g. "needs a partner". Null for every
    /// ordinary Available row. Mirrors <see cref="Wayfarer.Core.Unlocks.ResolvedUnlock.AvailableCondition"/>.</summary>
    public string? AvailableCondition { get; init; }

    public string? Quest { get; init; }

    public string? Giver { get; init; }

    public int Level { get; init; }

    public string? Zone { get; init; }

    public string Priority { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string? Description { get; init; }
}
