using Wayfarer.Routing;

namespace Wayfarer.Guidance;

/// <summary>Whether two guidances say the same thing, for deciding when to publish.
///
/// <para>A walk's length changes every frame the player moves, and so would a naive comparison.
/// What a surface has to re-lay-out for is the words and the shape of the route: which legs, in
/// which order, to which place. So a walk is compared by where it ends, never by its length; the
/// distance is a per-frame number the surfaces that show it read for themselves.</para></summary>
public static class GuidanceChange
{
    /// <summary>True when nothing a surface lays out from has changed.</summary>
    public static bool IsSame(PublishedGuidance? a, PublishedGuidance? b) =>
        Both(a, b, (x, y) =>
            ReferenceEquals(x.Source, y.Source)
            && SameObjective(x.Objective, y.Objective)
            && Both(x.Target, y.Target, SameEntry)
            && SameRoute(x.Route, y.Route));

    /// <summary>Two things that may each be missing are the same when neither is there, or when
    /// both are and <paramref name="same"/> says so.</summary>
    private static bool Both<T>(T? a, T? b, Func<T, T, bool> same)
        where T : class =>
        a is null || b is null ? a is null && b is null : same(a, b);

    /// <summary>Two lists are the same when they hold the same things in the same order.</summary>
    private static bool Each<T>(IReadOnlyList<T> a, IReadOnlyList<T> b, Func<T, T, bool> same)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!same(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameObjective(Objective a, Objective b) =>
        string.Equals(a.Headline, b.Headline, StringComparison.Ordinal)
        && a.HeadlinePressable == b.HeadlinePressable
        && string.Equals(a.Kind, b.Kind, StringComparison.Ordinal)
        && Each(a.Entries, b.Entries, SameEntry);

    private static bool SameEntry(ObjectiveEntry a, ObjectiveEntry b) =>
        string.Equals(a.Text, b.Text, StringComparison.Ordinal)
        && a.Progress == b.Progress
        && a.Action == b.Action
        && SameDestination(a.Where, b.Where);

    private static bool SameDestination(Destination a, Destination b) => (a, b) switch
    {
        (Destination.Reachable x, Destination.Reachable y) => x.Places.SequenceEqual(y.Places),
        (Destination.AtObject x, Destination.AtObject y) => x.Id == y.Id && x.At == y.At,
        (Destination.InDuty x, Destination.InDuty y) => x.DutyId == y.DutyId,
        (Destination.InRoulette x, Destination.InRoulette y) => x.RouletteId == y.RouletteId,
        (Destination.Blocked x, Destination.Blocked y) => string.Equals(x.Reason, y.Reason, StringComparison.Ordinal),
        _ => false,
    };

    private static bool SameRoute(Route? a, Route? b) =>
        Both(a, b, (x, y) => x.End == y.End && Each(x.Legs, y.Legs, SameLeg));

    private static bool SameLeg(Leg a, Leg b) => (a, b) switch
    {
        (Leg.Walk x, Leg.Walk y) => x.To == y.To,
        (Leg.Teleport x, Leg.Teleport y) => x.AetheryteId == y.AetheryteId,
        (Leg.ShardHop x, Leg.ShardHop y) =>
            string.Equals(x.EntryShard, y.EntryShard, StringComparison.Ordinal)
            && string.Equals(x.ExitShard, y.ExitShard, StringComparison.Ordinal),
        (Leg.Door x, Leg.Door y) => string.Equals(x.Name, y.Name, StringComparison.Ordinal),
        _ => false,
    };
}
