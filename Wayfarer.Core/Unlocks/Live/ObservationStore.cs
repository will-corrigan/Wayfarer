using System.Globalization;

namespace Wayfarer.Core.Unlocks.Live;

/// <summary>The highest value ever seen for each (character, kind, id), and nothing else.
///
/// <para>It exists because several things worth gating on are only readable in a context the
/// player is rarely in when they look at the list — inside Bozja, or after the game has fetched
/// something from the server. For a genuinely non-decreasing value, an old observation is never
/// wrong: it is a valid lower bound forever, which is the entire premise. Everything that could
/// decrease is kept out by <see cref="IMonotonicSource{TId}"/>, not by a check here.</para>
///
/// <para>Plain strings and ints, no plugin types, so Core stays serialiser-agnostic and the store
/// can be persisted by whatever the host already persists with.</para></summary>
public sealed class ObservationStore
{
    private readonly Dictionary<string, Dictionary<string, Observation>> byCharacter =
        new(StringComparer.Ordinal);

    /// <summary>Records a value, keeping the higher of it and anything already recorded. A lower
    /// reading is not an error and is not a correction — for a monotonic source it can only mean
    /// the live read and the floor disagree about recency, and the floor is still true.</summary>
    public void Observe(string characterKey, string kind, uint id, int value, DateTimeOffset when)
    {
        var floors = Floors(characterKey);
        var key = Key(kind, id);
        if (floors.TryGetValue(key, out var existing) && existing.Value >= value)
        {
            return;
        }

        floors[key] = new Observation(value, when);
    }

    /// <summary>The highest value ever observed, or false when this character has never been in a
    /// position to observe it — which is the honest "we have never been able to tell", not a zero.</summary>
    public bool TryFloor(string characterKey, string kind, uint id, out int floor)
    {
        floor = 0;
        if (!byCharacter.TryGetValue(characterKey, out var floors)
            || !floors.TryGetValue(Key(kind, id), out var observation))
        {
            return false;
        }

        floor = observation.Value;
        return true;
    }

    /// <summary>Drops observations older than <paramref name="maxAge"/>, and trims a character's
    /// bucket to <paramref name="maxEntriesPerCharacter"/> newest entries. Purely a
    /// storage-growth backstop: for a monotonic value an old floor is still true, so nothing here
    /// is a correctness measure.</summary>
    public void Prune(TimeSpan maxAge, DateTimeOffset now, int? maxEntriesPerCharacter = null)
    {
        foreach (var character in byCharacter.Keys.ToList())
        {
            var floors = byCharacter[character];
            foreach (var key in floors.Keys.ToList())
            {
                if (now - floors[key].ObservedUtc > maxAge)
                {
                    floors.Remove(key);
                }
            }

            if (maxEntriesPerCharacter is { } cap && floors.Count > cap)
            {
                foreach (var key in floors.OrderBy(p => p.Value.ObservedUtc).Take(floors.Count - cap)
                             .Select(p => p.Key).ToList())
                {
                    floors.Remove(key);
                }
            }

            if (floors.Count == 0)
            {
                byCharacter.Remove(character);
            }
        }
    }

    private static string Key(string kind, uint id) =>
        $"{kind}:{id.ToString(CultureInfo.InvariantCulture)}";

    private Dictionary<string, Observation> Floors(string characterKey)
    {
        if (!byCharacter.TryGetValue(characterKey, out var floors))
        {
            floors = new Dictionary<string, Observation>(StringComparer.Ordinal);
            byCharacter[characterKey] = floors;
        }

        return floors;
    }

    private sealed record Observation(int Value, DateTimeOffset ObservedUtc);
}
