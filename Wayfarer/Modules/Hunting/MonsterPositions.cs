using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Wayfarer.Routing;

namespace Wayfarer.Modules.Hunting;

/// <summary>Where the monsters hunts ask for have been seen, from the file shipped beside the
/// plugin. Read once, by <see cref="Warm"/> off the game's thread, or by the first hunt that asks
/// if that comes sooner. A missing or unreadable file leaves every hunt guided to the stretch of
/// map its bill or log names, which is logged once.</summary>
internal sealed class MonsterPositions(IDalamudPluginInterface pluginInterface, IPluginLog log)
{
    /// <summary>How much ground around a reported spot counts as the spot: monsters wander, and
    /// arriving near where they were seen is arriving.</summary>
    private const float SpotRadius = 15f;

    /// <summary>The whole file, read by whichever thread asks first while any other waits for it.</summary>
    private readonly Lazy<IReadOnlyDictionary<uint, IReadOnlyList<MonsterSpot>>> spots = new(() => Read(pluginInterface, log));

    /// <summary>Reads the file now, so the first hunt looked at does not pay for it inside a frame.
    /// Safe off the game's thread.</summary>
    public void Warm() => _ = spots.Value;

    /// <summary>Where a monster has been seen in any of these zones, as places to search, or none.
    /// A name can belong to monsters in several zones, and a hunt means the one in its own.</summary>
    /// <param name="nameId">The monster's name id.</param>
    /// <param name="territories">The zones the hunt names for it.</param>
    public IReadOnlyList<Place> In(uint nameId, IReadOnlySet<uint> territories)
    {
        ArgumentNullException.ThrowIfNull(territories);
        if (territories.Count == 0 || !spots.Value.TryGetValue(nameId, out var seen))
        {
            return [];
        }

        // Height is not known: the reports carry none worth trusting, and a NaN height is measured
        // across the ground only.
        return [.. seen
            .Where(spot => territories.Contains(spot.Territory))
            .Select(spot => new Place(spot.Territory, spot.Map, spot.X, float.NaN, spot.Z, SpotRadius))];
    }

    private static IReadOnlyDictionary<uint, IReadOnlyList<MonsterSpot>> Read(IDalamudPluginInterface pluginInterface, IPluginLog log)
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
