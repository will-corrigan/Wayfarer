namespace Wayfarer.Core.Ui;

/// <summary>Whether the player is still approaching a "search this area" quest objective's circle,
/// or already standing inside it.
///
/// <para><b>Why the arrow cannot just point at the centre once you are inside.</b> The centre
/// coordinate the game hands over is not where the thing to find actually is — it is only the
/// middle of a circle the game itself draws on the map because it does NOT know, or does not say,
/// the exact spot. Once the player has walked into that circle, a precise-looking arrow a few
/// yalms away is worse than useless: it is confidently pointing at nothing in particular, which is
/// the exact defect this feature exists to remove (see <c>QuestObjectiveSource</c>'s doc comment
/// for the live report — an arrow sending a player 66 yalms at a circle's centre for a "search the
/// area" step).</para>
///
/// <para><b>Why this needs hysteresis, mirroring <see cref="Elevation"/>.</b> A player circling
/// near the boundary — which is common, since the boundary is itself an arbitrary line the game
/// drew, not a wall — would otherwise flip the readout between "outside" and "inside" wording (and
/// the arrow appearing/disappearing) every time they crossed it. <see cref="BoundaryHysteresisYalms"/>
/// is the same "one jump's worth" reasoning <see cref="Elevation"/> uses: enough that ordinary
/// movement near the edge cannot cross both bounds in one step.</para></summary>
public static class SearchArea
{
    /// <summary>How far past the radius the player has to walk, in either direction, before the
    /// readout changes its mind. One jump's worth — see the type doc comment.</summary>
    public const float BoundaryHysteresisYalms = 2f;

    /// <summary>Classifies whether the player is outside or inside the search circle, given what
    /// the readout was saying last frame.
    ///
    /// <para><paramref name="radiusYalms"/> null or non-positive means this is not a search-area
    /// objective at all — an ordinary point objective — in which case the answer is always
    /// <see cref="SearchAreaHint.NotApplicable"/> regardless of <paramref name="previous"/>, which
    /// is what keeps a zero-radius (or absent-radius) objective's behaviour byte-identical to
    /// before this feature existed.</para></summary>
    /// <param name="distanceYalms">Straight-line distance from the player to the circle's centre, or
    /// null when there is no target to measure against.</param>
    /// <param name="radiusYalms">The objective's search-area radius in yalms, or null for a point.
    /// Callers are expected to have already decided this IS an area (see
    /// <see cref="Navigation.SearchAreaRadius.IsArea"/> — <see cref="Guidance.GuidanceProjection"/>
    /// nulls out anything below that threshold before it reaches here); the non-positive guard
    /// below is defensive, not the primary gate.</param>
    /// <param name="previous">What was shown last frame, which is what supplies the hysteresis.</param>
    public static SearchAreaHint Classify(
        float? distanceYalms, float? radiusYalms, SearchAreaHint previous = SearchAreaHint.Outside)
    {
        if (radiusYalms is not { } radius || radius <= 0f
            || distanceYalms is not { } distance || float.IsNaN(distance))
        {
            return SearchAreaHint.NotApplicable;
        }

        // Symmetric dead zone around the true boundary: already inside stays inside until the
        // player walks BoundaryHysteresisYalms past the edge; already outside only becomes inside
        // once they are BoundaryHysteresisYalms past it the other way. Clamped at zero — inclusive
        // ("<=", not "<") — so a circle smaller than the hysteresis band can still ever be entered:
        // a radius of 1 clamps its enter bound to exactly 0, and standing on the centre (distance 0)
        // must count as having reached it.
        var enterAt = Math.Max(0f, radius - BoundaryHysteresisYalms);
        var exitAt = radius + BoundaryHysteresisYalms;

        var inside = previous == SearchAreaHint.Inside ? distance <= exitAt : distance <= enterAt;
        return inside ? SearchAreaHint.Inside : SearchAreaHint.Outside;
    }
}
