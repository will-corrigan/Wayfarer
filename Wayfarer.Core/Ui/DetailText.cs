namespace Wayfarer.Core.Ui;

/// <summary>Joining a list of facts into the fixed number of lines a block has room for, with an
/// honest tail when there were more.
///
/// <para>Truncating silently is the same defect as an ellipsised gutter: the player cannot tell
/// there was more, and the one place it matters most is a locked entry's requirements, where the
/// thing that went missing may be the thing standing in the way. The tail costs a line of its own
/// and takes it from the budget rather than being added past it — adding it past the budget is
/// exactly how the block used to run out of the bottom of the pane.</para></summary>
public static class DetailText
{
    /// <summary>The lines as bullets, capped at <paramref name="budget"/>.</summary>
    public static string Bullets(IReadOnlyList<string> lines, int budget, out int drawn) =>
        Join(lines, budget, bullet: true, out drawn);

    /// <summary>The lines as they are, capped at <paramref name="budget"/>. For a block whose lines
    /// are statements rather than a list — the Information section's giver, coordinates and quest.
    /// </summary>
    public static string Lines(IReadOnlyList<string> lines, int budget, out int drawn) =>
        Join(lines, budget, bullet: false, out drawn);

    private static string Join(IReadOnlyList<string> lines, int budget, bool bullet, out int drawn)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0 || budget <= 0)
        {
            drawn = 0;
            return string.Empty;
        }

        var shown = Math.Min(lines.Count, budget);
        drawn = shown;

        if (lines.Count <= shown)
        {
            return string.Join('\n', lines.Take(shown).Select(line => Mark(line, bullet)));
        }

        var tail = Mark($"and {lines.Count - shown + 1} more", bullet);
        return shown <= 1
            ? Mark($"and {lines.Count} more", bullet)
            : string.Join('\n', lines.Take(shown - 1).Select(line => Mark(line, bullet))) + "\n" + tail;
    }

    private static string Mark(string line, bool bullet) => bullet ? $"• {line}" : line;
}
