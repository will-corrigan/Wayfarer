using System.Globalization;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using Wayfarer.Guidance;
using static Wayfarer.Surfaces.ScenarioTree.ScenarioTreeMetrics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The block under the game's Main Scenario Guide: the entry being guided to, the route
/// line under it, and the compass with its distance in a column at either edge.
///
/// <para>The laying out is the game's own list nodes rather than arithmetic here. The block is one
/// row of two columns: the words, which are a list of the two lines, and the compass, which is a
/// list of the needle and its distance. Stacking, spacing and how tall the whole thing comes out
/// are the lists' answers, so a line that wraps pushes what is under it down by itself. Which side
/// the compass sits on is the order of the row, and hiding it is that column going invisible, which
/// the lists already skip.</para></summary>
internal sealed class GuidanceBlockNode : ResNode
{
    private const TextFlags DistanceFlags = TextFlags.Edge;
    private const string YalmsSuffix = "y";

    /// <summary>What the distance says once there is none left. A step that sends the player to an
    /// area is measured to the edge of it, so nought is reached the moment they step inside and
    /// stays there while they cross it. "0y" reads as a measurement that has stopped working;
    /// a word reads as an answer.</summary>
    private const string Arrived = "Here";

    private readonly HorizontalListNode columns;
    private readonly VerticalListNode words;
    private readonly VerticalListNode compassColumn;
    private readonly ResNode compassBox;
    private readonly PressableLine entry;
    private readonly PressableLine route;
    private readonly TextNode distance;
    private readonly CompassNode compass;
    private ScenarioTreeStyle style = new();
    private float distanceLeading = GameText.LeadingFor(ScenarioTreeStyle.SmallestFont);
    private (float? Needle, float? Yalms, float? Rise) heading;
    private string lastDistance = string.Empty;
    private ElevationHint elevation = ElevationHint.Level;
    private bool compassShown;

    public GuidanceBlockNode(ITextureProvider textures, IPluginLog log, Action onEntryPressed, Action onRoutePressed)
    {
        Width = RootWidth;

        entry = new PressableLine(GameColors.Body, MaxEntryLines, onEntryPressed);
        route = new PressableLine(GameColors.ListText, RouteLines, onRoutePressed);
        words = new VerticalListNode { FitContents = true };
        words.AddNode(entry);
        words.AddNode(route);

        // The needle is narrower than its own distance, so it hangs in a node the width of the
        // column and centres itself there rather than the column bending around it.
        compassBox = new ResNode();
        compass = new CompassNode(textures, log) { IsVisible = false }.AttachedTo(compassBox);
        distance = new TextNode
        {
            FontType = FontType.Axis,
            AlignmentType = AlignmentType.Top,
            TextFlags = DistanceFlags,
            TextColor = GameColors.ListText,
            TextOutlineColor = GameColors.ListTextEdge,
        };
        compassColumn = new VerticalListNode { FitContents = true, IsVisible = false };
        compassColumn.AddNode(compassBox);
        compassColumn.AddNode(distance);

        columns = new HorizontalListNode { ItemSpacing = CompassColumnGap, FitToContentHeight = true }.AttachedTo(this);
        columns.AddNode(words);
        columns.AddNode(compassColumn);

        Restyle(style);
    }

    /// <summary>Whether any line can be pressed. The addon's collision list is rebuilt by the
    /// surface when this changes.</summary>
    public bool AnyPressable => entry.Pressable || route.Pressable;

    /// <summary>The first of our lines the cursor moves down to, or null when neither can be pressed.</summary>
    public int? FirstStop => entry.Pressable ? GuideStops.Entry : route.Pressable ? GuideStops.Route : null;

    /// <summary>The last of our lines, or null when neither can be pressed.</summary>
    public int? LastStop => route.Pressable ? GuideStops.Route : entry.Pressable ? GuideStops.Entry : null;

    /// <summary>Applies a style: the type sizes, the columns' widths, the space between the lines,
    /// and which side the compass is on. Every one of the player's settings lands here.</summary>
    public void Restyle(ScenarioTreeStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        this.style = style;

        columns.Position = new Vector2(style.ContentLeft, RowTextTop);
        columns.Width = RootWidth - RightInset - style.ContentLeft;
        words.ItemSpacing = style.LineGap;

        SizeCompassColumn();
        SizeWordsColumn();
        OrderColumns();
        DrawHeading();
        Relayout();
    }

    /// <summary>Shows a guidance: the step on the first line, the way there on the second, and
    /// neither when there is no step. Called only when the guidance changed.</summary>
    public void SetWords(LineContent? entryContent, LineContent? routeContent)
    {
        entry.Set(entryContent);
        route.Set(entryContent is null ? null : routeContent);
        Relayout();
    }

    /// <summary>Tells both lines which addon they live in and where its focus falls back to when
    /// they go: the plate, the addon's own control.</summary>
    public unsafe void GuestOf(AtkUnitBase* addon, AtkResNode* plateFocus)
    {
        entry.GuestOf(addon, plateFocus);
        route.GuestOf(addon, plateFocus);
    }

    /// <summary>Writes our lines' own cursor records: up from the first pressable line goes to the
    /// stop above us, down from the last goes to the stop below, and the lines chain to each other.</summary>
    public void LinkNav(int aboveUs, int belowUs)
    {
        entry.SetNav(GuideStops.Entry, aboveUs, route.Pressable ? GuideStops.Route : belowUs);
        route.SetNav(GuideStops.Route, entry.Pressable ? GuideStops.Entry : aboveUs, belowUs);
    }

    /// <summary>Points the needle and writes the distance, every frame the player moves or turns.
    /// Whether the target counts as above or below the player is settled by
    /// <see cref="Elevation.Classify"/>, which holds its last answer through small changes so the
    /// mark does not flicker on a slope.</summary>
    public void SetHeading(float? needle, float? yalms, float? rise)
    {
        heading = (needle, yalms, rise);
        DrawHeading();
    }

    /// <summary>The compass column is as wide as the wider of the needle and the four characters
    /// its distance needs, so neither overhangs the other.</summary>
    private void SizeCompassColumn()
    {
        distanceLeading = GameText.LeadingFor(style.RouteFontSize);
        var width = MathF.Max(style.CompassSize, style.RouteFontSize * DistanceWidthInCharacters);

        compassColumn.Width = width;
        compassBox.Size = new Vector2(width, style.CompassSize);
        compass.Position = new Vector2((width - style.CompassSize) / 2f, 0f);
        distance.FontSize = style.RouteFontSize;
        distance.LineSpacing = (uint)distanceLeading;
        distance.Size = new Vector2(width, distanceLeading);
    }

    /// <summary>The words take the row, less the compass column only while there is one showing. A
    /// route that needs no compass gets the whole span rather than keeping a gap for one that may
    /// never come.</summary>
    private void SizeWordsColumn()
    {
        words.Width = columns.Width - (compassShown ? compassColumn.Width + CompassColumnGap : 0f);
        entry.Restyle(style.EntryFontSize, GameText.LeadingFor(style.EntryFontSize), words.Width);
        route.Restyle(style.RouteFontSize, GameText.LeadingFor(style.RouteFontSize), words.Width);
    }

    /// <summary>Which side the compass sits on, as the order of the row.</summary>
    private void OrderColumns() =>
        columns.ReorderNodes((first, second) => Rank(first).CompareTo(Rank(second)));

    private int Rank(NodeBase node) =>
        ReferenceEquals(node, compassColumn) == (style.Compass == CompassPlacement.Left) ? 0 : 1;

    /// <summary>Draws the heading last given, at whatever size and place the style puts it. Called
    /// again after a restyle, or the compass would move without resizing.</summary>
    private void DrawHeading()
    {
        var (needle, yalms, rise) = heading;
        var drawn = style.Compass != CompassPlacement.Hidden && needle is { } && yalms is { };
        if (drawn != compassShown)
        {
            compassShown = drawn;
            compassColumn.IsVisible = drawn;
            SizeWordsColumn();
            Relayout();
        }

        if (!drawn || needle is not { } radians || yalms is not { } distanceYalms)
        {
            compass.IsVisible = false;
            elevation = ElevationHint.Level;
            return;
        }

        elevation = Elevation.Classify(rise, elevation);
        compass.Show(style.CompassSize, radians, elevation);
        var remaining = MathF.Round(distanceYalms);
        SetDistance(remaining <= 0f ? Arrived : remaining.ToString(CultureInfo.InvariantCulture) + YalmsSuffix);
    }

    /// <summary>Lets the lists place everything, and takes the block's height from what they made.</summary>
    private void Relayout()
    {
        words.RecalculateLayout();
        compassColumn.RecalculateLayout();
        columns.RecalculateLayout();
        Height = columns.Position.Y + columns.Height;
    }

    private void SetDistance(string text)
    {
        if (string.Equals(text, lastDistance, StringComparison.Ordinal))
        {
            return;
        }

        lastDistance = text;
        distance.String = text;
    }
}
