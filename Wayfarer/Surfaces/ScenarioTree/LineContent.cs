using Dalamud.Game.Text.SeStringHandling;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>What one line of the block shows: the whole sentence, the words inside it that name
/// what a press does, the game's own mark in front of the line, and whether a press does
/// anything.</summary>
/// <param name="Words">The whole sentence, as the game writes it.</param>
/// <param name="Keyword">The words inside the sentence that name the press, or null when the
/// sentence does not name it and the whole line is the control.</param>
/// <param name="Glyph">The game's own mark in front of the line, or null.</param>
/// <param name="Pressable">Whether the line is a control.</param>
internal sealed record LineContent(
    string Words,
    string? Keyword = null,
    BitmapFontIcon? Glyph = null,
    bool Pressable = false);
