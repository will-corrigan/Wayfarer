using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Input;
using Wayfarer.Core.Navigation;
using Wayfarer.Modules;

namespace Wayfarer.Windows;

/// <summary>ImGui (Mouse-mode) presentation of the hunting log — same data source
/// (<see cref="HuntingLogService"/>), same row actions (SetPickup/SetRoute through
/// <see cref="INavigationProvider"/>) as the Hunting Log tab of <see cref="NativeHubWindow"/>, the
/// Controller-mode counterpart (spec §5). Duty-gated (non-routable) Grand Company Elite targets
/// render their duty name as a clickable "Open in Duty Finder" link instead of a Go button,
/// mirroring <see cref="ArrowWindow"/>'s own duty-objective link.</summary>
internal sealed class HuntingWindow(
    HuntingLogService hunting,
    ModuleRegistry modules,
    IObjectTable objects) : Window("Hunting Log###WayfarerHunting")
{
    private static readonly Vector4 LinkColor = new(0.4f, 0.7f, 1f, 1f);

    public override void OnOpen() => hunting.Recompute();

    public override void PreDraw() =>
        SizeConstraints = new() { MinimumSize = new(360 * ImGuiHelpers.GlobalScale, 260 * ImGuiHelpers.GlobalScale) };

    public override void Draw()
    {
        if (!hunting.Loaded)
        {
            ImGui.TextWrapped("Hunting log data failed to load — see the Dalamud log.");
            return;
        }

        if (hunting.ActiveLogLabel is not { } label)
        {
            ImGui.TextWrapped(hunting.NoLogReason ?? "No hunting log active.");
            return;
        }

        ImGui.TextUnformatted($"{label} — rank {hunting.CurrentRank}");
        ImGui.Separator();

        if (hunting.RemainingOnPage.Count == 0)
        {
            ImGui.TextWrapped("Nothing remaining on this page.");
            return;
        }

        DrawHuntHereButton();
        ImGui.Separator();

        if (ImGui.BeginChild("huntinglist"))
        {
            foreach (var target in hunting.HuntHereOrder)
            {
                DrawRow(target);
            }

            // Remaining targets not resolved into HuntHereOrder (different zone, or duty-gated) —
            // still shown, just without a distance.
            var shown = hunting.HuntHereOrder.Select(t => t.Monster).ToHashSet();
            if (hunting.CurrentTarget is { } current && !shown.Contains(current.Monster))
            {
                DrawRow(current);
            }
        }

        ImGui.EndChild();
    }

    private static void DrawDutyLink(string name, uint? cfcId)
    {
        if (cfcId is not { } id)
        {
            ImGui.TextDisabled(name);
            return;
        }

        ImGui.PushStyleColor(ImGuiCol.Text, LinkColor);
        ImGui.TextUnformatted(name);
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Open in Duty Finder");
        }

        if (ImGui.IsItemClicked())
        {
            DutyFinderAction.Execute(id);
        }
    }

    // The universal exit, mirrored from the hub window's own Stop button — this window is only
    // ever on screen when that one could not be created, and a hunt started from here (or a route
    // or single pickup started elsewhere) still needs a way out of it right here.
    private static void DrawStopButton(QuestNavigator navigator)
    {
        if (!navigator.Current.Engaged)
        {
            return;
        }

        ImGui.SameLine();
        if (ImGui.Button("Stop"))
        {
            navigator.ClearPickup();
        }
    }

    // Returns the concrete type rather than INavigationProvider (CA1859) — the only caller
    // (DrawRow/DrawHuntHereButton) consumes it purely through that interface's members anyway;
    // same reasoning as NativeHubWindow.ResolveNavigator.
    private QuestNavigator? ResolveNavigator() =>
        modules.Get<QuestHelperModule>() is { Enabled: true } questHelper ? questHelper.Navigator : null;

    private void DrawHuntHereButton()
    {
        var navigator = ResolveNavigator();
        var count = hunting.HuntHereOrder.Count;

        // "Hunt here" was the old label from when chaining stopped at the zone boundary, which is
        // also why it appeared to do nothing in a zone with nothing left. A hunt now works through
        // the whole rank, grouped by zone, so the label says what it does.
        if (navigator == null)
        {
            ImGui.TextDisabled($"Start hunting ({count}) — enable Quest Helper to navigate");
            return;
        }

        if (ImGui.Button($"Start hunting ({count})") && count > 0)
        {
            var targets = hunting.HuntHereOrder.Select(hunting.ToPickupTarget).Where(t => t != null).Select(t => t!).ToList();
            if (targets.Count > 0)
            {
                navigator.SetRoute(targets);
            }
        }

        DrawStopButton(navigator);
    }

    private void DrawRow(HuntingTargetView target)
    {
        ImGui.TextUnformatted($"{target.MonsterName}  ({target.Killed}/{target.Required})");

        if (!target.IsRoutable)
        {
            ImGui.SameLine();
            DrawDutyLink(target.DutyName ?? "an instanced duty", target.DutyContentFinderConditionId);
            return;
        }

        var player = objects.LocalPlayer;
        if (player != null)
        {
            var distance = NavMath.Distance(target.WorldX - player.Position.X, target.WorldY - player.Position.Y, target.WorldZ - player.Position.Z);
            ImGui.SameLine();
            ImGui.TextDisabled(target.IsLivePosition ? $"{NavMath.FormatDistance(distance)} (live)" : NavMath.FormatDistance(distance));
        }

        var navigator = ResolveNavigator();
        if (navigator == null)
        {
            return;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton($"Go##{target.Monster.BNpcNameId}") && hunting.ToPickupTarget(target) is { } pickup)
        {
            navigator.SetPickup(pickup);
        }
    }
}
