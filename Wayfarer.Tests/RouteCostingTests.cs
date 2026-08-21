using Wayfarer.Core.Navigation;

namespace Wayfarer.Tests;

/// <summary>Ishgard-shaped fixture (task-2-brief.md evidence): two territories sharing
/// one AethernetGroup, four map-link doors each way between them. Coordinates are
/// arbitrary but each territory's points live in their own local space, mirroring the
/// live game's Foundation (418) / The Pillars (419), group 4, 4+4 links.</summary>
public class RouteCostingTests
{
    private const uint Foundation = 418;
    private const uint Pillars = 419;
    private const uint SharedGroup = 4;

    private static readonly List<AetherytePoint> FoundationShards =
    [
        new(70, "Ishgard Aetheryte Plaza", 0f, 0f, Foundation, SharedGroup),
        new(80, "The Forgotten Knight", 200f, 0f, Foundation, SharedGroup),
    ];

    private static readonly List<AetherytePoint> PillarsShards =
    [
        new(83, "Athenaeum Astrologicum", 0f, 0f, Pillars, SharedGroup),
        new(85, "Saint Reymanaud's Cathedral", 400f, 0f, Pillars, SharedGroup),
    ];

    private static readonly List<MapLinkPoint> FoundationToPillarsLinks =
    [
        new("The Pillars", -600f, -600f),
        new("The Pillars", -300f, -300f),
        new("The Pillars", 300f, 300f),
        new("The Pillars", 600f, 600f),
    ];

    private static readonly List<MapLinkPoint> PillarsToFoundationLinks =
    [
        new("Foundation", -600f, -600f),
        new("Foundation", -300f, -300f),
        new("Foundation", 300f, 300f),
        new("Foundation", 600f, 600f),
    ];

    [Fact]
    public void AethernetCandidate_BeatsEntrance_WhenExitShardIsNearObjective()
    {
        // Player at the Foundation plaza (0,0); objective right next to the Pillars
        // shard at (400,0) → aethernet leg is short both ends. The nearest map-link
        // door is far (300,300) from the objective on the Pillars side.
        var aethernet = RouteCosting.AethernetCandidate(FoundationShards, PillarsShards, 0f, 0f, 410f, 0f);
        var entrance = RouteCosting.EntranceCandidate(
            FoundationToPillarsLinks, PillarsToFoundationLinks, 0f, 0f, 410f, 0f);

        Assert.NotNull(aethernet);
        Assert.NotNull(entrance);

        var chosen = RouteCosting.Choose(aethernet, entrance, null);

        Assert.Equal(RouteMode.Aethernet, chosen!.Mode);
        Assert.Equal("Ishgard Aetheryte Plaza", chosen.AethernetEntryName);
        Assert.Equal("Saint Reymanaud's Cathedral", chosen.AethernetExitName);
    }

    [Fact]
    public void EntranceCandidate_Wins_ForNearBoundaryObjectives()
    {
        // Player right next to the Foundation-side (600,600) door; objective right
        // next to the reciprocal Pillars-side (600,600) door — both a short walk from
        // their local door, versus an aethernet hop whose shards sit far away near the
        // city's other corner (Foundation: 0,0/200,0; Pillars: 0,0/400,0).
        var aethernet = RouteCosting.AethernetCandidate(FoundationShards, PillarsShards, 605f, 605f, 605f, 605f);
        var entrance = RouteCosting.EntranceCandidate(
            FoundationToPillarsLinks, PillarsToFoundationLinks, 605f, 605f, 605f, 605f);

        Assert.NotNull(aethernet);
        Assert.NotNull(entrance);

        var chosen = RouteCosting.Choose(aethernet, entrance, null);

        Assert.Equal(RouteMode.Entrance, chosen!.Mode);
    }

    [Fact]
    public void TeleportCandidate_Suppressed_WhenAetheryteTerritoryIsCurrentTerritory()
    {
        // The split-city TerritoryType.Aetheryte bug: objective is in Pillars (419),
        // but both territories' fallback resolves to the Foundation aetheryte (70) —
        // whose OWN territory (418) is where the player is standing right now.
        var fallback = new AetherytePoint(70, "Foundation", 0f, 0f, Foundation);

        var teleport = RouteCosting.TeleportCandidate(
            fallback,
            aetheryteTerritory: Foundation,
            targetTerritory: Pillars,
            currentTerritory: Foundation,
            tx: 400f,
            tz: 0f,
            unlocked: true);

        Assert.Null(teleport);
    }

    [Fact]
    public void TeleportCandidate_Allowed_WhenAetheryteIsInTargetTerritory()
    {
        var pillarsAetheryte = new AetherytePoint(83, "Athenaeum Astrologicum", 0f, 0f, Pillars);

        var teleport = RouteCosting.TeleportCandidate(
            pillarsAetheryte,
            aetheryteTerritory: Pillars,
            targetTerritory: Pillars,
            currentTerritory: Foundation,
            tx: 10f,
            tz: 0f,
            unlocked: true);

        Assert.NotNull(teleport);
        Assert.Equal(RouteMode.Teleport, teleport!.Mode);
        Assert.True(teleport.Cost < float.MaxValue);
    }

    [Fact]
    public void TeleportCandidate_RankedLast_WhenAetheryteIsInThirdTerritory()
    {
        const uint thirdTerritory = 999;
        var elsewhere = new AetherytePoint(1, "Elsewhere", 0f, 0f, thirdTerritory);

        var teleport = RouteCosting.TeleportCandidate(
            elsewhere,
            aetheryteTerritory: thirdTerritory,
            targetTerritory: Pillars,
            currentTerritory: Foundation,
            tx: 10f,
            tz: 0f,
            unlocked: true);

        Assert.NotNull(teleport);
        Assert.Equal(float.MaxValue, teleport!.Cost);

        // Still loses to a real entrance candidate when one exists…
        var entrance = RouteCosting.EntranceCandidate(
            FoundationToPillarsLinks, PillarsToFoundationLinks, 0f, 0f, 305f, 305f);
        Assert.Equal(RouteMode.Entrance, RouteCosting.Choose(teleport, entrance)!.Mode);

        // …but is still picked when it's the only candidate available.
        Assert.Equal(RouteMode.Teleport, RouteCosting.Choose(teleport, null)!.Mode);
    }

    [Fact]
    public void EntranceCandidate_PicksNearestOfFour_OnBothSides()
    {
        // Player closest to the (300,300) door; objective closest to the (-300,-300)
        // reciprocal door in Pillars space.
        var candidate = RouteCosting.EntranceCandidate(
            FoundationToPillarsLinks, PillarsToFoundationLinks, px: 310f, pz: 310f, tx: -310f, tz: -310f);

        Assert.NotNull(candidate);
        var expectedCost =
            NavMath.Distance(10f, 0, 10f) // player (310,310) -> door (300,300)
            + NavMath.Distance(10f, 0, 10f); // door (-300,-300) -> objective (-310,-310)
        Assert.Equal(expectedCost, candidate!.Cost, 3);
        Assert.Equal(300f, candidate.ArrowX);
        Assert.Equal(300f, candidate.ArrowZ);
    }

    [Fact]
    public void AethernetCandidate_Null_WhenGroupsDiffer()
    {
        var otherGroupShards = new List<AetherytePoint> { new(90, "Elsewhere Shard", 0f, 0f, Pillars, 99) };
        Assert.Null(RouteCosting.AethernetCandidate(FoundationShards, otherGroupShards, 0f, 0f, 0f, 0f));
    }

    [Fact]
    public void AethernetCandidate_Null_WhenEitherSideEmpty()
    {
        Assert.Null(RouteCosting.AethernetCandidate([], PillarsShards, 0f, 0f, 0f, 0f));
        Assert.Null(RouteCosting.AethernetCandidate(FoundationShards, [], 0f, 0f, 0f, 0f));
    }

    [Fact]
    public void EntranceCandidate_Null_WhenEitherSideEmpty()
    {
        Assert.Null(RouteCosting.EntranceCandidate([], PillarsToFoundationLinks, 0f, 0f, 0f, 0f));
        Assert.Null(RouteCosting.EntranceCandidate(FoundationToPillarsLinks, [], 0f, 0f, 0f, 0f));
    }

    [Fact]
    public void TeleportCandidate_Null_WhenAetheryteIsNull()
    {
        Assert.Null(RouteCosting.TeleportCandidate(null, 0, Pillars, Foundation, 0f, 0f, false));
    }

    [Fact]
    public void Choose_ReturnsNull_WhenAllCandidatesMissing()
    {
        Assert.Null(RouteCosting.Choose(null, null, null));
    }

    [Fact]
    public void Choose_SkipsMissingCandidates_WithoutError()
    {
        var entrance = RouteCosting.EntranceCandidate(
            FoundationToPillarsLinks, PillarsToFoundationLinks, 0f, 0f, 305f, 305f);
        var chosen = RouteCosting.Choose(null, entrance, null);
        Assert.Equal(RouteMode.Entrance, chosen!.Mode);
    }
}
