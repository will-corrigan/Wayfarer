using System.Numerics;
using KamiToolKit.MapOverlay;

namespace Wayfarer.Modules.Treasure;

/// <summary>One treasure spot on the map: faded, in the metal last seen there or bronze when none
/// has been; or, while nearby treasure is shown too and a chest is loaded there, that chest in its
/// own metal, bright and larger.</summary>
internal sealed class TreasureSpotMarker : MapMarkerNode
{
    private readonly TreasureSpot spot;
    private readonly LiveTreasure live;
    private readonly TreasureTiers tiers;
    private readonly TreasureMemory memory;
    private readonly bool lights;
    private readonly string key;
    private (bool Filled, TreasureTier? Tier)? drawn;

    /// <summary>Initializes a new instance of the <see cref="TreasureSpotMarker"/> class.</summary>
    /// <param name="spot">The spot it marks.</param>
    /// <param name="live">The treasure loaded now.</param>
    /// <param name="tiers">What each kind of chest is made of.</param>
    /// <param name="memory">The metal each spot has been seen holding.</param>
    /// <param name="lights">Whether it shows the chest standing on it.</param>
    public TreasureSpotMarker(TreasureSpot spot, LiveTreasure live, TreasureTiers tiers, TreasureMemory memory, bool lights)
    {
        this.spot = spot;
        this.live = live;
        this.tiers = tiers;
        this.memory = memory;
        this.lights = lights;
        key = TreasureMemory.KeyOf(spot);
        MapId = spot.Map;
        Position = new Vector2(spot.Position.X, spot.Position.Z);
        Size = new Vector2(24f, 24f);
        Show(false, memory.At(key));
    }

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        var chest = lights && live.Territory == spot.Territory && live.Map == spot.Map ? TreasureMatch.On(spot.Position, live.Positions) : -1;
        Show(chest >= 0, chest >= 0 ? tiers.Of(live.Kinds[chest]) : memory.At(key));
    }

    /// <param name="filled">Whether a chest is standing here now.</param>
    /// <param name="tier">Its metal, or the one last seen here, or null for none known.</param>
    private void Show(bool filled, TreasureTier? tier)
    {
        // By the metal, not the icon: a chest not read yet and a gold one share an icon, and the
        // tooltip still has to change when the read comes back.
        if (drawn == (filled, tier))
        {
            return;
        }

        drawn = (filled, tier);
        IconId = tier is { } metal ? TreasureIcons.For(metal) : TreasureIcons.Bronze;
        Alpha = filled ? 1f : 0.55f;
        MarkerScale = filled ? 1.3f : 1f;
        var metalName = tier switch
        {
            TreasureTier.Bronze => "Bronze",
            TreasureTier.Silver => "Silver",
            TreasureTier.Gold => "Gold",
            _ => null,
        };
        TextTooltip = (filled, metalName) switch
        {
            (true, { } name) => $"{name} treasure",
            (true, null) => "Treasure",
            (false, { } name) => $"{name} treasure has been seen here",
            _ => "Treasure can appear here",
        };
    }
}
