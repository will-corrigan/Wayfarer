namespace Wayfarer.Tests;

/// <summary>Mechanical proof that the journal page never attaches the same node to the same
/// container twice.
///
/// <para><b>Why this is a test and not a code review note.</b> KamiToolKit's node linker appends an
/// incoming node to the end of its new parent's sibling chain by walking that chain to its end. A
/// node that is <i>already</i> in the chain therefore gets linked onto itself or onto the node in
/// front of it, and the chain becomes a ring. The next attach to the same container walks that ring
/// looking for an end there no longer is — on the game's own main thread, in a loop with no exit.
/// The game stops responding, and because nothing was thrown there is no exception in the log, no
/// stack trace and no crash dump: the log simply ends.</para>
///
/// <para>That is precisely how the journal page shipped, and it could not be caught by any test that
/// needs a running game. What can be caught, and is caught here, is the shape of the mistake: a node
/// built with a parent and then handed to that same parent's <c>AddNode</c>. So the journal's own
/// files are required to fill every container through
/// <c>JournalNodes.AddOnce</c>, which skips a node the container already holds, and are forbidden
/// from calling <c>AddNode</c> themselves.</para></summary>
public class JournalAttachDisciplineTests
{
    /// <summary>The files that build the journal's node tree. The rule is theirs alone: other
    /// surfaces attach to plain <c>ResNode</c> parents, which are not layout containers and do not
    /// attach their own children.</summary>
    public static TheoryData<string> JournalSources =>
    [
        Path.Combine("Wayfarer", "Windows", "JournalWindow.cs"),
        Path.Combine("Wayfarer", "Windows", "Native", "JournalSectionNode.cs"),
        Path.Combine("Wayfarer", "Windows", "Native", "JournalFrameNode.cs"),
    ];

    [Theory]
    [MemberData(nameof(JournalSources))]
    public void EveryContainerIsFilledThroughAddOnce(string relativePath)
    {
        var (file, code) = SourceFile(relativePath);

        var message = $"{Path.GetFileName(file)} calls AddNode directly. Fill layout containers with "
            + "JournalNodes.AddOnce instead: a node attached twice makes the game's sibling chain "
            + "circular and the next attach never returns.";

        Assert.False(code.Contains(".AddNode(", StringComparison.Ordinal), message);
    }

    /// <summary>The other half of the same rule: a node destined for a container is built detached.
    /// Every <c>JournalNodes</c> factory takes a parent, and for a container that parent must be
    /// <c>null</c> — the container attaches it.</summary>
    [Theory]
    [MemberData(nameof(JournalSources))]
    public void NoNodeIsBuiltIntoAContainerItIsAlsoAddedTo(string relativePath)
    {
        var (file, code) = SourceFile(relativePath);

        foreach (var container in ContainerNames(code))
        {
            foreach (var caller in CallersTakingFirstArgument(code, container))
            {
                if (!caller.StartsWith("JournalNodes.", StringComparison.Ordinal))
                {
                    continue;
                }

                var message =
                    $"{Path.GetFileName(file)} builds a node with the container '{container}' as its parent, by "
                    + $"calling '{caller}'. The only factory that may be handed a container is AddOnce: a node "
                    + "built into a container and then added to it is attached twice, and the second attach "
                    + "makes the sibling chain circular.";

                Assert.True(
                    string.Equals(caller, "JournalNodes.AddOnce", StringComparison.Ordinal), message);
            }
        }
    }

    /// <summary>The name of every method called with <paramref name="container"/> as its first
    /// argument, on one line. A call written across several lines is not reported, which is the
    /// conservative direction: this guard may miss a spelling, never invent one.</summary>
    private static IEnumerable<string> CallersTakingFirstArgument(string code, string container)
    {
        var needle = $"({container}";
        var at = code.IndexOf(needle, StringComparison.Ordinal);

        while (at >= 0)
        {
            var after = at + needle.Length;
            if (after < code.Length && (code[after] is ',' or ')'))
            {
                var start = at;
                while (start > 0 && (char.IsLetterOrDigit(code[start - 1]) || code[start - 1] == '_'))
                {
                    start--;
                }

                // Qualifier included: the rule is about the node factories, and a call that merely
                // reads a container (the navigation walker's) is not one of them.
                while (start > 0 && (char.IsLetterOrDigit(code[start - 1]) || code[start - 1] is '_' or '.'))
                {
                    start--;
                }

                if (start < at)
                {
                    yield return code[start..at];
                }
            }

            at = code.IndexOf(needle, at + 1, StringComparison.Ordinal);
        }
    }

    /// <summary>Every local or field a container is stored in, taken off its declaration. Crude on
    /// purpose: a name that is not a container costs a redundant assertion, never a missed one.
    /// </summary>
    private static IEnumerable<string> ContainerNames(string code)
    {
        foreach (var line in code.Split('\n'))
        {
            var trimmed = line.Trim();
            var marker = trimmed.IndexOf(" = new ", StringComparison.Ordinal);
            if (marker < 0)
            {
                continue;
            }

            var tail = trimmed[(marker + " = new ".Length)..];
            if (!tail.Contains("ListNode", StringComparison.Ordinal))
            {
                continue;
            }

            var name = trimmed[..marker];
            var space = name.LastIndexOf(' ');
            yield return space < 0 ? name : name[(space + 1)..];
        }
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

    /// <summary>The file's code with its comment lines taken out. The rule is about what the code
    /// does; the prose that explains the rule must stay free to name what it forbids.</summary>
    private static (string File, string Code) SourceFile(string relativePath)
    {
        var file = Path.Combine(RepositoryRoot(), relativePath);
        Assert.True(File.Exists(file), $"{relativePath} does not exist.");

        var code = File.ReadAllLines(file)
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Where(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal));

        return (file, string.Join('\n', code));
    }
}
