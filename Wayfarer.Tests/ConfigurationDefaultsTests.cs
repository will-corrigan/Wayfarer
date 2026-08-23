using System.Text.RegularExpressions;

namespace Wayfarer.Tests;

/// <summary>Pins what a FRESH INSTALL switches on for the player without being asked. The rule the
/// player set is that the essentials are on — the arrow, the objective, the distance, the routing
/// advice — and everything that adds a mark to the screen or a line to the readout waits to be
/// asked for.
///
/// <para>Read out of the source file rather than out of the object, for the same reason
/// <see cref="SettingsCopyTests"/> is: <c>Configuration</c> lives in the plugin assembly, which
/// references Dalamud and cannot be loaded in a plain test host. A regex over the declarations is
/// cruder than reflection, and it catches exactly the drift this exists for — somebody adding
/// <c>= true</c> back onto one of these four while adding something else nearby.</para>
///
/// <para>What this deliberately does NOT try to prove is the other half of the promise, that an
/// existing player keeps what they chose. That half is a property of how the configuration is
/// loaded — Dalamud writes every public property on every save, so deserialisation has already put
/// the player's own value back before the declared default matters, and all four of these
/// properties are older than the Version field itself. It cannot be reached from here; see
/// <c>Configuration.Migrate</c>, where the reasoning is written down beside the code it is
/// about.</para></summary>
public partial class ConfigurationDefaultsTests
{
    /// <summary>Everything a first run must have switched ON. The user asked for these explicitly:
    /// "I want them all on by default."
    ///
    /// <list type="bullet">
    /// <item><description><c>MarkObjectiveWithMapFlag</c> moves the player's own map flag.</description></item>
    /// <item><description><c>MarkTargetsOnNameplates</c> puts markers over characters in the world.</description></item>
    /// <item><description><c>ShowOnWidget</c> — both of them, the nearby unlocks and the hunting
    /// summary — adds extra lines to a readout whose appeal is that it is short.</description></item>
    /// </list></summary>
    public static TheoryData<string> OnByDefault =>
        ["MarkObjectiveWithMapFlag", "MarkTargetsOnNameplates", "ShowOnWidget"];

    [Theory]
    [MemberData(nameof(OnByDefault))]
    public void Every_surface_is_switched_on_for_a_new_install(string property)
    {
        var source = ConfigurationSource();

        // The property still has to be there — an assertion that a deleted setting is not switched
        // on would pass for the wrong reason forever.
        Assert.Contains(property, source, StringComparison.Ordinal);

        // A bool with no initialiser is false, which would leave the surface off on a first run.
        Assert.Contains($"bool {property} {{ get; set; }} = true", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_quest_guidance_loop_is_still_on_for_a_new_install()
    {
        // The other half of "fewer options on by default": leaner must not mean emptier. These are
        // what the plugin IS, and a first run that has to switch the arrow on has missed the point.
        var source = ConfigurationSource();

        Assert.Contains("UseNativeReadout { get; set; } = true", source, StringComparison.Ordinal);
        Assert.Contains("ClickTeleportEnabled { get; set; } = true", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaning_the_defaults_bumped_the_configuration_version()
    {
        // Without the bump an existing configuration is never rewritten, and the version stamp is
        // the only record that the shipped defaults are no longer what an old file was written
        // against.
        var version = VersionPattern().Match(ConfigurationSource());

        Assert.True(version.Success, "Configuration.CurrentVersion was not found.");
        Assert.True(
            int.Parse(version.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture) >= 4,
            "Configuration.CurrentVersion must be at least 4 — the version that leaned the first-run defaults.");
    }

    [GeneratedRegex(
        @"CurrentVersion = (?<value>\d+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex VersionPattern();

    private static string ConfigurationSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Wayfarer", "Configuration.cs");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Wayfarer/Configuration.cs was not found above the test output directory.");
    }
}
