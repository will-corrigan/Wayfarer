using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>The journal's vocabulary as nodes, built once so the two surfaces that speak it — the
/// detail strip under the list and the full page that replaces it — cannot drift into two dialects.
///
/// <para>Every type here is one already proven in this plugin: <c>SimpleImageNode</c> for a crop of
/// the game's own page art, <c>IconImageNode</c> for runtime-chosen artwork, <c>TextNode</c> for
/// everything written. Nothing is constructed that has not been drawn on a screen before.</para>
/// </summary>
internal static class JournalNodes
{
    /// <summary>A crop of the Journal's own page art — a section glyph, the level disc, the reward
    /// tray. All of them live in <c>ui/uld/Journal_Detail.tex</c>, and the part rectangle has to be
    /// the authored one or the node samples past the edge of the texture and draws a band of
    /// nothing.
    ///
    /// <para>The load is guarded: a texture a patch has moved must cost a glyph, not the whole
    /// surface. A failed node stays invisible and the text it decorates is drawn regardless — the
    /// glyphs are the journal's accent, never its content.</para></summary>
    public static SimpleImageNode Art(
        NodeBase? parent, IPluginLog log, (float U, float V) at, float width, float height = 0f)
    {
        ArgumentNullException.ThrowIfNull(log);

        var size = new Vector2(width, height > 0f ? height : width);
        var node = new SimpleImageNode { Size = size, WrapMode = WrapMode.Stretch, IsVisible = false };

        try
        {
            node.LoadTexture(GameMetrics.JournalArt.Texture);
            node.TextureCoordinates = new Vector2(at.U, at.V);
            node.TextureSize = size;
        }
        catch (Exception ex)
        {
            var why = $"Wayfarer hub: {GameMetrics.JournalArt.Texture} could not be read, so the journal's "
                + "sections will be drawn without their glyphs. Nothing else is affected.";
            log.Warning(ex, why);
        }

        Attach(node, parent);
        return node;
    }

    /// <summary>A line of body text, attached and ready to be placed.</summary>
    public static TextNode Line(NodeBase? parent, uint size, Vector4 color, TextFlags flags)
    {
        var node = Body(size, color, flags);
        Attach(node, parent);
        return node;
    }

    /// <summary>A section heading in the journal's register: Axis over the glyph beside it, in the
    /// page's heading colour — a muted grey-brown, not the HUD's white, because this text is on
    /// paper. See <see cref="GameColors.JournalPage"/>. The words are the game's own — Addon 2835
    /// "Requirements", 463 "Reward", 543 "Description", 2836 "Information".</summary>
    public static TextNode Heading(NodeBase? parent, string text)
    {
        var node = Body(GameMetrics.Type.SecondarySize, GameColors.JournalPage.Heading, TextFlags.Ellipsis);
        node.String = text;
        Attach(node, parent);
        return node;
    }

    /// <summary>The entry's name. The game's own treatment — JournalDetail sets its heading in Axis
    /// 18 at leading 20, not in the window-title face. TrumpGothic belongs on the window's own title
    /// bar and on the level badge, nowhere else.
    ///
    /// <para>No edge. The HUD's titles are white with a bronze outline because they are drawn over
    /// the world; a near-black title on parchment with an outline under it reads as a printing
    /// fault.</para></summary>
    public static MeasuredTextNode Title(NodeBase? parent)
    {
        var node = new MeasuredTextNode
        {
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.DetailTitleSize,
            LineSpacing = GameMetrics.Type.DetailTitleLine,
            AlignmentType = AlignmentType.TopLeft,
            TextFlags = TextFlags.MultiLine | TextFlags.WordWrap,
            TextColor = GameColors.JournalPage.Title,
            MaxHeight = JournalWindowLayout.TitleHeight(JournalWindowLayout.MaxTitleLines),
        };
        Attach(node, parent);
        return node;
    }

    /// <summary>The kind word, pinned to the right of the title — the game's own caption column, in
    /// the page's quietest text colour.</summary>
    public static TextNode Kind(NodeBase? parent)
    {
        var node = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.SecondarySize,
            LineSpacing = GameMetrics.Type.SecondaryLine,
            AlignmentType = AlignmentType.TopRight,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.JournalPage.Meta,
        };
        Attach(node, parent);
        return node;
    }

    /// <summary>A wrapping block of the page's prose, which reports its own height so the stack above
    /// it never has to guess. <paramref name="maxLines"/> caps how tall it may grow, which is what
    /// keeps the page's own height finite.</summary>
    public static MeasuredTextNode Paragraph(NodeBase? parent, int maxLines)
    {
        var node = new MeasuredTextNode
        {
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.BodySize,
            LineSpacing = GameMetrics.Type.BodyLine,
            AlignmentType = AlignmentType.TopLeft,
            TextFlags = TextFlags.MultiLine | TextFlags.WordWrap,
            TextColor = GameColors.JournalPage.Body,
            MaxHeight = JournalWindowLayout.BlockHeight(maxLines),
        };
        Attach(node, parent);
        return node;
    }

    /// <summary>The number on the level badge. TrumpGothic is the Journal's own face for it, and the
    /// only place the page uses that face.</summary>
    public static TextNode Level(NodeBase? parent)
    {
        var node = new TextNode
        {
            FontType = FontType.TrumpGothic,
            FontSize = GameMetrics.Journal.BadgeTextSize,
            AlignmentType = AlignmentType.Center,
            TextFlags = TextFlags.Edge,
            TextColor = GameColors.Heading,
            TextOutlineColor = GameColors.HeadingEdge,
            IsVisible = false,
        };
        Attach(node, parent);
        return node;
    }

    /// <summary>A square of runtime-chosen icon art — the status marker, a reward's own picture, the
    /// banner. Hidden until something fills it, and sized by the caller because those three slots
    /// are three different sizes.</summary>
    public static IconImageNode Marker(NodeBase? parent, Vector2 size)
    {
        var node = new IconImageNode
        {
            Size = size,
            FitTexture = true,
            IsVisible = false,
        };
        Attach(node, parent);
        return node;
    }

    /// <summary>Sets a runtime icon and its part rectangle. The rectangle has to match the size the
    /// art is authored at, or the node samples past the edge of the texture and draws a band of
    /// nothing; the loaded texture answers when it can and the caller's measured size is the seed
    /// for when it cannot.</summary>
    public static void ApplyIcon(IconImageNode node, uint iconId, Vector2 authored)
    {
        node.IconId = iconId;
        var actual = node.ActualTextureSize;
        node.TextureSize = actual.X > 0f && actual.Y > 0f ? actual : authored;
    }

    /// <summary>Hands a set of nodes to a layout container, once each and never twice.
    ///
    /// <para><b>This is a guard against a hang, not a tidiness measure.</b> Attaching the same node
    /// to the same container twice makes the game's own sibling chain circular:
    /// <c>NodeLinker.EmplaceAsLastChild</c> walks to the end of the chain and links the incoming
    /// node onto it, so a node that is already <i>in</i> that chain is linked to itself or to the
    /// node in front of it. The next attach then walks that chain looking for its end, and there is
    /// no longer an end — the walk never returns, on the game's own main thread, with no exception
    /// and no crash dump. The game simply stops.</para>
    ///
    /// <para>So every container on the journal's surfaces is filled through here rather than through
    /// <c>AddNode</c> directly, and a node that is already in the container is skipped.</para>
    /// </summary>
    public static void AddOnce(LayoutListNode list, params NodeBase?[] nodes)
    {
        // Cast: NodeBase converts implicitly to a raw node pointer, which makes the unqualified
        // overload ambiguous.
        ArgumentNullException.ThrowIfNull((object?)list, nameof(list));
        ArgumentNullException.ThrowIfNull(nodes);

        foreach (var node in nodes)
        {
            if (node is null || list.Nodes.Contains(node))
            {
                continue;
            }

            list.AddNode(node);
        }
    }

    /// <summary>Attaches a fresh node to its parent, or leaves it detached when there is none.
    ///
    /// <para>A null parent is not an oversight — it is how a node destined for a layout container is
    /// built. <c>LayoutListNode.AddNode</c> attaches what it is given, so a node that has already
    /// been attached to the same container would be linked into the tree twice, and the second link
    /// is what makes the sibling chain circular — see <see cref="AddOnce"/> for what that costs.
    /// A layout container is therefore refused outright here: the container is the one that
    /// attaches its own children, and this is the choke point that makes it impossible to do it
    /// twice however the call site is written.</para></summary>
    private static void Attach(NodeBase node, NodeBase? parent)
    {
        if (parent is null or LayoutListNode)
        {
            return;
        }

        node.AttachNode(parent);
    }

    private static TextNode Body(uint size, Vector4 color, TextFlags flags) => new()
    {
        FontType = FontType.Axis,
        FontSize = size,
        AlignmentType = AlignmentType.TopLeft,
        TextFlags = flags,
        TextColor = color,
        LineSpacing = size == GameMetrics.Type.BodySize
            ? GameMetrics.Type.BodyLine
            : GameMetrics.Type.SecondaryLine,
    };
}
