namespace Wayfarer.Api.Dto;

/// <summary>Wire shape for a single row returned by the unlocks IPC gate, mirroring the
/// private ToolService.GetUnlocks row shape.</summary>
public sealed class UnlockRowDto
{
    public string Unlock { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? LockReason { get; init; }

    public string? Quest { get; init; }

    public string? Giver { get; init; }

    public int Level { get; init; }

    public string? Zone { get; init; }

    public string Priority { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string? Description { get; init; }
}
