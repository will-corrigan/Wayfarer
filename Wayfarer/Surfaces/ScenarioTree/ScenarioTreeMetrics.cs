namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The game's Main Scenario Guide, as its layout file lays it out, and where Wayfarer's
/// block goes inside it. Read from <c>ui/uld/ScenarioTree.uld</c>: root 340x86; the plate's icon
/// at x=13; job-quest rows from y=54 at a pitch of 26. Our block sits directly under whichever
/// job rows are showing and spans the content width, from the plate's icon edge to the right
/// inset. What the player can change about it lives in <see cref="ScenarioTreeStyle"/>.</summary>
internal static class ScenarioTreeMetrics
{
    /// <summary>The window's own name. Written out because the game's mapping has no struct for
    /// this one, so there is nothing to take it from.</summary>
    public const string AddonName = "ScenarioTree";

    /// <summary>The root node's width and height. The game hit-tests clicks against the root, so the
    /// root is grown to cover our block while it shows and restored when it hides.</summary>
    public const float RootWidth = 340f;

    /// <inheritdoc cref="RootWidth"/>
    public const float RootHeight = 86f;

    /// <summary>Where the first job-quest row starts, in root coordinates.</summary>
    public const float JobRowsTop = 54f;

    /// <summary>The distance from one job-quest row to the next.</summary>
    public const float JobRowPitch = 26f;

    /// <summary>Where a row's words sit inside the row. The job rows put theirs 11 down.</summary>
    public const float RowTextTop = 11f;

    /// <summary>The content's left edge: where the plate draws its own icon.</summary>
    public const float ContentLeft = 13f;

    /// <summary>Room left at the right edge so the words never touch the plate's border.</summary>
    public const float RightInset = 12f;

    /// <summary>Air between the compass column and the words.</summary>
    public const float CompassColumnGap = 6f;

    /// <summary>How many lines the entry's words may wrap to before they are cut.</summary>
    public const int MaxEntryLines = 2;

    /// <summary>The distance under the compass, smaller than the words.</summary>
    public const uint DistanceFontSize = 10;

    /// <inheritdoc cref="DistanceFontSize"/>
    public const float DistanceLeading = 14f;

    /// <summary>The distance's box, centred on the compass and wide enough that four digits and
    /// the unit fit without being cut.</summary>
    public const float DistanceWidth = 44f;

    /// <summary>The plate's title, text node 6 inside component node 13, and the "Current Main
    /// Scenario Quest" header above it, text node 11 of the addon.</summary>
    public const uint PlateTitleTextNodeId = 6;

    /// <inheritdoc cref="PlateTitleTextNodeId"/>
    public const uint HeaderTextNodeId = 11;

    /// <summary>The controller hint strip ("Back / Confirm Destination") the game draws in the
    /// job-row band while the guide has focus: node 9, 28 tall.</summary>
    public const uint HintBarNodeId = 9;

    /// <inheritdoc cref="HintBarNodeId"/>
    public const float HintBarHeight = 28f;

    /// <summary>The component inside the hint strip, node 10, and its text node, id 2 within it.</summary>
    public const uint HintBarComponentNodeId = 10;

    /// <inheritdoc cref="HintBarComponentNodeId"/>
    public const uint HintBarTextNodeId = 2;

    /// <summary>The headline plate, component node 13: the stop the controller cursor rests on when
    /// HUD Select reaches the guide, and the one our lines hang below in its navigation.</summary>
    public const uint PlateNodeId = 13;

    /// <summary>The text node inside a job-quest row component, id 3 within it.</summary>
    public const uint JobRowTextNodeId = 3;

    /// <summary>The job-quest row nodes, by id. A row is showing while it is visible and has words.</summary>
    public static readonly uint[] JobRowNodeIds = [7, 8];
}
