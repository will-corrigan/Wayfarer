using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using KamiToolKit.UiOverlay;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>The guidance readout, drawn with the game's own text nodes, fonts and colours instead
/// of an ImGui window.
///
/// It sits on the overlay layer above nameplates and below the player's own windows, where the
/// toolkit has already made it click-through, unfocusable and outside controller navigation, and
/// where it hides itself during cutscenes and with the Toggle UI Display hotkey. That is what lets
/// it replace the old widget for mouse and controller alike: it cannot be in the way, cannot steal
/// focus and cannot trap the cursor.
///
/// <b>Scale is not automatic and this is the one thing about overlays that surprises everyone.</b>
/// The toolkit deliberately de-scales overlay addons to raw screen pixels
/// (<c>addon-&gt;SetScale(1.0f / GetGlobalUIScale(), true)</c>, reapplied on every resolution
/// change) so overlay nodes can be positioned in absolute screen coordinates. A 14pt font here
/// renders at 14 raw pixels whether the player's interface size is 100% or 200%. Everything below
/// is therefore multiplied by <c>GetGlobalUIScale() * userScale</c> every frame, by hand — which is
/// also why the plugin's own text-size setting had to stay rather than being deleted as redundant.
///
/// <b>It must never throw.</b> <c>OnUpdate</c> runs every frame from the addon's update hook, so an
/// exception here is an exception sixty times a second inside the game's render path. The whole
/// body is wrapped; the first failure hides the node permanently and logs once.</summary>
internal sealed unsafe class GuidanceOverlayNode : OverlayNode
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

    // Microsoft's ten-foot guidance, and the reason it is here: on a TV the outer few percent of
    // the panel is behind the bezel or lost to overscan.
    private const float SafeMarginX = 48f;
    private const float SafeMarginY = 27f;

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

    private readonly Func<ReadoutFrame?> provider;
    private readonly IPluginLog log;

    private readonly TextNode[] lineNodes = new TextNode[MaxLines];
    private readonly HorizontalLineNode[] ruleNodes = new HorizontalLineNode[MaxLines];
    private readonly SimpleImageNode arrowNode;
    private readonly TextNode arrowWordsNode;
    private readonly string[] lastText = new string[MaxLines];

    private bool broken;
    private ArrowIconVariant? loadedVariant;
    private int textureAttempts;
    private ArrowHiddenReason lastReported = ArrowHiddenReason.None;
    private bool reportedOnce;

    public GuidanceOverlayNode(Func<ReadoutFrame?> provider, IPluginLog log)
    {
        this.provider = provider;
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
    }

    /// <inheritdoc/>
    public override OverlayLayer OverlayLayer => OverlayLayer.BehindUserInterface;

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        if (broken)
        {
            return;
        }

        try
        {
            Render();
        }
        catch (Exception ex)
        {
            broken = true;
            IsVisible = false;
            log.Error(ex, "Wayfarer readout: the overlay failed and has switched itself off for this session.");
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

    private void Render()
    {
        if (provider() is not { } frame || frame.Content.IsEmpty)
        {
            HideEverything();
            IsVisible = false;
            return;
        }

        IsVisible = true;
        var factor = AtkUnitBase.GetGlobalUIScale() * Math.Clamp(frame.Scale, 0.5f, 3f);
        var width = BaseWidth * factor;
        var y = LayoutArrow(frame, factor, width);
        y = LayoutLines(frame, factor, width, y);

        Size = new Vector2(width, y);
        Position = ResolvePosition(frame.Position, new Vector2(width, y));
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

        var size = BaseArrow * factor;
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

    private float LayoutLines(ReadoutFrame frame, float factor, float width, float top)
    {
        var y = top;
        var count = Math.Min(frame.Content.Lines.Count, MaxLines);

        for (var i = 0; i < count; i++)
        {
            var line = frame.Content.Lines[i];
            var node = lineNodes[i];
            var fontSize = FontSizeFor(line.Emphasis) * factor;

            if (line.Separated)
            {
                y += BaseGap * factor * 2f;
                ruleNodes[i].Size = new Vector2(width, 4f);
                ruleNodes[i].Position = new Vector2(0f, y);
                ruleNodes[i].IsVisible = true;
                y += (BaseGap * factor) + 4f;
            }
            else
            {
                ruleNodes[i].IsVisible = false;
            }

            node.FontType = FontFor(line.Emphasis);
            node.FontSize = (uint)Math.Max(fontSize, 8f);
            node.LineSpacing = (uint)Math.Max(fontSize + 2f, 10f);
            node.TextColor = ColorFor(line.Emphasis);
            node.TextOutlineColor = OutlineFor(line.Emphasis);

            // Assigning String builds a SeString; only do it when the words actually changed.
            if (!string.Equals(lastText[i], line.Text, StringComparison.Ordinal))
            {
                lastText[i] = line.Text;
                node.String = line.Text;
            }

            // Two lines' worth of height so a wrapped line has somewhere to go. WordWrap needs a
            // fixed width and grows downward into whatever height the node has.
            var height = (fontSize + 2f) * 2f;
            node.Size = new Vector2(width, height);
            node.Position = new Vector2(0f, y);
            node.IsVisible = true;
            y += height * 0.6f;
        }

        for (var i = count; i < MaxLines; i++)
        {
            lineNodes[i].IsVisible = false;
            ruleNodes[i].IsVisible = false;
        }

        return y + (BaseGap * factor);
    }

    private void HideEverything()
    {
        arrowNode.IsVisible = false;
        arrowWordsNode.IsVisible = false;
        for (var i = 0; i < MaxLines; i++)
        {
            lineNodes[i].IsVisible = false;
            ruleNodes[i].IsVisible = false;
        }
    }

    /// <summary>Where the readout sits. A plugin cannot join the game's HUD Layout editor, so the
    /// default instead <b>follows the game's own quest tracker</b> — including the way the tracker
    /// mirrors itself when the player moves it to the left half of the screen — which puts
    /// Wayfarer's guidance exactly where the player already looks for objectives, wherever they
    /// have chosen to put that. The corner presets are the fallback for anyone the default does not
    /// suit, and all of them respect the ten-foot safe area.</summary>
    private Vector2 ResolvePosition(ReadoutPosition preset, Vector2 size)
    {
        var screen = new Vector2(AtkStage.Instance()->ScreenSize.Width, AtkStage.Instance()->ScreenSize.Height);

        if (preset == ReadoutPosition.FollowQuestTracker && TryFollowQuestTracker(screen, size) is { } followed)
        {
            return followed;
        }

        var right = Math.Max(screen.X - size.X - SafeMarginX, SafeMarginX);
        var bottom = Math.Max(screen.Y - size.Y - SafeMarginY, SafeMarginY);
        return preset switch
        {
            ReadoutPosition.TopRight => new Vector2(right, SafeMarginY),
            ReadoutPosition.BottomLeft => new Vector2(SafeMarginX, bottom),
            ReadoutPosition.BottomRight => new Vector2(right, bottom),
            _ => new Vector2(SafeMarginX, SafeMarginY),
        };
    }

    private Vector2? TryFollowQuestTracker(Vector2 screen, Vector2 size)
    {
        var tracker = RaptureAtkUnitManager.Instance()->GetAddonByName("_ToDoList");
        if (tracker is null || !tracker->IsVisible)
        {
            return null;
        }

        var trackerPosition = new Vector2(tracker->X, tracker->Y);
        var trackerSize = new Vector2(tracker->RootNode->Width, tracker->RootNode->Height) * tracker->Scale;
        if (trackerSize.Y <= 0f)
        {
            return null;
        }

        // The tracker mirrors its own layout depending on which half of the screen it is on, so
        // match it: hang below it on the left, and align right edges on the right.
        var below = trackerPosition.Y + trackerSize.Y + 8f;
        var x = trackerPosition.X < screen.X / 2f
            ? trackerPosition.X
            : trackerPosition.X + trackerSize.X - size.X;

        return new Vector2(
            Math.Clamp(x, SafeMarginX, Math.Max(screen.X - size.X - SafeMarginX, SafeMarginX)),
            Math.Clamp(below, SafeMarginY, Math.Max(screen.Y - size.Y - SafeMarginY, SafeMarginY)));
    }
}
