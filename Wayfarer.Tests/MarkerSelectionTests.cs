using Wayfarer.Core.Navigation;

namespace Wayfarer.Tests;

/// <summary>Pure marker-precedence decision extracted from QuestNavigator's live
/// quest-marker scan. Territory 419 / map
/// 100 mirrors the live Pillars scenario used elsewhere in the test suite.</summary>
public class MarkerSelectionTests
{
    private const uint Territory = 419;
    private const uint MapId = 100;
    private const uint OtherMapId = 101;
    private const uint OtherTerritory = 418;

    [Fact]
    public void Select_ReturnsExact_WhenTerritoryAndMapBothMatch()
    {
        var markers = new List<MarkerPoint> { new(10f, 0f, 10f, Territory, MapId) };

        var (match, marker) = MarkerSelection.Select(markers, Territory, MapId, 0f, 0f, 0f);

        Assert.Equal(MarkerMatch.Exact, match);
        Assert.Equal(markers[0], marker);
    }

    [Fact]
    public void Select_ExactMatch_WinsOverCloserTerritoryOnlyMarker()
    {
        // The territory-only marker is right next to the player; the exact match is
        // far away. Exactness is a hard precedence — it must still win, because only
        // an exact match is safe to arrow straight at (see MarkerMatch.TerritoryOnly).
        var farExact = new MarkerPoint(1000f, 0f, 1000f, Territory, MapId);
        var nearTerritoryOnly = new MarkerPoint(1f, 0f, 1f, Territory, OtherMapId);
        var markers = new List<MarkerPoint> { nearTerritoryOnly, farExact };

        var (match, marker) = MarkerSelection.Select(markers, Territory, MapId, 0f, 0f, 0f);

        Assert.Equal(MarkerMatch.Exact, match);
        Assert.Equal(farExact, marker);
    }

    [Fact]
    public void Select_ReturnsTerritoryOnly_WhenMapDiffersButTerritoryMatches()
    {
        var markers = new List<MarkerPoint> { new(5f, 0f, 5f, Territory, OtherMapId) };

        var (match, marker) = MarkerSelection.Select(markers, Territory, MapId, 0f, 0f, 0f);

        Assert.Equal(MarkerMatch.TerritoryOnly, match);
        Assert.Equal(markers[0], marker);
    }

    [Fact]
    public void Select_PicksNearest_AmongMultipleTerritoryOnlyMarkers()
    {
        var far = new MarkerPoint(100f, 0f, 0f, Territory, OtherMapId);
        var near = new MarkerPoint(5f, 0f, 0f, Territory, OtherMapId);
        var markers = new List<MarkerPoint> { far, near };

        var (match, marker) = MarkerSelection.Select(markers, Territory, MapId, 0f, 0f, 0f);

        Assert.Equal(MarkerMatch.TerritoryOnly, match);
        Assert.Equal(near, marker);
    }

    [Fact]
    public void Select_ReturnsNone_WhenNoMarkerIsInCurrentTerritory()
    {
        // Markers exist, but only for a different territory — the caller's
        // cross-territory ("markers[0]") fallback path handles that case; this
        // function only reasons about same-territory candidates.
        var markers = new List<MarkerPoint> { new(0f, 0f, 0f, OtherTerritory, MapId) };

        var (match, marker) = MarkerSelection.Select(markers, Territory, MapId, 0f, 0f, 0f);

        Assert.Equal(MarkerMatch.None, match);
        Assert.Null(marker);
    }

    [Fact]
    public void Select_ReturnsNone_WhenMarkerListIsEmpty()
    {
        var (match, marker) = MarkerSelection.Select([], Territory, MapId, 0f, 0f, 0f);

        Assert.Equal(MarkerMatch.None, match);
        Assert.Null(marker);
    }

    [Fact]
    public void Select_PrefersAPointMarker_OverACloserAreaMarker()
    {
        // The area marker's own coordinate is only the CENTRE of a circle the true objective could
        // be anywhere inside — it is not more precise just because it happens to be nearer. A point
        // marker, wherever it is, is unambiguous evidence of exactly where the objective is, and
        // must win even against a closer area marker.
        var closerArea = new MarkerPoint(1f, 0f, 1f, Territory, MapId, Radius: 20f);
        var fartherPoint = new MarkerPoint(50f, 0f, 50f, Territory, MapId);
        var markers = new List<MarkerPoint> { closerArea, fartherPoint };

        var (match, marker) = MarkerSelection.Select(markers, Territory, MapId, 0f, 0f, 0f);

        Assert.Equal(MarkerMatch.Exact, match);
        Assert.Equal(fartherPoint, marker);
    }

    [Fact]
    public void Select_PrefersAPointMarker_OverACloserAreaMarker_InTheTerritoryOnlyTier()
    {
        var closerArea = new MarkerPoint(1f, 0f, 1f, Territory, OtherMapId, Radius: 20f);
        var fartherPoint = new MarkerPoint(50f, 0f, 50f, Territory, OtherMapId);
        var markers = new List<MarkerPoint> { closerArea, fartherPoint };

        var (match, marker) = MarkerSelection.Select(markers, Territory, MapId, 0f, 0f, 0f);

        Assert.Equal(MarkerMatch.TerritoryOnly, match);
        Assert.Equal(fartherPoint, marker);
    }

    [Fact]
    public void Select_PicksNearest_AmongMultipleAreaMarkers_WhenThereIsNoPointMarker()
    {
        // With no point marker to prefer, distance still breaks the tie among area markers exactly
        // as it always has among same-precision candidates.
        var far = new MarkerPoint(100f, 0f, 0f, Territory, MapId, Radius: 20f);
        var near = new MarkerPoint(5f, 0f, 0f, Territory, MapId, Radius: 20f);
        var markers = new List<MarkerPoint> { far, near };

        var (match, marker) = MarkerSelection.Select(markers, Territory, MapId, 0f, 0f, 0f);

        Assert.Equal(MarkerMatch.Exact, match);
        Assert.Equal(near, marker);
    }

    [Fact]
    public void Select_ReturnsTheAreaMarkerAndItsRadius_WhenItIsTheOnlyCandidate()
    {
        var markers = new List<MarkerPoint> { new(10f, 0f, 10f, Territory, MapId, Radius: 30f) };

        var (match, marker) = MarkerSelection.Select(markers, Territory, MapId, 0f, 0f, 0f);

        Assert.Equal(MarkerMatch.Exact, match);
        Assert.Equal(30f, marker!.Radius);
    }
}
