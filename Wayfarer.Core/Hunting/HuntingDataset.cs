using System.Text.Json;

namespace Wayfarer.Core.Hunting;

/// <summary>Curated hunting-log target data ported from Hunty's MIT-licensed
/// <c>monsters.json</c> (see THIRD_PARTY_NOTICES.md), stored as <c>data/hunting-targets.json</c>.
/// Names, per-kill flavor text, rewards and icons are intentionally not carried here — resolve
/// those from Lumina via <see cref="HuntingMonster.BNpcNameId"/> at load.</summary>
public sealed class HuntingDataset
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public int SchemaVersion { get; set; }

    public string CoordinateSystem { get; set; } = string.Empty;

    public string CoordinateSystemNote { get; set; } = string.Empty;

    /// <summary>Keyed by jobKey — see <see cref="HuntingLog"/>.</summary>
    public Dictionary<string, HuntingLog> Logs { get; set; } = new(StringComparer.Ordinal);

    public static HuntingDataset Parse(string json) =>
        JsonSerializer.Deserialize<HuntingDataset>(json, Options)
        ?? throw new JsonException("hunting dataset: empty document");
}
