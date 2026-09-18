namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The game's Main Scenario Guide, as its layout file lays it out, and where Wayfarer's
/// block goes inside it. Read from <c>ui/uld/ScenarioTree.uld</c>: root 340x86; job-quest rows
/// from y=54 at a pitch of 26, each with a 32-wide icon at x=44 and words at x=72. Our block
/// takes the pitch directly under whichever job rows are showing, with the compass in the icon
/// column and the words in the words column.</summary>
internal static class ScenarioTreeMetrics
{
    /// <summary>The addon's internal name.</summary>
    public const string AddonName = "ScenarioTree";

    /// <summary>The root node's width.</summary>
    public const float RootWidth = 340f;

    /// <summary>Where the first job-quest row starts, in root coordinates.</summary>
    public const float JobRowsTop = 54f;

    /// <summary>The distance from one job-quest row to the next.</summary>
    public const float JobRowPitch = 26f;

    /// <summary>The icon column's left edge and width. The compass is centred in it.</summary>
    public const float IconColumnLeft = 44f;

    /// <inheritdoc cref="IconColumnLeft"/>
    public const float IconColumnWidth = 32f;

    /// <summary>Where a row's words start.</summary>
    public const float WordsLeft = 72f;

    /// <summary>Room left at the right edge so the words never touch the plate's border.</summary>
    public const float RightInset = 12f;

    /// <summary>The words' width.</summary>
    public const float WordsWidth = RootWidth - WordsLeft - RightInset;

    /// <summary>The job rows' own face: Axis at this size.</summary>
    public const uint FontSize = 12;

    /// <summary>One line of words at that size, with its leading.</summary>
    public const float LinePitch = 18f;

    /// <summary>How many lines the entry's words may wrap to before they are cut.</summary>
    public const int MaxEntryLines = 2;

    /// <summary>The distance under the compass, smaller than the words.</summary>
    public const uint DistanceFontSize = 10;

    /// <summary>The compass ring's box, a little smaller than the icon column it sits in.</summary>
    public const float CompassSize = 30f;

    /// <summary>The job-quest row nodes, by id, whose visibility says how many rows are showing.</summary>
    public static readonly uint[] JobRowNodeIds = [7, 8];
}
