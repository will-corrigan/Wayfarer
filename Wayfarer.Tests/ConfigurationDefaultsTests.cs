using System.Text.RegularExpressions;

namespace Wayfarer.Tests;

/// <summary>Pins what a FRESH INSTALL switches on. The rule is that the plugin arrives working:
/// everything a player installed it for — the arrow, the objective, the distance, the routing advice,
/// the map flag, the nameplate markers, the nearby unlocks, the hunting summary — is on, and the
/// settings are where you go to have less rather than to have anything at all.
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
    /// <summary>The four surfaces a first run must have switched on. Each of them is the only way a
    /// player who has not opened the window learns that part of the plugin exists, which is why none
    /// of them waits to be asked for.
    ///
    /// <list type="bullet">
    /// <item><description><c>MarkObjectiveWithMapFlag</c> — the guidance the game itself
    /// draws.</description></item>
    /// <item><description><c>MarkTargetsOnNameplates</c> — the marker over a giver's head, read
    /// without opening anything.</description></item>
    /// <item><description><c>ShowOnWidget</c> — both of them, the nearby unlocks and the hunting
    /// summary, which is how a player finds either feature at all.</description></item>
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
        // The core of it, held separately from the four above so that a change of mind about the
        // surfaces can never reach the arrow. A first run that has to switch the arrow on has missed
        // the point of the plugin.
        var source = ConfigurationSource();

        Assert.Contains("UseNativeReadout { get; set; } = true", source, StringComparison.Ordinal);
        Assert.Contains("ClickTeleportEnabled { get; set; } = true", source, StringComparison.Ordinal);
    }

    /// <summary>The version only ever goes forwards. Nothing here claims a reason for the number it
    /// is at — version 4 was bumped for a default change that was reverted before it shipped, and
    /// walking a version stamp backwards would make an already-migrated config file look old. What is
    /// worth pinning is the direction: a config written by this build must never claim to be older
    /// than one written by a previous build, because Migrate short-circuits on
    /// <c>Version >= CurrentVersion</c> and would then silently stop running.</summary>
    [Fact]
    public void The_configuration_version_never_goes_backwards()
    {
        var version = VersionPattern().Match(ConfigurationSource());

        Assert.True(version.Success, "Configuration.CurrentVersion was not found.");

        var message = "Configuration.CurrentVersion must not go below 4: version 4 has shipped in a build, and a "
            + "stamp that moves backwards makes an already-migrated file look older than it is.";

        Assert.True(
            int.Parse(version.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture) >= 4,
            message);
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
