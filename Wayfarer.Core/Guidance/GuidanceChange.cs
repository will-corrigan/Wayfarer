using Wayfarer.Core.Routing;

namespace Wayfarer.Core.Guidance;

/// <summary>Whether two guidances say the same thing, for deciding when to publish.
///
/// <para>A walk's length changes every frame the player moves, and so would a naive comparison.
/// What a surface has to re-lay-out for is the words and the shape of the route: which legs, in
/// which order, to which place. So a walk is compared as "a walk", never by its length; the
/// distance is a per-frame number the surfaces that show it read for themselves.</para></summary>
public static class GuidanceChange
{
    /// <summary>True when nothing a surface lays out from has changed.</summary>
    public static bool IsSame(PublishedGuidance? a, PublishedGuidance? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return ReferenceEquals(a.Source, b.Source)
            && SameObjective(a.Objective, b.Objective)
            && SameRoute(a.Route, b.Route);
    }

    private static bool SameObjective(Objective a, Objective b)
    {
        if (!string.Equals(a.Headline, b.Headline, StringComparison.Ordinal)
            || a.Progress != b.Progress
            || a.Entries.Count != b.Entries.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Entries.Count; i++)
        {
            if (!SameEntry(a.Entries[i], b.Entries[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameEntry(ObjectiveEntry a, ObjectiveEntry b) =>
        string.Equals(a.Text, b.Text, StringComparison.Ordinal)
        && a.Progress == b.Progress
        && SameDestination(a.Where, b.Where);

    private static bool SameDestination(Destination a, Destination b) => (a, b) switch
    {
        (Destination.Reachable x, Destination.Reachable y) => x.Places.SequenceEqual(y.Places),
        (Destination.InDuty x, Destination.InDuty y) => x.DutyId == y.DutyId,
        (Destination.Blocked x, Destination.Blocked y) => string.Equals(x.Reason, y.Reason, StringComparison.Ordinal),
        _ => false,
    };

    private static bool SameRoute(Route? a, Route? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a.End != b.End || a.Legs.Count != b.Legs.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Legs.Count; i++)
        {
            if (!SameLeg(a.Legs[i], b.Legs[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameLeg(Leg a, Leg b) => (a, b) switch
    {
        (Leg.Walk, Leg.Walk) => true,
        (Leg.Teleport x, Leg.Teleport y) => x.AetheryteId == y.AetheryteId,
        (Leg.ShardHop x, Leg.ShardHop y) =>
            string.Equals(x.EntryShard, y.EntryShard, StringComparison.Ordinal)
            && string.Equals(x.ExitShard, y.ExitShard, StringComparison.Ordinal),
        (Leg.Door x, Leg.Door y) => string.Equals(x.Name, y.Name, StringComparison.Ordinal),
        _ => false,
    };
}
