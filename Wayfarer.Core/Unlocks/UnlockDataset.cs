using System.Text.Json;

namespace Wayfarer.Core.Unlocks;

public static class UnlockDataset
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static List<UnlockDefinition> Parse(string json) =>
        JsonSerializer.Deserialize<Root>(json, Options)?.Unlocks
        ?? throw new JsonException("unlocks dataset: empty document");

    private sealed class Root
    {
        public List<UnlockDefinition> Unlocks { get; set; } = [];
    }
}
