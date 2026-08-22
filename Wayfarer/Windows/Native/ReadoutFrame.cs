using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>One frame's worth of readout, gathered off the guidance snapshot before it reaches the
/// overlay node. Everything that needs a Dalamud service is resolved here so the node's per-frame
/// path stays arithmetic and string assignment.</summary>
/// <param name="Content">The lines to draw, already ordered and weighted.</param>
/// <param name="ArrowRadians">Screen-space rotation for the direction chevron, or null when there
/// is nothing to point at. Exactly one direction indicator ever exists.</param>
/// <param name="Scale">The player's own text-size setting. Multiplied by the game's interface
/// scale inside the node — see <see cref="GuidanceOverlayNode"/> for why that is not automatic.</param>
/// <param name="Position">Which anchor the readout uses.</param>
internal readonly record struct ReadoutFrame(
    ReadoutContent Content,
    float? ArrowRadians,
    float Scale,
    ReadoutPosition Position);
