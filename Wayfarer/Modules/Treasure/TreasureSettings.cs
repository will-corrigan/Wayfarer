using Wayfarer.App.Config;

namespace Wayfarer.Modules.Treasure;

/// <summary>The treasure module's two switches, saved as they change.</summary>
internal sealed class TreasureSettings(IConfigStore configs)
{
    private const string ConfigName = "treasure";

    private readonly TreasureConfig config = configs.Load<TreasureConfig>(ConfigName);

    /// <summary>Whether every place a coffer can appear in the Occult Crescent is marked on the map.</summary>
    public bool CofferSpots
    {
        get => config.ShowCofferSpots;
        set
        {
            if (config.ShowCofferSpots != value)
            {
                config.ShowCofferSpots = value;
                configs.Save(ConfigName, config);
            }
        }
    }

    /// <summary>Whether treasure the game has loaded near the player is marked on the map, in any zone.</summary>
    public bool NearbyTreasure
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
}
