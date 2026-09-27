using Dalamud.Plugin.Services;
using Wayfarer.App;
using Wayfarer.App.Modules;

namespace Wayfarer.Modules.Treasure;

/// <summary>The treasure module: treasure marked on the game's map. Chests the game has loaded
/// nearby, and every place the zone can put one.</summary>
internal sealed class TreasureModule(TreasureSettings settings, TreasureMap map, IFramework framework) : IModule
{
    /// <summary>What the module is called, everywhere.</summary>
    public const string ModuleName = "Treasure";

    /// <summary>The game's gold treasure chest, as its maps draw one.</summary>
    private const uint ChestIcon = 60354;

    private const string NearbyName = "Show nearby treasure";
    private const string NearbyDescription = "Marks treasure chests the game has loaded near you on the map, in any zone, dungeons included.";
    private const string SpotsName = "Overlay all potential treasure on the map";
    private const string SpotsDescription = "Marks every place treasure can appear in the zone you are in, such as dungeon chests and Occult Crescent coffers. With nearby treasure shown too, a spot with treasure on it now turns gold.";

    /// <inheritdoc/>
    public string Name => ModuleName;

    /// <inheritdoc/>
    public uint Icon => ChestIcon;

    /// <inheritdoc/>
    public string Description => "Marks treasure on the map.";

    /// <inheritdoc/>
    public IReadOnlyList<ModuleSetting> Settings =>
    [
        new ModuleSetting(NearbyName, NearbyDescription, () => settings.Nearby, on => settings.Nearby = on),
        new ModuleSetting(SpotsName, SpotsDescription, () => settings.Spots, on => settings.Spots = on),
    ];

    /// <inheritdoc/>
    public Task ApplyAsync() => framework.OnTheGameThread(() =>
    {
        map.Allow();
        map.Show(settings.Nearby, settings.Spots);
    });

    /// <inheritdoc/>
    public Task StopAsync() => framework.OnTheGameThread(map.Stop);
}
