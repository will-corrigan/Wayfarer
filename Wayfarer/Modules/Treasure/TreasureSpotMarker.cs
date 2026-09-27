using System.Numerics;
using KamiToolKit.MapOverlay;

namespace Wayfarer.Modules.Treasure;

/// <summary>One treasure spot on the map: the faded bronze chest, or, while nearby treasure is shown
/// too and the game has a chest loaded there, that chest in its own metal, bright and larger.</summary>
internal sealed class TreasureSpotMarker : MapMarkerNode
{
    private readonly TreasureSpot spot;
    private readonly LiveTreasure live;
    private readonly TreasureTiers tiers;
    private readonly bool lights;
    private uint? icon;

    /// <summary>Initializes a new instance of the <see cref="TreasureSpotMarker"/> class.</summary>
    /// <param name="spot">The spot it marks.</param>
    /// <param name="live">The treasure loaded now.</param>
    /// <param name="tiers">What each kind of chest is made of.</param>
    /// <param name="lights">Whether it shows the chest standing on it.</param>
    public TreasureSpotMarker(TreasureSpot spot, LiveTreasure live, TreasureTiers tiers, bool lights)
    {
        this.spot = spot;
        this.live = live;
        this.tiers = tiers;
        this.lights = lights;
        MapId = spot.Map;
        Position = new Vector2(spot.Position.X, spot.Position.Z);
        Size = new Vector2(24f, 24f);
        Show(null);
    }

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        var chest = lights && live.Territory == spot.Territory && live.Map == spot.Map ? TreasureMatch.On(spot.Position, live.Positions) : -1;
        Show(chest >= 0 ? tiers.Of(live.Kinds[chest]) : null);
    }

    /// <param name="tier">The metal of the chest standing here, or null for none.</param>
    private void Show(TreasureTier? tier)
    {
        var now = tier is { } metal ? TreasureIcons.For(metal) : 0u;
        if (icon == now)
        {
            return;
        }

        icon = now;
        var filled = tier is not null;
        IconId = filled ? now : TreasureIcons.Bronze;
        Alpha = filled ? 1f : 0.55f;
        MarkerScale = filled ? 1.3f : 1f;
        TextTooltip = tier switch
        {
            TreasureTier.Bronze => "Bronze treasure",
            TreasureTier.Silver => "Silver treasure",
            TreasureTier.Gold => "Gold treasure",
            TreasureTier.Unknown => "Treasure",
            _ => "Treasure can appear here",
        };
    }
}
