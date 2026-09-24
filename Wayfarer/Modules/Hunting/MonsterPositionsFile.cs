using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wayfarer.Modules.Hunting;

/// <summary>Where the monsters hunts ask for have been seen, as shipped beside the plugin: for each
/// monster name, the spots players have reported one standing, by zone and map, across the ground.
///
/// <para>No game file says where an ordinary monster spawns: the server places them, and the only
/// thing a bill or a hunting log gives is the name of a stretch of map. These spots come from
/// FFXIV Teamcraft's monster data, which players report as they play. They carry no height, since
/// the reports do not record one worth trusting.</para></summary>
/// <param name="Monsters">Spots by monster name id.</param>
internal sealed record MonsterPositionsFile(IReadOnlyDictionary<uint, IReadOnlyList<MonsterSpot>> Monsters)
{
    /// <summary>What the file is called beside the plugin.</summary>
    public const string FileName = "monster-positions.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Reads the file's contents.</summary>
    public static MonsterPositionsFile Parse(string json) =>
        JsonSerializer.Deserialize<MonsterPositionsFile>(json, Options) ?? new MonsterPositionsFile(new Dictionary<uint, IReadOnlyList<MonsterSpot>>());

    /// <summary>The file's contents.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Options);
}
