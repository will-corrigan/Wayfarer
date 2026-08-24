using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>A job gate is said by the game's own name for the job set, never by enumerating it.
///
/// <para>The field report is the fixture: <i>"some of the requirements are SO long when it lists
/// every class and then needs to be level x"</i>. The thirty-job sentence below is what the plugin
/// used to print for a category the game itself calls "Disciple of War or Magic", and it is kept
/// here as a fixture precisely so the short form can be asserted against it.</para></summary>
public class JobGateTextTests
{
    /// <summary>How long the phrase is allowed to get, in characters.
    ///
    /// <para>A character count and not a pixel width, deliberately: the only thing that can measure
    /// Axis 14 is the running client, so a test that claimed pixels here would be claiming something
    /// it cannot know. What it can assert is the bound the class actually promises — the shape of the
    /// phrase, never the mask. Sixty is set against the journal page's 376-wide column, where an
    /// Axis-14 line holds a little over fifty characters, so a phrase inside this bound is one or at
    /// worst two lines and the old one was five.</para></summary>
    private const int PhraseBudget = 60;

    /// <summary>The members the plugin used to print. The point of every assertion below is that
    /// this list never reaches a player's screen.</summary>
    private static string[] EveryCombatJob => HostileContent.EveryCombatJob;

    [Fact]
    public void A_thirty_job_category_is_said_by_its_own_name_and_a_level()
    {
        var said = JobGateText.Describe("Disciple of War or Magic", EveryCombatJob, 70);

        Assert.Equal("Disciple of War or Magic Lv. 70", said);
    }

    [Fact]
    public void The_short_form_is_a_phrase_and_the_enumeration_was_a_paragraph()
    {
        var said = JobGateText.Describe("Disciple of War or Magic", EveryCombatJob, 70);
        var enumerated = HostileContent.ThirtyJobRequirement;

        Assert.True(said.Length <= PhraseBudget, $"'{said}' is {said.Length} characters");
        Assert.True(
            enumerated.Length > PhraseBudget * 4,
            "the fixture no longer reproduces the defect");
    }

    [Fact]
    public void No_member_of_the_category_is_ever_named_when_the_category_has_a_name()
    {
        var said = JobGateText.Describe("Disciple of War or Magic", EveryCombatJob, 70);

        foreach (var job in EveryCombatJob)
        {
            Assert.DoesNotContain(job, said, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void A_single_job_gate_is_said_as_that_job()
    {
        // The ordinary job quest: the category IS one job, and its name is the job's name.
        Assert.Equal("Weaver Lv. 50", JobGateText.Describe("Weaver", ["Weaver"], 50));
    }

    [Fact]
    public void A_nameless_category_falls_back_to_the_job_it_flags()
    {
        Assert.Equal("Botanist Lv. 50", JobGateText.Describe(null, ["Botanist"], 50));
        Assert.Equal("Botanist Lv. 50", JobGateText.Describe("   ", ["Botanist"], 50));
    }

    [Fact]
    public void A_nameless_category_of_many_jobs_is_capped_rather_than_enumerated()
    {
        var said = JobGateText.Describe(null, EveryCombatJob, 70);

        Assert.Equal("gladiator or pugilist or marauder or 27 more Lv. 70", said);
        Assert.True(said.Length <= PhraseBudget);
    }

    [Fact]
    public void A_nameless_category_of_a_few_jobs_names_them_all()
    {
        Assert.Equal(
            "Weaver or Culinarian Lv. 50", JobGateText.Describe(null, ["Weaver", "Culinarian"], 50));
    }

    [Fact]
    public void A_gate_that_names_nobody_is_a_level_and_nothing_else()
    {
        Assert.Equal("Lv. 50", JobGateText.Describe(null, [], 50));
        Assert.Equal(string.Empty, JobGateText.Describe(null, [], 0));
    }

    [Fact]
    public void A_category_with_no_level_is_said_without_an_invented_one()
    {
        Assert.Equal("Disciple of the Land", JobGateText.Describe("Disciple of the Land", [], 0));
        Assert.Equal(string.Empty, JobGateText.Level(0));
    }

    /// <summary>The cap is a promise about length, so it is asserted as one: whatever the mask
    /// holds, the phrase is bounded by three names and a count.</summary>
    [Fact]
    public void The_fallback_never_names_more_than_the_cap()
    {
        var many = Enumerable.Range(0, 200).Select(i => $"job{i}").ToArray();
        var said = JobGateText.Who(null, many);

        Assert.Equal(JobGateText.MaxNamedJobs, said.Split(" or ").Length - 1);
        Assert.EndsWith("or 197 more", said, StringComparison.Ordinal);
    }
}
