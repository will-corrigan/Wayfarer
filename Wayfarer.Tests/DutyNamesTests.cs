using Wayfarer.Surfaces.DutyFinder;

namespace Wayfarer.Tests;

public class DutyNamesTests
{
    private const uint Sastasha = 1u;
    private const uint Halatali = 2u;

    [Fact]
    public void A_name_answers_with_its_duty()
    {
        var names = new DutyNames();
        names.Add("Sastasha", Sastasha);

        Assert.Equal(Sastasha, names.DutyNamed("Sastasha"));
    }

    [Fact]
    public void A_name_no_duty_was_filed_under_answers_nothing()
    {
        var names = new DutyNames();
        names.Add("Sastasha", Sastasha);

        Assert.Null(names.DutyNamed("Halatali"));
    }

    [Fact]
    public void A_name_two_duties_share_answers_nothing()
    {
        var names = new DutyNames();
        names.Add("The Cloud Deck", Sastasha);
        names.Add("The Cloud Deck", Halatali);

        Assert.Null(names.DutyNamed("The Cloud Deck"));
    }

    [Fact]
    public void One_duty_listed_twice_under_its_own_name_still_answers()
    {
        var names = new DutyNames();
        names.Add("Sastasha", Sastasha);
        names.Add("Sastasha", Sastasha);

        Assert.Equal(Sastasha, names.DutyNamed("Sastasha"));
    }

    [Fact]
    public void A_shared_name_stays_unanswerable_however_it_is_filed_afterwards()
    {
        var names = new DutyNames();
        names.Add("The Cloud Deck", Sastasha);
        names.Add("The Cloud Deck", Halatali);
        names.Add("The Cloud Deck", Sastasha);

        Assert.Null(names.DutyNamed("The Cloud Deck"));
    }

    [Fact]
    public void Nothing_without_a_name_or_a_duty_is_filed()
    {
        var names = new DutyNames();
        names.Add(string.Empty, Sastasha);
        names.Add("Nowhere", 0);

        Assert.Null(names.DutyNamed(string.Empty));
        Assert.Null(names.DutyNamed("Nowhere"));
    }

    [Fact]
    public void Clearing_forgets_both_the_names_and_what_was_shared()
    {
        var names = new DutyNames();
        names.Add("The Cloud Deck", Sastasha);
        names.Add("The Cloud Deck", Halatali);

        names.Clear();
        names.Add("The Cloud Deck", Halatali);

        Assert.Equal(Halatali, names.DutyNamed("The Cloud Deck"));
    }

    [Fact]
    public void Names_are_matched_exactly()
    {
        var names = new DutyNames();
        names.Add("Sastasha", Sastasha);

        Assert.Null(names.DutyNamed("sastasha"));
        Assert.Null(names.DutyNamed("Sastasha "));
    }
}
