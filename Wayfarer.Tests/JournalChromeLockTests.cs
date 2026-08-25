namespace Wayfarer.Tests;

/// <summary>Mechanical proof that the journal page's invisible window chrome can never answer a
/// click.
///
/// <para><b>Why this is a test and not a code review note.</b> Nothing about a game node's parent
/// being invisible reaches into that child's own <c>NodeFlags</c> — <c>Plugin.BuildJournal</c> hands
/// this addon a <c>WindowNode</c> constructed with <c>IsVisible = false</c> so the page can be
/// chromeless, but the header's collision node keeps its own <c>Visible</c>, <c>HasCollision</c>,
/// <c>RespondToMouse</c> and <c>EmitsEvents</c> regardless. That is a full-width, invisible drag
/// handle sitting across this window's own top edge, at the exact row of pixels
/// <c>JournalPlacementTests</c> proves this window gets pinned to whenever the hub is docked at the
/// top of the screen — which is where the hub's own draggable title bar lives too. No test that
/// needs a running game can see an invisible node accept a click; what can be caught, and is caught
/// here, is that <see cref="Wayfarer.Windows.JournalWindow"/> disarms it before anything else is
/// built.</para></summary>
public class JournalChromeLockTests
{
    [Fact]
    public void The_page_disarms_the_window_nodes_header_collision_before_building_anything_else()
    {
        var code = SourceFile(Path.Combine("Wayfarer", "Windows", "JournalWindow.cs"));
        var build = MethodBody(code, "private void Build()");

        var lockCall = build.IndexOf("LockChrome();", StringComparison.Ordinal);
        var firstNode = build.IndexOf("frame = new JournalFrameNode", StringComparison.Ordinal);

        var message = "LockChrome() must be the first statement in Build() — a header collision "
            + "disarmed after the page's nodes are attached is one that was live for at least one frame.";

        Assert.True(lockCall >= 0, "Build() no longer calls LockChrome().");
        Assert.True(firstNode >= 0 && lockCall < firstNode, message);
    }

    [Fact]
    public void LockChrome_strips_collision_and_mouse_response_from_the_header()
    {
        var code = SourceFile(Path.Combine("Wayfarer", "Windows", "JournalWindow.cs"));
        var method = MethodBody(code, "private void LockChrome()");

        Assert.Contains("HeaderCollisionNode.RemoveNodeFlags(", method, StringComparison.Ordinal);
        Assert.Contains("NodeFlags.HasCollision", method, StringComparison.Ordinal);
        Assert.Contains("NodeFlags.RespondToMouse", method, StringComparison.Ordinal);
        Assert.Contains("NodeFlags.EmitsEvents", method, StringComparison.Ordinal);
    }

    [Fact]
    public void LockChrome_disables_the_headers_buttons_too()
    {
        var code = SourceFile(Path.Combine("Wayfarer", "Windows", "JournalWindow.cs"));
        var method = MethodBody(code, "private void LockChrome()");

        Assert.Contains("CloseButtonNode.IsEnabled = false", method, StringComparison.Ordinal);
        Assert.Contains("ConfigurationButtonNode.IsEnabled = false", method, StringComparison.Ordinal);
        Assert.Contains("InformationButtonNode.IsEnabled = false", method, StringComparison.Ordinal);
    }

    /// <summary>The body of the named method, up to (but not including) the next method at the same
    /// indent — crude brace counting, which is all a source-shape guard needs.</summary>
    private static string MethodBody(string code, string signature)
    {
        var start = code.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{signature}' was not found.");

        var openBrace = code.IndexOf('{', start);
        Assert.True(openBrace >= 0);

        var depth = 0;
        for (var i = openBrace; i < code.Length; i++)
        {
            if (code[i] == '{')
            {
                depth++;
            }
            else if (code[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return code[openBrace..(i + 1)];
                }
            }
        }

        return code[openBrace..];
    }

    /// <summary>Walks up from the test binary to the directory holding <c>Wayfarer.slnx</c>, then
    /// reads <paramref name="relativePath"/> with comment lines removed — the rule is about what the
    /// code does, not the prose explaining it.</summary>
    private static string SourceFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Wayfarer.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var file = Path.Combine(dir.FullName, relativePath);
        Assert.True(File.Exists(file), $"{relativePath} does not exist.");

        var lines = File.ReadAllLines(file)
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Where(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal));

        return string.Join('\n', lines);
    }
}
