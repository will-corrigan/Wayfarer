using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Wayfarer.Core.Navigation;
using Wayfarer.Modules;

namespace Wayfarer.Windows;

internal sealed unsafe class ArrowWindow : Window
{
    private const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoFocusOnAppearing;

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
    private static void DrawDutyObjective(NavigationState state)
    {
        if (state.Reason is { } reason)
        {
            ImGui.TextWrapped(reason);
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
            ImGui.TextDisabled(zone);
        }
    }

    private void DrawQuestLine(NavigationState state)
    {
        ImGui.Separator();
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
