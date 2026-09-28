using System.Globalization;
using Wayfarer.App.Config;

namespace Wayfarer.Modules.Treasure;

/// <summary>What metal each treasure spot has been seen holding, remembered across sessions.
///
/// <para>The Occult Crescent keeps a spot to one metal, eight silver spots among the sixty-odd,
/// but nothing in the game files says which, so the only way to know is to have seen one. The
/// newest sighting always wins: should a spot ever turn up another metal, the memory follows it
/// and says so, rather than keep insisting on the old one.</para></summary>
internal sealed class TreasureMemory(IConfigStore configs)
{
    private const string ConfigName = "treasure-memory";

    private readonly TreasureMemoryConfig config = configs.Load<TreasureMemoryConfig>(ConfigName);

    /// <summary>A spot's key: its zone, the floor's map, and where it stands across the ground, to the
    /// yalm. A spot is placed at the same point in every session, so this names it for as long as the
    /// layout does; the map keeps two spots stacked on different floors apart.</summary>
    public static string KeyOf(TreasureSpot spot)
    {
        ArgumentNullException.ThrowIfNull(spot);
        return string.Create(CultureInfo.InvariantCulture, $"{spot.Territory}:{spot.Map}:{MathF.Round(spot.Position.X)}:{MathF.Round(spot.Position.Z)}");
    }

    /// <summary>The metal last seen at a spot, or null when none has been seen there.</summary>
    public TreasureTier? At(TreasureSpot spot) => At(KeyOf(spot));

    /// <summary>The metal last seen at the spot with this key, or null when none has been seen there.
    /// For a caller that asks every frame and has worked out the key once.</summary>
    public TreasureTier? At(string key) => config.Spots.TryGetValue(key, out var tier) ? tier : null;

    /// <summary>Notes the metal of a chest seen standing on a spot. Saves only when that is new or
    /// different; a metal not known is no sighting at all.</summary>
    /// <returns>The metal remembered there before, when this sighting changed it; null otherwise.</returns>
    public TreasureTier? Saw(TreasureSpot spot, TreasureTier tier)
    {
        if (tier is TreasureTier.Unknown)
        {
            return null;
        }

        var key = KeyOf(spot);
        var had = config.Spots.TryGetValue(key, out var was);
        if (had && was == tier)
        {
            return null;
        }

        config.Spots[key] = tier;
        configs.Save(ConfigName, config);
        return had ? was : null;
    }
}
