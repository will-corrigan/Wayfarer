using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>Never white on parchment.
///
/// <para>The journal page shipped wearing the readout's colours, which are light-on-transparent
/// because everything else Wayfarer draws sits over the 3D world. On a sheet of cream paper that made
/// the giver line at the foot near-invisible, and the player photographed it. A comment claiming a
/// contrast ratio is not a check; this is.</para></summary>
public class JournalPaletteTests
{
    [Fact]
    public void Every_role_clears_the_minimum_contrast_against_the_parchment()
    {
        foreach (var (role, colour) in JournalPalette.Roles)
        {
            var ratio = JournalPalette.Contrast(colour, JournalPalette.Parchment);
            Assert.True(
                ratio >= JournalPalette.MinimumContrast,
                $"the {role} colour is {ratio:0.00}:1 on the parchment, under {JournalPalette.MinimumContrast}:1");
        }
    }

    /// <summary>The direction matters as much as the ratio. White on cream also fails, but a
    /// near-white that happened to clear the ratio would still be wrong on paper — this page is dark
    /// text on a light ground, and every role has to be on the dark side of it.</summary>
    [Fact]
    public void Every_role_is_darker_than_the_parchment()
    {
        var paper = JournalPalette.Luminance(JournalPalette.Parchment);

        foreach (var (role, colour) in JournalPalette.Roles)
        {
            Assert.True(
                JournalPalette.Luminance(colour) < paper,
                $"the {role} colour is lighter than the paper it is drawn on");
        }
    }

    /// <summary>The specific value the player photographed: the readout's near-white body colour, on
    /// this page's parchment. Kept as the fixture the assertion above is about, so the rule cannot
    /// quietly stop meaning anything.</summary>
    [Fact]
    public void The_colour_that_was_shipped_would_fail_this()
    {
        var nearWhite = new System.Numerics.Vector4(1f, 1f, 1f, 1f);
        var ratio = JournalPalette.Contrast(nearWhite, JournalPalette.Parchment);

        Assert.True(ratio < JournalPalette.MinimumContrast, $"white on cream measured {ratio:0.00}:1");
    }

    /// <summary>The page reads as a hierarchy: the name is the strongest thing on it, the prose next,
    /// the section headings quieter than the prose they introduce, and the lines that are only
    /// <i>about</i> the entry quietest of all. That order is the game's, and it is a property of the
    /// values rather than of how they happen to be listed.</summary>
    [Fact]
    public void The_roles_read_as_a_hierarchy()
    {
        var title = JournalPalette.Luminance(JournalPalette.Title);
        var body = JournalPalette.Luminance(JournalPalette.Body);
        var heading = JournalPalette.Luminance(JournalPalette.Heading);
        var meta = JournalPalette.Luminance(JournalPalette.Meta);

        Assert.True(title < body, "the title is not the strongest thing on the page");
        Assert.True(body < heading, "the prose is not stronger than its own heading");
        Assert.True(heading < meta, "a section heading is not stronger than the footnote");
    }

    /// <summary>A sanity check on the arithmetic itself, against two ratios everybody knows: black on
    /// white is 21:1 and a colour on itself is 1:1. A contrast function that is wrong would make every
    /// assertion above meaningless.</summary>
    [Fact]
    public void The_contrast_arithmetic_is_the_standard_one()
    {
        var black = new System.Numerics.Vector4(0f, 0f, 0f, 1f);
        var white = new System.Numerics.Vector4(1f, 1f, 1f, 1f);

        Assert.Equal(21f, JournalPalette.Contrast(black, white), 2);
        Assert.Equal(1f, JournalPalette.Contrast(white, white), 5);
    }
}
