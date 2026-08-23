using System.Text.Json;

namespace Wayfarer.Core.Unlocks;

public static class UnlockDataset
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Reads the catalogue, or throws with enough detail to fix it.
    ///
    /// <para>A single value of the wrong JSON kind — <c>"keyItem": "yes"</c> — throws here and
    /// disables the entire unlocks feature. The raw exception says only that a string could not
    /// become a boolean, so it is rewrapped with the JSON path of the value that did it: whoever
    /// reads the log gets the entry, not a puzzle. <c>data/validate-unlocks.mjs</c> is the fence
    /// that stops such a value being committed in the first place; this is what happens if it is
    /// reached anyway.</para></summary>
    public static List<UnlockDefinition> Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Root>(json, Options)?.Unlocks
                ?? throw new JsonException("unlocks dataset: empty document");
        }
        catch (JsonException ex) when (ex.Path is { Length: > 0 })
        {
            throw new JsonException($"unlocks dataset: {ex.Path} could not be read — {ex.Message}", ex);
        }
    }

    private sealed class Root
    {
        public List<UnlockDefinition> Unlocks { get; set; } = [];
    }
}
