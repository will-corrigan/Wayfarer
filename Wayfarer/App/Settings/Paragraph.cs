using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Wayfarer.Ui;

namespace Wayfarer.App.Settings;

/// <summary>A block of words that wraps to a width and is exactly as tall as the words it ended up
/// drawing. Sizing to the text is what keeps a page's words inside their pane: a fixed height either
/// cuts a long sentence off or leaves a hole under a short one.</summary>
internal sealed class Paragraph : TextNode
{
    private const uint DefaultFontSize = 12;

    public Paragraph()
    {
        FontType = FontType.Axis;
        FontSize = DefaultFontSize;
        LineSpacing = (uint)GameText.LeadingFor(DefaultFontSize);
        AlignmentType = AlignmentType.TopLeft;
        TextFlags = TextFlags.WordWrap | TextFlags.MultiLine;
        TextColor = GameColors.ListText;
        TextOutlineColor = GameColors.ListTextEdge;
    }

    /// <summary>Wraps the words to a width and takes the height they needed.</summary>
    public void Set(string words, float width)
    {
        Size = new Vector2(width, LineSpacing);
        String = words;
        Height = MathF.Max(LineSpacing, GetTextDrawSize(considerScale: false).Y);
    }
}
