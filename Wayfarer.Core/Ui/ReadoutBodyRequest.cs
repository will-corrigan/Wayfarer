namespace Wayfarer.Core.Ui;

/// <summary>Everything <see cref="ReadoutBodyLayout.Compose"/> needs to know about a frame: the scale
/// it is drawn at, the lines it has to hold, and which of the readout's optional parts are actually on
/// screen.
///
/// <para>The flags are all "is this being drawn", never "should this be drawn" — whether the arrow can
/// be generated, whether the banner's art could be read and whether this host takes a mouse at all are
/// decided long before layout. By the time they arrive here they are facts about the frame.</para>
/// </summary>
public sealed record ReadoutBodyRequest
{
    /// <summary>The scale every ULD unit is multiplied by: the player's interface size times their own
    /// text-size preference. 1 is the game's authored size.</summary>
    public float Factor { get; init; } = 1f;

    /// <summary>The subordinate lines, in reading order — everything beneath the plate. The name on the
    /// plate is not one of these: it does not wrap, so it cannot change the banner's height.</summary>
    public IReadOnlyList<ReadoutBlock> Lines { get; init; } = [];

    /// <summary>Whether the bearing arrow is on screen. It takes no vertical space of its own — it sits
    /// in the gutter beside the first subordinate line — so this changes where the arrow goes and
    /// which line keeps its medallion, and nothing else.</summary>
    public bool Arrow { get; init; }

    /// <summary>The player's arrow-size preference, clamped to half..double by the layout.</summary>
    public float ArrowScale { get; init; } = 1f;

    /// <summary>Whether the readout is saying the direction in words instead, which is what it falls
    /// back to when the arrow could not be generated at all. This is the only optional part that has a
    /// height, and it is a section of its own precisely so that its absence moves nothing.</summary>
    public bool DirectionInWords { get; init; }

    /// <summary>Whether the banner's art could be read. With no plate there is no chevron to click and
    /// no gutter art, so the medallions and the switcher go with it; every word the readout was going
    /// to say is still said, in the plain heads-up colours.</summary>
    public bool Banner { get; init; } = true;

    /// <summary>Whether this host draws the settings cog. False on the click-through overlay, where an
    /// affordance that takes no input would be a lie.</summary>
    public bool Cog { get; init; }

    /// <inheritdoc cref="Cog"/>
    public bool Switcher { get; init; }
}
