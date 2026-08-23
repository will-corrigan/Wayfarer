using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>One frame's worth of readout, gathered off the guidance snapshot before it reaches the
/// overlay node. Everything that needs a Dalamud service is resolved here so the node's per-frame
/// path stays arithmetic and string assignment.</summary>
/// <param name="Content">The lines to draw, already ordered and weighted.</param>
/// <param name="ArrowRadians">Screen-space rotation for the direction chevron, or null when there
/// is nothing to point at. Exactly one direction indicator ever exists.</param>
/// <param name="ArrowHidden">Why <paramref name="ArrowRadians"/> is null, for the log. See
/// <see cref="ArrowHiddenReason"/>.</param>
/// <param name="ArrowIcon">Which of the minimap's chevrons to cut the arrow from. Read every frame
/// so the setting applies without a reload.</param>
/// <param name="ArrowScale">The player's own arrow-size setting, on top of the interface scale.</param>
/// <param name="Scale">The player's own text-size setting. Multiplied by the game's interface
/// scale inside the node — see <see cref="GuidanceOverlayNode"/> for why that is not automatic.</param>
/// <param name="Position">Which anchor the readout uses.</param>
/// <param name="ClickableTeleport">Whether this frame's teleport line is actually offered as a
/// click. A line is marked as a teleport whether or not the surface can act on it — the mark says
/// what the line means — so the host has to be told separately, or a player who has turned
/// click-to-teleport off still gets a hand cursor over a line that will refuse them.</param>
internal readonly record struct ReadoutFrame(
    ReadoutContent Content,
    float? ArrowRadians,
    ArrowHiddenReason ArrowHidden,
    ArrowIconVariant ArrowIcon,
    float ArrowScale,
    float Scale,
    ReadoutPosition Position,
    bool ClickableTeleport);
