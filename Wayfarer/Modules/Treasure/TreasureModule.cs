using Dalamud.Plugin.Services;
using Wayfarer.App;
using Wayfarer.App.Modules;

namespace Wayfarer.Modules.Treasure;

/// <summary>The treasure module: treasure marked on the game's map. Every place a coffer can appear
/// in the Occult Crescent, lit while one is there, and any chest the game has loaded nearby.</summary>
internal sealed class TreasureModule(TreasureSettings settings, CofferSpots spots, TreasureMap map, IFramework framework) : IModule
{
    /// <summary>What the module is called, everywhere.</summary>
    public const string ModuleName = "Treasure";

    /// <summary>The game's gold treasure chest, as its maps draw one.</summary>
    private const uint ChestIcon = 60354;

    private const string SpotsName = "Coffer spots in the Occult Crescent";
    private const string SpotsDescription = "Marks every place a treasure coffer can appear in the Occult Crescent on the map, and lights up the ones with a coffer there now.";
    private const string NearbyName = "Nearby treasure on the map";
    private const string NearbyDescription = "Marks treasure chests near you on the map, in any zone, dungeons included.";

    /// <inheritdoc/>
    public string Name => ModuleName;

    /// <inheritdoc/>
    public uint Icon => ChestIcon;

    /// <inheritdoc/>
    public string Description => "Marks treasure on the map.";

    /// <inheritdoc/>
    public IReadOnlyList<ModuleSetting> Settings =>
    [
        new ModuleSetting(SpotsName, SpotsDescription, () => settings.CofferSpots, on => settings.CofferSpots = on),
        new ModuleSetting(NearbyName, NearbyDescription, () => settings.NearbyTreasure, on => settings.NearbyTreasure = on),
    ];

    /// <inheritdoc/>
    public async Task ApplyAsync()
    {
        map.Allow();
        if (settings.CofferSpots)
        {
            await framework.OffTheGameThread(spots.Warm).ConfigureAwait(false);
        }

        await framework.OnTheGameThread(() => map.Show(settings.CofferSpots, settings.NearbyTreasure)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task StopAsync() => framework.OnTheGameThread(map.Stop);
}
