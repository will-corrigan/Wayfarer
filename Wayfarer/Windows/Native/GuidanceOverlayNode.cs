using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using KamiToolKit.UiOverlay;
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
    // distance, two routing lines, zone, and the muted context block). Pooled rather than
    // allocated per frame: nothing in a per-frame path should allocate.
    private const int MaxLines = 10;

    private const float BaseWidth = 320f;
    private const float BaseHeadingSize = 20f;
    private const float BasePrimarySize = 15f;
    private const float BaseSecondarySize = 13f;
    private const float BaseMutedSize = 12f;
    private const float BaseArrow = 34f;
    private const float BaseGap = 3f;

    // Microsoft's ten-foot guidance, and the reason it is here: on a TV the outer few percent of
    // the panel is behind the bezel or lost to overscan.
    private const float SafeMarginX = 48f;
    private const float SafeMarginY = 27f;

    private readonly Func<ReadoutFrame?> provider;
    private readonly IPluginLog log;

    private readonly TextNode[] lineNodes = new TextNode[MaxLines];
    private readonly HorizontalLineNode[] ruleNodes = new HorizontalLineNode[MaxLines];
    private readonly SimpleImageNode arrowNode;
    private readonly string[] lastText = new string[MaxLines];

    private bool broken;

    public GuidanceOverlayNode(Func<ReadoutFrame?> provider, IPluginLog log)
    {
        this.provider = provider;
        this.log = log;

        // The game's own direction indicator is a plain image node whose rotation is written every
        // frame (AtkImageNode PlayerCone / PlayerConeRotation on the minimap), so this copies the
        // mechanism rather than inventing one. The chevron comes off the minimap's own texture
        // sheet; the origin has to be the icon's centre or it pivots around its corner.
        arrowNode = new SimpleImageNode
        {
            TexturePath = "ui/uld/NaviMap.tex",
            TextureCoordinates = new Vector2(400f, 96f),
            TextureSize = new Vector2(24f, 24f),
            Size = new Vector2(BaseArrow, BaseArrow),
            OriginX = BaseArrow / 2f,
            OriginY = BaseArrow / 2f,
            IsVisible = false,
        };
        arrowNode.AttachNode(this);

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
        if (frame.Content.ShowArrow && frame.ArrowRadians is { } radians)
        {
            var size = BaseArrow * factor;
            arrowNode.Size = new Vector2(size, size);
            arrowNode.OriginX = size / 2f;
            arrowNode.OriginY = size / 2f;
            arrowNode.Position = new Vector2((width / 2f) - (size / 2f), 0f);
            arrowNode.Rotation = radians;
            arrowNode.IsVisible = true;
            return size + (BaseGap * factor);
        }

        arrowNode.IsVisible = false;
        return 0f;
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
