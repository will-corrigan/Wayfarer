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
    /// <summary>The largest extent any node on this surface may be given. Every size the game is
    /// handed lands in an unsigned 16-bit field; this is that field's own ceiling, and a number past
    /// it means the arithmetic that produced it has gone wrong rather than that the player wants a
    /// node that big.</summary>
    private const float MaxNodeExtent = 65535f;

    /// <summary>A crop of the Journal's own page art — a section glyph, the level disc, the reward
    /// tray. All of them live in <c>ui/uld/Journal_Detail.tex</c>, and the part rectangle has to be
    /// the authored one or the node samples past the edge of the texture and draws a band of
    /// nothing.
    ///
    /// <para>The load is guarded: a texture a patch has moved must cost a glyph, not the whole
    /// surface. A failed node stays invisible and the text it decorates is drawn regardless — the
    /// glyphs are the journal's accent, never its content.</para>
    ///
    /// <para>So is the crop. <see cref="Crop"/> refuses a rectangle the loaded sheet does not
    /// actually contain rather than handing it to the game, and a node asked for at a size the game
    /// cannot draw is never given one.</para></summary>
    public static SimpleImageNode Art(
        NodeBase? parent, IPluginLog log, (float U, float V) at, float width, float height = 0f)
    {
        ArgumentNullException.ThrowIfNull(log);

        var size = Drawable(new Vector2(width, height > 0f ? height : width));
        var node = new SimpleImageNode { Size = size, WrapMode = WrapMode.Stretch, IsVisible = false };

        if (size == Vector2.Zero)
        {
            log.Warning(
                $"Wayfarer journal: a piece of the journal's page art was asked for at {width}x{height}, which is "
                + "not a size the game can draw, so it is left out. Nothing else is affected.");
            return node;
        }

        try
        {
            node.LoadTexture(GameMetrics.JournalArt.Texture);
            Crop(node, log, GameMetrics.JournalArt.Texture, new Vector2(at.U, at.V), size);
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

    /// <summary>Sets an image node's part rectangle, having first checked the loaded sheet actually
    /// holds it.
    ///
    /// <para>This is the same discipline the status icons already have — "does this exist in this
    /// patch?" asked before the answer is handed to the game rather than after. A sheet a patch has
    /// shrunk or renumbered otherwise leaves a node sampling outside the texture it was cropped
    /// from, and the piece is left out entirely instead. The node keeps whatever visibility it had;
    /// this only ever takes a crop away.</para>
    ///
    /// <para>The bounds check is deliberately one-sided. A texture the game reports at a scaled
    /// resolution reads as "no objection" — the crop is applied and the worst case is the band of
    /// nothing it always was. Only a sheet that is definitely too small for the rectangle, or that
    /// did not load at all, costs the piece.</para>
    ///
    /// <para>Returns whether the crop was applied, which the caller keeps: a piece refused once must
    /// stay refused through every later resize, not be shown again by the next relayout.</para>
    /// </summary>
    public static bool Crop(
        SimpleImageNode node, IPluginLog log, string texture, Vector2 at, Vector2 size)
    {
        ArgumentNullException.ThrowIfNull((object?)node, nameof(node));

        if (!Croppable(log, texture, node.TexturePath, node.ActualTextureSize, at, size))
        {
            node.IsVisible = false;
            return false;
        }

        node.TextureCoordinates = at;
        node.TextureSize = size;
        return true;
    }

    /// <inheritdoc cref="Crop(SimpleImageNode, IPluginLog, string, Vector2, Vector2)"/>
    /// <remarks>A nine-grid cannot report the size of the sheet it loaded, so the bounds arm is not
    /// available here and the check is "did the texture load at all".</remarks>
    public static bool Crop(
        SimpleNineGridNode node, IPluginLog log, string texture, Vector2 at, Vector2 size)
    {
        ArgumentNullException.ThrowIfNull((object?)node, nameof(node));

        if (!Croppable(log, texture, node.TexturePath, Vector2.Zero, at, size))
        {
            node.IsVisible = false;
            return false;
        }

        node.TextureCoordinates = at;
        node.TextureSize = size;
        return true;
    }

    /// <summary>A size the game can be given, or <see cref="Vector2.Zero"/> for one it cannot.
    ///
    /// <para>Every size on this surface ends up in an unsigned 16-bit field, so a negative one wraps
    /// to something enormous and a non-finite one is undefined outright. Refusing here is what makes
    /// "the number was wrong" cost a missing piece rather than the game.</para></summary>
    public static Vector2 Drawable(Vector2 size) =>
        float.IsFinite(size.X) && float.IsFinite(size.Y)
        && size.X > 0f && size.Y > 0f
        && size.X <= MaxNodeExtent && size.Y <= MaxNodeExtent
            ? size
            : Vector2.Zero;

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

    /// <summary>A rectangle of runtime-chosen icon art — the status marker, a reward's own picture,
    /// the banner. Hidden until something fills it, and sized by the caller because those three
    /// slots are three different sizes and only one of them is square.
    ///
    /// <para>A size the game cannot draw produces a node that is permanently invisible rather than a
    /// node with a bad size in it: see <see cref="Drawable"/>.</para></summary>
    public static IconImageNode Marker(NodeBase? parent, Vector2 size)
    {
        var node = new IconImageNode
        {
            // Zero when the size was refused, which is the state ApplyIcon reads as "this slot has
            // no room, so it has no picture either". A node of no size draws nothing and takes no
            // room in the stack above it, which is exactly what a slot nobody can size should do.
            Size = Drawable(size),
            FitTexture = true,
            IsVisible = false,
        };

        Attach(node, parent);
        return node;
    }

    /// <summary>Sets a runtime icon and its part rectangle. The rectangle has to match the size the
    /// art is authored at, or the node samples past the edge of the texture and draws a band of
    /// nothing; the loaded texture answers when it can and the caller's measured size is the seed
    /// for when it cannot.
    ///
    /// <para>Both the id and the rectangle are checked before either reaches the game. An id of zero
    /// is "there is no art for this", and a part rectangle that is not a size the game can sample —
    /// including the one an unresolvable icon leaves behind — hides the node instead of being
    /// handed over.</para></summary>
    public static void ApplyIcon(IconImageNode node, uint iconId, Vector2 authored)
    {
        ArgumentNullException.ThrowIfNull((object?)node, nameof(node));

        if (iconId == 0 || Drawable(node.Size) == Vector2.Zero)
        {
            node.IsVisible = false;
            return;
        }

        node.IconId = iconId;

        var actual = node.ActualTextureSize;
        var wanted = Drawable(actual) != Vector2.Zero ? actual : Drawable(authored);
        if (wanted == Vector2.Zero)
        {
            // No usable rectangle from either the loaded texture or the caller. Drawing the slot
            // would mean sampling a region nobody has vouched for, so there is no slot.
            node.IsVisible = false;
            return;
        }

        node.TextureSize = wanted;
        node.IsVisible = true;
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

    /// <summary>Whether a part rectangle may be handed to the game: the numbers have to be ones it
    /// can sample, the texture has to have actually loaded, and — when the sheet can say how big it
    /// is — the rectangle has to be inside it.</summary>
    private static bool Croppable(
        IPluginLog log, string texture, string loadedPath, Vector2 sheet, Vector2 at, Vector2 size)
    {
        ArgumentNullException.ThrowIfNull(log);

        if (Drawable(size) == Vector2.Zero || !float.IsFinite(at.X) || !float.IsFinite(at.Y)
            || at.X < 0f || at.Y < 0f)
        {
            log.Warning(
                $"Wayfarer journal: a crop of {texture} at ({at.X},{at.Y}) sized {size.X}x{size.Y} is not a "
                + "rectangle the game can sample, so that piece is left out. Nothing else is affected.");
            return false;
        }

        if (string.IsNullOrEmpty(loadedPath))
        {
            log.Warning(
                $"Wayfarer journal: {texture} did not resolve in this game version, so the piece cropped from it "
                + $"at ({at.X},{at.Y}) is left out. Nothing else is affected.");
            return false;
        }

        if (sheet.X > 0f && sheet.Y > 0f && (at.X + size.X > sheet.X || at.Y + size.Y > sheet.Y))
        {
            log.Warning(
                $"Wayfarer journal: {texture} is {sheet.X}x{sheet.Y} in this game version, which does not hold the "
                + $"crop at ({at.X},{at.Y}) sized {size.X}x{size.Y}, so that piece is left out. Nothing else is "
                + "affected.");
            return false;
        }

        return true;
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
