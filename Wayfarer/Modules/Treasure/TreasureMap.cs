using System.Numerics;
using Dalamud.Plugin.Services;
using KamiToolKit.MapOverlay;
using Wayfarer.App;

namespace Wayfarer.Modules.Treasure;

/// <summary>Wayfarer's treasure markers over the game's map, as switched on: treasure the game has
/// loaded nearby, and every treasure spot of the zone the player is in. With both, loaded treasure on
/// a spot is drawn by lighting the spot. Owns the overlay and every marker on it; nothing here
/// outlives <see cref="Stop"/>. Game thread only.
///
/// <para>A zone's spots are read off the game's thread when the player arrives, and put on the map
/// when the read comes back, if they are still in that zone and the map is still shown. Once stopped,
/// the map shows nothing until the module is applied again, so a read or an apply landing after the
/// plugin stopped cannot put back an overlay nothing will take down.</para></summary>
internal sealed class TreasureMap(TreasureSpots spots, LiveTreasure live, TreasureTiers tiers, IClientState clientState, IFramework framework, IPluginLog log) : IAsyncDisposable
{
    /// <summary>How many loaded chests off every spot can be shown at once.</summary>
    private const int NearbyMarkers = 32;

    private readonly List<TreasureSpotMarker> spotMarkers = [];
    private MapOverlayController? overlay;
    private (bool Nearby, bool Spots) showing;
    private volatile bool stopped;
    private volatile bool disposed;
    private List<TreasureSpot> spotsHere = [];
    private uint spotsTerritory;
    private List<int> offSpots = [];
    private DateTime offSpotsAt;

    /// <summary>Shows what is switched on and nothing else. Safe to call again with the same
    /// switches, which leaves the map as it is.</summary>
    public void Show(bool nearby, bool spotsToo)
    {
        var wanted = (nearby, spotsToo);
        if (overlay is not null && showing == wanted && !stopped)
        {
            return;
        }

        Clear();
        if (stopped || (!nearby && !spotsToo))
        {
            return;
        }

        overlay = new MapOverlayController();
        showing = wanted;
        if (nearby)
        {
            for (var place = 0; place < NearbyMarkers; place++)
            {
                overlay.AddMarker(new NearbyTreasureMarker(place, live, tiers, OffSpots));
            }
        }

        overlay.Enable();
        if (spotsToo)
        {
            clientState.TerritoryChanged += OnTerritoryChanged;
            Load(clientState.TerritoryType);
        }
    }

    /// <summary>Lets <see cref="Show"/> draw again: the module is being applied. Never after the map
    /// has been disposed.</summary>
    public void Allow() => stopped = disposed;

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
        disposed = true;
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
        if (overlay is null)
        {
            return;
        }

        clientState.TerritoryChanged -= OnTerritoryChanged;
        overlay.Dispose();
        overlay = null;
        showing = default;
        spotMarkers.Clear();
        spotsHere = [];
        spotsTerritory = 0;
    }

    private void OnTerritoryChanged(uint territory) => Load(territory);

    /// <summary>Reads a zone's spots off the game's thread and puts them on the map when they come back.</summary>
    private void Load(uint territory)
    {
        Place(territory, []);
        GameThread.Let(
            async () =>
            {
                var found = await Task.Run(() => spots.In(territory)).ConfigureAwait(false);
                await framework.OnTheGameThread(() => Place(territory, found)).ConfigureAwait(false);
            },
            log,
            $"mark the treasure spots of territory {territory} on the map");
    }

    /// <summary>Replaces the spot markers with these, unless the player has moved on or the spots
    /// are no longer shown.</summary>
    private void Place(uint territory, IReadOnlyList<TreasureSpot> found)
    {
        if (overlay is null || stopped || !showing.Spots || territory != clientState.TerritoryType)
        {
            return;
        }

        foreach (var marker in spotMarkers)
        {
            overlay.RemoveMarker(marker);
        }

        spotMarkers.Clear();
        foreach (var spot in found)
        {
            var marker = new TreasureSpotMarker(spot, live, tiers, showing.Nearby);
            spotMarkers.Add(marker);
            overlay.AddMarker(marker);
        }

        spotsTerritory = territory;
        spotsHere = [.. found];
    }

    /// <summary>The loaded chests not already drawn as a lit spot, worked out once a frame.</summary>
    private List<int> OffSpots()
    {
        if (offSpotsAt != framework.LastUpdateUTC)
        {
            offSpotsAt = framework.LastUpdateUTC;

            // Only the spots of the floor the player is on: a chest upstairs must not light a spot
            // drawn on the floor below, where it would vanish from the floor it is on.
            offSpots = TreasureMatch.OffSpots(
                live.Positions,
                spotsTerritory == live.Territory ? [.. spotsHere.Where(spot => spot.Map == live.Map).Select(spot => spot.Position)] : []);
        }

        return offSpots;
    }
}
