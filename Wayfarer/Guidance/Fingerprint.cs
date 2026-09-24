namespace Wayfarer.Guidance;

/// <summary>A list of the game's numbers folded into one, for a source to tell cheaply whether
/// anything it is made from moved since last frame.</summary>
internal static class Fingerprint
{
    /// <summary>Every item folded together, in order.</summary>
    /// <typeparam name="T">What the items are.</typeparam>
    public static int Of<T>(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var hash = default(HashCode);
        foreach (var item in items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
