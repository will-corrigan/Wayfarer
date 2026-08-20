namespace Wayfarer.Core.Unlocks;

public sealed class UnlockDefinition
{
    public int Level { get; set; }

    public string Unlock { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string? Quest { get; set; }

    public string? QuestKind { get; set; }

    public string? Notes { get; set; }

    public string? Description { get; set; }

    public string Priority { get; set; } = "nice";

    public bool Cosmetic { get; set; }
}
