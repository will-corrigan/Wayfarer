namespace Wayfarer.Tests;

/// <summary>Mechanical proof of the hub window's per-tick safety net around a native defect no test
/// that needs a running game can trigger on demand.
///
/// <para><b>Why this is a test and not a code review note.</b> <c>NativeAddon</c>'s own <c>Hide()</c>
/// hook forces a <c>Close()</c> on any native call to the addon's Hide vtable slot, and that
/// <c>Close()</c> only starts the closing animation — the deallocation that unsubscribes
/// <c>OnFrameworkUpdate</c> (<c>OnFinalize</c>) runs several frames later. So there is a real window
/// in which the hub has gone not-visible but its own per-tick handler keeps running. What is caught
/// here is the shape of the guard against it: the diagnostic write happens unconditionally, and
/// every other per-tick write — including the one that repositions a live journal page — stops the
/// moment <c>IsOpen</c> is seen to be false.</para></summary>
public class HubDragSafetyTests
{
    private const string File = "NativeHubWindow.cs";

    [Fact]
    public void OnFrameworkUpdate_logs_diagnostics_before_checking_whether_the_window_is_still_open()
    {
        var body = MethodBody(SourceFile(), "private void OnFrameworkUpdate(IFramework fw)");

        var diagnosticsCall = body.IndexOf("LogDragDiagnostics();", StringComparison.Ordinal);
        var openCheck = body.IndexOf("if (!IsOpen)", StringComparison.Ordinal);

        var message = "Diagnostics must be written before the !IsOpen guard returns — the one tick "
            + "that matters most is the one where the window has just gone not-visible.";

        Assert.True(diagnosticsCall >= 0, "OnFrameworkUpdate no longer calls LogDragDiagnostics().");
        Assert.True(openCheck >= 0, "OnFrameworkUpdate no longer guards on !IsOpen.");
        Assert.True(diagnosticsCall < openCheck, message);
    }

    [Fact]
    public void OnFrameworkUpdate_dismisses_an_orphaned_journal_page_and_stops_when_not_open()
    {
        var body = MethodBody(SourceFile(), "private void OnFrameworkUpdate(IFramework fw)");
        var guard = body[body.IndexOf("if (!IsOpen)", StringComparison.Ordinal)..];
        var guardBody = MethodBody(guard, "if (!IsOpen)");

        Assert.Contains("DismissJournalPage();", guardBody, StringComparison.Ordinal);
        Assert.Contains("return;", guardBody, StringComparison.Ordinal);
    }

    [Fact]
    public void LogDragDiagnostics_only_writes_when_position_size_or_visibility_actually_changed()
    {
        var method = MethodBody(SourceFile(), "private unsafe void LogDragDiagnostics()");

        Assert.Contains("lastDiagnosticPosition", method, StringComparison.Ordinal);
        Assert.Contains("lastDiagnosticSize", method, StringComparison.Ordinal);
        Assert.Contains("lastDiagnosticOpen", method, StringComparison.Ordinal);

        var earlyReturn = method.IndexOf("return;", StringComparison.Ordinal);
        var logCall = method.IndexOf("log.Information(", StringComparison.Ordinal);
        var message = "the unchanged-signature guard must run before the log write it exists to skip.";
        Assert.True(earlyReturn >= 0 && logCall >= 0 && earlyReturn < logCall, message);
    }

    /// <summary>The body of the named method or statement, up to (but not including) the point its
    /// own opening brace's depth returns to zero — crude brace counting, which is all a
    /// source-shape guard needs.</summary>
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
    /// reads the hub window's source with comment lines removed — the rule is about what the code
    /// does, not the prose explaining it.</summary>
    private static string SourceFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(Path.Combine(dir.FullName, "Wayfarer.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var file = Path.Combine(dir.FullName, "Wayfarer", "Windows", File);
        Assert.True(System.IO.File.Exists(file), $"{file} does not exist.");

        var lines = System.IO.File.ReadAllLines(file)
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Where(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal));

        return string.Join('\n', lines);
    }
}
