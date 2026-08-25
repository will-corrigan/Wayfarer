namespace Wayfarer.Tests;

/// <summary>Mechanical proof for the defect that took the journal page down after the double-attach
/// hang was fixed: <c>JournalSectionNode</c> attaches its body row to itself before the caller has
/// filled it, and <c>HorizontalListNode.OnRecalculateLayout</c> takes a <c>Max()</c> over its
/// children's heights when <c>FitToContentHeight</c> is set — "tallest of none" throws rather than
/// returning zero. The crash needs the running game's own node tree to reproduce at runtime (see
/// <see cref="JournalAttachDisciplineTests"/> for why this test project cannot construct one), so what
/// is pinned here is the source shape of the fix rather than the throw itself: that
/// <c>JournalNodes.AddOnce</c> gives an empty, about-to-be-cascaded-into row something safe to measure
/// before it is ever handed to a container that will recalculate it.</summary>
public class JournalEmptyContainerTests
{
    [Fact]
    public void AddOnce_gives_an_empty_FitToContentHeight_row_something_to_measure_before_it_is_attached()
    {
        var body = MethodBody(
            Path.Combine("Wayfarer", "Windows", "Native", "JournalNodes.cs"),
            "public static void AddOnce",
            "private static bool Croppable");

        Assert.Contains("FitToContentHeight", body, StringComparison.Ordinal);
        Assert.Contains("Nodes.Count", body, StringComparison.Ordinal);

        var guardAt = body.IndexOf("FitToContentHeight", StringComparison.Ordinal);
        var attachAt = body.LastIndexOf("list.AddNode(node)", StringComparison.Ordinal);

        var message = "AddOnce must check an incoming row's own child count before handing it to "
            + "list.AddNode — that call is what cascades a recalculation into the row, and the "
            + "check has to happen first or it guards nothing.";
        Assert.True(guardAt >= 0 && attachAt >= 0 && guardAt < attachAt, message);
    }

    [Fact]
    public void A_locked_quest_icon_miss_falls_back_to_the_duty_locks_padlock_before_words()
    {
        var body = MethodBody(
            Path.Combine("Wayfarer", "Windows", "Native", "HubStatusIcons.cs"),
            "public uint For(",
            "private bool Probe(");

        Assert.Contains("LockedQuestIcon", body, StringComparison.Ordinal);
        Assert.Contains("LockedDutyIcon", body, StringComparison.Ordinal);
    }

    [Fact]
    public void The_reward_banner_is_not_read_off_a_gate_quest_with_no_journal_page()
    {
        var body = MethodBody(
            Path.Combine("Wayfarer", "Windows", "Native", "HubJournalFacts.cs"),
            "private uint Quest(",
            endMarker: null);

        Assert.Contains("JournalGenre", body, StringComparison.Ordinal);

        var genreCheckAt = body.IndexOf("JournalGenre", StringComparison.Ordinal);
        var iconReadAt = body.IndexOf(".Icon", StringComparison.Ordinal);

        var message = "Quest() must check the resolved row's JournalGenre before reading its Icon — a "
            + "quest with no journal entry has no page for that Icon to be the banner of.";
        Assert.True(genreCheckAt >= 0 && iconReadAt >= 0 && genreCheckAt < iconReadAt, message);
    }

    /// <summary>The text of one method, comments stripped, found by its own signature and the next
    /// member's. Crude on purpose, like every other structural guard in this file set: a signature
    /// that moves costs a clearer failure message, never a silently-skipped assertion.</summary>
    private static string MethodBody(string relativePath, string startMarker, string? endMarker)
    {
        var file = Path.Combine(RepositoryRoot(), relativePath);
        Assert.True(File.Exists(file), $"{relativePath} does not exist.");

        var code = string.Join(
            '\n',
            File.ReadAllLines(file)
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Where(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal)));

        var start = code.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{Path.GetFileName(file)} no longer contains '{startMarker}'.");

        if (endMarker is null)
        {
            return code[start..];
        }

        var end = code.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"{Path.GetFileName(file)} no longer contains '{endMarker}' after '{startMarker}'.");

        return code[start..end];
    }

    /// <summary>Walks up from the test binary to the directory holding <c>Wayfarer.slnx</c>. Fails
    /// loudly rather than skipping: a silently-skipped structural guard is worse than none.</summary>
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Wayfarer.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir.FullName;
    }
}
