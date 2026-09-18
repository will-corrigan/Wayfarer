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

    /// <summary>Where a row's words sit inside the row. The job rows put theirs 11 down.</summary>
    public const float RowTextTop = 11f;

    /// <summary>The icon column's left edge; it runs to where the words start. The compass is
    /// centred in it.</summary>
    public const float IconColumnLeft = 44f;

    /// <inheritdoc cref="IconColumnLeft"/>
    public const float IconColumnWidth = WordsLeft - IconColumnLeft;

    /// <summary>Where a row's words start.</summary>
    public const float WordsLeft = 72f;

    /// <summary>Room left at the right edge so the words never touch the plate's border.</summary>
    public const float RightInset = 12f;

    /// <summary>The words' width.</summary>
    public const float WordsWidth = RootWidth - WordsLeft - RightInset;

    /// <summary>The entry's words: the quest tracker's own objective-line face, Axis 14 with a
    /// leading of 16 (ToDoList components 1005 and 1007).</summary>
    public const uint WordsFontSize = 14;

    /// <inheritdoc cref="WordsFontSize"/>
    public const float WordsLeading = 16f;

    /// <summary>The block the tracker gives an objective line: its leading plus room for the
    /// descenders before whatever hangs under it (ToDoList component 1007, h=26).</summary>
    public const float WordsBlock = 26f;

    /// <summary>The route line: the tracker's count-line face under an objective, Axis 12 with a
    /// leading of 14 (ToDoList component 1008).</summary>
    public const uint RouteFontSize = 12;

    /// <inheritdoc cref="RouteFontSize"/>
    public const float RouteLeading = 14f;

    /// <summary>How many lines the entry's words may wrap to before they are cut.</summary>
    public const int MaxEntryLines = 2;

    /// <summary>The distance under the compass, smaller than the words.</summary>
    public const uint DistanceFontSize = 10;

    /// <summary>The distance's box, centred on the compass and wider than the icon column so four
    /// digits and the unit fit without being cut.</summary>
    public const float DistanceWidth = 44f;

    /// <summary>The compass ring's box, a little smaller than the icon column it sits in.</summary>
    public const float CompassSize = 26f;

    /// <summary>The job-quest row nodes, by id, whose visibility says how many rows are showing.</summary>
    public static readonly uint[] JobRowNodeIds = [7, 8];
}
