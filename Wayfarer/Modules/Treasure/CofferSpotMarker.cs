using System.Numerics;
using KamiToolKit.MapOverlay;

namespace Wayfarer.Modules.Treasure;

/// <summary>One coffer spot on the map: the bronze chest while it is empty, or not known to be
/// filled, and the gold chest while the game has a coffer standing on it.</summary>
internal sealed class CofferSpotMarker : MapMarkerNode
{
    private const uint EmptyIcon = 60356;
    private const uint FilledIcon = 60354;

    private readonly CofferSpot spot;
    private readonly LiveTreasure live;
    private bool? filled;

    public CofferSpotMarker(CofferSpot spot, LiveTreasure live)
    {
        this.spot = spot;
        this.live = live;
        MapId = spot.Map;
        Position = new Vector2(spot.Position.X, spot.Position.Z);
        Size = new Vector2(24f, 24f);
        Show(false);
    }

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        // The chests loaded are the zone the player stands in; a spot elsewhere is only ever shown
        // as a spot.
        Show(live.Territory == spot.Territory && TreasureMatch.Holds(spot.Position, live.Positions));
    }

    private void Show(bool now)
    {
        if (filled == now)
        {
            return;
        }

        filled = now;
        IconId = now ? FilledIcon : EmptyIcon;
        Alpha = now ? 1f : 0.55f;
        MarkerScale = now ? 1.3f : 1f;
        TextTooltip = now ? "Treasure coffer" : "Treasure coffer spot";
    }
}
