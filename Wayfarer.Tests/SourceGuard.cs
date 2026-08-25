namespace Wayfarer.Tests;

/// <summary>Reads the repository's own source so a test can assert something about its shape.
///
/// <para><b>When this is the right tool.</b> The plugin assembly is not referenced by this test
/// project — it cannot be, because it links against the game — so an invariant that lives in a
/// window, a node or the vendored toolkit has no runnable test. Some of those invariants fail
/// silently: no throw, no log, and every existing test still green. A structural guard is worth
/// having for exactly those, and worth nothing for anything else. It proves that the code still
/// <i>says</i> what it said, never that the code works.</para></summary>
internal static class SourceGuard
{
    /// <summary>The named member's own body, brace-matched from its declaration. Scoped rather than
    /// "everything after the declaration" so an assertion cannot be satisfied by an unrelated method
    /// further down a long file.
    ///
    /// <para>An initialiser counts as a body, which is what makes this work on the expression-bodied
    /// factories: the braces matched are the object initialiser's.</para></summary>
    public static string Body(string code, string declaration)
    {
        var at = Declaration(code, declaration);

        var open = code.IndexOf('{', at);
        Assert.True(open >= 0, $"'{declaration}' has no body.");

        var depth = 0;
        for (var i = open; i < code.Length; i++)
        {
            depth += code[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return code[open..(i + 1)];
            }
        }

        Assert.Fail($"'{declaration}' has an unterminated body.");
        return string.Empty;
    }

    /// <summary>The single expression a member is written as, from its declaration to the statement
    /// terminator. For the expression-bodied properties, which have no braces to match.</summary>
    public static string Expression(string code, string declaration)
    {
        var at = Declaration(code, declaration);

        var arrow = code.IndexOf("=>", at, StringComparison.Ordinal);
        var end = code.IndexOf(';', at);
        Assert.True(arrow >= 0 && end > arrow, $"'{declaration}' is not written as a single expression.");

        return code[arrow..end];
    }

    /// <summary>How many times <paramref name="needle"/> appears. For the guards whose rule is "as
    /// many of these as of those" rather than "this one is present".</summary>
    public static int Occurrences(string code, string needle)
    {
        var count = 0;
        var at = code.IndexOf(needle, StringComparison.Ordinal);
        while (at >= 0)
        {
            count++;
            at = code.IndexOf(needle, at + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>The file's code with its comment lines taken out. The rule is about what the code
    /// does; the prose that explains the rule must stay free to name what it forbids.</summary>
    public static string SourceOf(string relativePath)
    {
        var file = Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(file), $"{relativePath} does not exist.");

        var code = File.ReadAllLines(file)
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));

        return string.Join('\n', code);
    }

    /// <summary>Where the named member is declared, or a loud failure. A guard that quietly stops
    /// finding what it guards is a guard that has stopped guarding.</summary>
    private static int Declaration(string code, string declaration)
    {
        var at = code.IndexOf(declaration, StringComparison.Ordinal);
        Assert.True(at >= 0, $"'{declaration}' is no longer in the source this test reads.");
        return at;
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
