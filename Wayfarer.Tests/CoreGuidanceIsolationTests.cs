namespace Wayfarer.Tests;

/// <summary>Mechanical proof that guidance arbitration is substitutable across sources: no concrete
/// source, feature service or feature-shaped payload type may be named anywhere under
/// <c>Wayfarer.Core/Guidance/</c> or in any side-effect coordinator.
///
/// This is the guard that keeps the arbiter payload-blind. The defect it exists to prevent is not
/// hypothetical: the previous navigator special-cased quest-shaped payloads, which is exactly why a
/// hunting target was not substitutable and vanished a tick after being selected. A grep-shaped
/// test is the only way to pin "nobody added a <c>if (source is HuntingSource)</c> back in".</summary>
public class CoreGuidanceIsolationTests
{
    /// <summary>Concrete source types, feature services and feature payload records. If guidance
    /// ever needs to know one of these names, the design has regressed.</summary>
    private static readonly string[] ForbiddenNames =
    [
        "QuestObjectiveSource",
        "UnlockRouteSource",
        "HuntingSource",
        "HuntingLogService",
        "UnlockService",
        "QuestNavigator",
        "PickupTarget",
        "HuntingTargetView",
        "ResolvedUnlock",
        "QuestManager",
        "MonsterNoteManager",
    ];

    /// <summary>Source ids, as string literals. A coordinator that branches on <c>"hunting"</c> is
    /// special-casing a feature just as surely as one that names its type.</summary>
    private static readonly string[] ForbiddenSourceIdLiterals =
    [
        "\"hunting\"",
        "\"unlocks\"",
        "\"quest\"",
    ];

    public static TheoryData<string> GuardedDirectories =>
    [
        Path.Combine("Wayfarer.Core", "Guidance"),
        Path.Combine("Wayfarer", "Guidance", "Coordinators"),
    ];

    [Theory]
    [MemberData(nameof(GuardedDirectories))]
    public void NoConcreteSourceTypeIsNamed(string relativeDirectory)
    {
        foreach (var (file, text) in SourceFilesIn(relativeDirectory))
        {
            foreach (var forbidden in ForbiddenNames)
            {
                Assert.False(
                    text.Contains(forbidden, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} names the concrete type '{forbidden}'. Guidance must stay payload-blind.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(GuardedDirectories))]
    public void NoSourceIdLiteralIsBranchedOn(string relativeDirectory)
    {
        foreach (var (file, text) in SourceFilesIn(relativeDirectory))
        {
            foreach (var forbidden in ForbiddenSourceIdLiterals)
            {
                var message = $"{Path.GetFileName(file)} contains the source id literal {forbidden}. "
                    + "Coordinators and the arbiter must never read SourceId.";
                Assert.False(text.Contains(forbidden, StringComparison.Ordinal), message);
            }
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

    private static IEnumerable<(string File, string Text)> SourceFilesIn(string relativeDirectory)
    {
        var directory = Path.Combine(RepositoryRoot(), relativeDirectory);
        if (!Directory.Exists(directory))
        {
            yield break; // the coordinator folder does not exist until coordinators do
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            // Comments are scanned out: this guard is about what the CODE knows. Doc comments must
            // stay free to name the very things they forbid ("never branch on \"hunting\"") —
            // otherwise the rule could not be explained where it is enforced.
            var code = File.ReadAllLines(file)
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
            yield return (file, string.Join('\n', code));
        }
    }
}
