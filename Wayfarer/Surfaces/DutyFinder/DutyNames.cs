namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>Duties by the name their row reads, and the rule for when that name does not identify
/// one. Reading a row's words is how a row is told what it is about, so everything turns on a name
/// meaning exactly one duty.
///
/// <para>Two duties under one name answer nothing rather than answering either: a mark on the
/// wrong duty tells the player to queue for something they have no reason to. One duty listed
/// twice under its own name is not that, and still answers.</para></summary>
internal sealed class DutyNames
{
    private readonly Dictionary<string, uint> dutiesByName = new(StringComparer.Ordinal);
    private readonly HashSet<string> shared = new(StringComparer.Ordinal);

    /// <summary>Forgets every name, for a list read afresh.</summary>
    public void Clear()
    {
        dutiesByName.Clear();
        shared.Clear();
    }

    /// <summary>Files a duty under the name its row reads. A name with nothing in it is no name.</summary>
    public void Add(string name, uint duty)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || duty == 0)
        {
            return;
        }

        if (dutiesByName.TryGetValue(name, out var already) && already != duty)
        {
            shared.Add(name);
            return;
        }

        dutiesByName[name] = duty;
    }

    /// <summary>The duty a row showing these words is for, or null when no one duty is.</summary>
    public uint? DutyNamed(string name) =>
        !shared.Contains(name) && dutiesByName.TryGetValue(name, out var duty) ? duty : null;
}
