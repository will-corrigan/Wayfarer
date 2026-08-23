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

    /// <summary>The most rows one line is allowed to wrap into.</summary>
    private const float MaxWrappedLines = 3f;

    /// <summary>The readout box, at the width the game gives its own always-on overlay — the quest
    /// tracker is 400 wide (ToDoList root <c>#1</c>) and every line in it 372.</summary>
    private const float BaseWidth = GameMetrics.Hud.Width;

    /// <summary>The heading. The tracker's own emphasis size is Axis 18 (ToDoList <c>1005 #5</c>,
    /// <c>1012 #3</c>); 20 was a window-title size on a heads-up element.</summary>
    private const float BaseHeadingSize = GameMetrics.Type.DetailTitleSize;

    /// <summary>The tracker sets its quest titles and its objective lines in the same Axis 14 and
    /// separates them by colour, not by size (ToDoList <c>1004</c>, <c>1005</c>, <c>1007</c>). So do
    /// these two.</summary>
    private const float BasePrimarySize = GameMetrics.Hud.LineSize;

    /// <inheritdoc cref="BasePrimarySize"/>
    private const float BaseSecondarySize = GameMetrics.Hud.LineSize;

    /// <summary>Counts and qualifiers. ToDoList <c>1008 #7</c> is Axis 12.</summary>
    private const float BaseMutedSize = GameMetrics.Hud.MetaSize;

    /// <summary>The arrow's side, before scale.
    ///
    /// <para>The size of the quest tracker's own markers — ToDoList <c>1003 #4</c>, <c>1008 #5</c>
    /// and <c>1012 #4</c> are all 24x24 — sitting in the same 28-wide gutter the tracker reserves for
    /// them. It sits <b>in line</b> with the text, as a left gutter, rather than towering over the
    /// words it belongs to.</para></summary>
    private const float BaseArrow = GameMetrics.Hud.IconSize;

    /// <summary>The readout's spacing unit. Half of what the tracker leaves either side of its icon
    /// column: its gutter is 28 for a 24-wide marker.</summary>
    private const float BaseGap = (GameMetrics.Hud.Gutter - GameMetrics.Hud.IconSize) / 2f;

    /// <summary>Where the arrow's centre sits on its line, as a fraction of that line's font size
    /// measured down from the text's top edge.
    ///
    /// <para>Not half the line box. Axis draws with its cap height in the upper part of the em, so
    /// the <i>optical</i> centre of a line of text is above its geometric centre; aligning to the
    /// box put the arrow a couple of pixels low against every line it sat beside. 0.58 of the font
    /// size is the cap-height centre, which is what the eye actually aligns to.</para></summary>
    private const float ArrowOpticalCentre = 0.58f;

    /// <summary>The settings cog's side, before scale. Sized against the heading it sits beside
    /// rather than against the readout: it is a mark on that line, not a button on a panel.</summary>
    private const float BaseCog = 13f;

    /// <summary>How visible the cog is when the pointer is not on it.
    ///
    /// <para><b>Why it is not simply hidden.</b> Revealing it on hover would be better — the player
    /// asked for exactly that — but the only thing that can tell this readout the pointer is over it
    /// is a collision rectangle, and a collision rectangle over the whole readout would swallow the
    /// world clicks and camera drags underneath it. That is the same trap the drag handle is a
    /// deliberate mode for. So the cog carries its own small collision and nothing else does: it is
    /// barely there until the pointer finds it, and it eats nothing but its own thirteen
    /// pixels.</para></summary>
    private const float CogIdleAlpha = 0.4f;

    /// <summary>Bits of <see cref="ClickTargets"/> — one per clickable node the readout can put on
    /// screen.</summary>
    private const int TeleportTarget = 1;

    /// <inheritdoc cref="TeleportTarget"/>
    private const int CogTarget = 2;

    /// <inheritdoc cref="TeleportTarget"/>
    private const int FollowTarget = 4;

    /// <summary>How far the readout has to change size before the move handle is rebuilt around it.
    /// KamiToolKit sizes the handle once, when move mode is switched on, so a readout that grows a
    /// line would otherwise be dragged by a box that no longer fits it.</summary>
    private const float MoveHandleResizeThreshold = 8f;

    private readonly IPluginLog log;
    private readonly ITextureProvider textures;

    /// <summary>Whether the per-change readout diagnostics should be written. Off by default —
    /// see <see cref="QuestHelperConfig.LogDiagnostics"/> for why.</summary>
    private readonly Func<bool> diagnosticsEnabled;
    private readonly TextNode[] lineNodes = new TextNode[MaxLines];
    private readonly HorizontalLineNode[] ruleNodes = new HorizontalLineNode[MaxLines];

    /// <summary>The direction arrow. An <c>ImGuiImageNode</c> rather than a texture-sheet crop
    /// because the arrow is <b>generated</b> — see <see cref="ArrowBitmap"/> for what was wrong with
    /// cropping the minimap's sheet, which is the defect this replaces. The node owns and disposes
    /// the texture wrap it is given.</summary>
    private readonly ImGuiImageNode arrowNode;

    /// <summary>The up/down mark that hangs off the arrow when the target is on a different level of
    /// the world. The game's own minimap does exactly this to a marker on another floor, and copying
    /// the placement convention is the point — a player already reads a badge on a marker without
    /// being told.
    ///
    /// <para><b>It is deliberately not the arrow's art.</b> It used to be: the same generated
    /// arrowhead, half size, turned through half a turn for "below". Direction and elevation are two
    /// different meanings and they were being drawn as the same shape, which the player read as a
    /// second heading to travel in. It is a stacked double chevron now
    /// (<see cref="ChevronBitmap"/>) — line-work where the arrow is a solid mass, so the two
    /// silhouettes have nothing in common at any size or in any colour variant.</para></summary>
    private readonly ImGuiImageNode elevationNode;

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

    /// <summary>The settings cog, or null in a host that cannot be clicked. Drawing one on the
    /// click-through overlay would be a lie: the controller's readout takes no input at all, and an
    /// affordance that does nothing is worse than none.</summary>
    private readonly ImGuiImageNode? cogNode;

    /// <summary>The "choose what to follow" caret, sitting beside the cog rather than on it, or null
    /// in a host that cannot be clicked.
    ///
    /// <para>Two things it is not. It is not a menu hanging off the cog — the cog opens settings and
    /// this opens a list, and hiding a second meaning behind one mark is how the info bar's
    /// right-click ended up being described as unintuitive. And it is not the list itself: the
    /// readout owns exactly one objective and never carries choices, which is the rule that keeps it
    /// glanceable and click-through-able. It is the door to the list, which lives in the
    /// window.</para>
    ///
    /// <para>Mouse only, for the same reason as the cog. A controller reaches the same list through
    /// the window's Following tab and through the Wayfarer entry in the game's own right-click
    /// menu, both of which take no cursor.</para></summary>
    private readonly ImGuiImageNode? followNode;

    private ArrowIconVariant? loadedVariant;
    private ArrowIconVariant? loadedElevationVariant;
    private bool arrowFailed;
    private bool elevationFailed;
    private bool cogLoaded;
    private bool cogFailed;
    private bool followLoaded;
    private bool followFailed;
    private ArrowHiddenReason lastReported = ArrowHiddenReason.None;
    private bool reportedOnce;
    private bool warnedTextureOnce;
    private string? lastBearingWords;
    private bool movable;
    private Vector2 movableSize;

    public ReadoutBodyNode(
        IPluginLog log,
        ITextureProvider textures,
        Func<bool>? diagnosticsEnabled = null,
        Action? onTeleportClicked = null,
        Action<Vector2>? onMoved = null,
        Action? onSettingsClicked = null,
        Action? onFollowClicked = null)
    {
        this.log = log;
        this.textures = textures;
        this.diagnosticsEnabled = diagnosticsEnabled ?? (static () => false);
        this.onMoved = onMoved;

        arrowNode = BuildArrow(BaseArrow);
        elevationNode = BuildArrow(BaseArrow / 2f);

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

        cogNode = onSettingsClicked is null ? null : BuildCog(onSettingsClicked);
        followNode = onFollowClicked is null ? null : BuildFollowCaret(onFollowClicked);
    }

    /// <summary>Which clickable targets are on screen right now, as a bit per target — the teleport
    /// advice and the settings cog.
    ///
    /// <para>The host watches this: the game only dispatches mouse events to nodes in its addon's
    /// collision list, and that list has to be rebuilt when the <b>set</b> of live collision nodes
    /// changes. A bool was enough while there was one of them; with two, "something is clickable"
    /// stays true across the teleport line appearing under a cog that was already there, and the
    /// list would never be rebuilt for it — a hit box that is never hit.</para></summary>
    public int ClickTargets { get; private set; }

    /// <summary>Lays the whole readout out for this frame and returns the size it needs, in the
    /// host's own units. The host positions and sizes itself from that; nothing else about the
    /// readout is the host's business.
    ///
    /// <para><b>The shape.</b> One left edge for everything, with the arrow in a gutter to the left
    /// of it, vertically centred on the first line of the block.
    /// <code>
    ///      Hunting Log - Warrior
    ///  &gt;   Highland Goobbue
    ///      1/3 killed
    ///      56 yalms
    /// </code>
    /// No panel and no border: what the player likes about this readout is that it looks like the
    /// game's own quest tracker, and a frame around it would undo exactly that.</para>
    ///
    /// <para><b>The gutter is reserved, permanently, and the heading now shares the indent.</b>
    /// The gutter was already a fixed width — it never depended on whether an arrow was in it — but
    /// the heading sat flush left while everything under it was indented past the arrow. With an
    /// arrow present that reads as the block hanging off the arrow; with no arrow it reads as the
    /// body having slid sideways for no reason, which is what "especially when it's not present
    /// everything looks shifted" describes. Indenting the heading too gives the whole readout one
    /// left edge that cannot move, in either state. The gutter is derived from the arrow size, which
    /// is derived from the type size, so it scales with the text at every interface and text
    /// scale.</para></summary>
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
        LayoutElevation(frame, arrowSize);
        LayoutHeadingControls(frame, factor, width, gutter);

        // The cog and the follow caret are live collision nodes whenever they are drawn, and the
        // clickable host watches this to know when the addon's collision list has to be rebuilt.
        if (cogNode is { IsVisible: true })
        {
            ClickTargets |= CogTarget;
        }

        if (followNode is { IsVisible: true })
        {
            ClickTargets |= FollowTarget;
        }

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
        elevationNode.IsVisible = false;
        arrowWordsNode.IsVisible = false;
        if (teleportHitBox is not null)
        {
            teleportHitBox.IsVisible = false;
        }

        if (cogNode is not null)
        {
            cogNode.IsVisible = false;
        }

        if (followNode is not null)
        {
            followNode.IsVisible = false;
        }

        ClickTargets = 0;

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

    /// <summary>Puts one heading-line control at <paramref name="x"/> and reports where the next one
    /// starts. A control whose art could not be generated is hidden and consumes no space, so the
    /// row closes up rather than leaving a gap where it would have been.</summary>
    /// <summary>How many rows this line's text will occupy at the width it has been given.
    ///
    /// <para><c>GetTextDrawSize</c> is the engine's own measurement and answers immediately — it is
    /// not a read of something that has to have been drawn first. Both of its axes are consulted
    /// because a node that is already wrapping reports a wrapped height while one that is not
    /// reports its full unwrapped width, and taking the larger estimate is right in either case. A
    /// measurement that comes back as nothing falls back to one row, which is the old behaviour and
    /// is never worse than it.</para></summary>
    private static float WrappedLines(TextNode node, float width)
    {
        if (width <= 1f)
        {
            return 1f;
        }

        var drawn = node.GetTextDrawSize();
        if (drawn.X <= 0f && drawn.Y <= 0f)
        {
            return 1f;
        }

        var byWidth = MathF.Ceiling(drawn.X / width);
        var byHeight = MathF.Ceiling(drawn.Y / Math.Max(node.LineSpacing, 1f));

        // Capped: a line that wants five rows is a content problem, and letting it push the rest of
        // the readout off the bottom of its slot would be trading a clipped line for a lost one.
        return Math.Clamp(Math.Max(byWidth, byHeight), 1f, MaxWrappedLines);
    }

    private static float PlaceHeadingControl(
        ImGuiImageNode? node, Func<bool> ensureTexture, float size, float gap, float x, float top, float width)
    {
        if (node is null)
        {
            return x;
        }

        if (!ensureTexture())
        {
            node.IsVisible = false;
            return x;
        }

        node.Size = new Vector2(size, size);

        // Origin follows the size for every control here, not only the rotated one: a node whose
        // origin is stale pivots around a point that is no longer its centre, and the caret is
        // drawn at half a turn.
        node.OriginX = size / 2f;
        node.OriginY = size / 2f;
        node.Position = new Vector2(Math.Clamp(x, 0f, width - size), top);
        node.IsVisible = true;
        return x + size + gap;
    }

    private static FontType FontFor(ReadoutEmphasis emphasis) =>
        emphasis == ReadoutEmphasis.Heading ? FontType.TrumpGothic : FontType.Axis;

    /// <summary>An image node that holds one of the generated arrow textures — the bearing arrow, or
    /// the smaller up/down chevron beside it.
    ///
    /// <para>The game's own direction indicator is a plain image node whose rotation is written every
    /// frame (<c>AtkImageNode PlayerCone / PlayerConeRotation</c> on the minimap), so this copies the
    /// mechanism rather than inventing one. The origin has to be the arrow's centre or it pivots
    /// around its corner.</para>
    ///
    /// <para><c>FitTexture</c> — AutoFit plus Stretch — means "fit the whole loaded TEXTURE into this
    /// node". That is now exactly right, and it is worth saying why it was catastrophic before: with
    /// a 448x212 texture sheet loaded and a 24x24 part selected, the part was ignored and the whole
    /// sheet was drawn squashed into 34 pixels. The arrow's texture is generated and contains nothing
    /// but the arrow, so there is no part to ignore.</para></summary>
    private ImGuiImageNode BuildArrow(float side)
    {
        var node = new ImGuiImageNode
        {
            TextureSize = new Vector2(ArrowBitmap.Size, ArrowBitmap.Size),
            Size = new Vector2(side, side),
            OriginX = side / 2f,
            OriginY = side / 2f,
            FitTexture = true,
            IsVisible = false,
        };
        node.AttachNode(this);
        return node;
    }

    /// <summary>The settings cog, wired to open the window on its Settings tab.
    ///
    /// <para><c>MouseClick</c> is the only one of these events that blocks: <c>MouseOver</c> and
    /// <c>MouseOut</c> ask the toolkit for <c>EmitsEvents</c> and <c>RespondToMouse</c> alone, while
    /// <c>MouseClick</c> adds <c>HasCollision</c>. The rectangle that swallows a world click is
    /// therefore exactly the cog and nothing more — which is the whole reason the readout can carry
    /// one at all.</para></summary>
    private ImGuiImageNode BuildCog(Action onSettingsClicked)
    {
        // Same generated-texture treatment as the arrow — see CogBitmap. FitTexture is correct here
        // for the same reason it is correct there: the whole texture IS the icon, so there is no
        // part for AutoFit to ignore.
        var cog = new ImGuiImageNode
        {
            TextureSize = new Vector2(CogBitmap.Size, CogBitmap.Size),
            Size = new Vector2(BaseCog, BaseCog),
            FitTexture = true,
            IsVisible = false,
            Alpha = CogIdleAlpha,
        };

        cog.AddEvent(AtkEventType.MouseClick, onSettingsClicked);
        cog.AddEvent(AtkEventType.MouseOver, () => cog.Alpha = 1f);
        cog.AddEvent(AtkEventType.MouseOut, () => cog.Alpha = CogIdleAlpha);
        cog.ShowClickableCursor = true;
        cog.AttachNode(this);
        return cog;
    }

    /// <summary>The "choose what to follow" caret, beside the cog, wired to open the window on its
    /// Following tab — where every followable thing is one list of rows: the main scenario, each
    /// accepted quest, an unlock route, a hunt. The list is not duplicated here. The readout owns
    /// exactly one objective and never carries choices; that rule is what keeps it glanceable and
    /// is why the click-through host can exist at all.
    ///
    /// <para>Same collision treatment as the cog, and for the same reason: <c>MouseClick</c> is the
    /// only one of these events that adds <c>HasCollision</c>, so the rectangle that swallows a
    /// world click is exactly the caret and nothing more.</para></summary>
    private ImGuiImageNode BuildFollowCaret(Action onFollowClicked)
    {
        var caret = new ImGuiImageNode
        {
            TextureSize = new Vector2(ChevronBitmap.Size, ChevronBitmap.Size),
            Size = new Vector2(BaseCog, BaseCog),
            FitTexture = true,
            IsVisible = false,
            Alpha = CogIdleAlpha,

            // The art points up; a "there is a list behind this" caret points down.
            Rotation = MathF.PI,
            OriginX = BaseCog / 2f,
            OriginY = BaseCog / 2f,
        };

        caret.AddEvent(AtkEventType.MouseClick, onFollowClicked);
        caret.AddEvent(AtkEventType.MouseOver, () => caret.Alpha = 1f);
        caret.AddEvent(AtkEventType.MouseOut, () => caret.Alpha = CogIdleAlpha);
        caret.ShowClickableCursor = true;
        caret.AttachNode(this);
        return caret;
    }

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

        // The tracker's own leading, two over the font size.
        const float Leading = GameMetrics.Hud.LineLeading - GameMetrics.Hud.LineSize;
        var height = (BasePrimarySize + Leading) * factor;
        arrowWordsNode.FontSize = (uint)Math.Max(BasePrimarySize * factor, 8f);
        arrowWordsNode.LineSpacing = (uint)Math.Max((BasePrimarySize * factor) + Leading, 10f);
        arrowWordsNode.String = NavMath.DescribeDirection(radians);
        arrowWordsNode.Size = new Vector2(width, height);
        arrowWordsNode.Position = new Vector2(0f, 0f);
        arrowWordsNode.IsVisible = true;
        return height + (BaseGap * factor);
    }

    /// <summary>Lays out every line and reports how tall the readout ended up, plus the vertical
    /// centre of the first line of the block — which is what the arrow is aligned against.
    ///
    /// <para>Every line, heading included, starts at the same left edge past the arrow gutter, so
    /// the readout is one block with one edge and the arrow is a mark beside it rather than
    /// something the body hangs off.</para></summary>
    private (float Bottom, float? FirstLineCentre) LayoutLines(
        ReadoutFrame frame, float factor, float width, float gutter, float top)
    {
        var y = top;
        var count = Math.Min(frame.Content.Lines.Count, MaxLines);
        var hitBoxPlaced = false;
        float? firstLineCentre = null;
        var lineWidth = width - gutter;

        for (var i = 0; i < count; i++)
        {
            var line = frame.Content.Lines[i];
            var heading = line.Emphasis == ReadoutEmphasis.Heading;
            var fontSize = FontSizeFor(line.Emphasis) * factor;
            y = LayoutRule(i, line, factor, gutter, lineWidth, y);

            // The height this line actually needs, measured, and the advance to match it. It used
            // to be two lines' worth of node with a ONE line advance, which is why a long nearby
            // unlock — "Something Or Other (350 yalms)" — appeared cut off: it wrapped correctly
            // into a node that had room for it, and then the next line was drawn on top of the
            // wrapped remainder, 40% of a line further up than the wrap had put it.
            var height = LayoutLine(i, line, fontSize, gutter, lineWidth, y);

            // The arrow aligns to the first line that is not the heading — the objective, which is
            // what it is pointing at — and to that line's optical centre rather than its box.
            firstLineCentre ??= heading ? null : y + (fontSize * ArrowOpticalCentre);

            hitBoxPlaced |= TryPlaceHitBox(frame, line, hitBoxPlaced, gutter, lineWidth, height, y);
            y += height;
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

        ClickTargets = hitBoxPlaced ? TeleportTarget : 0;
        return (y + (BaseGap * factor), firstLineCentre);
    }

    /// <summary>Hangs the up/down chevron off the arrow when the target is on a different level of
    /// the world, which is what the game's own minimap does to a marker on another floor.
    ///
    /// <para>Only ever when there is an arrow to hang it on. Whether to claim anything about
    /// elevation at all was decided long before this — see <c>Elevation</c> and <c>GroundHeight</c>
    /// — and by the time it arrives here it is a fact about the frame. The distance line says it in
    /// words as well, because a chevron is a convention the player has to already know.</para>
    ///
    /// <para>The offset parks it clear of the arrow's lower-right corner rather than overlapping it.
    /// The old art overlapped deliberately, to read as one compound mark — which was the mistake:
    /// two meanings fused into one glyph. A small gap is what says "this is a second, separate
    /// thing about the same target".</para></summary>
    private void LayoutElevation(ReadoutFrame frame, float arrowSize)
    {
        if (!arrowNode.IsVisible
            || frame.Content.Elevation == ElevationHint.Level
            || !EnsureElevationTexture(frame.ArrowIcon))
        {
            elevationNode.IsVisible = false;
            return;
        }

        var size = Math.Max(arrowSize * 0.6f, 8f);
        elevationNode.Size = new Vector2(size, size);
        elevationNode.OriginX = size / 2f;
        elevationNode.OriginY = size / 2f;

        // The art points straight up unrotated, so "above" needs no rotation at all and "below" is
        // half a turn. Same guarantee ArrowBitmap gives the bearing arrow.
        elevationNode.Rotation = frame.Content.Elevation == ElevationHint.Above ? 0f : MathF.PI;
        elevationNode.Position = arrowNode.Position + new Vector2(arrowSize * 0.74f, arrowSize * 0.52f);
        elevationNode.IsVisible = true;
    }

    /// <summary>Parks the settings cog at the end of the heading, which is where the game puts a
    /// panel's own controls and the one place on this readout that is never text.
    ///
    /// <para>Measured rather than pinned to the right-hand edge: the readout's box is a fixed 320
    /// units wide while its heading is usually a third of that, so a cog in the corner would float
    /// in empty space with nothing to belong to. The box is invisible; the words are the object.
    /// A measurement that comes back as nothing — the node has not been drawn yet — falls back to
    /// the right edge rather than stacking the cog on top of the first letter.</para></summary>
    private void LayoutHeadingControls(ReadoutFrame frame, float factor, float width, float gutter)
    {
        if (cogNode is null && followNode is null)
        {
            return;
        }

        var size = Math.Max(BaseCog * factor, 9f);
        var gap = BaseGap * factor * 2f;
        var heading = frame.Content.Lines.Count > 0
            && frame.Content.Lines[0].Emphasis == ReadoutEmphasis.Heading
            && lineNodes[0].IsVisible
                ? lineNodes[0]
                : null;

        // Measured from the end of the heading's words, not pinned to the right-hand edge: the
        // readout's box is a fixed 320 units wide while its heading is usually a third of that, so
        // a control in the corner would float in empty space with nothing to belong to. A
        // measurement that comes back as nothing — the node has not been drawn yet — falls back to
        // the right edge rather than stacking a control on top of the first letter.
        var headingWidth = heading is null ? 0f : heading.GetTextDrawSize().X;
        var top = heading is null
            ? 0f
            : heading.Position.Y + Math.Max(((BaseHeadingSize * factor) - size) / 2f, 0f);
        var x = headingWidth > 1f ? gutter + headingWidth + gap : width - size;

        // The caret comes first, closest to the words: it is about what the readout is saying,
        // while the cog is about the plugin. Both are siblings on the heading line — the caret is
        // not a menu hanging off the cog.
        x = PlaceHeadingControl(followNode, EnsureFollowTexture, size, gap, x, top, width);
        PlaceHeadingControl(cogNode, EnsureCogTexture, size, gap, x, top, width);
    }

    /// <summary>Generates the cog once. Same contract as the arrow's texture: the pixels are
    /// computed here and uploaded synchronously, so it either works or it throws, and if it throws
    /// there is simply no cog — the window is still on the info bar entry, the plugin list, the
    /// game's own context menu and <c>/wayfarer settings</c>.</summary>
    private bool EnsureCogTexture()
    {
        if (cogFailed)
        {
            return false;
        }

        if (cogLoaded)
        {
            return true;
        }

        try
        {
            var wrap = textures.CreateFromRaw(
                RawImageSpecification.Rgba32(CogBitmap.Size, CogBitmap.Size),
                CogBitmap.Render(),
                "Wayfarer settings cog");

            // Takes ownership: the node disposes the wrap with itself.
            cogNode!.LoadTexture(wrap);
            cogNode.TextureSize = new Vector2(CogBitmap.Size, CogBitmap.Size);
            cogLoaded = true;
            return true;
        }
        catch (Exception ex)
        {
            cogFailed = true;
            log.Error(ex, "Wayfarer readout: the settings cog could not be generated, so the readout has none. Settings are still on the info bar entry, the plugin list and /wayfarer settings.");
            return false;
        }
    }

    /// <summary>Generates the elevation mark in the current arrow colour, if it is not already
    /// loaded. Its own copy of the pixels rather than a shared wrap: KamiToolKit's image node takes
    /// ownership of the texture it is handed and disposes it with itself, so two nodes sharing one
    /// wrap is a double free waiting for a colour change.
    ///
    /// <para>Failing here costs the mark and nothing else — the distance line still says "above
    /// you" in words, which is the half of this feature that does not need a convention to
    /// read.</para></summary>
    private bool EnsureElevationTexture(ArrowIconVariant variant)
    {
        if (elevationFailed)
        {
            return false;
        }

        if (loadedElevationVariant == variant)
        {
            return true;
        }

        try
        {
            var wrap = textures.CreateFromRaw(
                RawImageSpecification.Rgba32(ChevronBitmap.Size, ChevronBitmap.Size),
                ChevronBitmap.Render(variant),
                $"Wayfarer elevation mark ({variant})");

            elevationNode.LoadTexture(wrap);
            elevationNode.TextureSize = new Vector2(ChevronBitmap.Size, ChevronBitmap.Size);
            loadedElevationVariant = variant;
            return true;
        }
        catch (Exception ex)
        {
            elevationFailed = true;
            log.Error(ex, "Wayfarer readout: the above/below mark could not be generated. The distance line still says which it is in words.");
            return false;
        }
    }

    /// <summary>Generates the follow caret once. A single downward chevron in plain white — the
    /// game's own shape for "there is a list behind this" — rather than the player's arrow colour,
    /// because it is a control and not a piece of guidance.</summary>
    private bool EnsureFollowTexture()
    {
        if (followFailed)
        {
            return false;
        }

        if (followLoaded)
        {
            return true;
        }

        try
        {
            var wrap = textures.CreateFromRaw(
                RawImageSpecification.Rgba32(ChevronBitmap.Size, ChevronBitmap.Size),
                ChevronBitmap.Render(ArrowIconVariant.White, strokes: 1),
                "Wayfarer follow caret");

            followNode!.LoadTexture(wrap);
            followNode.TextureSize = new Vector2(ChevronBitmap.Size, ChevronBitmap.Size);
            followLoaded = true;
            return true;
        }
        catch (Exception ex)
        {
            followFailed = true;
            log.Error(ex, "Wayfarer readout: the follow caret could not be generated, so the readout has none. What is being followed is still chosen from the window's Following tab and the game's own right-click menu.");
            return false;
        }
    }

    private float LayoutRule(int index, ReadoutLine line, float factor, float left, float width, float y)
    {
        if (!line.Separated)
        {
            ruleNodes[index].IsVisible = false;
            return y;
        }

        y += BaseGap * factor * 2f;
        ruleNodes[index].Size = new Vector2(width, GameMetrics.Window.RuleHeight);
        ruleNodes[index].Position = new Vector2(left, y);
        ruleNodes[index].IsVisible = true;
        return y + (BaseGap * factor) + GameMetrics.Window.RuleHeight;
    }

    /// <summary>Lays out one line and reports the height it took — which is the height it needs,
    /// measured, not a fixed guess.
    ///
    /// <para>The readout wraps rather than truncating (there is no marquee flag anywhere in the
    /// engine, and the game's own journal and tooltips grow downward), so a line's height is a
    /// function of its text and the width it has. Asking the game how wide the text draws and
    /// dividing by that width is the only way to know, and it is exact: the same measurement the
    /// engine uses to lay the glyphs out.</para></summary>
    private float LayoutLine(int index, ReadoutLine line, float fontSize, float left, float width, float y)
    {
        var node = lineNodes[index];
        node.FontType = FontFor(line.Emphasis);
        node.FontSize = (uint)Math.Max(fontSize, 8f);

        // One number for both the wrap spacing and the advance, so a wrapped line's second row and
        // the line after it cannot disagree about where they are.
        //
        // The tracker leads its lines by two over the font — Axis 14 at 16 in ToDoList 1004/1005/1007,
        // Axis 12 at 14 in 1008/1009 — against the four this used, which is the "text spacing is
        // huge" complaint: a heads-up element leads far tighter than a window does.
        var step = Math.Max(fontSize + (GameMetrics.Hud.LineLeading - GameMetrics.Hud.LineSize), 11f);
        node.LineSpacing = (uint)step;
        node.TextColor = ColorFor(line.Emphasis);
        node.TextOutlineColor = OutlineFor(line.Emphasis);

        // Assigning String builds a SeString; only do it when the words actually changed.
        if (!string.Equals(lastText[index], line.Text, StringComparison.Ordinal))
        {
            lastText[index] = line.Text;
            node.String = line.Text;
        }

        var height = step * WrappedLines(node, width);
        node.Size = new Vector2(width, height);
        node.Position = new Vector2(left, y);
        node.IsVisible = true;
        return height;
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

        if (reason != ArrowHiddenReason.TextureUnavailable && !diagnosticsEnabled())
        {
            return;
        }

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
            // Once for the whole session, not once per change of reason: guidance stopping and
            // starting again re-enters this reason every time, and the answer never changes.
            if (!warnedTextureOnce)
            {
                warnedTextureOnce = true;
                log.Warning(message);
            }

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
        if (!diagnosticsEnabled())
        {
            return;
        }

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
