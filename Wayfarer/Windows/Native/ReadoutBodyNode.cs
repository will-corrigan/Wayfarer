using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>The guidance readout itself — the arrow and the lines, drawn with the game's own text
/// nodes, fonts and colours.
///
/// <b>This is the only definition of what the readout looks like.</b> It is a plain
/// <c>ResNode</c> rather than an overlay node or a window so that both of the plugin's hosts can
/// contain it: the click-through overlay (<see cref="GuidanceOverlayNode"/>) and the chromeless
/// clickable addon (<see cref="ClickableReadoutAddon"/>). They differ in what the player can do to
/// it, never in what it looks like — there is no second layout pass anywhere to drift from this one.
///
/// <b>Scale is not automatic and this is the one thing about overlays that surprises everyone.</b>
/// KamiToolKit deliberately de-scales overlay addons to raw screen pixels
/// (<c>addon-&gt;SetScale(1.0f / GetGlobalUIScale(), true)</c>, reapplied on every resolution
/// change) so overlay nodes can be positioned in absolute screen coordinates. A 14pt font here
/// renders at 14 raw pixels whether the player's interface size is 100% or 200%. Everything below
/// is therefore multiplied by <c>GetGlobalUIScale() * userScale</c> every frame, by hand — which is
/// also why the plugin's own text-size setting had to stay rather than being deleted as redundant.
/// The clickable host applies the same de-scaling to itself, so both produce identical pixels.
///
/// <b>It must never throw.</b> Its hosts run <see cref="Layout"/> from a per-frame update, so an
/// exception here is an exception sixty times a second inside the game's render path. Each host
/// wraps the call and switches itself off on the first failure.</summary>
internal sealed unsafe class ReadoutBodyNode : ResNode
{
    // Enough for a heading plus the deepest readout the composer can produce (objective, step,
    // distance, two routing lines, zone, and the muted context block — a hunting summary, the
    // ambient objective and up to ReadoutComposer.MaxNearbyUnlockLines unlocks). Pooled rather than
    // allocated per frame: nothing in a per-frame path should allocate.
    private const int MaxLines = 12;

    private const float BaseWidth = 320f;
    private const float BaseHeadingSize = 20f;
    private const float BasePrimarySize = 15f;
    private const float BaseSecondarySize = 13f;
    private const float BaseMutedSize = 12f;

    /// <summary>The arrow's side, before scale. Sized against the primary line rather than against
    /// the readout: it sits <b>in line</b> with the text now, as a left gutter, and an arrow that
    /// towers over the words it belongs to reads as a separate object rather than as part of
    /// one.</summary>
    private const float BaseArrow = 22f;

    private const float BaseGap = 3f;

    /// <summary>How far the readout has to change size before the move handle is rebuilt around it.
    /// KamiToolKit sizes the handle once, when move mode is switched on, so a readout that grows a
    /// line would otherwise be dragged by a box that no longer fits it.</summary>
    private const float MoveHandleResizeThreshold = 8f;

    private readonly IPluginLog log;
    private readonly ITextureProvider textures;
    private readonly TextNode[] lineNodes = new TextNode[MaxLines];
    private readonly HorizontalLineNode[] ruleNodes = new HorizontalLineNode[MaxLines];

    /// <summary>The direction arrow. An <c>ImGuiImageNode</c> rather than a texture-sheet crop
    /// because the arrow is <b>generated</b> — see <see cref="ArrowBitmap"/> for what was wrong with
    /// cropping the minimap's sheet, which is the defect this replaces. The node owns and disposes
    /// the texture wrap it is given.</summary>
    private readonly ImGuiImageNode arrowNode;

    private readonly TextNode arrowWordsNode;
    private readonly string[] lastText = new string[MaxLines];

    /// <summary>Called when the player finishes dragging the readout, with the offset they dragged
    /// it by, in the host's own coordinates. Null in a host that cannot be dragged.</summary>
    private readonly Action<Vector2>? onMoved;

    /// <summary>An invisible click target parked over whichever line is the teleport advice, or
    /// null in a host that cannot be clicked. A <c>ResNode</c> draws nothing of its own, so the
    /// readout looks byte-for-byte the same with or without it — the only difference is a collision
    /// rectangle and the hand cursor over that one line.</summary>
    private readonly ResNode? teleportHitBox;

    private ArrowIconVariant? loadedVariant;
    private bool arrowFailed;
    private ArrowHiddenReason lastReported = ArrowHiddenReason.None;
    private bool reportedOnce;
    private string? lastBearingWords;
    private bool movable;
    private Vector2 movableSize;

    public ReadoutBodyNode(
        IPluginLog log,
        ITextureProvider textures,
        Action? onTeleportClicked = null,
        Action<Vector2>? onMoved = null)
    {
        this.log = log;
        this.textures = textures;
        this.onMoved = onMoved;

        // The game's own direction indicator is a plain image node whose rotation is written every
        // frame (AtkImageNode PlayerCone / PlayerConeRotation on the minimap), so this copies the
        // mechanism rather than inventing one. The origin has to be the arrow's centre or it pivots
        // around its corner.
        //
        // FitTexture — AutoFit plus Stretch — means "fit the whole loaded TEXTURE into this node".
        // That is now exactly right, and it is worth saying why it was catastrophic before: with a
        // 448x212 texture sheet loaded and a 24x24 part selected, the part was ignored and the whole
        // sheet was drawn squashed into 34 pixels. The arrow's texture is generated and contains
        // nothing but the arrow, so there is no part to ignore.
        arrowNode = new ImGuiImageNode
        {
            TextureSize = new Vector2(ArrowBitmap.Size, ArrowBitmap.Size),
            Size = new Vector2(BaseArrow, BaseArrow),
            OriginX = BaseArrow / 2f,
            OriginY = BaseArrow / 2f,
            FitTexture = true,
            IsVisible = false,
        };
        arrowNode.AttachNode(this);

        // The direction in words, for when the arrow cannot be drawn at all. A readout that says
        // "behind you, to the left" is still guidance; an arrow that silently fails is not.
        arrowWordsNode = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = (uint)BasePrimarySize,
            AlignmentType = AlignmentType.Center,
            TextFlags = TextFlags.Edge,
            TextColor = GameColors.Heading,
            TextOutlineColor = GameColors.HeadingEdge,
            IsVisible = false,
        };
        arrowWordsNode.AttachNode(this);

        BuildLinePool();

        if (onTeleportClicked is not null)
        {
            teleportHitBox = new ResNode { IsVisible = false };
            teleportHitBox.AddEvent(AtkEventType.MouseClick, onTeleportClicked);
            teleportHitBox.ShowClickableCursor = true;
            teleportHitBox.AttachNode(this);
        }
    }

    /// <summary>Whether a clickable target is on screen right now. The host watches this: the
    /// game only dispatches mouse events to nodes in its addon's collision list, and that list has
    /// to be rebuilt when the set of live collision nodes changes — which here means when the
    /// teleport advice appears or goes away, not only when the readout resizes.</summary>
    public bool HasLiveClickTarget { get; private set; }

    /// <summary>Lays the whole readout out for this frame and returns the size it needs, in the
    /// host's own units. The host positions and sizes itself from that; nothing else about the
    /// readout is the host's business.
    ///
    /// <para><b>The shape.</b> A heading on its own line, then a single unit: the arrow in a left
    /// gutter, vertically centred against the first line of the block, and every other line hard
    /// against one left edge just to its right.
    /// <code>
    /// Hunting Log - Warrior
    ///  &gt;   Highland Goobbue
    ///      1/3 killed
    ///      56 yalms
    /// </code>
    /// No panel and no border: what the player likes about this readout is that it looks like the
    /// game's own quest tracker, and a frame around it would undo exactly that.</para>
    ///
    /// <para><b>The gutter is always reserved</b>, whether or not there is an arrow to put in it. It
    /// costs a couple of dozen pixels of empty space when there is nothing to point at, and it buys
    /// a left edge that never moves — which is worth more, because the alternative is a readout that
    /// shuffles sideways every time an objective gains or loses coordinates.</para></summary>
    public Vector2 Layout(ReadoutFrame frame)
    {
        var factor = AtkUnitBase.GetGlobalUIScale() * Math.Clamp(frame.Scale, 0.5f, 3f);
        var width = BaseWidth * factor;
        var arrowSize = BaseArrow * factor * Math.Clamp(frame.ArrowScale, 0.5f, 2f);
        var gutter = arrowSize + (BaseGap * factor * 2f);

        var drawable = frame.ArrowRadians is not null && EnsureArrowTexture(frame.ArrowIcon);
        var y = LayoutWords(frame, drawable, factor, width);
        var (bottom, firstLineCentre) = LayoutLines(frame, factor, width, gutter, y);
        LayoutArrow(frame, drawable, arrowSize, gutter, firstLineCentre);

        var size = new Vector2(width, bottom);
        Size = size;
        ApplyMoveMode(frame.MoveMode, size);
        return size;
    }

    /// <summary>Takes the move handle down, and with it the viewport-level mouse listener behind it.
    ///
    /// <b>This has to be called before the node is disposed.</b> <c>NodeBase.Dispose()</c> disposes
    /// children, clears focus and detaches — but it never calls <c>DisableEditMode</c>, so the
    /// <c>ViewportEventListener</c> that drives dragging is registered against the game's global
    /// viewport and belongs to a plugin that has gone. That is the exact shape of the unload crash
    /// this plugin has already shipped once.</summary>
    public void StopMoving()
    {
        if (!movable)
        {
            return;
        }

        movable = false;
        OnMoveComplete = null;
        EnableMoving = false;
    }

    /// <summary>Hides every child. Used when there is nothing to say — the readout disappears
    /// rather than drawing a frame around emptiness.</summary>
    public void HideAll()
    {
        arrowNode.IsVisible = false;
        arrowWordsNode.IsVisible = false;
        if (teleportHitBox is not null)
        {
            teleportHitBox.IsVisible = false;
        }

        HasLiveClickTarget = false;

        for (var i = 0; i < MaxLines; i++)
        {
            lineNodes[i].IsVisible = false;
            ruleNodes[i].IsVisible = false;
        }
    }

    private static float FontSizeFor(ReadoutEmphasis emphasis) => emphasis switch
    {
        ReadoutEmphasis.Heading => BaseHeadingSize,
        ReadoutEmphasis.Primary => BasePrimarySize,
        ReadoutEmphasis.Secondary => BaseSecondarySize,
        _ => BaseMutedSize,
    };

    private static Vector4 ColorFor(ReadoutEmphasis emphasis) => emphasis switch
    {
        ReadoutEmphasis.Heading => GameColors.Heading,
        ReadoutEmphasis.Primary => GameColors.Body,
        ReadoutEmphasis.Secondary => GameColors.ListText,
        _ => GameColors.Dimmed,
    };

    private static Vector4 OutlineFor(ReadoutEmphasis emphasis) =>
        emphasis == ReadoutEmphasis.Heading ? GameColors.HeadingEdge : GameColors.BodyEdge;

    private static FontType FontFor(ReadoutEmphasis emphasis) =>
        emphasis == ReadoutEmphasis.Heading ? FontType.TrumpGothic : FontType.Axis;

    private void BuildLinePool()
    {
        for (var i = 0; i < MaxLines; i++)
        {
            lastText[i] = string.Empty;

            ruleNodes[i] = new HorizontalLineNode { IsVisible = false };
            ruleNodes[i].AttachNode(this);

            lineNodes[i] = new TextNode
            {
                FontType = FontType.Axis,
                FontSize = (uint)BaseSecondarySize,
                AlignmentType = AlignmentType.TopLeft,

                // Edge is not decoration over the 3D world — without an outline the text vanishes
                // against bright terrain. WordWrap plus MultiLine is how the game's own journal and
                // tooltips grow downward instead of truncating, which is the fix for the widget's
                // "half the text is cut off" complaint.
                TextFlags = TextFlags.Edge | TextFlags.WordWrap | TextFlags.MultiLine,
                TextColor = GameColors.Body,
                TextOutlineColor = GameColors.BodyEdge,
                IsVisible = false,
            };
            lineNodes[i].AttachNode(this);
        }
    }

    /// <summary>Turns the game's own HUD-Layout move handle on and off around the readout.
    ///
    /// <para>KamiToolKit's <c>EnableMoving</c> builds the handle once, at the size the node had at
    /// that moment, and registers a viewport-level mouse listener that marks clicks inside it as
    /// handled. Both facts shape what happens here: the handle has to be rebuilt when the readout
    /// changes size, and the whole thing has to be off unless the player asked for it, or the
    /// readout would silently eat world clicks and camera drags underneath itself.</para></summary>
    private void ApplyMoveMode(bool wanted, Vector2 size)
    {
        if (onMoved is null)
        {
            return;
        }

        if (!wanted)
        {
            StopMoving();
            return;
        }

        var resize = movable && Vector2.Distance(size, movableSize) > MoveHandleResizeThreshold;
        if (movable && !resize)
        {
            return;
        }

        if (resize)
        {
            EnableMoving = false;
        }

        movable = true;
        movableSize = size;
        OnMoveComplete = _ =>
        {
            onMoved(Position);
            Position = Vector2.Zero;
        };
        EnableMoving = true;
    }

    /// <summary>Puts the arrow in the gutter, level with the middle of the first line of the block.
    /// It takes no vertical space of its own — that is the whole point of the inline layout — so
    /// this runs after the lines have been placed and simply parks it beside them.</summary>
    private void LayoutArrow(ReadoutFrame frame, bool drawable, float size, float gutter, float? lineCentre)
    {
        if (frame.ArrowRadians is not { } radians)
        {
            arrowNode.IsVisible = false;
            ReportArrow(frame.ArrowHidden);
            return;
        }

        if (!drawable)
        {
            arrowNode.IsVisible = false;
            ReportArrow(ArrowHiddenReason.TextureUnavailable);
            return;
        }

        // The arrow-size setting has to be applied here as well as the text-size one: before this
        // it moved nothing at all on the readout that is actually on screen, because only the ImGui
        // fallback ever read it.
        arrowNode.Size = new Vector2(size, size);
        arrowNode.OriginX = size / 2f;
        arrowNode.OriginY = size / 2f;
        arrowNode.Position = new Vector2(
            (gutter - size) / 2f,
            (lineCentre ?? (size / 2f)) - (size / 2f));
        arrowNode.Rotation = radians;
        arrowNode.IsVisible = true;
        ReportArrow(ArrowHiddenReason.None);
        ReportBearing(radians);
    }

    /// <summary>The words fallback, on its own full-width line above the block. Only ever on screen
    /// when the arrow could not be generated at all — which, since the arrow is now computed rather
    /// than loaded, means something has gone genuinely wrong rather than merely slowly. It keeps its
    /// old full-width line rather than trying to fit "behind you, to the left" into a 25-pixel
    /// gutter.</summary>
    private float LayoutWords(ReadoutFrame frame, bool drawable, float factor, float width)
    {
        if (drawable || frame.ArrowRadians is not { } radians)
        {
            arrowWordsNode.IsVisible = false;
            return 0f;
        }

        var height = (BasePrimarySize + 2f) * factor;
        arrowWordsNode.FontSize = (uint)Math.Max(BasePrimarySize * factor, 8f);
        arrowWordsNode.LineSpacing = (uint)Math.Max((BasePrimarySize * factor) + 2f, 10f);
        arrowWordsNode.String = NavMath.DescribeDirection(radians);
        arrowWordsNode.Size = new Vector2(width, height);
        arrowWordsNode.Position = new Vector2(0f, 0f);
        arrowWordsNode.IsVisible = true;
        return height + (BaseGap * factor);
    }

    /// <summary>Lays out every line and reports how tall the readout ended up, plus the vertical
    /// centre of the first line of the block — which is what the arrow is aligned against.
    ///
    /// <para>Headings run the full width from the left edge; everything else is indented past the
    /// arrow gutter, so the block has one left edge and reads as a single object hanging off the
    /// arrow.</para></summary>
    private (float Bottom, float? FirstLineCentre) LayoutLines(
        ReadoutFrame frame, float factor, float width, float gutter, float top)
    {
        var y = top;
        var count = Math.Min(frame.Content.Lines.Count, MaxLines);
        var hitBoxPlaced = false;
        float? firstLineCentre = null;

        for (var i = 0; i < count; i++)
        {
            var line = frame.Content.Lines[i];
            var heading = line.Emphasis == ReadoutEmphasis.Heading;
            var left = heading ? 0f : gutter;
            var lineWidth = width - left;
            var fontSize = FontSizeFor(line.Emphasis) * factor;
            y = LayoutRule(i, line, factor, left, lineWidth, y);

            // Two lines' worth of height so a wrapped line has somewhere to go. WordWrap needs a
            // fixed width and grows downward into whatever height the node has; the advance below is
            // the single-line height, so an unwrapped line does not leave a blank one under it.
            var height = (fontSize + 2f) * 2f;
            LayoutLine(i, line, fontSize, left, lineWidth, height, y);

            var advance = height * 0.6f;
            firstLineCentre ??= heading ? null : y + (advance / 2f);

            hitBoxPlaced |= TryPlaceHitBox(frame, line, hitBoxPlaced, left, lineWidth, height, y);
            y += advance;
        }

        for (var i = count; i < MaxLines; i++)
        {
            lineNodes[i].IsVisible = false;
            ruleNodes[i].IsVisible = false;
        }

        if (!hitBoxPlaced && teleportHitBox is not null)
        {
            teleportHitBox.IsVisible = false;
        }

        HasLiveClickTarget = hitBoxPlaced;
        return (y + (BaseGap * factor), firstLineCentre);
    }

    private float LayoutRule(int index, ReadoutLine line, float factor, float left, float width, float y)
    {
        if (!line.Separated)
        {
            ruleNodes[index].IsVisible = false;
            return y;
        }

        y += BaseGap * factor * 2f;
        ruleNodes[index].Size = new Vector2(width, 4f);
        ruleNodes[index].Position = new Vector2(left, y);
        ruleNodes[index].IsVisible = true;
        return y + (BaseGap * factor) + 4f;
    }

    private void LayoutLine(
        int index, ReadoutLine line, float fontSize, float left, float width, float height, float y)
    {
        var node = lineNodes[index];
        node.FontType = FontFor(line.Emphasis);
        node.FontSize = (uint)Math.Max(fontSize, 8f);
        node.LineSpacing = (uint)Math.Max(fontSize + 2f, 10f);
        node.TextColor = ColorFor(line.Emphasis);
        node.TextOutlineColor = OutlineFor(line.Emphasis);

        // Assigning String builds a SeString; only do it when the words actually changed.
        if (!string.Equals(lastText[index], line.Text, StringComparison.Ordinal))
        {
            lastText[index] = line.Text;
            node.String = line.Text;
        }

        node.Size = new Vector2(width, height);
        node.Position = new Vector2(left, y);
        node.IsVisible = true;
    }

    /// <summary>Parks the invisible click target over the teleport line, if this host has one and
    /// this frame is actually offering the click. First match only: there is never more than one
    /// teleport advice, and a second hit box would be a click target over the wrong words.
    ///
    /// <para>The frame's own offer is what decides, not the line's action mark. With
    /// click-to-teleport turned off the composer still marks the line — the mark describes the line,
    /// not the surface — and placing a hit box on it gave the player a hand cursor over words that
    /// would then politely refuse to do anything.</para></summary>
    private bool TryPlaceHitBox(
        ReadoutFrame frame, ReadoutLine line, bool alreadyPlaced, float left, float width, float height, float y)
    {
        if (alreadyPlaced || teleportHitBox is null || !frame.ClickableTeleport || line.Action != ReadoutLineAction.Teleport)
        {
            return false;
        }

        teleportHitBox.Size = new Vector2(width, height * 0.6f);
        teleportHitBox.Position = new Vector2(left, y);
        teleportHitBox.IsVisible = true;
        return true;
    }

    /// <summary>Generates the arrow for the chosen colour and hands it to the image node, if it is
    /// not already loaded. Reloading on a variant change is what makes the setting apply live.
    ///
    /// <para>Unlike a texture read out of the game's resource system, this one cannot be merely
    /// <i>late</i> — the pixels are computed here and uploaded synchronously — so there is no retry
    /// loop and no "not ready yet" state. It either works or it throws, and if it throws the readout
    /// falls back to saying the direction in words and never tries again this session.</para></summary>
    private bool EnsureArrowTexture(ArrowIconVariant variant)
    {
        if (arrowFailed)
        {
            return false;
        }

        if (loadedVariant == variant)
        {
            return true;
        }

        try
        {
            var pixels = ArrowBitmap.Render(variant);
            var wrap = textures.CreateFromRaw(
                RawImageSpecification.Rgba32(ArrowBitmap.Size, ArrowBitmap.Size),
                pixels,
                $"Wayfarer arrow ({variant})");

            // Takes ownership: the node disposes the previous wrap and this one with itself.
            arrowNode.LoadTexture(wrap);
            arrowNode.TextureSize = new Vector2(ArrowBitmap.Size, ArrowBitmap.Size);
            loadedVariant = variant;

            if (arrowNode.ActualTextureSize == Vector2.Zero)
            {
                log.Warning(
                    "Wayfarer readout: the generated direction arrow reports no texture size after being "
                    + "uploaded. It is still being drawn; if there is a blank space where the arrow should "
                    + "be, this line is why.");
            }

            return true;
        }
        catch (Exception ex)
        {
            arrowFailed = true;
            log.Error(ex, "Wayfarer readout: the direction arrow could not be generated — showing the direction in words instead.");
            return false;
        }
    }

    /// <summary>Logs why there is (or is no longer) an arrow, once per change of reason. Deliberately
    /// not rate-limited by time: the interesting event is the transition, and this runs every frame.</summary>
    private void ReportArrow(ArrowHiddenReason reason)
    {
        if (reportedOnce && reason == lastReported)
        {
            return;
        }

        lastReported = reason;
        reportedOnce = true;

        var message = reason switch
        {
            ArrowHiddenReason.None => "Wayfarer readout: the direction arrow is being drawn.",
            ArrowHiddenReason.NotRequested => "Wayfarer readout: no direction arrow — nothing active has a direction to point at.",
            ArrowHiddenReason.NoTargetCoordinates => "Wayfarer readout: no direction arrow — the active objective has no target coordinates.",
            ArrowHiddenReason.NoPlayer => "Wayfarer readout: no direction arrow — there is no local player to measure a bearing from.",
            _ => "Wayfarer readout: the direction arrow could not be generated — showing the direction in words instead.",
        };

        if (reason == ArrowHiddenReason.TextureUnavailable)
        {
            log.Warning(message);
            return;
        }

        log.Debug(message);
    }

    /// <summary>Writes down what the arrow is currently claiming, so the one thing about it that
    /// cannot be settled by reading the code can be settled by looking at the screen once.
    ///
    /// <para><c>NavMath.ArrowAngle</c> is defined as "0 = straight up", and the words fallback is
    /// built from the same number. The arrow art is now generated by <see cref="ArrowBitmap"/>,
    /// which draws it pointing straight up and centred in its own image, so the rest orientation is
    /// a property of this codebase rather than an assumption about a game asset — there is no offset
    /// to get wrong. This line stays anyway, because "the arrow and the words disagree" is still the
    /// cheapest possible check that the bearing itself is right.</para></summary>
    private void ReportBearing(float radians)
    {
        var words = NavMath.DescribeDirection(radians);
        if (string.Equals(words, lastBearingWords, StringComparison.Ordinal))
        {
            return;
        }

        lastBearingWords = words;
        var degrees = radians * 180f / MathF.PI;
        log.Debug(
            $"Wayfarer readout: arrow rotation {degrees:F0}° = {words}. The arrow is drawn pointing straight " +
            "up unrotated; if the arrow on screen and these words disagree, the bearing is what is wrong.");
    }
}
