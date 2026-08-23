using System.Text.RegularExpressions;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>Pins the copy in the settings catalogue to the game's own convention: <b>labels,
/// section headings and option names are Title Case; descriptions are sentences and stay in
/// sentence case.</b> The player asked for this directly — "logs and settings should be title case
/// I think too" — and it is what makes a plugin window sit beside the game's own without looking
/// like a plugin window.
///
/// <para>Read out of the source file rather than out of the catalogue object, because the
/// catalogue lives in the plugin assembly, which references Dalamud and cannot be loaded here. A
/// regex over the declarations is cruder than reflection but catches exactly the drift this is for:
/// somebody adding a setting called "Show the thing".</para></summary>
public partial class SettingsCopyTests
{
    [Fact]
    public void Every_setting_label_is_title_case()
    {
        var source = CatalogSource();
        var labels = LabelPattern().Matches(source).Select(m => m.Groups["text"].Value).ToList();

        Assert.NotEmpty(labels);
        foreach (var label in labels)
        {
            Assert.Equal(DisplayNames.TitleCase(label), label);
        }
    }

    [Fact]
    public void Every_section_heading_is_title_case()
    {
        var source = CatalogSource();
        var sections = SectionPattern().Matches(source).Select(m => m.Groups["text"].Value).ToList();

        Assert.NotEmpty(sections);
        foreach (var section in sections)
        {
            Assert.Equal(DisplayNames.TitleCase(section), section);
        }
    }

    [Fact]
    public void The_readout_position_choice_offers_top_centre()
    {
        // The placement the player asked for by name, and the one the readout now defaults to.
        Assert.Contains("\"Top Centre\"", CatalogSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_readout_position_can_be_nudged_without_a_cursor()
    {
        // The controller half of the free-positioning work: two sliders, which the game's own
        // slider component steps with the d-pad.
        var source = CatalogSource();

        Assert.Contains("readout.positionX", source, StringComparison.Ordinal);
        Assert.Contains("readout.positionY", source, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"Label = ""(?<text>[^""]+)""", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LabelPattern();

    [GeneratedRegex(@"new SettingSection\(""(?<text>[^""]+)""", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex SectionPattern();

    private static string CatalogSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Wayfarer", "Settings", "SettingsCatalog.cs");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Wayfarer/Settings/SettingsCatalog.cs was not found above the test output directory.");
    }
}
