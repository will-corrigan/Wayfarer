namespace Wayfarer.Core.Unlocks;

/// <summary>How many stops a "Route Me" plan is allowed to hold, and the words that say so.
///
/// <para><b>Why a cap.</b> The plan was every available, locatable entry in whatever the filters had
/// left. On the 587-entry catalogue that was a few dozen; on 1,208 it is hundreds of waypoints
/// chained across every zone in the game, which is not a route anybody walks — it is a queue nobody
/// can see the end of, and the arrow spends the whole time pointing at a decision the player never
/// made.</para>
///
/// <para><b>Why the cap is written on the button.</b> This is the part that is not a detail. A
/// truncation the player cannot see reads as "this is everything": they walk eight stops, the route
/// ends, and the honest conclusion from what is on screen is that there was nothing else. Saying
/// "next 8 of 47" makes the plan a page of a larger thing, which is what it is, and it turns the end
/// of the route into an invitation to press it again rather than into a false finish. The same
/// reasoning is why <see cref="RoutePlanner.Order"/> stays uncapped: the ordering and the cap are
/// separate decisions, and a caller that wants the whole ordering must not have to defeat a cap to
/// get it.</para></summary>
public static class UnlockRouteCap
{
    /// <summary>Stops one plan may hold.
    ///
    /// <para>Eight is a walkable errand rather than a session: enough to be worth starting a route
    /// for instead of picking rows off one at a time, few enough that the last stop is still
    /// something the player can hold in their head when they take the first.</para></summary>
    public const int Stops = 8;

    /// <summary>How many stops a plan of <paramref name="total"/> candidates will actually hold.</summary>
    public static int Take(int total) => total < Stops ? Math.Max(total, 0) : Stops;

    /// <summary>Whether a plan of <paramref name="total"/> candidates leaves any out.</summary>
    public static bool Truncates(int total) => total > Stops;

    /// <summary>What the button says.
    ///
    /// <para>Three cases, and the middle one is the one worth being careful about. With nothing
    /// routable the button is bare and disabled. With <b>at most</b> <see cref="Stops"/> candidates
    /// the plan IS all of them, so the count alone is the whole truth and "next 8 of 8" would invent
    /// a cap the player has not hit. Past the cap it names both numbers, every time.</para></summary>
    public static string ButtonLabel(int total)
    {
        if (total <= 0)
        {
            return "Route Me";
        }

        return Truncates(total)
            ? $"Route: next {Stops} of {total}"
            : $"Route Me ({total})";
    }

    /// <summary>The same two numbers in the width a list row's trailing caption has: <c>8 of 47</c>
    /// capped, the bare count under the cap, empty with nothing to route. No "Route:" prefix — the
    /// row it sits on is already named "Unlock Route".</summary>
    public static string Caption(int total)
    {
        if (total <= 0)
        {
            return string.Empty;
        }

        return Truncates(total)
            ? $"{Stops} of {total}"
            : total.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The same statement for a surface with room for a sentence — the follow list's
    /// caption and the fallback window's tooltip. Says what the route will do and, when it is
    /// capped, what it will not.</summary>
    public static string Explanation(int total) =>
        Truncates(total)
            ? $"Walks the nearest {Stops} of {total}, nearest first. Press again for the next {Stops}."
            : "Walks every quest above, nearest first.";
}
