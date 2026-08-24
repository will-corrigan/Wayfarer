using Wayfarer.Core.Navigation;

namespace Wayfarer.Tests;

/// <summary>Sheet-shaped regression coverage for the third live "Teleport to Foundation
/// first" reproduction (2026-08-22, player at Fortemps Manor's door in The Pillars):
/// group derivation must come from raw sheet rows, never from position-resolved point
/// lists. Fixture mirrors live sheet truth exactly (verified via Lumina against the
/// game's own sqpack with Dalamud's own Lumina DLLs): Aetheryte rows 70/80–82 home to
/// Foundation (418), 83–87 to The Pillars (419), ALL AethernetGroup 4; Fortemps Manor
/// is territory 433 (map 222, MapMarkerRange 0 — no shards, no map-link entrances);
/// TerritoryType 433's fallback aetheryte is row 70. Crucially, NO Ishgard shard row's
/// position is resolvable (Map=0, dead Level refs), which is exactly why the previous,
/// point-list-based derivation produced an empty group set for the player's territory
/// and let the useless teleport through.</summary>
public class AethernetGroupsTests
{
    private const uint Foundation = 418;
    private const uint Pillars = 419;
    private const uint FortempsManor = 433;
    private const uint IshgardGroup = 4;

    // Gridania for the cross-city case: two territories, their own network group.
    private const uint NewGridania = 132;
    private const uint OldGridania = 133;
    private const uint GridaniaGroup = 2;

    /// <summary>Raw sheet rows: (Territory, AethernetGroup) only — positions do not
    /// exist here, mirroring that live Ishgard shard rows have no resolvable position.</summary>
    private static readonly List<AethernetSheetRow> SheetRows =
    [
        new(Foundation, IshgardGroup), // 70 (the hub aetheryte itself)
        new(Foundation, IshgardGroup), // 80 The Forgotten Knight
        new(Foundation, IshgardGroup), // 81 Skysteel Manufactory
        new(Foundation, IshgardGroup), // 82 The Brume
        new(Pillars, IshgardGroup),    // 83 Athenaeum Astrologicum
        new(Pillars, IshgardGroup),    // 84 The Jeweled Crozier
        new(Pillars, IshgardGroup),    // 85 Saint Reymanaud's Cathedral
        new(Pillars, IshgardGroup),    // 86 The Tribunal
        new(Pillars, IshgardGroup),    // 87 The Last Vigil
        new(NewGridania, GridaniaGroup),
        new(OldGridania, GridaniaGroup),
        new(9999, 0),                  // an overworld aetheryte: no network
    ];

    // The fallback aetheryte TerritoryType 433 resolves to: row 70, home 418, group 4.
    private static readonly AetherytePoint FoundationHub =
        new(70, "Foundation", 0f, 0f, Foundation, IshgardGroup);

    [Fact]
    public void ForTerritory_DerivesGroupsFromSheetRows_WithoutPositions()
    {
        Assert.Equal([IshgardGroup], AethernetGroups.ForTerritory(SheetRows, Pillars));
        Assert.Equal([IshgardGroup], AethernetGroups.ForTerritory(SheetRows, Foundation));
        Assert.Equal([GridaniaGroup], AethernetGroups.ForTerritory(SheetRows, NewGridania));
    }

    [Fact]
    public void ForTerritory_IgnoresZeroGroups_AndUnknownTerritories()
    {
        Assert.Empty(AethernetGroups.ForTerritory(SheetRows, 9999));
        Assert.Empty(AethernetGroups.ForTerritory(SheetRows, FortempsManor));
    }

    /// <summary>The user's exact case: standing in The Pillars at Fortemps Manor's
    /// door, objective inside the manor (433 — no shards, no entrance links), sole
    /// candidate is the TerritoryType fallback teleport to the Foundation hub. With
    /// groups derived from sheet rows, the player's territory (419) is group 4 and so
    /// is the candidate's home territory (418) — the teleport must be suppressed and
    /// navigation must fall through to the interior-fallback message.</summary>
    [Fact]
    public void UselessTeleport_Suppressed_AndInteriorFallbackSurfaces_WhenShardPositionsNeverResolve()
    {
        var currentGroups = AethernetGroups.ForTerritory(SheetRows, Pillars);
        var aetheryteHomeGroups = AethernetGroups.ForTerritory(SheetRows, Foundation);

        var teleport = RouteCosting.TeleportCandidate(
            FoundationHub,
            aetheryteTerritory: Foundation,
            targetTerritory: FortempsManor,
            currentTerritory: Pillars,
            tx: 0.8f,
            tz: 4.6f,
            unlocked: true,
            currentTerritoryAethernetGroups: currentGroups,
            aetheryteTerritoryAethernetGroups: aetheryteHomeGroups);

        Assert.Null(teleport);

        // No aethernet (433 has no shards), no entrance (map 222 has no map-links), no teleport, no
        // marker fallback — the honest interior message must win. This still holds now that the
        // manor's DOOR is a costed leg (InteriorRoute), because the player is standing AT the door:
        // that leg returns null inside InteriorRoute.AtEntranceYalms, which is the only place the
        // message is useful advice. From anywhere else in Ishgard the door's leg is what wins — see
        // InteriorRouteTests.
        var chosen = RouteCosting.Choose(null, null, teleport);
        Assert.Equal(OtherZoneOutcome.InteriorMessage, OtherZoneResolution.Resolve(chosen, null));
    }

    /// <summary>Answers directly the suspicion that the same-network suppression was itself removing
    /// the shard hop. It is not, and cannot be: suppression is a rule about TELEPORTS only. On the
    /// identical inputs — player in Foundation, objective inside Fortemps Manor, one Ishgard network
    /// — the teleport to the Foundation hub is correctly suppressed (a loading screen to reach a
    /// stop the free aethernet already covers) while the aethernet leg to the manor's door survives
    /// untouched. A shard hop inside a city is exactly what should be offered.</summary>
    [Fact]
    public void SameNetworkSuppression_RemovesTheTeleportOnly_NotTheAethernetLeg()
    {
        var currentGroups = AethernetGroups.ForTerritory(SheetRows, Foundation);

        var teleport = RouteCosting.TeleportCandidate(
            FoundationHub,
            aetheryteTerritory: Foundation,
            targetTerritory: FortempsManor,
            currentTerritory: Foundation,
            tx: 0.8f,
            tz: 4.6f,
            unlocked: true,
            currentTerritoryAethernetGroups: currentGroups,
            aetheryteTerritoryAethernetGroups: AethernetGroups.ForTerritory(SheetRows, Foundation));

        Assert.Null(teleport);

        // Same network, same suppressed shape — and the aethernet hop to the manor's door in The
        // Pillars is built regardless, because nothing in the suppression rule touches it.
        AetherytePoint[] foundationShards =
            [new(80, "The Forgotten Knight", 45.0f, 1.0f, Foundation, IshgardGroup)];
        AetherytePoint[] pillarsShards =
            [new(87, "The Last Vigil", 0.0f, -33.5f, Pillars, IshgardGroup)];

        var door = new InteriorEntrance(Pillars, 219, "Fortemps Manor", 32.0f, -6.0f);
        var leg = InteriorRoute.Route(
            door, Foundation, 45.0f, 1.0f, foundationShards, pillarsShards, [], []);

        Assert.NotNull(leg);
        Assert.Equal(RouteMode.Aethernet, leg.Mode);
        Assert.Equal("The Last Vigil", leg.AethernetExitName);

        // And it is what the router presents, rather than the interior message.
        Assert.Equal(
            OtherZoneOutcome.Route,
            OtherZoneResolution.Resolve(RouteCosting.Choose(null, null, teleport, leg), null));
    }

    /// <summary>Suppression must NOT fire across cities: player in Gridania (group 2),
    /// objective near/inside Ishgard — the teleport to the Foundation hub is the only
    /// sensible route and must survive.</summary>
    [Fact]
    public void CrossCityTeleport_Survives_WhenPlayerIsOnADifferentNetwork()
    {
        var currentGroups = AethernetGroups.ForTerritory(SheetRows, NewGridania);
        var aetheryteHomeGroups = AethernetGroups.ForTerritory(SheetRows, Foundation);

        var teleport = RouteCosting.TeleportCandidate(
            FoundationHub,
            aetheryteTerritory: Foundation,
            targetTerritory: FortempsManor,
            currentTerritory: NewGridania,
            tx: 0.8f,
            tz: 4.6f,
            unlocked: true,
            currentTerritoryAethernetGroups: currentGroups,
            aetheryteTerritoryAethernetGroups: aetheryteHomeGroups);

        Assert.NotNull(teleport);
        Assert.Equal(RouteMode.Teleport, teleport!.Mode);
    }
}
