using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using Lumina.Text.ReadOnly;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>The block Wayfarer draws inside the game's own Main Scenario Guide: the objective's
/// step, the route to it and the compass, hung under the job-quest rows in the game's own
/// arrangement — a marker gutter on the left, Axis-12 words beside it.
///
/// <para><b>A child of the game's node, so the game decides everything about being on screen.</b>
/// When the Main Scenario Guide hides, fades, moves in HUD Layout or changes scale, this goes with
/// it, with no code here about any of that. Its only decisions are what to draw and where inside
/// the root, and it draws nothing at all when there is nothing to say — see <see cref="HideAll"/>.
/// </para>
///
/// <para><b>Every line is a section that reports its own height</b> and a stack places the next one
/// after it, so no measurement of one line can move another. The pressable lines get a hit box for
/// the pointer and an anchor for the pad, mirrored onto the same rectangle by
/// <see cref="PressTargets"/>, and the compass takes the gutter beside the first line without
/// costing any height. All of it is <see cref="ScenarioTreeLayout"/>'s arithmetic, which is proved
/// without a client attached.</para></summary>
internal sealed unsafe class ScenarioTreeGuidanceNode : ResNode
{
    /// <summary>The deepest block the composer can emit: the step, then a distance, an entry shard, an
    /// aethernet hop, a teleport and a zone, with slack. Pooled: nothing in a per-frame path
    /// allocates.</summary>
    private const int MaxLines = 8;

    /// <summary>How every line behaves: outlined against the world, wrapping downward as the game's
    /// own journal does.</summary>
    private const TextFlags BodyFlags = TextFlags.Edge | TextFlags.WordWrap | TextFlags.MultiLine;

    /// <summary>One row of letters with an ascender and a descender, for asking a node how tall one
    /// row of it draws.</summary>
    private const string RowProbe = "Ag";

    /// <summary>The game's own art sheet for the Main Scenario Guide, from which the "!" medallion
    /// is taken.</summary>
    private const string BannerTexture = "ui/uld/ScenarioTree.tex";

    /// <summary>What a pressable line's words sit at when the pointer is not on them. The lift to
    /// full says the line is pressable.</summary>
    private const float PressableLineIdleAlpha = 0.8f;

    /// <summary>Padding above and below a pressable line's words, so its target is a row rather
    /// than the glyphs alone — the quest tracker's own inset.</summary>
    private const float PressableLineBoxPadding = GameMetrics.Row.TextTop;

    /// <summary>The presses a line can carry, as an index into the parallel arrays of boxes, slots
    /// and anchors. The line that carries each is chosen by the composer's own action mark.</summary>
    private const int LineTeleport = 0;

    /// <inheritdoc cref="LineTeleport"/>
    private const int LineDuty = 1;

    /// <inheritdoc cref="LineTeleport"/>
    private const int PressableLineCount = LineDuty + 1;

    /// <summary>Bits of <see cref="ClickTargets"/>, one per press.</summary>
    private const int TeleportTarget = 1;

    /// <inheritdoc cref="TeleportTarget"/>
    private const int DutyTarget = 2;

    private readonly IPluginLog log;
    private readonly Func<bool> diagnosticsEnabled;
    private readonly SectionStackNode stack;
    private readonly ResNode[] lineSections = new ResNode[MaxLines];
    private readonly ResNode footSection;
    private readonly TextNode[] lineNodes = new TextNode[MaxLines];
    private readonly HorizontalLineNode[] ruleNodes = new HorizontalLineNode[MaxLines];
    private readonly SimpleImageNode[] markerNodes = new SimpleImageNode[MaxLines];
    private readonly string[] lastText = new string[MaxLines];
    private readonly ResNode?[] lineHitBoxes = new ResNode?[PressableLineCount];
    private readonly LineSlot?[] lineSlots = new LineSlot?[PressableLineCount];
    private readonly NavFocusNode?[] navTargets = new NavFocusNode?[PressableLineCount];
    private readonly CompassGlyphs compass;

    private bool markerFailed;
    private int lastNavTargets = -1;
    private ArrowHiddenReason lastReported = ArrowHiddenReason.None;
    private bool reportedOnce;
    private bool warnedTextureOnce;

    public ScenarioTreeGuidanceNode(
        IPluginLog log,
        ITextureProvider textures,
        Func<bool> diagnosticsEnabled,
        Action? onTeleportClicked,
        Action? onDutyClicked)
    {
        this.log = log;
        this.diagnosticsEnabled = diagnosticsEnabled;

        // Clipping off: the medallion overhangs its row by design, and a partially clipped hit box
        // cannot be clicked.
        stack = new SectionStackNode { ClipListContents = false, ItemSpacing = 0f };
        stack.AttachNode(this);

        for (var i = 0; i < MaxLines; i++)
        {
            lineSections[i] = new ResNode { IsVisible = false };
            stack.AddNode(lineSections[i]);
        }

        footSection = new ResNode { IsVisible = false };
        stack.AddNode(footSection);

        BuildLinePool();

        // The floating parts, over the stack: they take no vertical room and are parked from where
        // the flow put the sections.
        compass = new CompassGlyphs(log, textures, this);
        lineHitBoxes[LineTeleport] = BuildLineHitBox(onTeleportClicked, LineTeleport);
        lineHitBoxes[LineDuty] = BuildLineHitBox(onDutyClicked, LineDuty);
        navTargets[LineTeleport] = PressTargets.BuildNavAnchor(onTeleportClicked, this);
        navTargets[LineDuty] = PressTargets.BuildNavAnchor(onDutyClicked, this);
    }

    /// <summary>Which clickable targets are on screen right now, as a bit per press. The host
    /// rebuilds the addon's collision list when this changes, because the game only dispatches
    /// mouse events to nodes in that list.</summary>
    public int ClickTargets { get; private set; }

    /// <summary>Lays the block out for this frame and returns the size it needs, in the game's own
    /// ULD units — the addon is already rendered at the player's interface size, so the only scale
    /// applied here is their text-size preference.</summary>
    public Vector2 Layout(ReadoutFrame frame)
    {
        var factor = Math.Clamp(frame.Scale, 0.5f, 3f);
        var width = ReadoutBodyLayout.Width(factor);
        var ringSize = ReadoutBodyLayout.CompassRingBox(factor, frame.ArrowScale);
        var needleSize = ReadoutBodyLayout.CompassNeedleBox(factor, frame.ArrowScale);
        var drawable = frame.ArrowRadians is not null && compass.EnsureNeedle(frame.ArrowIcon);

        stack.Width = width;
        stack.Position = Vector2.Zero;
        var arrowSlot = LayoutLines(frame, factor, width, drawable);
        stack.RecalculateLayout();

        // Only now the floating parts, from where the flow actually put the sections.
        LayoutCompass(frame, drawable, ringSize, needleSize, factor, ArrowCentre(arrowSlot, factor));
        SettleLineHitBoxes(LayoutLineHitBoxes());
        SettleNav();

        var size = new Vector2(width, stack.Height);
        Size = size;
        return size;
    }

    /// <summary>Hides every child. Used when there is nothing to say — the block disappears rather
    /// than leaving a gap under the banner.</summary>
    public void HideAll()
    {
        compass.Hide();
        foreach (var box in lineHitBoxes)
        {
            if (box is not null)
            {
                box.IsVisible = false;
            }
        }

        ClickTargets = 0;
        HideNav();
        HideLinesFrom(0);
        footSection.IsVisible = false;
        footSection.Height = 0f;
        stack.RecalculateLayout();
    }

    private static Vector4 ColorFor(ReadoutEmphasis emphasis) => emphasis switch
    {
        ReadoutEmphasis.Heading => GameColors.Heading,
        ReadoutEmphasis.Primary => GameColors.Body,
        ReadoutEmphasis.Secondary => GameColors.ListText,
        _ => GameColors.Dimmed,
    };

    private static Vector4 OutlineFor(ReadoutEmphasis emphasis) =>
        emphasis == ReadoutEmphasis.Heading ? GameColors.HeadingEdge : GameColors.BodyEdge;

    /// <summary>Which of the composer's action marks each pressable line answers to. The mark is
    /// the only thing that decides — never the wording, never the position.</summary>
    private static ReadoutLineAction ActionFor(int line) =>
        line == LineTeleport ? ReadoutLineAction.Teleport : ReadoutLineAction.OpenDutyFinder;

    private static int TargetFor(int line) => line == LineTeleport ? TeleportTarget : DutyTarget;

    /// <summary>Whether this surface is offering a press at all, over and above the composer having
    /// marked a line for it. The teleport is the only one with a setting of its own.</summary>
    private static bool Offered(ReadoutFrame frame, int press) =>
        press != LineTeleport || frame.ClickableTeleport;

    /// <summary>The one mapping from a line's abstract glyph mark to the game's own bitmap-font
    /// icon — see <c>ReadoutBodyNode.Icon</c> for why two of the three are admitted compromises.
    /// </summary>
    private static BitmapFontIcon? Icon(DtrGlyph glyph) => glyph switch
    {
        DtrGlyph.Aetheryte => BitmapFontIcon.Aetheryte,
        DtrGlyph.Duty => BitmapFontIcon.WaitingForDutyFinder,
        DtrGlyph.Monster => BitmapFontIcon.NotoriousMonster,
        _ => null,
    };

    /// <summary>How many rows this line's text will occupy at the width it has been given, measured
    /// unscaled because the node's own units are the ones every number here is in — see
    /// <c>ReadoutBodyNode.WrappedLines</c> for the two unit-domain traps this avoids.</summary>
    private static float WrappedLines(TextNode node, float width)
    {
        if (width <= 1f)
        {
            return 1f;
        }

        var drawn = node.GetTextDrawSize(considerScale: false);
        if (drawn.X <= 0f && drawn.Y <= 0f)
        {
            return 1f;
        }

        var step = Math.Max(node.LineSpacing, 1f);
        var one = node.GetTextDrawSize(RowProbe, considerScale: false).Y;
        var byHeight = one > 0f ? 1f + MathF.Round(Math.Max(drawn.Y - one, 0f) / step) : 1f;
        var byWidth = MathF.Ceiling(drawn.X / width);
        return Math.Clamp(Math.Max(byWidth, byHeight), 1f, ReadoutBodyLayout.MaxWrappedLines);
    }

    private void BuildLinePool()
    {
        for (var i = 0; i < MaxLines; i++)
        {
            lastText[i] = string.Empty;

            ruleNodes[i] = new HorizontalLineNode { IsVisible = false };
            ruleNodes[i].AttachNode(lineSections[i]);

            markerNodes[i] = BuildMarker(i);

            lineNodes[i] = new TextNode
            {
                FontType = FontType.Axis,
                FontSize = GameMetrics.Banner.SubLineSize,
                AlignmentType = AlignmentType.TopLeft,
                TextFlags = BodyFlags,
                TextColor = GameColors.Body,
                TextOutlineColor = GameColors.BodyEdge,
                IsVisible = false,
            };
            lineNodes[i].AttachNode(lineSections[i]);
        }
    }

    /// <summary>One "!" quest medallion — the game's own part, drawn 1:1 at its native 32.</summary>
    private SimpleImageNode BuildMarker(int index)
    {
        var marker = new SimpleImageNode
        {
            Size = new Vector2(GameMetrics.Banner.MarkerSize, GameMetrics.Banner.MarkerSize),
            WrapMode = KamiToolKit.Enums.WrapMode.Stretch,
            IsVisible = false,
        };

        try
        {
            marker.LoadTexture(BannerTexture);
            marker.TextureCoordinates = new Vector2(GameMetrics.Banner.MarkerU, GameMetrics.Banner.MarkerV);
            marker.TextureSize = new Vector2(GameMetrics.Banner.MarkerSize, GameMetrics.Banner.MarkerSize);
        }
        catch (Exception ex)
        {
            markerFailed = true;
            log.Error(ex, "Wayfarer guidance: the game's Main Scenario Guide art could not be read, so lines are drawn without their medallions.");
        }

        marker.AttachNode(lineSections[index]);
        return marker;
    }

    /// <summary>A pressable line's target, plus the hover that lights the line's own words and not
    /// the box — so a short place name does not light a band of empty space to its right.</summary>
    private ResNode? BuildLineHitBox(Action? onClicked, int line)
    {
        if (onClicked is null)
        {
            return null;
        }

        var box = PressTargets.BuildHitBox(onClicked, this);
        box.AddEvent(AtkEventType.MouseOver, () => SetLineHighlight(line, hovered: true));
        box.AddEvent(AtkEventType.MouseOut, () => SetLineHighlight(line, hovered: false));
        return box;
    }

    private void SetLineHighlight(int line, bool hovered)
    {
        if (lineSlots[line] is { } slot)
        {
            lineNodes[slot.Index].Alpha = hovered ? 1f : PressableLineIdleAlpha;
        }
    }

    /// <summary>Lays out every line, returns the slot the compass takes, and hides the pooled slots
    /// this frame did not use.</summary>
    private LineSlot? LayoutLines(ReadoutFrame frame, float factor, float width, bool arrowDrawable)
    {
        var count = Math.Min(frame.Content.Lines.Count, MaxLines);
        LineSlot? arrowSlot = null;
        Array.Clear(lineSlots);

        var left = ReadoutBodyLayout.SubLineLeft(factor);
        var lineWidth = ReadoutBodyLayout.SubLineWidth(factor);
        var arrowWanted = arrowDrawable && frame.ArrowRadians is not null;

        for (var i = 0; i < count; i++)
        {
            var line = frame.Content.Lines[i];
            var takenByArrow = arrowWanted && arrowSlot is null;
            var slot = LayoutSubLine(i, line, left, lineWidth, factor, drawMarker: !takenByArrow, gutter: ReadoutBodyLayout.GutterLine(i));
            if (takenByArrow)
            {
                arrowSlot = slot;
            }

            ClaimPresses(frame, line, slot);
        }

        HideLinesFrom(count);

        footSection.Size = new Vector2(width, ReadoutBodyLayout.FootHeight(factor));
        footSection.IsVisible = true;
        return arrowSlot;
    }

    /// <summary>Lays out one line and reports where its words ended up, inside its own section. The
    /// height is measured rather than assumed, and the measurement goes into this line's section and
    /// nowhere else.</summary>
    private LineSlot LayoutSubLine(int index, ReadoutLine line, float left, float width, float factor, bool drawMarker, bool gutter)
    {
        var section = lineSections[index];
        var node = lineNodes[index];
        var fontSize = ReadoutBodyLayout.SubLineFontSize(factor);
        var ruleAdvance = LayoutRule(index, line, factor, left, width);
        var step = ReadoutBodyLayout.SubLineStep(factor);

        node.FontType = FontType.Axis;
        node.FontSize = (uint)fontSize;
        node.LineSpacing = (uint)step;
        node.TextColor = ColorFor(line.Emphasis);
        node.TextOutlineColor = OutlineFor(line.Emphasis);
        node.TextFlags = BodyFlags;
        node.Width = width;
        SetGlyphLineText(index, line);

        var block = new ReadoutBlock(line.Marked, line.Separated, WrappedLines(node, width));
        var height = ReadoutBodyLayout.TextHeight(block, factor, gutter);
        var textTop = ReadoutBodyLayout.TextTop(block, factor, gutter);

        node.Size = new Vector2(width, step * Math.Clamp(block.Rows, 1f, ReadoutBodyLayout.MaxWrappedLines));
        node.Position = new Vector2(left, textTop);
        node.IsVisible = true;
        node.Alpha = 1f;

        section.Size = new Vector2(stack.Width, ReadoutBodyLayout.LineHeight(block, factor, gutter));
        section.IsVisible = true;

        LayoutMarker(index, line, factor, ruleAdvance, height, drawMarker);
        return new LineSlot(index, ruleAdvance, textTop, height, fontSize, left, width);
    }

    private float LayoutRule(int index, ReadoutLine line, float factor, float left, float width)
    {
        if (!line.Separated)
        {
            ruleNodes[index].IsVisible = false;
            return 0f;
        }

        ruleNodes[index].Size = new Vector2(width, GameMetrics.Window.RuleHeight);
        ruleNodes[index].Position = new Vector2(left, ReadoutBodyLayout.RuleTop(factor));
        ruleNodes[index].IsVisible = true;
        return ReadoutBodyLayout.RuleAdvance(separated: true, factor);
    }

    private void LayoutMarker(int index, ReadoutLine line, float factor, float afterRule, float height, bool draw)
    {
        var marker = markerNodes[index];
        if (!draw || !line.Marked || markerFailed)
        {
            marker.IsVisible = false;
            return;
        }

        var size = GameMetrics.Banner.MarkerSize * factor;
        marker.Size = new Vector2(size, size);
        marker.Position = new Vector2(
            ReadoutBodyLayout.GutterLeft(factor),
            afterRule + ((Math.Min(height, GameMetrics.Banner.SubLinePitch * factor) - size) / 2f));
        marker.IsVisible = true;
    }

    /// <summary>Hands a line its words, with the game's own icon inside them where the composer put
    /// one, and only when something actually changed — assigning <c>String</c> re-runs the engine's
    /// text flow, and this is a per-frame path.</summary>
    private void SetGlyphLineText(int index, ReadoutLine line)
    {
        if (Icon(line.Glyph) is not { } icon)
        {
            if (!string.Equals(lastText[index], line.Text, StringComparison.Ordinal))
            {
                lastText[index] = line.Text;
                lineNodes[index].String = line.Text;
            }

            return;
        }

        var at = Math.Clamp(line.GlyphAt, 0, line.Text.Length);
        var key = $"{line.Glyph}@{at}:{line.Text}";
        if (string.Equals(lastText[index], key, StringComparison.Ordinal))
        {
            return;
        }

        var builder = new SeStringBuilder();
        builder.AddText(line.Text[..at]).AddIcon(icon).AddText(line.Text[at..]);
        lastText[index] = key;
        lineNodes[index].String = new ReadOnlySeString(builder.Build().Encode());
    }

    private void ClaimPresses(ReadoutFrame frame, ReadoutLine line, LineSlot slot)
    {
        for (var press = 0; press < PressableLineCount; press++)
        {
            if (lineSlots[press] is null
                && lineHitBoxes[press] is not null
                && line.Action == ActionFor(press)
                && Offered(frame, press))
            {
                lineSlots[press] = slot;
                SetLineHighlight(press, hovered: false);
            }
        }
    }

    private void HideLinesFrom(int first)
    {
        for (var i = first; i < MaxLines; i++)
        {
            lineNodes[i].IsVisible = false;
            ruleNodes[i].IsVisible = false;
            markerNodes[i].IsVisible = false;
            lineSections[i].IsVisible = false;
            lineSections[i].Height = 0f;
        }
    }

    /// <summary>Where the compass's centre lands: the optical centre of the first line, or the
    /// block's first pitch when there is no line at all.</summary>
    private float ArrowCentre(LineSlot? slot, float factor) =>
        slot is { } line
            ? stack.Y + lineSections[line.Index].Y + line.TextTop + (line.FontSize * GameMetrics.Type.CapHeightCentre)
            : stack.Y + (GameMetrics.Banner.SubLinePitch * factor / 2f);

    /// <summary>Puts the compass in the marker column beside the first line — centred in the
    /// medallion's own column, never allowed to reach the words; see
    /// <see cref="ReadoutBodyLayout.Arrow"/> for the same rule as a proof.</summary>
    private void LayoutCompass(ReadoutFrame frame, bool drawable, float ringSize, float needleSize, float factor, float lineCentre)
    {
        if (frame.ArrowRadians is not { } radians)
        {
            compass.Hide();
            ReportArrow(frame.ArrowHidden);
            return;
        }

        if (!drawable)
        {
            compass.Hide();
            ReportArrow(ArrowHiddenReason.TextureUnavailable);
            return;
        }

        var centred = ReadoutBodyLayout.GutterLeft(factor) + ((ReadoutBodyLayout.GutterWidth(factor) - ringSize) / 2f);
        var clear = ReadoutBodyLayout.SubLineLeft(factor) - ringSize;
        var centre = new Vector2(Math.Max(Math.Min(centred, clear), 0f) + (ringSize / 2f), lineCentre);
        compass.Show(centre, ringSize, needleSize, radians, frame.ArrowIcon, frame.Content.Elevation);
        ReportArrow(ArrowHiddenReason.None);
    }

    /// <summary>Parks each press's box over the line that carries it, from where the flow put that
    /// line's section, and says which landed.</summary>
    private int LayoutLineHitBoxes()
    {
        var placed = 0;
        for (var press = 0; press < PressableLineCount; press++)
        {
            if (lineHitBoxes[press] is not { } box || lineSlots[press] is not { } slot)
            {
                continue;
            }

            var height = Math.Min(slot.FontSize + (PressableLineBoxPadding * 2f), slot.Height);
            var top = stack.Y + lineSections[slot.Index].Y + slot.RuleAdvance + Math.Max((slot.Height - height) / 2f, 0f);
            box.Size = new Vector2(slot.Width, height);
            box.Position = new Vector2(slot.Left, top);
            box.IsVisible = true;
            placed |= TargetFor(press);
        }

        return placed;
    }

    private void SettleLineHitBoxes(int placed)
    {
        for (var press = 0; press < PressableLineCount; press++)
        {
            if ((placed & TargetFor(press)) == 0 && lineHitBoxes[press] is { } box)
            {
                box.IsVisible = false;
            }
        }

        ClickTargets = placed;
    }

    private void HideNav()
    {
        foreach (var nav in navTargets)
        {
            if (nav is not null)
            {
                nav.IsVisible = false;
            }
        }

        lastNavTargets = -1;
    }

    /// <summary>Puts each anchor where its box ended up and, when the set of live presses changed,
    /// joins them into a ring the d-pad walks: down to the next, up to the previous, both wrapping.
    /// </summary>
    private void SettleNav()
    {
        for (var press = 0; press < PressableLineCount; press++)
        {
            PressTargets.MirrorNav(navTargets[press], lineHitBoxes[press]);
        }

        if (ClickTargets == lastNavTargets)
        {
            return;
        }

        lastNavTargets = ClickTargets;
        Span<int> live = stackalloc int[PressableLineCount];
        var count = 0;
        for (var i = 0; i < PressableLineCount; i++)
        {
            if (navTargets[i] is { IsVisible: true })
            {
                live[count++] = i;
            }
        }

        for (var i = 0; i < count; i++)
        {
            if (navTargets[live[i]] is not { } nav)
            {
                continue;
            }

            var index = i + 1;
            nav.NavIndex = index;
            nav.NavUp = i == 0 ? count : i;
            nav.NavDown = i == count - 1 ? 1 : index + 1;
            nav.NavLeft = index;
            nav.NavRight = index;
        }
    }

    /// <summary>Logs why there is (or is no longer) a compass, once per change of reason. A missing
    /// texture is warned about once per session regardless of the diagnostics setting.</summary>
    private void ReportArrow(ArrowHiddenReason reason)
    {
        if (reportedOnce && reason == lastReported)
        {
            return;
        }

        lastReported = reason;
        reportedOnce = true;

        if (reason == ArrowHiddenReason.TextureUnavailable)
        {
            if (!warnedTextureOnce)
            {
                warnedTextureOnce = true;
                log.Warning("Wayfarer guidance: the compass could not be generated, so none is drawn this session.");
            }

            return;
        }

        if (!diagnosticsEnabled())
        {
            return;
        }

        log.Debug(reason switch
        {
            ArrowHiddenReason.None => "Wayfarer guidance: the compass is being drawn.",
            ArrowHiddenReason.NotRequested => "Wayfarer guidance: no compass — nothing active has a direction to point at.",
            ArrowHiddenReason.NoTargetCoordinates => "Wayfarer guidance: no compass — the objective has no target coordinates.",
            ArrowHiddenReason.NoPlayer => "Wayfarer guidance: no compass — there is no local player to measure a bearing from.",
            _ => "Wayfarer guidance: no compass.",
        });
    }
}
