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
    private const string YalmsSuffix = "y";

    private readonly TextNode entry;
    private readonly TextNode route;
    private readonly TextNode distance;
    private readonly CompassNode compass;
    private string lastDistance = string.Empty;

    public GuidanceBlockNode(ITextureProvider textures, IPluginLog log)
    {
        Width = ScenarioTreeMetrics.RootWidth;

        entry = Words(WordFlags, GameColors.Body);
        entry.Position = new Vector2(ScenarioTreeMetrics.WordsLeft, 0f);
        entry.Width = ScenarioTreeMetrics.WordsWidth;
        entry.AttachNode(this);

        route = Words(SingleLineFlags, GameColors.ListText);
        route.Position = new Vector2(ScenarioTreeMetrics.WordsLeft, ScenarioTreeMetrics.LinePitch);
        route.Size = new Vector2(ScenarioTreeMetrics.WordsWidth, ScenarioTreeMetrics.LinePitch);
        route.AttachNode(this);

        compass = new CompassNode(textures, log) { IsVisible = false };
        compass.Position = new Vector2(
            ScenarioTreeMetrics.IconColumnLeft + ((ScenarioTreeMetrics.IconColumnWidth - ScenarioTreeMetrics.CompassSize) / 2f),
            (ScenarioTreeMetrics.LinePitch - ScenarioTreeMetrics.CompassSize) / 2f);
        compass.AttachNode(this);

        distance = Words(SingleLineFlags, GameColors.Dimmed);
        distance.FontSize = ScenarioTreeMetrics.DistanceFontSize;
        distance.AlignmentType = AlignmentType.Top;
        distance.Position = new Vector2(ScenarioTreeMetrics.IconColumnLeft, compass.Position.Y + ScenarioTreeMetrics.CompassSize);
        distance.Size = new Vector2(ScenarioTreeMetrics.IconColumnWidth, ScenarioTreeMetrics.LinePitch);
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
        var lines = Math.Clamp(MathF.Ceiling(entry.GetTextDrawSize(considerScale: false).Y / ScenarioTreeMetrics.LinePitch), 1, ScenarioTreeMetrics.MaxEntryLines);
        entry.Height = lines * ScenarioTreeMetrics.LinePitch;

        route.String = routeWords ?? string.Empty;
        route.IsVisible = routeWords is not null;
        route.Y = entry.Height;

        Height = entry.Height + (route.IsVisible ? ScenarioTreeMetrics.LinePitch : 0f);
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

    private static TextNode Words(TextFlags flags, Vector4 color) => new()
    {
        FontType = FontType.Axis,
        FontSize = ScenarioTreeMetrics.FontSize,
        LineSpacing = (uint)ScenarioTreeMetrics.LinePitch,
        AlignmentType = AlignmentType.TopLeft,
        TextFlags = flags,
        TextColor = color,
        TextOutlineColor = GameColors.BodyEdge,
    };
}
