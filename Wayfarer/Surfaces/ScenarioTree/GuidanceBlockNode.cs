using System.Globalization;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;
using Wayfarer.Core.Presentation;
using Wayfarer.Ui;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The block Wayfarer draws under the game's Main Scenario Guide: the entry being
/// guided to, the route line beneath it, and the compass with its distance in the icon column
/// beside them. Laid out in the addon's own root coordinates.
///
/// <para>Two kinds of input at two rates. <see cref="SetWords"/> is called when the guidance
/// changes and re-lays the text; <see cref="SetHeading"/> is called every frame and only moves
/// the needle and rewrites the distance.</para>
///
/// <para>A route line with a press is a control on a heads-up surface, which is two nodes that
/// agree about one rectangle: an invisible box the pointer clicks, and a zero-sized component the
/// game's controller cursor comes to rest on. Both are shown only while the line is pressable,
/// and both run the one action the surface was given.</para></summary>
internal sealed class GuidanceBlockNode : ResNode
{
    private const TextFlags WordFlags = TextFlags.Edge | TextFlags.WordWrap | TextFlags.MultiLine;
    private const TextFlags SingleLineFlags = TextFlags.Edge | TextFlags.Ellipsis;
    private const TextFlags DistanceFlags = TextFlags.Edge;
    private const string YalmsSuffix = "y";

    /// <summary>What a pressable line's words sit at when nothing is on them. The lift to full is
    /// what says the line can be pressed.</summary>
    private const float PressableIdleAlpha = 0.8f;

    /// <summary>Where the controller anchor sits inside the line's rectangle: two units in from the
    /// left at half the height, the same as the toolkit's own navigable rows.</summary>
    private const float NavAnchorInset = 2f;

    private readonly TextNode entry;
    private readonly TextNode route;
    private readonly TextNode distance;
    private readonly CompassNode compass;
    private readonly CollisionNode hitBox;
    private readonly NavFocusNode navAnchor;
    private string lastDistance = string.Empty;

    public GuidanceBlockNode(ITextureProvider textures, IPluginLog log, Action onRoutePressed)
    {
        Width = ScenarioTreeMetrics.RootWidth;

        entry = Words(WordFlags, GameColors.Body, ScenarioTreeMetrics.WordsFontSize, ScenarioTreeMetrics.WordsLeading);
        entry.Position = new Vector2(ScenarioTreeMetrics.WordsLeft, ScenarioTreeMetrics.RowTextTop);
        entry.Width = ScenarioTreeMetrics.WordsWidth;
        entry.AttachNode(this);

        route = Words(SingleLineFlags, GameColors.ListText, ScenarioTreeMetrics.RouteFontSize, ScenarioTreeMetrics.RouteLeading);
        route.Position = new Vector2(ScenarioTreeMetrics.WordsLeft, ScenarioTreeMetrics.RowTextTop + ScenarioTreeMetrics.WordsBlock);
        route.Size = new Vector2(ScenarioTreeMetrics.WordsWidth, ScenarioTreeMetrics.RouteLeading);
        route.AttachNode(this);

        // The pointer's half of the press: an invisible collision rectangle over the route's words.
        // The hover lights the words, not the box.
        hitBox = new CollisionNode { IsVisible = false, ShowClickableCursor = true };
        hitBox.AddEvent(AtkEventType.MouseClick, onRoutePressed);
        hitBox.AddEvent(AtkEventType.MouseOver, () => route.Alpha = 1f);
        hitBox.AddEvent(AtkEventType.MouseOut, () => route.Alpha = PressableIdleAlpha);
        hitBox.AttachNode(this);

        // The controller's half: a component the game's cursor can rest on, sized to nothing so it
        // covers nothing, whose Confirm runs the same action.
        navAnchor = new NavFocusNode
        {
            OnSelected = onRoutePressed,
            OnHoverStart = () => route.Alpha = 1f,
            OnHoverEnd = () => route.Alpha = PressableIdleAlpha,
            Size = Vector2.Zero,
            IsVisible = false,
        };
        navAnchor.CollisionNode.RemoveNodeFlags(NodeFlags.Fill);
        navAnchor.AttachNode(this);

        // Centred in the icon column and on the first line of words.
        compass = new CompassNode(textures, log) { IsVisible = false };
        compass.Position = new Vector2(
            ScenarioTreeMetrics.IconColumnLeft + ((ScenarioTreeMetrics.IconColumnWidth - ScenarioTreeMetrics.CompassSize) / 2f),
            ScenarioTreeMetrics.RowTextTop + ((ScenarioTreeMetrics.WordsLeading - ScenarioTreeMetrics.CompassSize) / 2f));
        compass.AttachNode(this);

        distance = Words(DistanceFlags, GameColors.ListText, ScenarioTreeMetrics.DistanceFontSize, ScenarioTreeMetrics.RouteLeading);
        distance.TextOutlineColor = GameColors.ListTextEdge;
        distance.AlignmentType = AlignmentType.Top;
        distance.Position = new Vector2(
            ScenarioTreeMetrics.IconColumnLeft + ((ScenarioTreeMetrics.IconColumnWidth - ScenarioTreeMetrics.DistanceWidth) / 2f),
            compass.Position.Y + ScenarioTreeMetrics.CompassSize);
        distance.Size = new Vector2(ScenarioTreeMetrics.DistanceWidth, ScenarioTreeMetrics.RouteLeading);
        distance.AttachNode(this);
    }

    /// <summary>Whether the route line can be pressed right now. The surface refreshes the addon's
    /// collision list when this changes, because the game only dispatches clicks to nodes in it.</summary>
    public bool RoutePressable { get; private set; }

    /// <summary>Lays the words out. Null entry words hide the whole block.</summary>
    public void SetWords(string? entryWords, RouteLine? routeLine)
    {
        if (entryWords is null)
        {
            IsVisible = false;
            RoutePressable = false;
            return;
        }

        entry.String = entryWords;
        var lines = Math.Clamp(MathF.Ceiling(entry.GetTextDrawSize(considerScale: false).Y / ScenarioTreeMetrics.WordsLeading), 1, ScenarioTreeMetrics.MaxEntryLines);
        entry.Height = lines * ScenarioTreeMetrics.WordsLeading;

        // The last line gets the tracker's full block before anything hangs under it.
        var entryBlock = ((lines - 1) * ScenarioTreeMetrics.WordsLeading) + ScenarioTreeMetrics.WordsBlock;
        route.Y = ScenarioTreeMetrics.RowTextTop + entryBlock;
        LayoutRoute(routeLine);

        Height = ScenarioTreeMetrics.RowTextTop + entryBlock + (route.IsVisible ? ScenarioTreeMetrics.RouteLeading : 0f);
        IsVisible = true;
    }

    /// <summary>Turns the needle and rewrites the distance, or hides both when there is nothing to
    /// point at.</summary>
    public void SetHeading(float? needle, float? yalms)
    {
        if (needle is not { } radians || yalms is not { } distanceYalms)
        {
            compass.IsVisible = false;
            distance.IsVisible = false;
            return;
        }

        compass.Show(ScenarioTreeMetrics.CompassSize, radians);

        var words = MathF.Round(distanceYalms).ToString(CultureInfo.InvariantCulture) + YalmsSuffix;
        if (!string.Equals(words, lastDistance, StringComparison.Ordinal))
        {
            lastDistance = words;
            distance.String = words;
        }

        distance.IsVisible = true;
    }

    /// <summary>The game's own font icon for a route mark.</summary>
    private static BitmapFontIcon? Icon(RouteGlyph glyph) => glyph switch
    {
        RouteGlyph.Aetheryte => BitmapFontIcon.Aetheryte,
        RouteGlyph.Duty => BitmapFontIcon.WaitingForDutyFinder,
        _ => null,
    };

    private static ReadOnlySeString WithIcon(RouteLine line)
    {
        if (Icon(line.Glyph) is not { } icon)
        {
            return line.Text;
        }

        var builder = new SeStringBuilder();
        builder.AddIcon(icon).AddText(line.Text);
        return new ReadOnlySeString(builder.Build().Encode());
    }

    private static TextNode Words(TextFlags flags, Vector4 color, uint fontSize, float leading) => new()
    {
        FontType = FontType.Axis,
        FontSize = fontSize,
        LineSpacing = (uint)leading,
        AlignmentType = AlignmentType.TopLeft,
        TextFlags = flags,
        TextColor = color,
        TextOutlineColor = GameColors.BodyEdge,
    };

    private void LayoutRoute(RouteLine? routeLine)
    {
        if (routeLine is null)
        {
            route.IsVisible = false;
            hitBox.IsVisible = false;
            navAnchor.IsVisible = false;
            RoutePressable = false;
            return;
        }

        route.String = WithIcon(routeLine);
        route.IsVisible = true;

        RoutePressable = routeLine.Press is not null;
        route.Alpha = RoutePressable ? PressableIdleAlpha : 1f;

        hitBox.Position = route.Position;
        hitBox.Size = route.Size;
        hitBox.IsVisible = RoutePressable;

        navAnchor.Position = route.Position + new Vector2(NavAnchorInset, route.Height / 2f);
        navAnchor.IsVisible = RoutePressable;
    }
}
