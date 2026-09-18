using System.Globalization;
using Wayfarer.Core.Guidance;

namespace Wayfarer.Core.Presentation;

/// <summary>The words a surface prints for an entry: its text, with its count after it the way
/// the game's own tracker writes one, "Slay opo-opos. 3/8".</summary>
public static class EntryWords
{
    private const string CountSeparator = " ";
    private const string CountDivider = "/";

    public static string Describe(ObjectiveEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Progress is { } count
            ? entry.Text + CountSeparator + count.Done.ToString(CultureInfo.InvariantCulture) + CountDivider + count.Needed.ToString(CultureInfo.InvariantCulture)
            : entry.Text;
    }
}
