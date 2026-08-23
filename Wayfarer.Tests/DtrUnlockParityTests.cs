using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The info bar and the readout must never disagree about whether there is something to
/// pick up here.
///
/// <para>They cannot, structurally: <c>ReadoutFeed</c> builds both from one call to its own
/// <c>NearbyUnlocks()</c>, which returns nothing when the unlock module is disabled or its
/// "show on the readout" setting is off — so the bar cannot alert about pickups the readout has
/// been told to keep quiet about. These pin the other half of it: given the same list, both
/// surfaces say the same thing.</para></summary>
public class DtrUnlockParityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Both_surfaces_agree_about_nearby_unlocks_while_a_mode_is_engaged(int count)
    {
        var unlocks = Names(count);

        var readout = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = Engaged(),
            NearbyUnlocks = unlocks,
        });
        var bar = DtrComposer.Compose(new DtrInputs
        {
            Engaged = true,
            Step = DtrNextStep.Walk,
            DistanceYalms = 56f,
            NearbyUnlockCount = unlocks.Count,
        });

        Assert.Equal(bar.UnlocksNearby, MentionsUnlocks(readout));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Both_surfaces_agree_about_nearby_unlocks_while_idle(int count)
    {
        var unlocks = Names(count);

        var readout = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = new NavigationState { Mode = NavigationState.Modes.Idle, SourceLabel = "Wayfarer" },
            NearbyUnlocks = unlocks,
        });
        var bar = DtrComposer.Compose(new DtrInputs { NearbyUnlockCount = unlocks.Count });

        Assert.Equal(bar.UnlocksNearby, MentionsUnlocks(readout));
    }

    [Fact]
    public void An_engaged_readout_with_unlocks_nearby_really_does_compose_the_line()
    {
        // Asserted directly rather than only through the parity check above, because the player
        // reported not seeing this line while the bar was alerting.
        var content = ReadoutComposer.Compose(new ReadoutInputs
        {
            State = Engaged(),
            NearbyUnlocks = ["Chocobo Racing", "Triple Triad"],
        });

        Assert.Contains(
            content.Lines,
            line => string.Equals(line.Text, "2 unlocks nearby", StringComparison.Ordinal));
    }

    [Fact]
    public void An_empty_unlock_list_produces_no_line_and_no_alert()
    {
        var content = ReadoutComposer.Compose(new ReadoutInputs { State = Engaged(), NearbyUnlocks = [] });

        Assert.False(MentionsUnlocks(content));
        Assert.False(DtrComposer.Compose(new DtrInputs { Engaged = true }).UnlocksNearby);
    }

    private static List<string> Names(int count) =>
        [.. Enumerable.Range(1, count).Select(i => $"Unlock {i}")];

    private static NavigationState Engaged() => new()
    {
        Mode = NavigationState.Modes.SameZone,
        SourceId = "hunting",
        SourceLabel = "Hunting Log - Warrior",
        Engaged = true,
        QuestName = "Highland Goobbue",
        TargetX = 10f,
        TargetZ = 10f,
    };

    private static bool MentionsUnlocks(ReadoutContent content) =>
        content.Lines.Any(line => line.Text.Contains("unlock", StringComparison.OrdinalIgnoreCase));
}
