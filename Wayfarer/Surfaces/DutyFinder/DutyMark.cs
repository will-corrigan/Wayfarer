namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>A mark a module wants on a duty's row: what to draw, and what to say about it when
/// the player rests on it. Where it goes is the window's to settle, because other modules mark the
/// same rows and they share the room.</summary>
/// <param name="IconId">The icon to draw.</param>
/// <param name="Tooltip">What the mark means, in the player's own words. Empty for a mark that
/// explains itself.</param>
internal readonly record struct DutyMark(uint IconId, string Tooltip);
