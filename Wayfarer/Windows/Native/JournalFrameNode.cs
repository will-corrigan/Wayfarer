using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>The Journal's parchment and its gilt border, as nodes.
///
/// <para><b>What this is.</b> JournalDetail's page background (<c>#54</c>) and its frame group
/// (<c>#11</c>) reproduced piece for piece: one nine-grid of paper, then sixteen images of gold on
/// top of it. Nothing here is invented — the destinations, the part rectangles and the mirror flags
/// all come out of <see cref="JournalFrameLayout"/>, which reads them off the game's own tree, and
/// the assembly was rendered to a PNG at the authored 496x628 and looked at before it was built as
/// nodes.</para>
///
/// <para><b>Width is fixed, height is free.</b> Fourteen of the sixteen pieces are plain images
/// drawn at the size their art is authored at and cannot be stretched; the two vertical rails are
/// nine-grids with one-pixel caps and absorb every height difference. That is the game's own
/// arrangement, and it is why this node is only ever
/// <see cref="GameMetrics.JournalFrame.Width"/> wide.</para>
///
/// <para><b>The border is decoration and never content.</b> Every load is guarded and a piece that
/// fails stays invisible: a texture a patch has moved must cost the ornament, not the page. The
/// parchment is the same — a page with no paper under it is still a readable page.</para></summary>
internal sealed class JournalFrameNode : ResNode
{
    /// <summary>What is said if the border's own sheet cannot be read. Said once: it is one texture
    /// and every piece of the frame would otherwise say it sixteen times.</summary>
    private const string BorderUnavailable =
        "Wayfarer journal: ui/uld/Journal_Frame.tex could not be read, so the journal window is drawn on "
        + "its parchment without the gilt border. Nothing else is affected.";

    private readonly IPluginLog log;
    private readonly SimpleNineGridNode parchmentNode;
    private readonly bool parchmentDrawn;
    private readonly List<(SimpleImageNode Node, int SourceNode, bool Drawn)> plainPieces = [];
    private readonly List<(SimpleNineGridNode Node, int SourceNode, bool Drawn)> railPieces = [];

    public JournalFrameNode(IPluginLog log)
    {
        this.log = log;
        parchmentNode = BuildParchment(out parchmentDrawn);

        // Built from the authored height so every piece exists with its own part rectangle from the
        // start; Layout only ever moves and resizes them afterwards. Building from the live height
        // would mean a resize could be the first thing that ever creates a node, which is how a
        // border comes to be missing a corner for one frame.
        //
        // A piece whose crop was refused is remembered as such, so the relayout below never shows it
        // again: "this piece has no art in this game version" has to survive every resize.
        foreach (var piece in JournalFrameLayout.Pieces(GameMetrics.JournalFrame.AuthoredHeight))
        {
            if (piece.Stretches)
            {
                railPieces.Add((BuildRail(piece, out var railDrawn), piece.SourceNode, railDrawn));
            }
            else
            {
                plainPieces.Add((BuildPlate(piece, out var plateDrawn), piece.SourceNode, plateDrawn));
            }
        }
    }

    /// <summary>Whether every piece of the border found its art. False means the frame is drawing
    /// short of its sixteen pieces, which is a state worth one line in the log rather than a
    /// silently gapped edge.</summary>
    public bool IsComplete =>
        parchmentDrawn && plainPieces.TrueForAll(p => p.Drawn) && railPieces.TrueForAll(p => p.Drawn);

    /// <summary>The box inside the border's rails — where the page's own contents have to live.
    /// </summary>
    public ScreenRect Inner => JournalFrameLayout.Inner(Height);

    /// <summary>Re-lays the border for the node's current size. Only the rails and the foot move:
    /// the top is fixed and the horizontal run is authored, so a resize is two heights and five
    /// positions.</summary>
    public void Layout()
    {
        var height = Height;
        Place(parchmentNode, JournalFrameLayout.Parchment(height), parchmentDrawn);

        var pieces = JournalFrameLayout.Pieces(height);
        foreach (var (node, sourceNode, drawn) in plainPieces)
        {
            Place(node, Find(pieces, sourceNode), drawn);
        }

        foreach (var (node, sourceNode, drawn) in railPieces)
        {
            Place(node, Find(pieces, sourceNode), drawn);
        }
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        Layout();
    }

    private static ScreenRect Find(IReadOnlyList<JournalFramePiece> pieces, int sourceNode)
    {
        foreach (var piece in pieces)
        {
            if (piece.SourceNode == sourceNode)
            {
                return piece.Destination;
            }
        }

        return default;
    }

    /// <summary>Moves and resizes one piece, or hides it. A destination the game cannot be given —
    /// empty, negative, or not a finite number — hides the piece instead of being written: the
    /// border is decoration, and the worst a bad number here may cost is a missing edge.</summary>
    private static void Place(NodeBase node, ScreenRect rect, bool drawn)
    {
        var size = JournalNodes.Drawable(new Vector2(rect.Width, rect.Height));
        var placeable = drawn && !rect.IsEmpty && size != Vector2.Zero
            && float.IsFinite(rect.X) && float.IsFinite(rect.Y);

        node.IsVisible = placeable;
        if (!placeable)
        {
            return;
        }

        node.Position = new Vector2(rect.X, rect.Y);
        node.Size = size;
    }

    /// <summary>The paper. JournalDetail <c>#54</c>: a nine-grid on <c>Journal_Detail.tex</c>
    /// (376,28) 96x96 with corner offsets T20 B28 L24 R24 — the only piece of this window that
    /// stretches in both directions, and the reason it can.</summary>
    private SimpleNineGridNode BuildParchment(out bool drawn)
    {
        var node = new SimpleNineGridNode { IsVisible = false };
        drawn = false;

        try
        {
            node.TexturePath = GameMetrics.JournalArt.Texture;
            drawn = JournalNodes.Crop(
                node,
                log,
                GameMetrics.JournalArt.Texture,
                new Vector2(GameMetrics.JournalArt.Parchment.U, GameMetrics.JournalArt.Parchment.V),
                new Vector2(
                    GameMetrics.JournalArt.ParchmentPartSize, GameMetrics.JournalArt.ParchmentPartSize));

            // Vector4 order is (Top, Bottom, Left, Right), and these four are the game's own.
            node.Offsets = new Vector4(
                GameMetrics.JournalArt.ParchmentTopOffset,
                GameMetrics.JournalArt.ParchmentBottomOffset,
                GameMetrics.JournalArt.ParchmentSideOffset,
                GameMetrics.JournalArt.ParchmentSideOffset);
        }
        catch (Exception ex)
        {
            const string why =
                "Wayfarer journal: ui/uld/Journal_Detail.tex could not be read, so the journal window is drawn "
                + "without its parchment. The page itself is unaffected.";
            log.Warning(ex, why);
            drawn = false;
        }

        node.AttachNode(this);
        return node;
    }

    /// <summary>One plain piece of gold, at the size its art is authored at.
    ///
    /// <para><c>WrapMode.Tile</c> and no <c>AutoFit</c>, deliberately: the node and the part are the
    /// same rectangle, so there is nothing to stretch, and <c>FitTexture</c> would set the
    /// <c>AutoFit</c> flag and take the mirror flag with it. The mirror is the whole reason a
    /// 240x192 sheet holding one corner can close a border on four sides.</para></summary>
    private SimpleImageNode BuildPlate(JournalFramePiece piece, out bool drawn)
    {
        var node = new SimpleImageNode
        {
            Size = JournalNodes.Drawable(new Vector2(piece.Destination.Width, piece.Destination.Height)),
            WrapMode = WrapMode.Tile,
            IsVisible = false,
        };

        drawn = LoadBorder(node, piece);
        if (piece.FlipHorizontally)
        {
            node.ImageNodeFlags |= ImageNodeFlags.FlipH;
        }

        node.AttachNode(this);
        return node;
    }

    /// <summary>One of the two vertical rails: a 32x40 nine-grid with a one-pixel cap top and
    /// bottom, which is what lets the border be any height.</summary>
    private SimpleNineGridNode BuildRail(JournalFramePiece piece, out bool drawn)
    {
        var node = new SimpleNineGridNode { IsVisible = false };
        drawn = false;

        try
        {
            node.TexturePath = GameMetrics.JournalFrame.Texture;
            drawn = JournalNodes.Crop(
                node,
                log,
                GameMetrics.JournalFrame.Texture,
                new Vector2(piece.Source.X, piece.Source.Y),
                new Vector2(piece.Source.Width, piece.Source.Height));
            node.Offsets = new Vector4(1f, 1f, 0f, 0f);
        }
        catch (Exception ex)
        {
            log.Warning(ex, BorderUnavailable);
            drawn = false;
        }

        node.AttachNode(this);
        return node;
    }

    private bool LoadBorder(SimpleImageNode node, JournalFramePiece piece)
    {
        try
        {
            node.LoadTexture(GameMetrics.JournalFrame.Texture);

            // Checked against the sheet that actually loaded rather than the one this was authored
            // against: a patch that shrinks or renumbers Journal_Frame.tex must cost the ornament,
            // never a node sampling outside the texture it was cropped from.
            return JournalNodes.Crop(
                node,
                log,
                GameMetrics.JournalFrame.Texture,
                new Vector2(piece.Source.X, piece.Source.Y),
                new Vector2(piece.Source.Width, piece.Source.Height));
        }
        catch (Exception ex)
        {
            log.Warning(ex, BorderUnavailable);
            return false;
        }
    }
}
