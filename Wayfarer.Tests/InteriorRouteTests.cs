using Wayfarer.Core.Guidance;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The three-place case: the player is in one half of a split city, the objective is
/// inside a building, and the building's door is in the OTHER half. Every coordinate below was
/// extracted from the live sheet data (Aetheryte / TerritoryType / Map / MapMarker, via Lumina
/// over the installed sqpack) rather than invented, so a fixture cannot quietly stop describing
/// the game:
///
/// <list type="bullet">
/// <item><description><b>Ishgard, AethernetGroup 4</b> — Foundation (418, map 218) and The
/// Pillars (419, map 219). Fortemps Manor is territory 433, map 222, MapMarkerRange 0, with no
/// Aetheryte rows homed in it; the only thing locating it anywhere in the data is a DataType 0
/// MapMarker on map 219 at pixel (1088, 1012) whose PlaceNameSubtext is 433's own PlaceName row
/// 2320 — world (32.0, -6.0) in The Pillars.</description></item>
/// <item><description><b>Limsa Lominsa, AethernetGroup 1</b> — Upper Decks (128, map 11) and
/// Lower Decks (129, map 12). Maelstrom Barracks is territory 536, same shape, located only by a
/// DataType 0 marker on map 11 at pixel (1225, 1148) — world (100.5, 62.0) in the Upper
/// Decks. Present so the fix is not tuned to the one city the defect was reported
/// in.</description></item>
/// </list></summary>
public class InteriorRouteTests
{
    private const uint Foundation = 418;
    private const uint Pillars = 419;
    private const uint PillarsMap = 219;
    private const uint IshgardGroup = 4;

    private const uint LowerDecks = 129;
    private const uint UpperDecks = 128;
    private const uint UpperDecksMap = 11;
    private const uint LimsaGroup = 1;

    // Aetheryte rows 70, 80-82. The Forgotten Knight (80) is where the player was standing when
    // the defect was reported.
    private static readonly AetherytePoint[] FoundationShards =
    [
        new(70, "Ishgard Aetheryte Plaza", -63.5f, 45.0f, Foundation, IshgardGroup),
        new(80, "The Forgotten Knight", 45.0f, 1.0f, Foundation, IshgardGroup),
        new(81, "Skysteel Manufactory", -112.0f, -29.0f, Foundation, IshgardGroup),
        new(82, "The Brume", 50.0f, 68.0f, Foundation, IshgardGroup),
    ];

    // Aetheryte rows 83-87. The Last Vigil (87) is the shard nearest Fortemps Manor's door.
    private static readonly AetherytePoint[] PillarsShards =
    [
        new(83, "Athenaeum Astrologicum", 134.0f, -64.5f, Pillars, IshgardGroup),
        new(84, "The Jeweled Crozier", -134.5f, -14.5f, Pillars, IshgardGroup),
        new(85, "Saint Reymanaud's Cathedral", -78.5f, -127.0f, Pillars, IshgardGroup),
        new(86, "The Tribunal", 78.0f, -126.0f, Pillars, IshgardGroup),
        new(87, "The Last Vigil", 0.0f, -33.5f, Pillars, IshgardGroup),
    ];

    // Aetheryte rows 41/42/48. Row 93 (Airship Landing) is deliberately absent: its position does
    // not resolve from any sheet, so the point builder drops it in the live plugin too.
    private static readonly AetherytePoint[] UpperDecksShards =
    [
        new(41, "The Aftcastle", 15.5f, 72.5f, UpperDecks, LimsaGroup),
        new(42, "Culinarians' Guild", -56.5f, -131.5f, UpperDecks, LimsaGroup),
        new(48, "Marauders' Guild", -4.0f, -218.0f, UpperDecks, LimsaGroup),
    ];

    private static readonly AetherytePoint[] LowerDecksShards =
    [
        new(8, "Limsa Lominsa Aetheryte Plaza", -82.0f, -0.5f, LowerDecks, LimsaGroup),
        new(43, "Arcanists' Guild", -335.5f, 54.5f, LowerDecks, LimsaGroup),
        new(44, "Fishermen's Guild", -179.5f, 183.0f, LowerDecks, LimsaGroup),
        new(49, "Hawkers' Alley", -215.0f, 51.5f, LowerDecks, LimsaGroup),
    ];

    private static readonly InteriorEntrance FortempsManor =
        new(Pillars, PillarsMap, "Fortemps Manor", 32.0f, -6.0f);

    private static readonly InteriorEntrance MaelstromBarracks =
        new(UpperDecks, UpperDecksMap, "Maelstrom Barracks", 100.5f, 62.0f);

    /// <summary>The reported defect, as reported: following "Heroes of the Hour", objective inside
    /// Fortemps Manor, player standing at The Forgotten Knight in Foundation — the other half of
    /// Ishgard. Before the interior door was a costed leg there was no candidate to rank at all
    /// (433 has no shards, no aetheryte of its own and no map-link doors, and the only teleport on
    /// offer is correctly suppressed as same-network), so the router fell through to the bare
    /// "In Fortemps Manor — find the entrance" message from right across the city.</summary>
    [Fact]
    public void AcrossASplitCity_TheAethernetLegIsOffered_NotTheInteriorMessage()
    {
        var route = InteriorRoute.Route(
            FortempsManor,
            currentTerritory: Foundation,
            px: 45.0f,
            pz: 1.0f,
            FoundationShards,
            PillarsShards,
            [],
            []);

        Assert.NotNull(route);
        Assert.Equal(RouteMode.Aethernet, route.Mode);
        Assert.Equal("The Forgotten Knight", route.AethernetEntryName);
        Assert.Equal("The Last Vigil", route.AethernetExitName);

        // The walk left after stepping out of The Last Vigil: (0, -33.5) to the door at (32, -6).
        Assert.Equal(42.19f, route.RemainingYalms!.Value, 2);

        // The arrow points at the shard the player boards, in their own territory's space.
        Assert.Equal(45.0f, route.ArrowX!.Value, 2);
        Assert.Equal(1.0f, route.ArrowZ!.Value, 2);
    }

    /// <summary>The reported case all the way to the words on screen: the costed leg, through
    /// <see cref="GuidanceProjection"/>, out of <see cref="ReadoutComposer"/>. Pins both what the
    /// player now reads AND that the arrow points at the shard they board — and that the readout
    /// says "Fortemps Manor" exactly once, where it used to say it three times.
    ///
    /// <para>The distance is supplied, and it is zero. The reported position <i>is</i> shard 80's own
    /// coordinates, so zero is what the live plugin measures here — other-zone mode measures to the
    /// way in, not to the objective. Leaving it null made this test pin a line list the plugin would
    /// never produce at that position, and hid the arrival line claiming the player had arrived at
    /// the Fortemps Manor door while the two lines under it explained how to get there.</para>
    /// </summary>
    [Fact]
    public void TheReportedCase_ReadsAsAShardHopWithThePlaceNamedOnce()
    {
        var leg = InteriorRoute.Route(
            FortempsManor, Foundation, 45.0f, 1.0f, FoundationShards, PillarsShards, [], []);

        var objective = new GuidanceObjective(
            new ObjectiveKey("quest", "67782"),
            new ObjectiveDestination.TerritoryOnly(433, 222),
            new ObjectiveCopy("Heroes of the Hour", "Enter Fortemps Manor.", "Main Scenario"),
            QuestId: 67782);

        var state = GuidanceProjection.Build(
            objective,
            GuidanceEngagement.Ambient,
            new RouteResult.OtherZone(
                "Fortemps Manor",
                0f,
                0f,
                EntranceX: leg!.ArrowX,
                EntranceZ: leg.ArrowZ,
                AethernetEntryName: leg.AethernetEntryName,
                AethernetExitName: leg.AethernetExitName,
                RemainingYalms: leg.RemainingYalms));

        var content = ReadoutComposer.Compose(new ReadoutInputs { State = state, DistanceYalms = 0f });

        Assert.Equal(
            [
                "Main Scenario",
                "Heroes of the Hour",
                "Enter Fortemps Manor.",
                "0 yalms",
                "To The Forgotten Knight aetheryte",
                "Aethernet to The Last Vigil, then 42 yalms",
            ],
            content.Lines.Select(line => line.Text),
            StringComparer.Ordinal);

        Assert.True(content.ShowArrow);
        Assert.Equal(45.0f, content.TargetX!.Value, 2);
        Assert.Equal(1.0f, content.TargetZ!.Value, 2);

        Assert.Single(
            content.Lines,
            line => line.Text.Contains("Fortemps Manor", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            content.Lines, line => line.Text.Contains("find the entrance", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The same shape in a different split city, so the fix is a property of split cities
    /// and interiors rather than of Ishgard: player in the Lower Decks at Hawkers' Alley,
    /// objective inside the Maelstrom Barracks, whose door is in the Upper Decks.</summary>
    [Fact]
    public void AcrossANonIshgardSplitCity_TheAethernetLegIsOffered()
    {
        var route = InteriorRoute.Route(
            MaelstromBarracks,
            currentTerritory: LowerDecks,
            px: -215.0f,
            pz: 51.5f,
            LowerDecksShards,
            UpperDecksShards,
            [],
            []);

        Assert.NotNull(route);
        Assert.Equal(RouteMode.Aethernet, route.Mode);
        Assert.Equal("Hawkers' Alley", route.AethernetEntryName);
        Assert.Equal("The Aftcastle", route.AethernetExitName);
        Assert.Equal(85.65f, route.RemainingYalms!.Value, 2);
    }

    /// <summary>The case the fallback message is actually FOR: standing at the manor's door. No
    /// route is returned, so the caller surfaces "find the entrance" — which is the right thing to
    /// say here and only here.</summary>
    [Fact]
    public void AtTheDoor_NoRouteIsOffered_SoTheInteriorMessageStands()
    {
        Assert.True(InteriorRoute.AtEntrance(FortempsManor, Pillars, 35.0f, -8.0f));
        Assert.Null(InteriorRoute.Route(
            FortempsManor, Pillars, 35.0f, -8.0f, PillarsShards, PillarsShards, [], []));
    }

    /// <summary>Standing in the door's OWN territory but across it — at The Jeweled Crozier, 167
    /// yalms from the manor. The shard hop still wins, because the within-one-territory case is
    /// the same question, not a different one.</summary>
    [Fact]
    public void FarAcrossTheDoorsOwnTerritory_TheAethernetLegStillWins()
    {
        var route = InteriorRoute.Route(
            FortempsManor, Pillars, -134.5f, -14.5f, PillarsShards, PillarsShards, [], []);

        Assert.NotNull(route);
        Assert.Equal(RouteMode.Aethernet, route.Mode);
        Assert.Equal("The Jeweled Crozier", route.AethernetEntryName);
        Assert.Equal("The Last Vigil", route.AethernetExitName);
    }

    /// <summary>A short walk in the door's own territory: too far to be "at the entrance", too
    /// near for a shard hop to earn its travel menu. The walk to the door wins and the door is
    /// what the arrow points at.</summary>
    [Fact]
    public void AShortWalkAwayInTheDoorsOwnTerritory_TheWalkWins()
    {
        var route = InteriorRoute.Route(
            FortempsManor, Pillars, 60.0f, -20.0f, PillarsShards, PillarsShards, [], []);

        Assert.NotNull(route);
        Assert.Equal(RouteMode.Entrance, route.Mode);
        Assert.Equal("Fortemps Manor", route.EntranceName);
        Assert.Equal(32.0f, route.ArrowX!.Value, 2);
        Assert.Equal(-6.0f, route.ArrowZ!.Value, 2);
    }

    /// <summary>A player on a different network entirely (Gridania, group 2) gets no interior leg —
    /// there is no free way across, so the caller's ordinary cross-city teleport candidate is left
    /// to win. The interior leg must not manufacture a route that does not exist.</summary>
    [Fact]
    public void FromAnotherCity_NoInteriorLegIsBuilt_SoTheTeleportCandidateStillDecides()
    {
        AetherytePoint[] gridaniaShards =
        [
            new(2, "Gridania Aetheryte Plaza", 35.0f, 28.0f, 132, 2),
            new(25, "Archers' Guild", 166.0f, 87.5f, 132, 2),
        ];

        Assert.Null(InteriorRoute.Route(
            FortempsManor, currentTerritory: 132, px: 35.0f, pz: 28.0f, gridaniaShards, PillarsShards, [], []));
    }

    /// <summary>An interior reached by a physical map-link from wherever the player is standing
    /// still routes through that door, so the interior leg is not aethernet-only.</summary>
    [Fact]
    public void AMapLinkIntoTheDoorsMap_IsRoutedThrough()
    {
        var route = InteriorRoute.Route(
            FortempsManor,
            currentTerritory: 155,
            px: 0f,
            pz: 0f,
            [],
            PillarsShards,
            [new MapLinkPoint("Gates of Judgement", 10f, 0f)],
            [new MapLinkPoint("Coerthas Central Highlands", 40f, -6f)]);

        Assert.NotNull(route);
        Assert.Equal(RouteMode.Entrance, route.Mode);
        Assert.Equal("Gates of Judgement", route.EntranceName);
    }
}
