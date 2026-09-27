using Wayfarer.App.Config;

namespace Wayfarer.Modules.Treasure;

/// <summary>The treasure module's two switches, saved as they change.</summary>
internal sealed class TreasureSettings(IConfigStore configs)
{
    private const string ConfigName = "treasure";

    private readonly TreasureConfig config = configs.Load<TreasureConfig>(ConfigName);

    /// <summary>Whether treasure the game has loaded near the player is marked on the map.</summary>
    public bool Nearby
    {
        get => config.ShowNearbyTreasure;
        set
        {
            if (config.ShowNearbyTreasure != value)
            {
                config.ShowNearbyTreasure = value;
                configs.Save(ConfigName, config);
            }
        }
    }

    /// <summary>Whether every place the zone can put treasure is marked on the map.</summary>
    public bool Spots
    {
        get => config.ShowTreasureSpots;
        set
        {
            if (config.ShowTreasureSpots != value)
            {
                config.ShowTreasureSpots = value;
                configs.Save(ConfigName, config);
            }
        }
    }
}
