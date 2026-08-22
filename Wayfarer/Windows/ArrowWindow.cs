using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Wayfarer.Core.Input;
using Wayfarer.Core.Navigation;
using Wayfarer.Modules;

namespace Wayfarer.Windows;

internal sealed unsafe class ArrowWindow : Window
{
    private const ImGuiWindowFlags SharedFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoFocusOnAppearing;

    // Floor for the user-resizable width so a drag can't collapse the widget to nothing.
    private const float MinWidthUnscaled = 160f;

    private static readonly Vector4 LinkColor = new(0.4f, 0.7f, 1f, 1f);

    private readonly INavigationProvider navigator;
    private readonly ModuleRegistry modules;
    private readonly QuestHelperConfig cfg;
    private readonly IObjectTable objects;
    private readonly IClientState clientState;
    private readonly IPluginLog log;
    private readonly InputModeService inputMode;
    private readonly InputModeConfig inputModeCfg;
    private readonly Action saveConfig;

    // Height for *this* frame's SizeConstraints — the previous frame's measured content height
    // (see PreDraw/MeasureHeightForNextFrame). Seeded with a sane small default so the very
    // first frame (nothing measured yet, and possibly a stale ini-persisted size from before
    // this sizing model existed) can't render collapsed or with leftover dead space; it
    // self-corrects to the real content height from frame 2 onward regardless.
    private float desiredHeight = 80f;

    public ArrowWindow(
        INavigationProvider navigator,
        ModuleRegistry modules,
        QuestHelperConfig cfg,
        IObjectTable objects,
        IClientState clientState,
        IPluginLog log,
        InputModeService inputMode,
        InputModeConfig inputModeCfg,
        Action saveConfig)
        : base("###WayfarerArrow")
    {
        this.navigator = navigator;
        this.modules = modules;
        this.cfg = cfg;
        this.objects = objects;
        this.clientState = clientState;
        this.log = log;
        this.inputMode = inputMode;
        this.inputModeCfg = inputModeCfg;
        this.saveConfig = saveConfig;
        RespectCloseHotkey = false; // Esc must not close a HUD widget
        IsOpen = true;              // visibility is governed by DrawConditions
        Flags = SharedFlags;
    }

    public override bool DrawConditions() =>
        !cfg.WidgetHidden && !string.Equals(navigator.Current.Mode, NavigationState.Modes.Hidden, StringComparison.Ordinal);

    public override void PreDraw()
    {
        Flags = cfg.ArrowLocked ? SharedFlags | ImGuiWindowFlags.NoMove : SharedFlags;

        // Width is freely user-resizable (drag the side/corner grips) between the min floor and
        // unbounded; height is pinned to desiredHeight via EQUAL min/max, so ImGui's own
        // resize-grip/edge-drag clamps any attempted vertical drag straight back to that exact
        // value every frame — dragging the bottom edge or the corner simply has no vertical
        // effect, with no oscillation, because nothing else ever calls SetWindowSize/fights this
        // constraint (unlike an ImGuiCond.Always SetWindowSize call at the end of Draw, which
        // rubber-banded against the corner grip's own same-frame resize). desiredHeight is
        // re-measured from actual content at the end of every Draw (see
        // MeasureHeightForNextFrame) — a standard one-frame-lag auto-height idiom that
        // self-corrects within one frame regardless of what a stale ini-persisted size held.
        var minWidth = MinWidthUnscaled * ImGuiHelpers.GlobalScale;
        SizeConstraints = new()
        {
            MinimumSize = new(minWidth, desiredHeight),
            MaximumSize = new(float.MaxValue, desiredHeight),
        };
    }

    public override void Draw()
    {
        // TextScale is a QuestHelperConfig setting (0.8–2.0 via the slider) but a stray manual
        // config edit could push it out of range — SetWindowFontScale doesn't clamp on its own.
        ImGui.SetWindowFontScale(Math.Clamp(cfg.TextScale, 0.1f, 5f));

        var state = navigator.Current;
        switch (state.Mode)
        {
            case NavigationState.Modes.SameZone:
                DrawArrow(state);
                break;
            case NavigationState.Modes.OtherZone:
                DrawOtherZone(state);
                break;
            case NavigationState.Modes.DutyObjective:
                DrawDutyObjective(state);
                break;
            case NavigationState.Modes.Idle:
                ImGui.TextDisabled("No quest followed");
                break;
            default:
                if (state.Reason is { } reason)
                {
                    ImGui.TextDisabled(reason);
                }

                break;
        }

        Gap();
        DrawQuestLine(state);
        DrawControllerGlyphHint();
        DrawUnlocksButton();
        DrawGlanceableUnlocks();
        DrawGlanceableHunting();
        ControllerHint.Draw(inputModeCfg, saveConfig);

        MeasureHeightForNextFrame();
    }

    private static void CenteredText(string text)
    {
        var w = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX(MathF.Max(0f, (ImGui.GetWindowSize().X - w) / 2f));
        ImGui.Text(text);
    }

    // "Aethernet to X, then 40 yalms" / "Through Y, then 40 yalms" — RemainingYalms is
    // the walk after the shard hop or door crossing (RouteCosting's per-candidate
    // second leg); absent for candidates where it wasn't computed.
    private static string RemainingSuffix(NavigationState state) =>
        state.RemainingYalms is { } r ? $", then {NavMath.FormatDistance(r)}" : string.Empty;

    // Objective is inside instanced duty content: no arrow, no route — just say so.
    // Deliberately its own branch (not the generic default/Reason fallback) so this
    // reads as prominent, actionable guidance rather than the muted "can't help" text.
    // When the duty can be queued right now, DutyContentFinderConditionId is set and the
    // reason follows the fixed "Complete the duty: {name}" template — split on the prefix
    // so the duty name alone renders as a clickable link (its tooltip already says "Open
    // in Duty Finder"; there's no separate "queue via Duty Finder" suffix to render — the
    // link IS the affordance). Every other case (not yet unlocked) just wraps the plain
    // text. The prefix and the link are each their own TextWrapped/TextUnformatted call
    // starting at a fresh line — never glued together with SameLine(0, 0) — so each wraps
    // independently at word boundaries no matter how narrow the window gets; gluing them
    // is what previously produced a one-character-per-line column at narrow widths.
    private static void DrawDutyObjective(NavigationState state)
    {
        if (state.Reason is not { } reason)
        {
            return;
        }

        if (state.DutyContentFinderConditionId is { } cfcId
            && reason.StartsWith(DutyObjectiveGuidance.CompleteDutyPrefix, StringComparison.Ordinal))
        {
            var name = reason[DutyObjectiveGuidance.CompleteDutyPrefix.Length..];

            ImGui.PushTextWrapPos(0f);
            ImGui.TextWrapped(DutyObjectiveGuidance.CompleteDutyPrefix);
            DrawDutyLink(name, cfcId);
            ImGui.PopTextWrapPos();
            return;
        }

        ImGui.TextWrapped(reason);
    }

    // Hyperlink-style duty name: distinct color, an underline drawn under the text
    // rect via the window draw list, a tooltip, and a click that opens the Duty
    // Finder for that duty — client UI navigation, not a server-affecting action.
    private static unsafe void DrawDutyLink(string name, uint cfcId)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, LinkColor);
        ImGui.TextUnformatted(name);
        ImGui.PopStyleColor();

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(min.X, max.Y), new Vector2(max.X, max.Y), ImGui.GetColorU32(LinkColor));

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Open in Duty Finder");
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (ImGui.IsItemClicked())
        {
            var agent = AgentContentsFinder.Instance();
            if (agent != null)
            {
                agent->OpenRegularDuty(cfcId, false);
            }
        }
    }

    /// <summary>Stores this frame's actual content height (cursor Y at the end of Draw, plus the
    /// window's bottom padding) into <see cref="desiredHeight"/> for next frame's PreDraw to pin
    /// via SizeConstraints — see the comment there for why a stored-and-reapplied constraint is
    /// used instead of an explicit SetWindowSize call.</summary>
    private void MeasureHeightForNextFrame() =>
        desiredHeight = ImGui.GetCursorPosY() + ImGui.GetStyle().WindowPadding.Y;

    /// <summary>Vertical breathing room between the window's sections. A little more generous in
    /// Controller mode for couch readability (spec §4) than the mouse-mode default.</summary>
    private void Gap()
    {
        if (inputMode.Mode == InputMode.Controller)
        {
            ImGuiHelpers.ScaledDummy(0f, 8f);
        }
        else
        {
            ImGui.Spacing();
        }
    }

    /// <summary>Controller-mode-only legend for the confirm/cancel glyphs used by items in this
    /// window that are still reachable only through Dalamud's stopgap gamepad-nav mode (d-pad
    /// focus + confirm), pending the native context-menu action surface (task A2).</summary>
    private void DrawControllerGlyphHint()
    {
        if (inputMode.Mode != InputMode.Controller)
        {
            return;
        }

        var glyphs = inputMode.Glyphs;
        ImGui.TextDisabled($"{glyphs.Confirm} select   {glyphs.Cancel} back");
    }

    private void DrawArrow(NavigationState state)
    {
        if (state.TargetX is null || state.TargetZ is null)
        {
            return;
        }

        DrawArrowTo(state.TargetX.Value, state.TargetY, state.TargetZ.Value);

        if (state.AethernetExitName is { } exit)
        {
            // Arrow already points at the entry shard in this case.
            CenteredText($"→ {state.AethernetEntryName} shard");
            CenteredText($"Aethernet to: {exit}");
        }
    }

    private void DrawArrowTo(float tx, float? ty, float tz)
    {
        var player = objects.LocalPlayer;
        if (player == null)
        {
            return;
        }

        var dx = tx - player.Position.X;
        var dy = (ty ?? player.Position.Y) - player.Position.Y;
        var dz = tz - player.Position.Z;

        var distance = NavMath.Distance(dx, dy, dz);
        if (distance < 5f)
        {
            // Point-blank arrow direction is numerically meaningless; show arrival instead.
            CenteredText("You've arrived");
            return;
        }

        var yaw = 0f;
        var cm = CameraManager.Instance();
        if (cm != null && cm->Camera != null)
        {
            yaw = cm->Camera->DirH;
        }

        var angle = NavMath.ArrowAngle(NavMath.Bearing(dx, dz), yaw);

        var size = 48f * cfg.ArrowScale * ImGuiHelpers.GlobalScale;
        var width = MathF.Max(
            ImGui.CalcTextSize(navigator.Current.QuestName ?? string.Empty).X,
            size + (16f * ImGuiHelpers.GlobalScale));
        ImGui.Dummy(new(width, size));
        var min = ImGui.GetItemRectMin();
        var c = new Vector2(min.X + (width / 2f), min.Y + (size / 2f));
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);
        Vector2 P(float x, float y) => new(c.X + (x * cos) - (y * sin), c.Y + (x * sin) + (y * cos));

        var dl = ImGui.GetWindowDrawList();
        var h = size / 2f;

        // Two-tone arrowhead so the pointing end is unambiguous.
        dl.AddTriangleFilled(
            P(0, -h),
            P(-h * 0.6f, h * 0.45f),
            P(0, h * 0.1f),
            ImGui.GetColorU32(new Vector4(1f, 0.82f, 0.25f, 1f)));
        dl.AddTriangleFilled(
            P(0, -h),
            P(0, h * 0.1f),
            P(h * 0.6f, h * 0.45f),
            ImGui.GetColorU32(new Vector4(0.85f, 0.62f, 0.12f, 1f)));

        CenteredText(NavMath.FormatDistance(distance));
    }

    private void DrawOtherZone(NavigationState state)
    {
        if (state.EntranceX is { } ex && state.EntranceZ is { } ez)
        {
            DrawArrowTo(ex, null, ez);
            if (state.AethernetExitName is { } exitName)
            {
                CenteredText($"→ {state.AethernetEntryName} shard");
                CenteredText($"Aethernet to: {exitName}{RemainingSuffix(state)}");
            }
            else
            {
                CenteredText($"Through: {state.EntranceName}{RemainingSuffix(state)}");
            }

            // Routing info above is its own visual group — give the guidance
            // (teleport suggestion / no-route message) below a little breathing room.
            ImGui.Spacing();
        }

        if (state.AetheryteName is null)
        {
            // No live marker, no known map-link entrance, and no teleport worth
            // suggesting (see RouteCosting.TeleportCandidate) — most commonly an
            // interior objective (e.g. inside a manor) with no entrance modeled in the
            // map-link data. QuestNavigator's OtherZoneResolution.InteriorMessage is the
            // single source of truth for this text (Core-tested) — Reason carries it
            // straight through, so this is just display, not re-derivation.
            if (state.EntranceX is null && state.Reason is { } reason)
            {
                ImGui.TextWrapped(reason);
            }
        }
        else if (!state.AetheryteUnlocked)
        {
            ImGui.TextWrapped($"Objective is in {state.ZoneName ?? "another zone"} — nearest aetheryte is {state.AetheryteName}, but you are not attuned there.");
        }
        else
        {
            var label = cfg.ClickTeleportEnabled
                ? $"Teleport to {state.AetheryteName} first (click)"
                : $"Teleport to {state.AetheryteName} first";
            if (ImGui.Selectable(label) && cfg.ClickTeleportEnabled && state.AetheryteId is { } id)
            {
                TeleportAction.Execute(id, cfg, clientState, log);
            }
        }

        if (state.ZoneName is { } zone)
        {
            ImGui.Spacing();
            ImGui.TextDisabled(zone);
        }
    }

    private void DrawQuestLine(NavigationState state)
    {
        ImGui.Separator();
        ImGui.Spacing();
        if (state.RouteStop is { } stop && state.RouteTotal is { } total)
        {
            CenteredText($"Stop {stop} of {total}");
        }

        if (ImGui.Selectable(state.QuestName ?? "(no quest)"))
        {
            ImGui.OpenPopup("questpicker");
        }

        if (state.StepLabel is { Length: > 0 } label
            && !string.Equals(label, state.QuestName, StringComparison.OrdinalIgnoreCase))
        {
            ImGui.TextWrapped(label);
        }

        if (ImGui.BeginPopup("questpicker"))
        {
            if (ImGui.MenuItem("Follow MSQ"))
            {
                navigator.ClearPickup();
                navigator.FollowedOverride = null;
            }

            if (state.RouteTotal is not null && ImGui.MenuItem("Cancel route"))
            {
                navigator.ClearPickup();
            }

            ImGui.Separator();
            foreach (var (id, name) in navigator.GetAcceptedQuests())
            {
                if (ImGui.MenuItem(name))
                {
                    navigator.ClearPickup();
                    navigator.FollowedOverride = id;
                }
            }

            ImGui.EndPopup();
        }
    }

    /// <summary>Cross-module: hidden entirely when <see cref="UnlockChecklistModule"/> isn't
    /// registered or is disabled (task-5-brief.md delta 3).</summary>
    private void DrawUnlocksButton()
    {
        if (modules.Get<UnlockChecklistModule>() is not { Enabled: true } unlockModule)
        {
            return;
        }

        ImGui.Separator();
        Gap();
        var count = unlockModule.Unlocks.AvailableHereCount;
        var highlight = count > 0;
        var label = highlight ? $"Unlocks ({count})" : "Unlocks";
        if (highlight)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.82f, 0.25f, 1f));
        }

        if (inputMode.Mode == InputMode.Controller)
        {
            // No clickable-only affordances in controller mode (spec §4) — this becomes a
            // glanceable status line; the action moves to the context-menu surface (task A2).
            ImGui.TextUnformatted(label);
        }
        else if (ImGui.SmallButton(label))
        {
            unlockModule.Window.IsOpen = true;
        }

        if (highlight)
        {
            ImGui.PopStyleColor();
        }
    }

    /// <summary>Glanceable unlock lines (spec §4, task A3): the top 2-3 Available unlocks in the
    /// current zone, nearest-first, read straight from the already-computed
    /// <see cref="UnlockService.GlanceableHere"/> — never rescanned here. Only the distance shown
    /// per line is recomputed every frame, and that's cheap arithmetic against the live player
    /// position (the same cost model <see cref="DrawArrowTo"/> already pays), not a new scan.
    /// Absent when the module is missing/disabled, the config toggle is off, or there's simply
    /// nothing available here right now — kept subtle (small disabled-style text) under the
    /// existing content, with the same InputMode-aware spacing as the rest of the window.</summary>
    private void DrawGlanceableUnlocks()
    {
        if (modules.Get<UnlockChecklistModule>() is not { Enabled: true } unlockModule
            || !unlockModule.Config.ShowOnWidget)
        {
            return;
        }

        var here = unlockModule.Unlocks.GlanceableHere;
        if (here.Count == 0)
        {
            return;
        }

        Gap();
        var player = objects.LocalPlayer;
        foreach (var u in here)
        {
            if (player != null)
            {
                var distance = NavMath.Distance(
                    u.GiverX - player.Position.X, u.GiverY - player.Position.Y, u.GiverZ - player.Position.Z);
                ImGui.TextDisabled($"{u.Def.Unlock} ({NavMath.FormatDistance(distance)})");
            }
            else
            {
                ImGui.TextDisabled(u.Def.Unlock);
            }
        }
    }

    /// <summary>Glanceable hunting-log line (spec §4/§5): the current target's name and live kill
    /// count, read straight from the already-computed <see cref="HuntingLogModule.Hunting"/> state
    /// — never rescanned here, same "the module keeps this fresh in the background" split as
    /// <see cref="DrawGlanceableUnlocks"/>. Absent when the module is missing/disabled, the config
    /// toggle is off, or nothing is currently active.</summary>
    private void DrawGlanceableHunting()
    {
        if (modules.Get<HuntingLogModule>() is not { Enabled: true } huntingModule
            || !huntingModule.Config.ShowOnWidget)
        {
            return;
        }

        var hunting = huntingModule.Hunting;
        if (hunting.CurrentTarget is not { } target)
        {
            return;
        }

        Gap();
        var line = $"Hunting: {target.MonsterName} ({target.Killed}/{target.Required})";
        if (target.IsRoutable)
        {
            var player = objects.LocalPlayer;
            if (player != null)
            {
                var distance = NavMath.Distance(
                    target.WorldX - player.Position.X, target.WorldY - player.Position.Y, target.WorldZ - player.Position.Z);
                line += target.IsLivePosition
                    ? $" — {NavMath.FormatDistance(distance)}"
                    : $" — {NavMath.FormatDistance(distance)} (route)";
            }
        }
        else
        {
            line += $" — {target.DutyName}";
        }

        ImGui.TextDisabled(line);
    }
}
