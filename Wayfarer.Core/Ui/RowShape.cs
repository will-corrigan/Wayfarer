namespace Wayfarer.Core.Ui;

/// <summary>What a list row is made of. Three shapes, all built from the game's own row atoms.
/// </summary>
public enum RowShape
{
    /// <summary>A two-line entry: an icon, a name with a right-hand caption, and a dimmed line
    /// underneath. The game's Axis-14 row stacked on its Axis-12 row.</summary>
    Entry,

    /// <summary>A section header. One line, the game's own header row.</summary>
    Section,

    /// <summary>A note — prose that has to wrap rather than be cut, because the meaning is at the end
    /// of the sentence.</summary>
    Note,
}
