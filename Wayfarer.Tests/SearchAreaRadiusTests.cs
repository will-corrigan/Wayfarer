using Wayfarer.Core.Navigation;

namespace Wayfarer.Tests;

/// <summary>Pins <see cref="SearchAreaRadius.IsArea"/> against the REAL quest-step radii the
/// threshold was measured from — not synthetic numbers — so a future change to the constant fails
/// loudly against the exact fixtures that justified it. Re-verified live against the currently
/// installed game data (Lumina over the retail <c>sqpack</c>) for this task, not merely copied from
/// the provenance spec's own numbers.</summary>
public class SearchAreaRadiusTests
{
    // --- Genuine areas: the client draws a circle for these. ---
    [Fact]
    public void HeroesOfTheHour_FinalStep_IsAnArea()
    {
        // Quest 67782 "Heroes of the Hour", the "talk to Aymeric to complete the quest" step
        // (Level row 6211189, Level.Type 51). THIS is the step that fooled the original revert
        // (commit 24c96bb) into concluding radius could not separate the two cases — it read as an
        // ordinary single-NPC "speak with" step and was assumed to be a point. It is not: the client
        // draws a circle for it too — see SearchAreaRadius for the full evidence.
        Assert.True(SearchAreaRadius.IsArea(203f));
    }

    [Fact]
    public void TheFullReportWartsAndAll_FrogSearchStep_BothLocationsAreAreas()
    {
        // Quest 69901 "The Full Report, Warts and All", the frog-transfiguration search step —
        // the genuine "search this area" objective the user originally reported. It carries TWO
        // Type-51 area locations on the same todo slot (Level rows 8912037 and 9033405) alongside
        // three radius-1 Type-8 companion-escort points on that identical step — classification is
        // per location, not per step.
        Assert.True(SearchAreaRadius.IsArea(406f));
        Assert.True(SearchAreaRadius.IsArea(102f));
    }

    [Fact]
    public void TheOneKnownRadiusTypeDisagreement_ResolvesInFavourOfRadius()
    {
        // The ONE row in the whole quest-step census where Level.Radius >= 20 but Level.Type != 51:
        // Level row 10589881, radius 50, Type 49 ("Battle NPC / target range") — the "Land at the
        // designated location" step of Quest 70602 "Leap into the Unknown". Radius wins: it is the
        // field that actually varies per location and is what the client uses to draw the circle,
        // while Type is corroboration, not an override — a 50-yalm landing zone reads as a genuine
        // area regardless of which type code the row happens to be filed under.
        Assert.True(SearchAreaRadius.IsArea(50f));
    }

    // --- Genuine points: an ordinary, precise objective — including some with wording that says
    // "search", which is exactly why wording is not the signal (verified: see the provenance spec
    // §7b — "Search" steps exist that are points and "Speak" steps exist that are areas). ---
    [Fact]
    public void ASearchWordedStepAtRadiusOne_IsStillAPoint()
    {
        // "The Hazy Professor" (Quest 65780), "Search for the students near Camp Drybone." — Level.
        // Type 45, radius 1. A good trap fixture precisely because the wording says "search" while
        // the location is exact: this is why the classifier must never read step text.
        Assert.False(SearchAreaRadius.IsArea(1f));
    }

    [Fact]
    public void HeroesOfTheHour_EnterFortempsManor_IsAPoint()
    {
        // Quest 67782 "Heroes of the Hour", "Enter Fortemps Manor." — Level.Type 51, radius 2. A
        // small Type-51 "pop range" (a doorway), not a search area — proof that Level.Type alone is
        // over-inclusive and radius is the decisive signal, not a shortcut for it.
        Assert.False(SearchAreaRadius.IsArea(2f));
    }

    [Fact]
    public void HeroesOfTheHour_SpeakWithLucia_IsAPoint()
    {
        // Same quest, its very first step: an ordinary Type-8 NPC-talk point.
        Assert.False(SearchAreaRadius.IsArea(1f));
    }

    // --- The boundary itself: the threshold sits in a near-empty valley (21 of 24,260 step
    // locations game-wide fall between 6 and 20 yalms), so this is not a knife-edge in real data —
    // these values are synthetic probes of the constant, not additional real fixtures. ---
    [Fact]
    public void TheThresholdIsInclusive()
    {
        Assert.Equal(SearchAreaRadius.ThresholdYalms, 20f);
        Assert.True(SearchAreaRadius.IsArea(20f));
    }

    [Fact]
    public void JustBelowTheThreshold_IsAPoint()
    {
        Assert.False(SearchAreaRadius.IsArea(19.9f));
    }

    [Fact]
    public void ZeroAndNegativeRadii_AreNeverAreas()
    {
        Assert.False(SearchAreaRadius.IsArea(0f));
        Assert.False(SearchAreaRadius.IsArea(-1f));
    }
}
