using Wayfarer.Core.Navigation;

namespace Wayfarer.Tests;

/// <summary>Which map(s) should be scanned for an aetheryte's map markers. Sheet-shaped
/// after live Ishgard data (verified via Lumina against the game's own sqpack,
/// 2026-08-22): every Ishgard shard row (80–82 Foundation, 83–87 The Pillars) carries
/// Map=0 and dead Level refs, while the TERRITORY's map (218 Foundation / 219 The
/// Pillars) does carry the DataType 4 markers for those shards — so position
/// resolution must fall back to the territory's map when the row's own map reference
/// is missing. Because of that gap, intra-Ishgard shard routing never fired at all.</summary>
public class AetheryteMapSearchTests
{
    private const uint FoundationMap = 218;
    private const uint PillarsMap = 219;

    [Fact]
    public void RowMap_ComesFirst_WhenBothPresent()
    {
        // Hub row 70 shape: own Map=218, territory 418's map also 218-adjacent; a row
        // with a real map still gets the territory map as a backstop, in order.
        Assert.Equal(
            [FoundationMap, PillarsMap],
            AetheryteMapSearch.CandidateMaps(FoundationMap, PillarsMap));
    }

    [Fact]
    public void TerritoryMap_IsTheFallback_WhenRowMapIsMissing()
    {
        // Ishgard shard shape: Aetheryte.Map = 0, TerritoryType.Map = 219.
        Assert.Equal([PillarsMap], AetheryteMapSearch.CandidateMaps(0, PillarsMap));
    }

    [Fact]
    public void RowMap_Alone_WhenTerritoryMapIsMissing()
    {
        Assert.Equal([FoundationMap], AetheryteMapSearch.CandidateMaps(FoundationMap, 0));
    }

    [Fact]
    public void Unresolvable_WhenBothMapsAreMissing()
    {
        Assert.Empty(AetheryteMapSearch.CandidateMaps(0, 0));
    }

    [Fact]
    public void DuplicateMaps_AreSearchedOnce()
    {
        // Row 70's real shape: Aetheryte.Map = 218 AND Territory 418's map = 218.
        Assert.Equal(
            [FoundationMap],
            AetheryteMapSearch.CandidateMaps(FoundationMap, FoundationMap));
    }

    /// <summary>Once the map fallback resolves positions for all nine Ishgard rows
    /// (plaza 70 + shards 80–87), the router must produce intra-Ishgard aethernet
    /// candidates in BOTH directions — the exact outcome that never happened live
    /// while the shard lists were position-filtered down to empty.</summary>
    [Fact]
    public void IshgardShards_WithResolvedPositions_ProduceIntraCityAethernetCandidates()
    {
        const uint foundation = 418;
        const uint pillars = 419;
        const uint group = 4;

        List<AetherytePoint> foundationShards =
        [
            new(70, "Ishgard Aetheryte Plaza", 0f, 0f, foundation, group),
            new(80, "The Forgotten Knight", 120f, -40f, foundation, group),
            new(81, "Skysteel Manufactory", -140f, 60f, foundation, group),
            new(82, "The Brume", 80f, 180f, foundation, group),
        ];

        List<AetherytePoint> pillarsShards =
        [
            new(83, "Athenaeum Astrologicum", -60f, -120f, pillars, group),
            new(84, "The Jeweled Crozier", 40f, 20f, pillars, group),
            new(85, "Saint Reymanaud's Cathedral", 160f, -80f, pillars, group),
            new(86, "The Tribunal", 200f, 100f, pillars, group),
            new(87, "The Last Vigil", -20f, 220f, pillars, group),
        ];

        var toPillars = RouteCosting.AethernetCandidate(
            foundationShards, pillarsShards, 10f, 0f, 150f, -70f);
        Assert.NotNull(toPillars);
        Assert.Equal(RouteMode.Aethernet, toPillars.Mode);
        Assert.Equal("Ishgard Aetheryte Plaza", toPillars.AethernetEntryName);
        Assert.Equal("Saint Reymanaud's Cathedral", toPillars.AethernetExitName);

        var toFoundation = RouteCosting.AethernetCandidate(
            pillarsShards, foundationShards, 30f, 15f, 85f, 175f);
        Assert.NotNull(toFoundation);
        Assert.Equal("The Jeweled Crozier", toFoundation.AethernetEntryName);
        Assert.Equal("The Brume", toFoundation.AethernetExitName);
    }
}
