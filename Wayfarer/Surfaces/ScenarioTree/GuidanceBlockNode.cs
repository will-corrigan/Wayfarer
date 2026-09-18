using System.Globalization;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Wayfarer.Ui;
using static Wayfarer.Surfaces.ScenarioTree.ScenarioTreeMetrics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The block under the game's Main Scenario Guide: the entry being guided to, the route
/// line under it, and the compass with its distance in the icon column. Either line can be a
/// control. Words are set when the guidance changes; the needle and distance every frame.</summary>
internal sealed class GuidanceBlockNode : ResNode
{
    private const TextFlags WrappingFlags = TextFlags.Edge | TextFlags.WordWrap | TextFlags.MultiLine;
    private const TextFlags SingleLineFlags = TextFlags.Edge | TextFlags.Ellipsis;
    private const TextFlags DistanceFlags = TextFlags.Edge;
    private const string YalmsSuffix = "y";
    private const int EntryNavIndex = 1;
    private const int RouteNavIndex = 2;

    private static readonly Vector2 CompassOrigin = new(
        IconColumnLeft + ((IconColumnWidth - CompassSize) / 2f),
        RowTextTop + ((WordsLeading - CompassSize) / 2f));

    private readonly PressableLine entry;
    private readonly PressableLine route;
    private readonly TextNode distance;
    private readonly CompassNode compass;
    private string lastDistance = string.Empty;

    public GuidanceBlockNode(ITextureProvider textures, IPluginLog log, Action onEntryPressed, Action onRoutePressed)
    {
        Width = RootWidth;

        entry = Attach(new PressableLine(WrappingFlags, GameColors.Body, WordsFontSize, WordsLeading, MaxEntryLines, onEntryPressed));
        entry.Position = new Vector2(WordsLeft, RowTextTop);
        entry.Width = WordsWidth;

        route = Attach(new PressableLine(SingleLineFlags, GameColors.ListText, RouteFontSize, RouteLeading, 1, onRoutePressed));
        route.Width = WordsWidth;

        compass = Attach(new CompassNode(textures, log) { Position = CompassOrigin, IsVisible = false });
        distance = Attach(Distance());
    }

    /// <summary>Whether any line can be pressed. The addon's collision list is rebuilt by the
    /// surface when this changes.</summary>
    public bool AnyPressable => entry.Pressable || route.Pressable;

    public void SetWords(LineContent? entryContent, LineContent? routeContent)
    {
        IsVisible = entryContent is not null;
        if (entryContent is null)
        {
            return;
        }

        entry.Set(entryContent);
        var entryBlock = entry.Height - WordsLeading + WordsBlock;

        route.Position = new Vector2(WordsLeft, RowTextTop + entryBlock);
        route.Set(routeContent);

        entry.SetNav(EntryNavIndex, route.Pressable ? RouteNavIndex : EntryNavIndex, route.Pressable ? RouteNavIndex : EntryNavIndex);
        route.SetNav(RouteNavIndex, entry.Pressable ? EntryNavIndex : RouteNavIndex, entry.Pressable ? EntryNavIndex : RouteNavIndex);

        Height = RowTextTop + entryBlock + (route.IsVisible ? route.Height : 0f);
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

    private static TextNode Distance() => new()
    {
        FontType = FontType.Axis,
        FontSize = DistanceFontSize,
        LineSpacing = (uint)RouteLeading,
        AlignmentType = AlignmentType.Top,
        TextFlags = DistanceFlags,
        TextColor = GameColors.ListText,
        TextOutlineColor = GameColors.ListTextEdge,
        Position = new Vector2(IconColumnLeft + ((IconColumnWidth - DistanceWidth) / 2f), CompassOrigin.Y + CompassSize),
        Size = new Vector2(DistanceWidth, RouteLeading),
    };

    private T Attach<T>(T node)
        where T : KamiToolKit.BaseTypes.NodeBase
    {
        node.AttachNode(this);
        return node;
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
