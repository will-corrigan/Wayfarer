using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Input;
using Wayfarer.Core.Unlocks;
using Wayfarer.Modules;

namespace Wayfarer.Windows;

/// <summary>The ImGui rendering of the unlock checklist — same data and same row actions as the
/// Checklist tab of <see cref="NativeHubWindow"/>.
///
/// This is a <b>fallback surface</b>, not a destination: the native window serves mouse and
/// controller alike, and everything opens that. This exists for the case where it cannot be created
/// at all, so the checklist is never simply unreachable.</summary>
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
            ImGui.TextWrapped("The unlock catalogue could not be read.");
            if (unlocks.LoadError is { Length: > 0 } why)
            {
                ImGui.TextWrapped(why);
            }

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

    /// <summary>What to show where a level would go. An entry with no level is not level 0 — no
    /// source states a level for it — so it is labelled with what it is instead.</summary>
    private static string LevelOrCategory(UnlockDefinition d) =>
        d.Level is { } lv ? $"lv{lv}" : d.Category ?? "no level requirement";

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
            UnlockStatus.CollectionLocked => 8,
            UnlockStatus.RequirementsUnknown => 9,
            UnlockStatus.UnknownGate => 10,
            UnlockStatus.LockedOut => 11,
            _ => 12,
        }).ThenBy(u => u.QuestLevel);

    /// <summary>One-line explanation for each status tag icon ("[grab]"/"[accepted]"/"[locked]"/
    /// "[done]"); every other locked-flavor status shares the same "[locked]" icon in
    /// <see cref="DrawRow"/>, so it shares this text too.</summary>
    private static string StatusTagTooltip(UnlockStatus status) => status switch
    {
        UnlockStatus.Available => "Ready to pick up.",
        UnlockStatus.Accepted => "In progress. See your Journal.",
        UnlockStatus.Done => "Completed.",
        UnlockStatus.LockedOut => "No longer obtainable.",
        UnlockStatus.CollectionLocked => "Needs a set of collectibles. Hover for the list.",
        UnlockStatus.RequirementsUnknown => "Requirements unknown. Treat as not available.",
        UnlockStatus.UnknownGate => "Requirements unknown. Treat as not available.",
        _ => "Locked.",
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
        if (ImGui.Checkbox("Complete", ref showDone))
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
            ImGui.TextDisabled($"Route Me ({routable.Count}) — enable Quest Helper");
            return;
        }

        if (ImGui.Button($"Route Me ({routable.Count})") && routable.Count > 0)
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
            ImGui.SetTooltip("Walks every quest above, nearest first.");
        }

        DrawStopButton();

        ImGui.SameLine();
        ImGui.TextDisabled("nearest first");
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
            // An entry with no level gets its own section rather than a level band. Its quest row
            // is a hidden level-1 reward row, so banding it would file the Extreme-trial trophy
            // mounts under "Level 0–9" — a claim no source makes and the player would have to
            // scroll past. Those sections sort after every level band.
            "Level" => visible.GroupBy(
                    u => u.Def.Level is null
                        ? u.Def.Category ?? "No level requirement"
                        : $"Level {(u.QuestLevel / 10) * 10}–{((u.QuestLevel / 10) * 10) + 9}",
                    StringComparer.Ordinal)
                .OrderBy(g => g.Any(u => u.Def.Level is null) ? 1 : 0)
                .ThenBy(g => g.Min(u => u.QuestLevel)),
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

            // Both "we don't know" states share the existing unknown-gate amber; the tag text is
            // what separates them, so no new colour has to be learned.
            UnlockStatus.UnknownGate or UnlockStatus.RequirementsUnknown => ("[?]", new Vector4(0.7f, 0.6f, 0.3f, 1f)),
            UnlockStatus.CollectionLocked => ("[collect]", new Vector4(0.55f, 0.55f, 0.55f, 1f)),
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

        // The whole missing list, not just the blocker the reason names: being told you need
        // "Rose Lanner" when you need seven mounts is the same failure in miniature.
        if (u.Status == UnlockStatus.CollectionLocked && u.MissingRequirements.Count > 1)
        {
            foreach (var missing in u.MissingRequirements)
            {
                ImGui.BulletText(missing);
            }
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
                    ? "Click to be guided there."
                    : "Enable Quest Helper to navigate.");
        }
        else if (u.Status == UnlockStatus.Accepted)
        {
            ImGui.TextDisabled(navigator != null
                ? "In your journal — click to follow it."
                : "In your journal — enable Quest Helper to follow it.");

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

        ImGui.TextWrapped("Nothing in the game's data backs these up, so they cannot be checked.");
        foreach (var u in unverified)
        {
            ImGui.BulletText($"{u.Def.Unlock} ({LevelOrCategory(u.Def)})");
            if (!ImGui.IsItemHovered())
            {
                continue;
            }

            // The catalogue records why it can't verify this one — show that first; it is the
            // difference between "we have no idea" and "you unlock it by talking to an NPC".
            var tip = u.Def.Requires?.Label ?? u.Def.Description ?? u.Def.Notes;
            if (tip is { Length: > 0 })
            {
                ImGui.SetTooltip(tip);
            }
        }
    }
}
