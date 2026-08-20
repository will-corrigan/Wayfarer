using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Unlocks;
using Wayfarer.Modules;

namespace Wayfarer.Windows;

internal sealed class UnlockWindow : Window
{
    private static readonly string[] GroupModes = ["Zone", "Level", "Type"];
    private static readonly (string Key, string Label)[] CategoryChips =
        [("content", "Content"), ("system", "Systems"), ("cosmetic", "Cosmetics"), ("zone", "Zones")];

    private static readonly (string Key, string Label)[] PriorityChips =
        [("essential", "Essential"), ("nice", "Nice"), ("optional", "Optional")];

    private readonly UnlockService unlocks;
    private readonly ModuleRegistry modules;
    private readonly IObjectTable objects;
    private readonly IClientState clientState;
    private readonly FilterState filter = new();
    private int groupMode; // index into GroupModes
    private string search = string.Empty;

    /// <summary>Resolved once per <see cref="Draw"/> call: the navigator to route through when
    /// the player clicks a pickup, or null when <see cref="QuestHelperModule"/> isn't registered
    /// or is disabled (task-5-brief.md delta 3) — in which case rows and the route button fall
    /// back to a non-clickable "enable Quest Helper to navigate" state.</summary>
    private QuestNavigator? navigator;

    public UnlockWindow(UnlockService unlocks, ModuleRegistry modules, IObjectTable objects, IClientState clientState)
        : base("Unlocks###WayfarerUnlocks")
    {
        this.unlocks = unlocks;
        this.modules = modules;
        this.objects = objects;
        this.clientState = clientState;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(430, 300) };
    }

    // OnOpen already runs on the framework thread in Dalamud's window system, so a
    // direct call is correct here (RunOnFrameworkThread would just return a completed task).
    public override void OnOpen() => unlocks.Recompute();

    public override void Draw()
    {
        if (!unlocks.Loaded)
        {
            ImGui.TextWrapped("Unlock data failed to load — see the Dalamud log.");
            return;
        }

        navigator = modules.Get<QuestHelperModule>() is { Enabled: true } questHelper
            ? questHelper.Navigator
            : null;

        DrawFilterBar();
        ImGui.Separator();

        var visible = unlocks.Entries
            .Where(u => u.Status != UnlockStatus.Unverified && UnlockFilters.Matches(u, filter))
            .ToList();

        DrawRouteButton(visible);
        ImGui.Separator();

        if (ImGui.BeginChild("unlocklist"))
        {
            foreach (var group in GroupEntries(visible))
            {
                if (!ImGui.CollapsingHeader(
                    $"{group.Key} ({group.Count()})###grp{group.Key}",
                    ImGuiTreeNodeFlags.DefaultOpen))
                {
                    continue;
                }

                foreach (var u in OrderInGroup(group))
                {
                    DrawRow(u);
                }
            }

            DrawUnverified();
        }

        ImGui.EndChild();
    }

    private static void DrawChips((string Key, string Label)[] chips, HashSet<string> active)
    {
        var first = true;
        foreach (var (key, label) in chips)
        {
            if (!first)
            {
                ImGui.SameLine();
            }

            first = false;
            var on = active.Contains(key);
            if (on)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.9f, 0.72f, 0.25f, 0.6f));
            }

            if (ImGui.SmallButton(label))
            {
                if (on)
                {
                    active.Remove(key);
                }
                else
                {
                    active.Add(key);
                }
            }

            if (on)
            {
                ImGui.PopStyleColor();
            }
        }
    }

    private static IEnumerable<ResolvedUnlock> OrderInGroup(IEnumerable<ResolvedUnlock> group) =>
        group.OrderBy(u => u.Status switch
        {
            UnlockStatus.Available => 0,
            UnlockStatus.Accepted => 1,
            UnlockStatus.QuestLocked => 2,
            UnlockStatus.LevelLocked => 3,
            _ => 4,
        }).ThenBy(u => u.QuestLevel);

    private void DrawFilterBar()
    {
        ImGui.SetNextItemWidth(90);
        ImGui.Combo("##groupby", ref groupMode, GroupModes, GroupModes.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140);
        if (ImGui.InputTextWithHint("##search", "search...", ref search, 64))
        {
            filter.Search = search;
        }

        ImGui.SameLine();
        var showDone = filter.ShowDone;
        if (ImGui.Checkbox("Done", ref showDone))
        {
            filter.ShowDone = showDone;
        }

        DrawChips(CategoryChips, filter.Categories);
        ImGui.SameLine();
        ImGui.TextDisabled("|");
        ImGui.SameLine();
        DrawChips(PriorityChips, filter.Priorities);
    }

    private void DrawRouteButton(List<ResolvedUnlock> visible)
    {
        var routable = visible.Where(u => u.Status == UnlockStatus.Available && u.GiverTerritory != null).ToList();
        if (navigator == null)
        {
            ImGui.TextDisabled($"Route me ({routable.Count}) — enable Quest Helper to navigate");
            return;
        }

        if (ImGui.Button($"Route me ({routable.Count})") && routable.Count > 0)
        {
            var player = objects.LocalPlayer;
            var ordered = RoutePlanner.Order(
                routable,
                clientState.TerritoryType,
                player?.Position.X ?? 0,
                player?.Position.Z ?? 0);
            var targets = ordered.Select(UnlockService.ToPickupTarget).Where(t => t != null).Select(t => t!).ToList();
            if (targets.Count > 0)
            {
                navigator.SetRoute(targets);
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled("chains the arrow through every available pickup shown");
    }

    private IEnumerable<IGrouping<string, ResolvedUnlock>> GroupEntries(List<ResolvedUnlock> visible)
    {
        if (string.Equals(GroupModes[groupMode], "Level", StringComparison.Ordinal))
        {
            return visible.GroupBy(u => $"Level {(u.QuestLevel / 10) * 10}–{((u.QuestLevel / 10) * 10) + 9}", StringComparer.Ordinal)
                          .OrderBy(g => g.Min(u => u.QuestLevel));
        }

        if (string.Equals(GroupModes[groupMode], "Type", StringComparison.Ordinal))
        {
            return visible.GroupBy(u => UnlockFilters.Category(u.Def), StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal);
        }

        var currentZone = CurrentZoneName(); // hoisted: one scan of Entries per Draw, not per zone group
        return visible.GroupBy(u => u.ZoneName ?? "Unknown location", StringComparer.Ordinal)
                      .OrderByDescending(g => string.Equals(g.Key, currentZone, StringComparison.Ordinal))
                      .ThenBy(g => g.Key, StringComparer.Ordinal);
    }

    private string? CurrentZoneName()
    {
        var here = clientState.TerritoryType;
        return unlocks.Entries.FirstOrDefault(u => u.GiverTerritory == here)?.ZoneName;
    }

    private void DrawRow(ResolvedUnlock u)
    {
        var (icon, color) = u.Status switch
        {
            UnlockStatus.Done => ("[done]", new Vector4(0.5f, 0.8f, 0.5f, 1f)),
            UnlockStatus.Accepted => ("[accepted]", new Vector4(0.6f, 0.8f, 1f, 1f)),
            UnlockStatus.Available => ("[grab]", new Vector4(1f, 0.82f, 0.25f, 1f)),
            _ => ("[locked]", new Vector4(0.55f, 0.55f, 0.55f, 1f)),
        };
        var greyed = u.Status is UnlockStatus.LevelLocked or UnlockStatus.QuestLocked or UnlockStatus.Done;
        if (greyed)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.55f, 1f));
        }

        ImGui.TextColored(color, icon);
        ImGui.SameLine();
        var label = $"{u.Def.Unlock}  (lv{u.QuestLevel}{(u.ZoneName is { } z ? $", {z}" : string.Empty)})##{u.QuestRowId}_{u.Def.Unlock}";
        var clicked = ImGui.Selectable(label);
        if (greyed)
        {
            ImGui.PopStyleColor();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(320);
            ImGui.TextUnformatted(u.Def.Description ?? u.Def.Unlock);
            if (u.Def.Quest is { } q)
            {
                ImGui.TextDisabled($"Quest: {q}");
            }

            if (u.LockReason is { } reason)
            {
                ImGui.TextDisabled(reason);
            }

            if (u.Def.Notes is { } notes)
            {
                ImGui.TextDisabled(notes);
            }

            if (u.Status == UnlockStatus.Available)
            {
                ImGui.TextDisabled(u.GiverTerritory == null
                    ? "Location unknown — find the quest giver manually."
                    : navigator != null
                        ? "Click to have the arrow guide you there."
                        : "Enable Quest Helper to navigate.");
            }

            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        if (clicked && u.Status == UnlockStatus.Available && navigator != null && UnlockService.ToPickupTarget(u) is { } target)
        {
            navigator.SetPickup(target);
        }
    }

    private void DrawUnverified()
    {
        var unverified = unlocks.Entries.Where(u => u.Status == UnlockStatus.Unverified).ToList();
        if (unverified.Count == 0)
        {
            return;
        }

        if (!ImGui.CollapsingHeader($"Unverified ({unverified.Count})###grpUnverified"))
        {
            return;
        }

        ImGui.TextWrapped("These wiki entries have no quest name or one that doesn't match game data; statuses can't be checked.");
        foreach (var u in unverified)
        {
            ImGui.BulletText($"{u.Def.Unlock} (lv{u.Def.Level})");
            if (ImGui.IsItemHovered() && (u.Def.Description ?? u.Def.Notes) is { } tip)
            {
                ImGui.SetTooltip(tip);
            }
        }
    }
}
