using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Wayfarer.Core.Input;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;
using Wayfarer.Modules;
using Wayfarer.Windows.Native;

namespace Wayfarer.Windows;

/// <summary>The plugin-drawn guidance readout — now a <b>fallback</b>, not the primary surface.
///
/// The readout is a native overlay drawn with the game's own text nodes, fonts and colours; this
/// window only appears when that overlay could not be created, or when the player has deliberately
/// turned it off. It renders the exact same <see cref="ReadoutContent"/> the overlay does, so the
/// two cannot say different things, and the several hundred lines of bespoke layout that used to
/// live here — and that were the actual cause of the "half the text is cut off" complaint — are
/// gone with it.
///
/// It keeps what the overlay physically cannot have: clickable teleport and duty-finder lines, and
/// the entry buttons at the bottom that open the checklist, open the hunting log and stop whatever
/// mode is engaged. An overlay is click-through by construction, which is exactly why it can never
/// be in the way — but it also means that whenever this window is the one on screen (the overlay
/// off, or unavailable), it must be a genuinely usable way back into Wayfarer on its own, not just
/// a readout. See <see cref="DtrEntry"/> for the surface that covers the same job while the overlay
/// <i>is</i> active.</summary>
internal sealed unsafe class ArrowWindow : Window
{
    private const ImGuiWindowFlags SharedFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoFocusOnAppearing;

    // Floor for the user-resizable width so a drag can't collapse the readout to nothing.
    private const float MinWidthUnscaled = 220f;

    private readonly INavigationProvider navigator;
    private readonly ReadoutFeed feed;
    private readonly GuidanceOverlay overlay;
    private readonly ModuleRegistry modules;
    private readonly InputModeService inputMode;
    private readonly QuestHelperConfig cfg;
    private readonly IObjectTable objects;
    private readonly IClientState clientState;
    private readonly IPluginLog log;

    // Height for *this* frame's SizeConstraints — the previous frame's measured content height.
    // Seeded with a sane small default so the very first frame can't render collapsed; it
    // self-corrects to the real content height from frame 2 onward regardless.
    private float desiredHeight = 80f;

    public ArrowWindow(
        INavigationProvider navigator,
        ReadoutFeed feed,
        GuidanceOverlay overlay,
        ModuleRegistry modules,
        InputModeService inputMode,
        QuestHelperConfig cfg,
        IObjectTable objects,
        IClientState clientState,
        IPluginLog log)
        : base("###WayfarerArrow")
    {
        this.navigator = navigator;
        this.feed = feed;
        this.overlay = overlay;
        this.modules = modules;
        this.inputMode = inputMode;
        this.cfg = cfg;
        this.objects = objects;
        this.clientState = clientState;
        this.log = log;
        RespectCloseHotkey = false; // Esc must not close a HUD readout
        IsOpen = true;              // visibility is governed by DrawConditions
        Flags = SharedFlags;
    }

    public override bool DrawConditions() => feed.ShouldShow() && !overlay.IsActive;

    public override void PreDraw()
    {
        Flags = cfg.ArrowLocked ? SharedFlags | ImGuiWindowFlags.NoMove : SharedFlags;

        // Width is freely user-resizable between the min floor and unbounded; height is pinned to
        // desiredHeight via EQUAL min/max, so a vertical drag is clamped straight back every frame
        // with no oscillation. desiredHeight is re-measured from actual content at the end of every
        // Draw — a one-frame-lag auto-height idiom that self-corrects regardless of what a stale
        // ini-persisted size held.
        var minWidth = MinWidthUnscaled * ImGuiHelpers.GlobalScale;
        SizeConstraints = new()
        {
            MinimumSize = new(minWidth, desiredHeight),
            MaximumSize = new(float.MaxValue, desiredHeight),
        };
    }

    public override void Draw()
    {
        // A stray manual config edit could push TextScale out of the slider's range, and
        // SetWindowFontScale doesn't clamp on its own.
        ImGui.SetWindowFontScale(Math.Clamp(cfg.TextScale, 0.1f, 5f));

        var content = feed.Compose(cfg.ClickTeleportEnabled);
        DrawArrow(content);

        foreach (var line in content.Lines)
        {
            DrawLine(line);
        }

        DrawEntryButtons();

        desiredHeight = ImGui.GetCursorPosY() + ImGui.GetStyle().WindowPadding.Y;
    }

    private static void CenteredWrapped(string text)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var size = ImGui.CalcTextSize(text).X;
        if (size < width)
        {
            ImGui.SetCursorPosX(MathF.Max(0f, (ImGui.GetWindowSize().X - size) / 2f));
        }

        ImGui.TextWrapped(text);
    }

    // The fallback's own way back into Wayfarer — restored after the native-overlay rewrite
    // dropped them along with the rest of this window's bespoke layout (see the class doc
    // comment). A mouse can click these directly; a controller gets the same information as
    // plain text, exactly like the old widget did, because the game's own context menu (see
    // ContextMenuMode) is that player's real entry point and a stray focusable ImGui button here
    // would just be a second, worse one.
    private void DrawEntryButtons()
    {
        var drewChecklist = DrawChecklistButton();
        var drewHunting = DrawHuntingButton(sameLine: drewChecklist);
        var drewStop = DrawStopButton(sameLine: drewChecklist || drewHunting);

        if (drewChecklist || drewHunting || drewStop)
        {
            ImGui.Spacing();
        }
    }

    // The universal exit, mirrored from the hub window's own Stop buttons (see
    // NativeHubWindow.OnStopClicked) so a route or hunt started while this fallback happens to be
    // the one on screen has a way out of it right here too, rather than only through the hub.
    private bool DrawStopButton(bool sameLine)
    {
        if (!navigator.Current.Engaged)
        {
            return false;
        }

        if (sameLine)
        {
            ImGui.SameLine();
        }

        if (inputMode.Mode == InputMode.Controller)
        {
            ImGui.TextUnformatted("Stop");
        }
        else if (ImGui.SmallButton("Stop"))
        {
            navigator.ClearPickup();
        }

        return true;
    }

    private bool DrawChecklistButton()
    {
        if (modules.Get<UnlockChecklistModule>() is not { Enabled: true } unlockModule)
        {
            return false;
        }

        const string label = "Open Wayfarer ▸";
        if (inputMode.Mode == InputMode.Controller)
        {
            ImGui.TextUnformatted(label);
        }
        else if (ImGui.SmallButton(label))
        {
            unlockModule.OpenChecklist();
        }

        return true;
    }

    private bool DrawHuntingButton(bool sameLine)
    {
        if (modules.Get<HuntingLogModule>() is not { Enabled: true } huntingModule)
        {
            return false;
        }

        var hunting = huntingModule.Hunting;
        var label = hunting.ActiveLogLabel is null
            ? "Hunting log"
            : $"Hunting log ({hunting.RemainingOnPage.Count})";

        if (sameLine)
        {
            ImGui.SameLine();
        }

        if (inputMode.Mode == InputMode.Controller)
        {
            ImGui.TextUnformatted(label);
        }
        else if (ImGui.SmallButton(label))
        {
            huntingModule.OpenLog();
        }

        return true;
    }

    private void DrawLine(ReadoutLine line)
    {
        if (line.Separated)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        switch (line.Emphasis)
        {
            case ReadoutEmphasis.Heading:
                ImGui.PushStyleColor(ImGuiCol.Text, GameColors.Heading);
                ImGui.TextWrapped(line.Text);
                ImGui.PopStyleColor();
                break;

            case ReadoutEmphasis.Primary:
                CenteredWrapped(line.Text);
                break;

            case ReadoutEmphasis.Muted:
                ImGui.PushStyleColor(ImGuiCol.Text, GameColors.Dimmed);
                ImGui.TextWrapped(line.Text);
                ImGui.PopStyleColor();
                break;

            default:
                DrawSecondary(line);
                break;
        }
    }

    // The affordances this surface has that the overlay does not: the teleport advice and the
    // "complete this duty" line are both clickable here. Everything else is plain text — a
    // readout is for reading.
    private void DrawSecondary(ReadoutLine line)
    {
        var state = navigator.Current;
        if (cfg.ClickTeleportEnabled
            && state.AetheryteId is { } id
            && state.AetheryteUnlocked
            && line.Text.EndsWith("(click)", StringComparison.Ordinal))
        {
            if (ImGui.Selectable(line.Text))
            {
                TeleportAction.Execute(id, cfg, clientState, log);
            }

            return;
        }

        // The composer's own marker for "duty can be queued now" (see
        // DutyObjectiveGuidance.CompleteDutyPrefix's doc comment) — the row id lives on the
        // navigation state, not the line itself, so this is the one place both are in scope.
        if (state.DutyContentFinderConditionId is { } cfcId
            && line.Text.StartsWith(DutyObjectiveGuidance.CompleteDutyPrefix, StringComparison.Ordinal))
        {
            if (ImGui.Selectable(line.Text))
            {
                DutyFinderAction.Execute(cfcId);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Open in Duty Finder");
            }

            return;
        }

        ImGui.TextWrapped(line.Text);
    }

    private void DrawArrow(ReadoutContent content)
    {
        if (!content.ShowArrow || content.TargetX is not { } tx || content.TargetZ is not { } tz)
        {
            return;
        }

        var player = objects.LocalPlayer;
        if (player == null)
        {
            return;
        }

        var yaw = 0f;
        var cameraManager = CameraManager.Instance();
        if (cameraManager != null && cameraManager->Camera != null)
        {
            yaw = cameraManager->Camera->DirH;
        }

        var angle = NavMath.ArrowAngle(
            NavMath.Bearing(tx - player.Position.X, tz - player.Position.Z), yaw);

        var size = 48f * cfg.ArrowScale * ImGuiHelpers.GlobalScale;
        var width = MathF.Max(ImGui.GetContentRegionAvail().X, size + (16f * ImGuiHelpers.GlobalScale));
        ImGui.Dummy(new(width, size));
        var min = ImGui.GetItemRectMin();
        var centre = new Vector2(min.X + (width / 2f), min.Y + (size / 2f));
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);
        Vector2 P(float x, float y) => new(centre.X + (x * cos) - (y * sin), centre.Y + (x * sin) + (y * cos));

        var drawList = ImGui.GetWindowDrawList();
        var half = size / 2f;

        // Two-tone arrowhead so the pointing end is unambiguous.
        drawList.AddTriangleFilled(
            P(0, -half),
            P(-half * 0.6f, half * 0.45f),
            P(0, half * 0.1f),
            ImGui.GetColorU32(new Vector4(1f, 0.82f, 0.25f, 1f)));
        drawList.AddTriangleFilled(
            P(0, -half),
            P(0, half * 0.1f),
            P(half * 0.6f, half * 0.45f),
            ImGui.GetColorU32(new Vector4(0.85f, 0.62f, 0.12f, 1f)));
    }
}
