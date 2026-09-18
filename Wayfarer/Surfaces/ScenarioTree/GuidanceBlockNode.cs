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
using static Wayfarer.Surfaces.ScenarioTree.ScenarioTreeMetrics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The block under the game's Main Scenario Guide: the entry being guided to, the route
/// line under it, and the compass with its distance in the icon column. Words are set when the
/// guidance changes; the needle and distance every frame.</summary>
internal sealed class GuidanceBlockNode : ResNode
{
    private const TextFlags WrappingFlags = TextFlags.Edge | TextFlags.WordWrap | TextFlags.MultiLine;
    private const TextFlags SingleLineFlags = TextFlags.Edge | TextFlags.Ellipsis;
    private const TextFlags DistanceFlags = TextFlags.Edge;
    private const string YalmsSuffix = "y";
    private const float PressableIdleAlpha = 0.8f;
    private const float NavAnchorInset = 2f;

    private static readonly Vector2 CompassOrigin = new(
        IconColumnLeft + ((IconColumnWidth - CompassSize) / 2f),
        RowTextTop + ((WordsLeading - CompassSize) / 2f));

    private readonly TextNode entry;
    private readonly TextNode route;
    private readonly TextNode distance;
    private readonly CompassNode compass;
    private readonly CollisionNode hitBox;
    private readonly NavFocusNode navAnchor;
    private string lastDistance = string.Empty;

    public GuidanceBlockNode(ITextureProvider textures, IPluginLog log, Action onRoutePressed)
    {
        Width = RootWidth;
        entry = Attach(EntryWords());
        route = Attach(RouteLine());
        hitBox = Attach(HitBox(onRoutePressed));
        navAnchor = Attach(NavAnchor(onRoutePressed));
        compass = Attach(new CompassNode(textures, log) { Position = CompassOrigin, IsVisible = false });
        distance = Attach(Distance());
    }

    /// <summary>Whether the route line can be pressed. The addon's collision list is rebuilt by the
    /// surface when this changes.</summary>
    public bool RoutePressable => hitBox.IsVisible;

    public void SetWords(string? entryWords, RouteLine? routeLine)
    {
        IsVisible = entryWords is not null;
        if (entryWords is null)
        {
            return;
        }

        entry.String = entryWords;
        entry.Height = EntryLines() * WordsLeading;

        var entryBlock = ((EntryLines() - 1) * WordsLeading) + WordsBlock;
        route.Y = RowTextTop + entryBlock;
        SetRoute(routeLine);

        Height = RowTextTop + entryBlock + (route.IsVisible ? RouteLeading : 0f);
    }

    public void SetHeading(float? needle, float? yalms)
    {
        distance.IsVisible = needle is not null && yalms is not null;
        if (needle is not { } radians || yalms is not { } distanceYalms)
        {
            compass.IsVisible = false;
            return;
        }

        compass.Show(CompassSize, radians);
        SetDistance(MathF.Round(distanceYalms).ToString(CultureInfo.InvariantCulture) + YalmsSuffix);
    }

    private static TextNode EntryWords()
    {
        var words = Words(WrappingFlags, GameColors.Body, WordsFontSize, WordsLeading);
        words.Position = new Vector2(WordsLeft, RowTextTop);
        words.Width = WordsWidth;
        return words;
    }

    private static TextNode RouteLine()
    {
        var words = Words(SingleLineFlags, GameColors.ListText, RouteFontSize, RouteLeading);
        words.Position = new Vector2(WordsLeft, RowTextTop + WordsBlock);
        words.Size = new Vector2(WordsWidth, RouteLeading);
        return words;
    }

    private static TextNode Distance()
    {
        var words = Words(DistanceFlags, GameColors.ListText, DistanceFontSize, RouteLeading);
        words.TextOutlineColor = GameColors.ListTextEdge;
        words.AlignmentType = AlignmentType.Top;
        words.Position = new Vector2(IconColumnLeft + ((IconColumnWidth - DistanceWidth) / 2f), CompassOrigin.Y + CompassSize);
        words.Size = new Vector2(DistanceWidth, RouteLeading);
        return words;
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

    private static BitmapFontIcon? Icon(RouteGlyph glyph) => glyph switch
    {
        RouteGlyph.Aetheryte => BitmapFontIcon.Aetheryte,
        RouteGlyph.Duty => BitmapFontIcon.WaitingForDutyFinder,
        _ => null,
    };

    private static ReadOnlySeString WithIcon(RouteLine line) =>
        Icon(line.Glyph) is { } icon
            ? new ReadOnlySeString(new SeStringBuilder().AddIcon(icon).AddText(line.Text).Build().Encode())
            : line.Text;

    private T Attach<T>(T node)
        where T : KamiToolKit.BaseTypes.NodeBase
    {
        node.AttachNode(this);
        return node;
    }

    private CollisionNode HitBox(Action onPressed)
    {
        var box = new CollisionNode { IsVisible = false, ShowClickableCursor = true };
        box.AddEvent(AtkEventType.MouseClick, onPressed);
        box.AddEvent(AtkEventType.MouseOver, () => route.Alpha = 1f);
        box.AddEvent(AtkEventType.MouseOut, () => route.Alpha = PressableIdleAlpha);
        return box;
    }

    private NavFocusNode NavAnchor(Action onPressed)
    {
        var anchor = new NavFocusNode
        {
            OnSelected = onPressed,
            OnHoverStart = () => route.Alpha = 1f,
            OnHoverEnd = () => route.Alpha = PressableIdleAlpha,
            Size = Vector2.Zero,
            IsVisible = false,
        };
        anchor.CollisionNode.RemoveNodeFlags(NodeFlags.Fill);
        return anchor;
    }

    private int EntryLines() =>
        Math.Clamp((int)MathF.Ceiling(entry.GetTextDrawSize(considerScale: false).Y / WordsLeading), 1, MaxEntryLines);

    private void SetRoute(RouteLine? line)
    {
        route.IsVisible = line is not null;
        hitBox.IsVisible = line?.Press is not null;
        navAnchor.IsVisible = hitBox.IsVisible;
        if (line is null)
        {
            return;
        }

        route.String = WithIcon(line);
        route.Alpha = hitBox.IsVisible ? PressableIdleAlpha : 1f;
        hitBox.Position = route.Position;
        hitBox.Size = route.Size;
        navAnchor.Position = route.Position + new Vector2(NavAnchorInset, route.Height / 2f);
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
