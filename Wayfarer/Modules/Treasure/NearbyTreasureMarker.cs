using System.Numerics;
using KamiToolKit.MapOverlay;

namespace Wayfarer.Modules.Treasure;

/// <summary>One of a fixed set of markers for loaded treasure that stands on no known coffer spot.
/// The marker in place <c>n</c> shows the <c>n</c>th such chest, and hides while there are fewer.</summary>
internal sealed class NearbyTreasureMarker : MapMarkerNode
{
    private const uint Icon = 60354;

    private readonly int place;
    private readonly LiveTreasure live;
    private readonly Func<IReadOnlyList<int>> offSpots;
    private string? named;

    /// <summary>Initializes a new instance of the <see cref="NearbyTreasureMarker"/> class.</summary>
    /// <param name="place">Which of the chests off every spot this marker shows.</param>
    /// <param name="live">The treasure loaded now.</param>
    /// <param name="offSpots">Indexes into the loaded treasure of the chests standing on no spot.</param>
    public NearbyTreasureMarker(int place, LiveTreasure live, Func<IReadOnlyList<int>> offSpots)
    {
        this.place = place;
        this.live = live;
        this.offSpots = offSpots;
        IconId = Icon;
        Size = new Vector2(24f, 24f);
        MarkerScale = 1.3f;
        IsVisible = false;
    }

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        var chests = offSpots();
        if (place >= chests.Count)
        {
            IsVisible = false;
            return;
        }

        var chest = chests[place];
        IsVisible = true;
        MapId = live.Map;
        Position = new Vector2(live.Positions[chest].X, live.Positions[chest].Z);
        if (!string.Equals(named, live.Names[chest], StringComparison.Ordinal))
        {
            named = live.Names[chest];
            TextTooltip = named;
        }
    }
}
