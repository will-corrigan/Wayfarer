using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
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
    private const float BaseArrow = 34f;
    private const float BaseGap = 3f;

    private const string ArrowTexturePath = "ui/uld/NaviMap.tex";

    /// <summary>How many frames in a row the chevron is allowed to come back "not ready" before the
    /// readout gives up on it and shows words instead. The texture is loaded through the game's own
    /// resource system, which can legitimately not have it on the first frames after a zone change
    /// or a login, so a single failure must not be final — but neither may this retry forever.</summary>
    private const int TextureRetries = 120;

    /// <summary>The 24x24 chevron parts on the minimap's own sheet, in <see cref="ArrowIconVariant"/>
    /// order. These are not guesses: <c>ui/uld/NaviMap.uld</c> declares every one of them as a real
    /// part of texture 3, and the extracted sheet shows the same chevron in amber, green, blue, red
    /// and white at exactly these offsets.</summary>
    private static readonly Vector2[] ArrowCoordinates =
    [
        new(352f, 96f),
        new(376f, 96f),
        new(400f, 96f),
        new(424f, 96f),
        new(424f, 120f),
    ];

    private readonly IPluginLog log;
    private readonly TextNode[] lineNodes = new TextNode[MaxLines];
    private readonly HorizontalLineNode[] ruleNodes = new HorizontalLineNode[MaxLines];
    private readonly SimpleImageNode arrowNode;
    private readonly TextNode arrowWordsNode;
    private readonly string[] lastText = new string[MaxLines];

    /// <summary>An invisible click target parked over whichever line is the teleport advice, or
    /// null in a host that cannot be clicked. A <c>ResNode</c> draws nothing of its own, so the
    /// readout looks byte-for-byte the same with or without it — the only difference is a collision
    /// rectangle and the hand cursor over that one line.</summary>
    private readonly ResNode? teleportHitBox;

    private ArrowIconVariant? loadedVariant;
    private int textureAttempts;
    private ArrowHiddenReason lastReported = ArrowHiddenReason.None;
    private bool reportedOnce;
    private string? lastBearingWords;

    public ReadoutBodyNode(IPluginLog log, Action? onTeleportClicked = null)
    {
        this.log = log;

        // The game's own direction indicator is a plain image node whose rotation is written every
        // frame (AtkImageNode PlayerCone / PlayerConeRotation on the minimap), so this copies the
        // mechanism rather than inventing one. The chevron comes off the minimap's own texture
        // sheet; the origin has to be the icon's centre or it pivots around its corner.
        //
        // FitTexture is the line that makes it appear at all, and it is worth saying why. A fresh
        // image node has WrapMode None and no image flags — every image node in the toolkit that is
        // meant to be seen sets one or the other, and this one set neither, so it drew nothing
        // whatever its texture coordinates were. FitTexture is the toolkit's own shorthand for the
        // pair (AutoFit plus Stretch), and it additionally scales the 24-pixel part up to whatever
        // size the readout asks for instead of pinning it at 24.
        //
        // The texture is NOT loaded here. A node constructed while the plugin loads can be
        // constructed before login, and the game's resource system locks a texture loaded that early
        // to the default UI theme — so the load is deferred to the first frame an arrow is actually
        // wanted, where the variant is known and the result can be checked.
        arrowNode = new SimpleImageNode
        {
            TextureSize = new Vector2(24f, 24f),
            Size = new Vector2(BaseArrow, BaseArrow),
            OriginX = BaseArrow / 2f,
            OriginY = BaseArrow / 2f,
            FitTexture = true,
            IsVisible = false,
        };
        arrowNode.AttachNode(this);

        // The direction in words, for when the chevron cannot be drawn. A readout that says "behind
        // you, to the left" is still guidance; an arrow that silently fails to render is not.
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
    /// readout is the host's business.</summary>
    public Vector2 Layout(ReadoutFrame frame)
    {
        var factor = AtkUnitBase.GetGlobalUIScale() * Math.Clamp(frame.Scale, 0.5f, 3f);
        var width = BaseWidth * factor;
        var y = LayoutArrow(frame, factor, width);
        y = LayoutLines(frame, factor, width, y);

        Size = new Vector2(width, y);
        return new Vector2(width, y);
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

    private float LayoutArrow(ReadoutFrame frame, float factor, float width)
    {
        if (frame.ArrowRadians is not { } radians)
        {
            arrowNode.IsVisible = false;
            arrowWordsNode.IsVisible = false;
            ReportArrow(frame.ArrowHidden);
            return 0f;
        }

        // The arrow-size setting has to be applied here as well as the text-size one: before this
        // it moved nothing at all on the readout that is actually on screen, because only the ImGui
        // fallback ever read it.
        var size = BaseArrow * factor * Math.Clamp(frame.ArrowScale, 0.5f, 2f);
        if (EnsureArrowTexture(frame.ArrowIcon))
        {
            arrowWordsNode.IsVisible = false;
            arrowNode.Size = new Vector2(size, size);
            arrowNode.OriginX = size / 2f;
            arrowNode.OriginY = size / 2f;
            arrowNode.Position = new Vector2((width / 2f) - (size / 2f), 0f);
            arrowNode.Rotation = radians;
            arrowNode.IsVisible = true;
            ReportArrow(ArrowHiddenReason.None);
            ReportBearing(radians);
            return size + (BaseGap * factor);
        }

        arrowNode.IsVisible = false;
        if (textureAttempts >= TextureRetries)
        {
            ReportArrow(ArrowHiddenReason.TextureUnavailable);
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

    private float LayoutLines(ReadoutFrame frame, float factor, float width, float top)
    {
        var y = top;
        var count = Math.Min(frame.Content.Lines.Count, MaxLines);
        var hitBoxPlaced = false;

        for (var i = 0; i < count; i++)
        {
            var line = frame.Content.Lines[i];
            var fontSize = FontSizeFor(line.Emphasis) * factor;
            y = LayoutRule(i, line, factor, width, y);

            // Two lines' worth of height so a wrapped line has somewhere to go. WordWrap needs a
            // fixed width and grows downward into whatever height the node has.
            var height = (fontSize + 2f) * 2f;
            LayoutLine(i, line, fontSize, width, height, y);

            hitBoxPlaced |= TryPlaceHitBox(frame, line, hitBoxPlaced, width, height, y);
            y += height * 0.6f;
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
        return y + (BaseGap * factor);
    }

    private float LayoutRule(int index, ReadoutLine line, float factor, float width, float y)
    {
        if (!line.Separated)
        {
            ruleNodes[index].IsVisible = false;
            return y;
        }

        y += BaseGap * factor * 2f;
        ruleNodes[index].Size = new Vector2(width, 4f);
        ruleNodes[index].Position = new Vector2(0f, y);
        ruleNodes[index].IsVisible = true;
        return y + (BaseGap * factor) + 4f;
    }

    private void LayoutLine(int index, ReadoutLine line, float fontSize, float width, float height, float y)
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
        node.Position = new Vector2(0f, y);
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
        ReadoutFrame frame, ReadoutLine line, bool alreadyPlaced, float width, float height, float y)
    {
        if (alreadyPlaced || teleportHitBox is null || !frame.ClickableTeleport || line.Action != ReadoutLineAction.Teleport)
        {
            return false;
        }

        teleportHitBox.Size = new Vector2(width, height * 0.6f);
        teleportHitBox.Position = new Vector2(0f, y);
        teleportHitBox.IsVisible = true;
        return true;
    }

    /// <summary>Loads the chosen chevron if it is not already loaded, and reports whether one can
    /// actually be drawn. Reloading on a variant change is what makes the setting apply live; the
    /// bounded retry is what stops a texture that is merely late (a zone change, a fresh login) from
    /// being mistaken for one that is missing.</summary>
    private bool EnsureArrowTexture(ArrowIconVariant variant)
    {
        var index = (int)variant;
        if (index < 0 || index >= ArrowCoordinates.Length)
        {
            index = 0;
        }

        if (loadedVariant != (ArrowIconVariant)index)
        {
            loadedVariant = (ArrowIconVariant)index;
            textureAttempts = 0;
            arrowNode.TextureCoordinates = ArrowCoordinates[index];
            arrowNode.LoadTexture(ArrowTexturePath);
        }

        // Zero means "invalid or not ready" — the toolkit's own words for it.
        if (arrowNode.ActualTextureSize != Vector2.Zero)
        {
            return true;
        }

        if (textureAttempts < TextureRetries)
        {
            textureAttempts++;
            arrowNode.LoadTexture(ArrowTexturePath);
            arrowNode.TextureCoordinates = ArrowCoordinates[index];
        }

        return false;
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
            _ => $"Wayfarer readout: the direction chevron could not be loaded from {ArrowTexturePath} after "
                + $"{TextureRetries} attempts — showing the direction in words instead.",
        };

        if (reason == ArrowHiddenReason.TextureUnavailable)
        {
            log.Warning(message);
            return;
        }

        log.Debug(message);
    }

    /// <summary>Writes down what the chevron is currently claiming, so the one thing about it that
    /// cannot be settled by reading the code can be settled by looking at the screen once.
    ///
    /// <para><c>NavMath.ArrowAngle</c> is defined as "0 = straight up", and the words fallback is
    /// built from the same number, so the words are right by construction. The image node is not:
    /// the angle is handed straight to <c>Rotation</c>, which is only correct if the chevron in
    /// <c>ui/uld/NaviMap.tex</c> points straight up at rest — likely, and unverified. If it does
    /// not, every arrow is wrong by a fixed multiple of 90 degrees while the words stay right,
    /// which is a quiet way to send someone the wrong way.</para>
    ///
    /// <para>So: on every change of compass direction, this logs the rotation being applied and the
    /// direction it is supposed to mean. Compare one line against the arrow on screen and the
    /// question is closed — if the readout says "north-east" and the chevron points down-right, the
    /// rest orientation is a quarter turn out.</para></summary>
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
            $"Wayfarer readout: chevron rotation {degrees:F0}° = {words}. The art at " +
            $"{ArrowCoordinates[0].X},{ArrowCoordinates[0].Y} of {ArrowTexturePath} is assumed to point " +
            "straight up unrotated; if the arrow on screen and these words disagree by a quarter turn, " +
            "that assumption is what is wrong.");
    }
}
