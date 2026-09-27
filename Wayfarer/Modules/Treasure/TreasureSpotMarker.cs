using System.Numerics;
using KamiToolKit.MapOverlay;

namespace Wayfarer.Modules.Treasure;

/// <summary>One treasure spot on the map: the bronze chest, or, while nearby treasure is shown too and
/// the game has treasure loaded there, the gold chest that nearby treasure is drawn with.</summary>
internal sealed class TreasureSpotMarker : MapMarkerNode
{
    private const uint EmptyIcon = 60356;
    private const uint FilledIcon = 60354;

    private readonly TreasureSpot spot;
    private readonly LiveTreasure live;
    private readonly bool lights;
    private bool? filled;

    /// <summary>Initializes a new instance of the <see cref="TreasureSpotMarker"/> class.</summary>
    /// <param name="spot">The spot it marks.</param>
    /// <param name="live">The treasure loaded now.</param>
    /// <param name="lights">Whether it turns gold while treasure stands on it.</param>
    public TreasureSpotMarker(TreasureSpot spot, LiveTreasure live, bool lights)
    {
        this.spot = spot;
        this.live = live;
        this.lights = lights;
        MapId = spot.Map;
        Position = new Vector2(spot.Position.X, spot.Position.Z);
        Size = new Vector2(24f, 24f);
        Show(false);
    }

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        Show(lights && live.Territory == spot.Territory && live.Map == spot.Map && TreasureMatch.Holds(spot.Position, live.Positions));
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
        TextTooltip = now ? "Treasure" : "Treasure can appear here";
    }
}
