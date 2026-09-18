using Lumina.Text.ReadOnly;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>What one line of the block shows: its words, an icon in front of them, and whether
/// a press does something.</summary>
/// <param name="Words">The words, which may carry a font icon of their own.</param>
/// <param name="IconId">A game icon drawn in front of the words, or null.</param>
/// <param name="Pressable">Whether the line is a control.</param>
internal sealed record LineContent(ReadOnlySeString Words, uint? IconId, bool Pressable);
