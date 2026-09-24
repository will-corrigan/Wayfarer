using Wayfarer.Guidance;

namespace Wayfarer.Tests;

/// <summary>Who holds guidance, and who has it back when they let go. A hunt takes over from the
/// quest being followed, and the quest has to come back when the hunt ends without anyone having
/// to remember to ask for it.</summary>
public class FocusTests
{
    private readonly FakeSource quests = new("Quests");
    private readonly FakeSource hunting = new("Hunting");
    private readonly FakeSource unlocks = new("Unlocks");

    [Fact]
    public void Claiming_takes_over_and_says_who_was_pushed_aside()
    {
        var focus = new Focus();
        focus.Offer(quests);

        var displaced = focus.Claim(hunting);

        Assert.Same(hunting, focus.Holder);
        Assert.Same(quests, displaced);
    }

    [Fact]
    public void Letting_go_hands_back_to_whoever_was_pushed_aside()
    {
        var focus = new Focus();
        focus.Offer(quests);
        focus.Claim(hunting);

        focus.Yield(hunting);

        Assert.Same(quests, focus.Holder);
    }

    [Fact]
    public void The_one_pushed_aside_most_recently_has_it_back_first()
    {
        var focus = new Focus();
        focus.Offer(quests);
        focus.Claim(unlocks);
        focus.Claim(hunting);

        focus.Yield(hunting);
        Assert.Same(unlocks, focus.Holder);

        focus.Yield(unlocks);
        Assert.Same(quests, focus.Holder);
    }

    [Fact]
    public void Offering_holds_when_nobody_does()
    {
        var focus = new Focus();

        focus.Offer(quests);

        Assert.Same(quests, focus.Holder);
    }

    [Fact]
    public void Offering_while_someone_holds_waits_behind_them_without_pushing_them_aside()
    {
        // The quests module offers itself every time any setting is applied. That must never take
        // guidance away from a hunt the player chose.
        var focus = new Focus();
        focus.Claim(hunting);

        focus.Offer(quests);

        Assert.Same(hunting, focus.Holder);
        focus.Yield(hunting);
        Assert.Same(quests, focus.Holder);
    }

    [Fact]
    public void Offering_again_while_waiting_keeps_its_place()
    {
        var focus = new Focus();
        focus.Offer(quests);
        focus.Claim(hunting);

        focus.Offer(quests);

        Assert.Same(hunting, focus.Holder);
    }

    [Fact]
    public void Letting_go_while_waiting_means_never_being_handed_it_back()
    {
        // The quests module switched off while a hunt was being guided.
        var focus = new Focus();
        focus.Offer(quests);
        focus.Claim(hunting);

        focus.Yield(quests);
        focus.Yield(hunting);

        Assert.Null(focus.Holder);
    }

    [Fact]
    public void Claiming_while_waiting_comes_to_the_front()
    {
        var focus = new Focus();
        focus.Offer(quests);
        focus.Claim(hunting);

        var displaced = focus.Claim(quests);

        Assert.Same(quests, focus.Holder);
        Assert.Same(hunting, displaced);
        focus.Yield(quests);
        Assert.Same(hunting, focus.Holder);
    }

    [Fact]
    public void Claiming_what_is_already_held_pushes_nobody_aside()
    {
        var focus = new Focus();
        focus.Claim(hunting);

        Assert.Null(focus.Claim(hunting));
        Assert.Same(hunting, focus.Holder);
    }
}
