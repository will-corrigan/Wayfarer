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

    /// <summary>Which of the seven domains this row belongs to — <c>duties</c>, <c>capabilities</c>,
    /// <c>collection</c>, <c>titles</c>, <c>logs</c>, <c>jobs</c>, <c>travel</c>. Empty when the
    /// entry's channel maps to no domain, which the shipped catalogue is asserted never to be in.
    ///
    /// <para>The four values this used to carry (<c>content</c>, <c>system</c>, <c>cosmetic</c>,
    /// <c>zone</c>) were read off the catalogue's <c>type</c> field, and at 1,208 entries they stopped
    /// telling a consumer anything: everything the nine <c>type</c> values had no word for came out
    /// <c>cosmetic</c> or <c>system</c>. <see cref="Channel"/> is beside it for consumers that want
    /// the fine-grained answer rather than the grouping.</para></summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>The catalogue's own <c>channel</c> — the enumeration's vocabulary for what kind of
    /// thing this is (<c>duty</c>, <c>title</c>, <c>orchestrion</c>, <c>gathering-folklore</c>, …).
    /// Finer than <see cref="Category"/> and the field it is derived from.</summary>
    public string Channel { get; init; } = string.Empty;

    public string? Description { get; init; }
}
