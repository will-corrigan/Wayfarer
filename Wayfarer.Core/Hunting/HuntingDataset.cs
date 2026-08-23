using System.Text.Json;

namespace Wayfarer.Core.Hunting;

/// <summary>Curated hunting-log target data ported from Hunty's MIT-licensed
/// <c>monsters.json</c> (see THIRD_PARTY_NOTICES.md), stored as <c>data/hunting-targets.json</c>.
/// Names, per-kill flavor text, rewards and icons are intentionally not carried here — resolve
/// those from Lumina via <see cref="HuntingMonster.BNpcNameId"/> at load.
///
/// <para>The file's <c>schemaVersion</c>, <c>coordinateSystem</c>, <c>coordinateSystemNote</c> and
/// <c>source</c> headers are deliberately not modelled here. They are provenance for whoever edits
/// the data, checked by <c>data/validate-hunting-targets.mjs</c>; carrying them as properties the
/// plugin never reads only invites the belief that something enforces them at runtime.</para></summary>
public sealed class HuntingDataset
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Keyed by jobKey — see <see cref="HuntingLog"/>.</summary>
    public Dictionary<string, HuntingLog> Logs { get; set; } = new(StringComparer.Ordinal);

    public static HuntingDataset Parse(string json) =>
        JsonSerializer.Deserialize<HuntingDataset>(json, Options)
        ?? throw new JsonException("hunting dataset: empty document");
}
