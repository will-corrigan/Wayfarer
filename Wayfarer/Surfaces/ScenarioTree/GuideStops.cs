namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Where Wayfarer's own stops sit in the guide's cursor index space. The guide's own
/// records use two, three and four, so ours start at a hundred and cannot be mistaken for them.
/// The order here is the order the cursor moves through them.</summary>
internal static class GuideStops
{
    /// <summary>The step being guided to.</summary>
    public const int Entry = 100;

    /// <summary>The way there.</summary>
    public const int Route = 101;

    /// <summary>The settings cog beside the guide's own heading.</summary>
    public const int Settings = 102;
}
