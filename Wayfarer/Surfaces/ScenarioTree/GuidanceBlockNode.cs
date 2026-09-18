using System.Globalization;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Wayfarer.Core.Ui;
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

    /// <summary>Our stops in the addon's controller navigation, well clear of the game's own.</summary>
    private const int EntryNavIndex = 100;
    private const int RouteNavIndex = 101;

    private static readonly Vector2 CompassOrigin = new(
        IconColumnLeft + ((IconColumnWidth - CompassSize) / 2f),
        RowTextTop + ((WordsLeading - CompassSize) / 2f));

    private readonly PressableLine entry;
    private readonly PressableLine route;
    private readonly TextNode distance;
    private readonly CompassNode compass;
    private string lastDistance = string.Empty;
    private ElevationHint elevation = ElevationHint.Level;

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

    /// <summary>The controls the addon should treat as focusable: each pressable line's, in order.</summary>
    public unsafe nint[] FocusTargets => [.. new[] { entry, route }.Where(line => line.Pressable).Select(line => (nint)line.FocusTarget)];

    /// <summary>The first of our stops the cursor can move down to from the plate, or null.</summary>
    public int? FirstStop => entry.Pressable ? EntryNavIndex : route.Pressable ? RouteNavIndex : null;

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

        Height = RowTextTop + entryBlock + (route.IsVisible ? route.Height : 0f);
    }

    /// <summary>Moves the pad cursor between our lines and the plate ourselves, because the plate's
    /// own input code never consults the index table. The cursor goes round in a loop: plate, step
    /// line, route line, plate; up runs the loop the other way.</summary>
    public unsafe void WireFocus(AtkUnitBase* addon, AtkResNode* plateFocus)
    {
        entry.OnUp = () => Focus(addon, plateFocus);
        entry.OnDown = () => Focus(addon, route.Pressable ? route.FocusTarget : plateFocus);
        route.OnUp = () => Focus(addon, entry.Pressable ? entry.FocusTarget : plateFocus);
        route.OnDown = () => Focus(addon, plateFocus);
        entry.SetNav(EntryNavIndex, EntryNavIndex, EntryNavIndex);
        route.SetNav(RouteNavIndex, RouteNavIndex, RouteNavIndex);
    }

    public void SetHeading(float? needle, float? yalms, float? rise)
    {
        distance.IsVisible = needle is not null && yalms is not null;
        if (needle is not { } radians || yalms is not { } distanceYalms)
        {
            compass.IsVisible = false;
            elevation = ElevationHint.Level;
            return;
        }

        elevation = Elevation.Classify(rise, elevation);
        compass.Show(CompassSize, radians, elevation);
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

    private static unsafe void Focus(AtkUnitBase* addon, AtkResNode* node)
    {
        if (node != null)
        {
            AtkStage.Instance()->AtkInputManager->SetFocus(node, addon, 0);
        }
    }

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
