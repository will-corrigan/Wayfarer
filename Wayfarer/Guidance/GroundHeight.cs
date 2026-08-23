using System.Numerics;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;

namespace Wayfarer.Guidance;

/// <summary>Finds the height of the ground under a point, using the game's own collision.
///
/// <para><b>Why the readout needs this.</b> The readout wants to say "the target is above you", and
/// the only height it has for the target is whatever coordinate the objective came with. For a quest
/// that is the game's own marker data and it is right. For a curated hunting or unlock coordinate it
/// is whatever was written down, which may be a nominal figure that has never been checked against
/// the terrain. Claiming a target is two storeys up off a number like that is exactly the confident
/// wrong answer this feature must not give, so the coordinate is checked against the world first and
/// the claim is dropped when it cannot be.</para>
///
/// <para><b>The recipe</b> is the one <c>una-xiv/umbra</c> uses to ground a world marker: cast
/// <i>up</i> a little first, to get out of any geometry the stored point is buried in, then cast
/// <i>down</i> from there. <c>BGCollisionModule.RaycastMaterialFilter</c> is a client-structs member
/// function with no third-party dependency, and its own documentation notes that the update lock is
/// taken as a shared lock during raycasts — i.e. casting from the framework thread, which is the only
/// place this is ever called from, is the supported pattern.</para>
///
/// <para><b>Both distances are short on purpose.</b> A collision ray returns the top of
/// <i>whatever is there</i> — a crate, a branch, the roof of the building the target is standing
/// inside. Casting up two yalms and down twenty keeps the answer within the storey the coordinate
/// already claims to be on: it corrects a point that is a little under or a little over the floor,
/// and it cannot silently relocate the target to a roof. A cast that finds nothing at all means the
/// coordinate is nowhere near any surface, which is precisely the case that has to be
/// suppressed.</para></summary>
internal static class GroundHeight
{
    /// <summary>How far above the stored point to start. Enough to escape a coordinate sitting a
    /// little inside the floor; not enough to reach a ceiling and start measuring the storey above.</summary>
    private const float EscapeUpYalms = 2f;

    /// <summary>How far down to look for a floor. Twenty yalms is two or three storeys — generous
    /// for a coordinate that is merely imprecise, and far short of the distance that would let the
    /// answer come back from a different level of the building.</summary>
    private const float SearchDownYalms = 20f;

    /// <summary>The ground height under <paramref name="point"/>, or null when nothing was hit —
    /// in which case the caller must not claim anything about elevation.
    ///
    /// <para>Never throws: this is reachable from the readout's per-frame path, and a collision
    /// module that is not ready yet (between zones, during a load) is a normal state, not a
    /// fault.</para></summary>
    public static float? Resolve(Vector3 point)
    {
        try
        {
            var start = point with { Y = point.Y + EscapeUpYalms };
            if (BGCollisionModule.RaycastMaterialFilter(point, Vector3.UnitY, out var up, EscapeUpYalms))
            {
                // Something is directly overhead within the escape distance: start just under it,
                // so the downward cast does not immediately re-hit the same surface.
                start = up.Point with { Y = up.Point.Y - 0.01f };
            }

            return BGCollisionModule.RaycastMaterialFilter(start, -Vector3.UnitY, out var down, SearchDownYalms)
                ? down.Point.Y
                : null;
        }
        catch (Exception)
        {
            // Deliberately silent. There is no useful per-frame report to make and the caller's
            // fallback — say nothing about elevation — is already the safe answer.
            return null;
        }
    }
}
