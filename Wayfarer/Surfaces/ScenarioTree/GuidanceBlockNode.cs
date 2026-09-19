using System.Globalization;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using Wayfarer.Core.Ui;
using Wayfarer.Ui;
using static Wayfarer.Surfaces.ScenarioTree.ScenarioTreeMetrics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The block under the game's Main Scenario Guide: the entry being guided to, the route
/// line under it, and the compass with its distance in a column at either edge. Either line can be
/// a control. Words are set when the guidance changes, the needle and distance every frame, and
/// the whole block re-lays itself when the style changes.</summary>
internal sealed class GuidanceBlockNode : ResNode
{
    private const TextFlags WrappingFlags = TextFlags.Edge | TextFlags.WordWrap | TextFlags.MultiLine;
    private const TextFlags SingleLineFlags = TextFlags.Edge | TextFlags.Ellipsis;
    private const TextFlags DistanceFlags = TextFlags.Edge;
    private const string YalmsSuffix = "y";

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

        entry = Attach(new PressableLine(WrappingFlags, GameColors.Body, MaxEntryLines, onEntryPressed));
        route = Attach(new PressableLine(SingleLineFlags, GameColors.ListText, 1, onRoutePressed));
        compass = Attach(new CompassNode(textures, log) { IsVisible = false });
        distance = Attach(new TextNode
        {
            FontType = FontType.Axis,
            AlignmentType = AlignmentType.Top,
            TextFlags = DistanceFlags,
            TextColor = GameColors.ListText,
            TextOutlineColor = GameColors.ListTextEdge,
        });

        Restyle(style);
    }

    /// <summary>Whether any line can be pressed. The addon's collision list is rebuilt by the
    /// surface when this changes.</summary>
    public bool AnyPressable => entry.Pressable || route.Pressable;

    /// <summary>The first of our lines the cursor moves down to, or null when neither can be pressed.</summary>
    public int? FirstStop => entry.Pressable ? GuideStops.Entry : route.Pressable ? GuideStops.Route : null;

    /// <summary>The last of our lines, or null when neither can be pressed.</summary>
    public int? LastStop => route.Pressable ? GuideStops.Route : entry.Pressable ? GuideStops.Entry : null;

    /// <summary>Applies a style: type sizes, the columns, and the compass, then re-lays the words.</summary>
    public void Restyle(ScenarioTreeStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        this.style = style;

        ApplyColumns();
        PlaceCompass();
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

    /// <summary>The words' left edge and width: the whole content width, less the compass column
    /// only while there is a compass in it. A route that needs no compass gets the whole span
    /// rather than keeping a gap for one that may never come.</summary>
    private (float Left, float Width) WordsColumn()
    {
        var column = compassShown ? style.CompassSize + CompassColumnGap : 0f;
        var left = style.Compass == CompassPlacement.Left ? style.ContentLeft + column : style.ContentLeft;
        return (left, RootWidth - RightInset - style.ContentLeft - column);
    }

    /// <summary>Puts the lines in the column the compass has left them.</summary>
    private void ApplyColumns()
    {
        var (left, width) = WordsColumn();
        entry.Restyle(style.EntryFontSize, GameText.LeadingFor(style.EntryFontSize), left, width);
        route.Restyle(style.RouteFontSize, GameText.LeadingFor(style.RouteFontSize), left, width);
    }

    /// <summary>Draws the heading last given, at whatever size and place the style puts it. Called
    /// again after a restyle, or the compass would move without resizing.</summary>
    private void DrawHeading()
    {
        var (needle, yalms, rise) = heading;
        var drawn = style.Compass != CompassPlacement.Hidden && needle is { } && yalms is { };
        if (drawn != compassShown)
        {
            compassShown = drawn;
            ApplyColumns();
            Relayout();
        }

        distance.IsVisible = drawn;
        if (!drawn || needle is not { } radians || yalms is not { } distanceYalms)
        {
            compass.IsVisible = false;
            elevation = ElevationHint.Level;
            return;
        }

        elevation = Elevation.Classify(rise, elevation);
        compass.Show(style.CompassSize, radians, elevation);
        SetDistance(MathF.Round(distanceYalms).ToString(CultureInfo.InvariantCulture) + YalmsSuffix);
    }

    private T Attach<T>(T node)
        where T : KamiToolKit.BaseTypes.NodeBase
    {
        node.AttachNode(this);
        return node;
    }

    /// <summary>The compass sits in its column level with the entry's first line, its distance
    /// right under it.</summary>
    private void PlaceCompass()
    {
        var left = style.Compass == CompassPlacement.Left ? style.ContentLeft : RootWidth - RightInset - style.CompassSize;
        var top = RowTextTop + ((GameText.LeadingFor(style.EntryFontSize) - style.CompassSize) / 2f);
        compass.Position = new Vector2(left, MathF.Max(0f, top));

        // The distance reads at the route's size, in a box wide enough for four digits and the
        // unit, which at a small compass is wider than the compass itself.
        distanceLeading = GameText.LeadingFor(style.RouteFontSize);
        var width = MathF.Max(style.CompassSize, style.RouteFontSize * DistanceWidthInCharacters);
        distance.FontSize = style.RouteFontSize;
        distance.LineSpacing = (uint)distanceLeading;
        distance.Size = new Vector2(width, distanceLeading);
        distance.Position = new Vector2(left + ((style.CompassSize - width) / 2f), compass.Position.Y + style.CompassSize);
    }

    /// <summary>Stacks the route under the entry with the style's gap, and sizes the block to
    /// whichever is taller: the words or the compass column.</summary>
    private void Relayout()
    {
        entry.Position = new Vector2(entry.Position.X, RowTextTop);
        var entryBottom = entry.IsVisible ? RowTextTop + entry.Height : RowTextTop;
        route.Position = new Vector2(route.Position.X, entryBottom + style.LineGap);
        var wordsBottom = route.IsVisible ? route.Position.Y + route.Height : entryBottom;
        var compassBottom = compassShown ? distance.Position.Y + distanceLeading : 0f;
        Height = MathF.Max(wordsBottom, compassBottom);
    }

    private void SetDistance(string words)
    {
        if (string.Equals(words, lastDistance, StringComparison.Ordinal))
        {
            return;
        }

        lastDistance = words;
        distance.String = words;
    }
}
