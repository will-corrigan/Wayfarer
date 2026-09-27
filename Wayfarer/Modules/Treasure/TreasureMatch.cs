using System.Numerics;

namespace Wayfarer.Modules.Treasure;

/// <summary>Which loaded chests stand on a known treasure spot.</summary>
internal static class TreasureMatch
{
    /// <summary>How far across the ground a chest may stand from a spot and still be the one on it.
    /// A spot is exactly where the chest is placed, so this only absorbs rounding.</summary>
    public const float Reach = 3f;

    /// <summary>Where in <paramref name="chests"/> the chest standing on this spot is, or -1.</summary>
    public static int On(Vector3 spot, IReadOnlyList<Vector3> chests)
    {
        ArgumentNullException.ThrowIfNull(chests);
        for (var i = 0; i < chests.Count; i++)
        {
            if (Across(spot, chests[i]) <= Reach)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Whether any of these chests stands on this spot.</summary>
    public static bool Holds(Vector3 spot, IReadOnlyList<Vector3> chests) => On(spot, chests) >= 0;

    /// <summary>Where in <paramref name="chests"/> each chest standing on none of these spots is,
    /// in order.</summary>
    public static List<int> OffSpots(IReadOnlyList<Vector3> chests, IReadOnlyList<Vector3> spots)
    {
        ArgumentNullException.ThrowIfNull(chests);
        ArgumentNullException.ThrowIfNull(spots);
        var off = new List<int>();
        for (var i = 0; i < chests.Count; i++)
        {
            if (!Holds(chests[i], spots))
            {
                off.Add(i);
            }
        }

        return off;
    }

    private static float Across(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));
}
