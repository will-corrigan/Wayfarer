namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Where the compass sits in the block, or that it is not drawn.</summary>
internal enum CompassPlacement
{
    /// <summary>In a column at the right edge, the words to its left.</summary>
    Right,

    /// <summary>In a column at the left edge, the words to its right.</summary>
    Left,

    /// <summary>Not drawn; the words take the whole width.</summary>
    Hidden,
}
