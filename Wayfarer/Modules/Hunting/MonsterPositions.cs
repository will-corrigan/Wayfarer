using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Wayfarer.Routing;

namespace Wayfarer.Modules.Hunting;

/// <summary>Where the monsters hunts ask for have been seen, from the file shipped beside the
/// plugin. Read once, the first time a hunt asks. A missing or unreadable file leaves every hunt
/// guided to the stretch of map its bill or log names, which is logged once.</summary>
internal sealed class MonsterPositions(IDalamudPluginInterface pluginInterface, IPluginLog log)
{
    /// <summary>How much ground around a reported spot counts as the spot: monsters wander, and
    /// arriving near where they were seen is arriving.</summary>
    private const float SpotRadius = 15f;

    private IReadOnlyDictionary<uint, IReadOnlyList<MonsterSpot>>? spots;

    /// <summary>Where a monster has been seen in any of these zones, as places to search, or none.
    /// A name can belong to monsters in several zones, and a hunt means the one in its own.</summary>
    /// <param name="nameId">The monster's name id.</param>
    /// <param name="territories">The zones the hunt names for it.</param>
    public IReadOnlyList<Place> In(uint nameId, IReadOnlySet<uint> territories)
    {
        ArgumentNullException.ThrowIfNull(territories);
        spots ??= Read();
        if (!spots.TryGetValue(nameId, out var seen))
        {
            return [];
        }

        // Height is not known: the reports carry none worth trusting, and a NaN height is measured
        // across the ground only.
        return [.. seen
            .Where(spot => territories.Contains(spot.Territory))
            .Select(spot => new Place(spot.Territory, spot.Map, spot.X, float.NaN, spot.Z, SpotRadius))];
    }

    private IReadOnlyDictionary<uint, IReadOnlyList<MonsterSpot>> Read()
    {
        var path = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, MonsterPositionsFile.FileName);
        try
        {
            return MonsterPositionsFile.Parse(File.ReadAllText(path)).Monsters;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            log.Error(ex, $"{MonsterPositionsFile.FileName} could not be read, so hunts are guided to the part of the map their bill or log names.");
            return new Dictionary<uint, IReadOnlyList<MonsterSpot>>();
        }
    }
}
