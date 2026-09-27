using System.Numerics;
using Dalamud.Plugin.Services;
using KamiToolKit.MapOverlay;
using Wayfarer.App;

namespace Wayfarer.Modules.Treasure;

/// <summary>Wayfarer's treasure markers over the game's map: every coffer spot, lit while filled,
/// and loaded treasure standing on no spot. Owns the overlay and every marker on it; nothing here
/// outlives <see cref="Stop"/>. Game thread only, apart from <see cref="Allow"/>.
///
/// <para>A switch or a login applies the module by reading layout files off the game's thread and
/// then showing the map. If the plugin stops in between, that late <see cref="Show"/> must not put an
/// overlay back that nothing will ever take down, so once stopped, the map shows nothing until the
/// module is applied again.</para></summary>
internal sealed class TreasureMap(CofferSpots spots, LiveTreasure live, IFramework framework) : IAsyncDisposable
{
    /// <summary>How many chests off every spot can be shown at once. A dungeon room holds a few.</summary>
    private const int NearbyMarkers = 32;

    private MapOverlayController? overlay;
    private volatile bool stopped;
    private (bool Spots, bool Nearby) showing;
    private List<Vector3> spotsHere = [];
    private uint spotsHereTerritory = uint.MaxValue;
    private List<int> offSpots = [];
    private DateTime offSpotsAt;

    /// <summary>Shows what is switched on and nothing else. Safe to call again with the same
    /// switches, which leaves the map as it is.</summary>
    public void Show(bool coffers, bool nearby)
    {
        if (stopped || (!coffers && !nearby))
        {
            Clear();
            return;
        }

        if (overlay is not null && showing == (coffers, nearby))
        {
            return;
        }

        if (overlay is null)
        {
            overlay = new MapOverlayController();
            overlay.Enable();
        }
        else
        {
            overlay.RemoveAllMarkers();
        }

        showing = (coffers, nearby);
        spotsHereTerritory = uint.MaxValue;
        if (coffers)
        {
            foreach (var spot in spots.All)
            {
                overlay.AddMarker(new CofferSpotMarker(spot, live));
            }
        }

        if (nearby)
        {
            for (var place = 0; place < NearbyMarkers; place++)
            {
                overlay.AddMarker(new NearbyTreasureMarker(place, live, OffSpots));
            }
        }
    }

    /// <summary>Lets <see cref="Show"/> draw again: the module is being applied. Any thread.</summary>
    public void Allow() => stopped = false;

    /// <summary>Takes every marker off the map, and keeps them off until <see cref="Allow"/>.</summary>
    public void Stop()
    {
        stopped = true;
        Clear();
    }

    /// <summary>Stops the map on the game's thread, the only place the overlay may be let go of. Once
    /// the game itself is closing no frame will come to do it, and the toolkit takes its own nodes
    /// down as the plugin unloads.</summary>
    public ValueTask DisposeAsync()
    {
        stopped = true;
        if (framework.IsFrameworkUnloading)
        {
            overlay = null;
            return ValueTask.CompletedTask;
        }

        return new ValueTask(framework.OnTheGameThread(Stop));
    }

    private void Clear()
    {
        overlay?.Dispose();
        overlay = null;
        showing = default;
    }

    /// <summary>The loaded chests standing on no spot being shown, worked out once a frame.</summary>
    private List<int> OffSpots()
    {
        if (offSpotsAt != framework.LastUpdateUTC)
        {
            offSpotsAt = framework.LastUpdateUTC;
            offSpots = TreasureMatch.OffSpots(live.Positions, SpotsHere());
        }

        return offSpots;
    }

    /// <summary>The spots in the zone the player stands in, while spots are shown; none otherwise,
    /// so every chest is shown as nearby treasure.</summary>
    private List<Vector3> SpotsHere()
    {
        if (!showing.Spots)
        {
            return [];
        }

        if (spotsHereTerritory != live.Territory)
        {
            spotsHereTerritory = live.Territory;
            spotsHere = [.. spots.All.Where(spot => spot.Territory == spotsHereTerritory).Select(spot => spot.Position)];
        }

        return spotsHere;
    }
}
