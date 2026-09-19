namespace Wayfarer.Presentation;

/// <summary>The mark drawn in front of a route line, naming the kind of thing a press does. The
/// surface maps each to one of the game's own font icons.</summary>
public enum RouteGlyph
{
    /// <summary>No mark: the line is words only.</summary>
    None,

    /// <summary>The aetheryte crystal: the press teleports.</summary>
    Aetheryte,

    /// <summary>The Duty Finder mark: the press opens the Duty Finder.</summary>
    Duty,
}
