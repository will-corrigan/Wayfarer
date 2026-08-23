namespace Wayfarer.Core.Ui;

/// <summary>Where the guidance readout sits.
///
/// A plugin cannot register with the game's HUD Layout editor — <c>AddonHudLayoutScreen</c>'s tables
/// are fixed-size with no registration API, and KamiToolKit explicitly opts its own addons out — so
/// this enum plus a free X/Y position is the substitute for "drag it where you want in HUD Layout".
///
/// <b>The presets are seeds, not the answer.</b> Picking one puts the readout somewhere sensible;
/// nudging it (with the sliders on a controller, or by dragging it with a mouse) switches to
/// <see cref="Custom"/> and keeps whatever the player chose. <see cref="Custom"/> is stored as a
/// fraction of the usable screen rather than as pixels, so it survives a resolution change instead
/// of stranding the readout off the edge of a smaller screen.
///
/// <b>Values are append-only.</b> They are persisted in the player's config as integers, so
/// inserting one in the middle would silently move everybody's readout.</summary>
public enum ReadoutPosition
{
    /// <summary>Follows the game's own quest tracker, mirroring the way it flips sides when the
    /// player moves it across the screen. No longer the default: on a 16:9 television layout it
    /// landed the readout underneath the minimap, which clipped the second line clean off.</summary>
    FollowQuestTracker,

    TopLeft,

    TopRight,

    BottomLeft,

    BottomRight,

    /// <summary>Top centre — clear of the minimap and the quest tracker on a default HUD, and the
    /// placement the readout now defaults to.</summary>
    TopCentre,

    BottomCentre,

    /// <summary>Wherever the player put it, stored as a fraction of the usable screen.</summary>
    Custom,
}
