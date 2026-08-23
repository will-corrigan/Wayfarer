using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Input;
using Wayfarer.Core.Unlocks;
using Wayfarer.Modules;

namespace Wayfarer.Windows;

internal sealed class UnlockWindow(
    IUnlockProvider unlocks,
    ModuleRegistry modules,
    IObjectTable objects,
    IClientState clientState,
    InputModeService inputMode) : Window("Unlocks###WayfarerUnlocks")
{
    // Lumina's Quest sheet offsets row ids by this amount; QuestNavigator.FollowedOverride
    // and GetAcceptedQuests() both work in the raw (unoffset) ushort id space — see
    // QuestNavigator/UnlockService, which redeclare the same constant for the same reason.
    private const uint QuestRowIdOffset = 65536;

    private static readonly string[] GroupModes = ["Zone", "Level", "Type"];
    private static readonly (string Key, string Label)[] CategoryChips =
        [("content", "Content"), ("system", "Systems"), ("cosmetic", "Cosmetics"), ("zone", "Zones")];

    private static readonly (string Key, string Label)[] PriorityChips =
        [("essential", "Essential"), ("nice", "Nice"), ("optional", "Optional")];

    private readonly FilterState filter = new();
    private int groupMode; // index into GroupModes
    private string search = string.Empty;

    /// <summary>Resolved once per <see cref="Draw"/> call: the navigator to route through when
    /// the player clicks a pickup, or null when <see cref="QuestHelperModule"/> isn't registered
    /// or is disabled (task-5-brief.md delta 3) — in which case rows and the route button fall
    /// back to a non-clickable "enable Quest Helper to navigate" state.</summary>
    private INavigationProvider? navigator;

    // OnOpen already runs on the framework thread in Dalamud's window system, so a
    // direct call is correct here (RunOnFrameworkThread would just return a completed task).
    public override void OnOpen() => unlocks.Recompute();

    // GlobalScale can change live (user drags the Dalamud UI-scale slider without restarting),
    // so the minimum size is recomputed every frame rather than fixed once in the constructor.
    public override void PreDraw() =>
        SizeConstraints = new() { MinimumSize = new(430 * ImGuiHelpers.GlobalScale, 300 * ImGuiHelpers.GlobalScale) };

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
        if (inputMode.Mode == InputMode.Controller)
        {
            ImGuiHelpers.ScaledDummy(0f, 6f);
        }

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
            UnlockStatus.InstanceLocked => 4,
            UnlockStatus.GrandCompanyLocked => 5,
            UnlockStatus.BeastTribeLocked => 6,
            UnlockStatus.MountLocked => 7,
            UnlockStatus.UnknownGate => 8,
            UnlockStatus.LockedOut => 9,
            _ => 10,
        }).ThenBy(u => u.QuestLevel);

    /// <summary>One-line explanation for each status tag icon ("[grab]"/"[accepted]"/"[locked]"/
    /// "[done]"); every other locked-flavor status shares the same "[locked]" icon in
    /// <see cref="DrawRow"/>, so it shares this text too.</summary>
    private static string StatusTagTooltip(UnlockStatus status) => status switch
    {
        UnlockStatus.Available => "Ready to pick up from the quest giver.",
        UnlockStatus.Accepted => "Picked up but not finished — check your Journal for the next step.",
        UnlockStatus.Done => "Completed.",
        UnlockStatus.LockedOut => "No longer obtainable — a quest that locks it out was completed.",
        UnlockStatus.UnknownGate => "Gated behind something this plugin can't check (e.g. a festival window or housing).",
        _ => "Locked — requirements not yet met.",
    };

    private void DrawFilterBar()
    {
        ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
        ImGui.Combo("##groupby", ref groupMode, GroupModes, GroupModes.Length);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140 * ImGuiHelpers.GlobalScale);
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
            var targets = ordered.Select(unlocks.ToPickupTarget).Where(t => t != null).Select(t => t!).ToList();
            if (targets.Count > 0)
            {
                navigator.SetRoute(targets);
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Guides you through picking up every quest shown above, nearest first. The arrow advances automatically as you accept each one.");
        }

        DrawStopButton();

        ImGui.SameLine();
        ImGui.TextDisabled("chains the arrow through every available pickup shown");
    }

    // The universal exit, mirrored from the hub window's own Stop button — this window is only
    // ever on screen when that one could not be created, and whatever engaged the arrow (a route
    // started from here, a hunt, or a single pickup) still needs a way out of it right here.
    private void DrawStopButton()
    {
        if (navigator?.Current.Engaged != true)
        {
            return;
        }

        ImGui.SameLine();
        if (ImGui.Button("Stop"))
        {
            navigator.ClearPickup();
        }
    }

    private IEnumerable<IGrouping<string, ResolvedUnlock>> GroupEntries(List<ResolvedUnlock> visible) =>
        GroupModes[groupMode] switch
        {
            "Level" => visible.GroupBy(u => $"Level {(u.QuestLevel / 10) * 10}–{((u.QuestLevel / 10) * 10) + 9}", StringComparer.Ordinal)
                               .OrderBy(g => g.Min(u => u.QuestLevel)),
            "Type" => visible.GroupBy(u => UnlockFilters.Category(u.Def), StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal),
            _ => GroupByZone(visible),
        };

    private IEnumerable<IGrouping<string, ResolvedUnlock>> GroupByZone(List<ResolvedUnlock> visible)
    {
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
            UnlockStatus.LockedOut => ("[gone]", new Vector4(0.8f, 0.4f, 0.4f, 1f)),
            UnlockStatus.UnknownGate => ("[?]", new Vector4(0.7f, 0.6f, 0.3f, 1f)),
            _ => ("[locked]", new Vector4(0.55f, 0.55f, 0.55f, 1f)),
        };
        var greyed = u.Status is not (UnlockStatus.Available or UnlockStatus.Accepted);
        if (greyed)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.55f, 1f));
        }

        ImGui.TextColored(color, icon);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(StatusTagTooltip(u.Status));
        }

        ImGui.SameLine();

        // Accepted rows carry the quest name on the row itself — the player needs to know which
        // quest to finish without hovering. Every other status keeps its quest in the tooltip
        // only, to avoid cluttering the list.
        var questSuffix = u.Status == UnlockStatus.Accepted && u.Def.Quest is { Length: > 0 } quest
            ? $" — {quest}"
            : string.Empty;
        var label = $"{u.Def.Unlock}{questSuffix}  (lv{u.QuestLevel}{(u.ZoneName is { } z ? $", {z}" : string.Empty)})##{u.QuestRowId}_{u.Def.Unlock}";
        var clicked = ImGui.Selectable(label);
        if (greyed)
        {
            ImGui.PopStyleColor();
        }

        if (ImGui.IsItemHovered())
        {
            DrawRowTooltip(u);
        }

        if (!clicked || navigator == null)
        {
            return;
        }

        if (u.Status == UnlockStatus.Available && unlocks.ToPickupTarget(u) is { } target)
        {
            navigator.SetPickup(target);
        }
        else if (u.Status == UnlockStatus.Accepted && u.QuestRowId is { } questRowId)
        {
            navigator.ClearPickup();
            navigator.FollowedOverride = (ushort)(questRowId - QuestRowIdOffset);
        }
    }

    private void DrawRowTooltip(ResolvedUnlock u)
    {
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(320 * ImGuiHelpers.GlobalScale);
        ImGui.TextUnformatted(u.Def.Description ?? u.Def.Unlock);
        if (u.Def.Quest is { } q)
        {
            ImGui.TextDisabled(u.GiverName is { Length: > 0 } giver
                ? $"Quest: {q} (from {giver})"
                : $"Quest: {q}");
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
        else if (u.Status == UnlockStatus.Accepted)
        {
            ImGui.TextDisabled(navigator != null
                ? "In your journal — click to follow it with the arrow."
                : "In your journal — enable Quest Helper to follow it with the arrow.");

            // Live objective: only available while Quest Helper is enabled
            // (navigator != null) and only once the game has published a marker for this step.
            if (navigator != null
                && u.QuestRowId is { } questRowId
                && navigator.GetAcceptedQuestObjective(questRowId - QuestRowIdOffset) is { Length: > 0 } objective)
            {
                ImGui.TextDisabled($"Next: {objective}");
            }
        }

        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
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
