using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Wayfarer.Core.Navigation;
using Wayfarer.Modules;

namespace Wayfarer.Windows;

internal sealed unsafe class ArrowWindow : Window
{
    private const ImGuiWindowFlags SharedFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoFocusOnAppearing;

    private static readonly Vector4 LinkColor = new(0.4f, 0.7f, 1f, 1f);

    private readonly INavigationProvider navigator;
    private readonly ModuleRegistry modules;
    private readonly QuestHelperConfig cfg;
    private readonly IObjectTable objects;
    private readonly IClientState clientState;
    private readonly IPluginLog log;

    public ArrowWindow(
        INavigationProvider navigator,
        ModuleRegistry modules,
        QuestHelperConfig cfg,
        IObjectTable objects,
        IClientState clientState,
        IPluginLog log)
        : base("###WayfarerArrow")
    {
        this.navigator = navigator;
        this.modules = modules;
        this.cfg = cfg;
        this.objects = objects;
        this.clientState = clientState;
        this.log = log;
        RespectCloseHotkey = false; // Esc must not close a HUD widget
        IsOpen = true;              // visibility is governed by DrawConditions
        Flags = BaseFlags;
    }

    // AlwaysAutoResize keeps the window snug by default; opting out (AutoSizeWidget =
    // false) makes it manually resizable — its size then persists via ImGui's window
    // ID the same way its position already does — and lets long text wrap instead of
    // stretching the widget.
    private ImGuiWindowFlags BaseFlags =>
        cfg.AutoSizeWidget ? SharedFlags | ImGuiWindowFlags.AlwaysAutoResize : SharedFlags;

    public override bool DrawConditions() =>
        !cfg.WidgetHidden && !string.Equals(navigator.Current.Mode, NavigationState.Modes.Hidden, StringComparison.Ordinal);

    public override void PreDraw() =>
        Flags = cfg.ArrowLocked ? BaseFlags | ImGuiWindowFlags.NoMove : BaseFlags;

    public override void Draw()
    {
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

        ImGui.Spacing();
        DrawQuestLine(state);
        DrawUnlocksButton();
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
    // When the duty can be queued right now, DutyContentFinderConditionId is set and
    // the reason follows the fixed "Complete the duty: {name} — queue via Duty
    // Finder" template — split on those markers so the duty name alone renders as a
    // clickable link; every other case (not yet unlocked) just wraps the plain text.
    private static void DrawDutyObjective(NavigationState state)
    {
        if (state.Reason is not { } reason)
        {
            return;
        }

        if (state.DutyContentFinderConditionId is { } cfcId
            && reason.StartsWith(DutyObjectiveGuidance.CompleteDutyPrefix, StringComparison.Ordinal)
            && reason.EndsWith(DutyObjectiveGuidance.CompleteDutySuffix, StringComparison.Ordinal))
        {
            var name = reason[DutyObjectiveGuidance.CompleteDutyPrefix.Length..^DutyObjectiveGuidance.CompleteDutySuffix.Length];

            ImGui.PushTextWrapPos(0f);
            ImGui.TextUnformatted(DutyObjectiveGuidance.CompleteDutyPrefix);
            ImGui.SameLine(0, 0);
            DrawDutyLink(name, cfcId);
            ImGui.SameLine(0, 0);
            ImGui.TextUnformatted(DutyObjectiveGuidance.CompleteDutySuffix);
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

        var size = 48f * cfg.ArrowScale;
        var width = MathF.Max(ImGui.CalcTextSize(navigator.Current.QuestName ?? string.Empty).X, size + 16f);
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
            if (state.EntranceX is null)
            {
                ImGui.TextWrapped($"Objective is in {state.ZoneName ?? "another zone"} — no route found.");
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
        ImGui.Spacing();
        var count = unlockModule.Unlocks.AvailableHereCount;
        var highlight = count > 0;
        if (highlight)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.82f, 0.25f, 1f));
        }

        if (ImGui.SmallButton(highlight ? $"Unlocks ({count})" : "Unlocks"))
        {
            unlockModule.Window.IsOpen = true;
        }

        if (highlight)
        {
            ImGui.PopStyleColor();
        }
    }
}
