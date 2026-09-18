using System.Globalization;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Wayfarer.Ui;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The block Wayfarer draws under the game's Main Scenario Guide: the entry being
/// guided to, the route's words beneath it, and the compass with its distance in the icon
/// column beside them. Laid out in the addon's own root coordinates.
///
/// <para>Two kinds of input at two rates. <see cref="SetWords"/> is called when the guidance
/// changes and re-lays the text; <see cref="SetHeading"/> is called every frame and only moves
/// the needle and rewrites the distance. Nothing per-frame touches a text node's string except
/// the distance, which is one short number.</para></summary>
internal sealed class GuidanceBlockNode : ResNode
{
    private const TextFlags WordFlags = TextFlags.Edge | TextFlags.WordWrap | TextFlags.MultiLine;
    private const TextFlags SingleLineFlags = TextFlags.Edge | TextFlags.Ellipsis;
    private const TextFlags DistanceFlags = TextFlags.Edge;
    private const string YalmsSuffix = "y";

    private readonly TextNode entry;
    private readonly TextNode route;
    private readonly TextNode distance;
    private readonly CompassNode compass;
    private string lastDistance = string.Empty;

    public GuidanceBlockNode(ITextureProvider textures, IPluginLog log)
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

    /// <summary>Lays the words out. Null entry words hide the whole block.</summary>
    public void SetWords(string? entryWords, string? routeWords)
    {
        if (entryWords is null)
        {
            IsVisible = false;
            return;
        }

        entry.String = entryWords;
        var lines = Math.Clamp(MathF.Ceiling(entry.GetTextDrawSize(considerScale: false).Y / ScenarioTreeMetrics.WordsLeading), 1, ScenarioTreeMetrics.MaxEntryLines);
        entry.Height = lines * ScenarioTreeMetrics.WordsLeading;

        // The last line gets the tracker's full block before anything hangs under it.
        var entryBlock = ((lines - 1) * ScenarioTreeMetrics.WordsLeading) + ScenarioTreeMetrics.WordsBlock;
        route.String = routeWords ?? string.Empty;
        route.IsVisible = routeWords is not null;
        route.Y = ScenarioTreeMetrics.RowTextTop + entryBlock;

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
}
