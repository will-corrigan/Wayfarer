using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Wayfarer.Core.Unlocks;
using Wayfarer.Modules;

namespace Wayfarer.Windows;

/// <summary>Native (KamiToolKit <see cref="NativeAddon"/>) presentation of the unlock checklist —
/// the Controller-mode counterpart to <see cref="UnlockWindow"/> (spec §3). Same data source
/// (<see cref="IUnlockProvider"/>), same filters (group-by, Done toggle, category/priority chips —
/// no free-text search, since the design mandate rules out typing on controller), same row actions
/// (SetPickup/FollowedOverride/SetRoute through the same <see cref="INavigationProvider"/> the
/// ImGui window uses). Every component here (<see cref="TabBarNode"/>, <see cref="CheckboxNode"/>,
/// <see cref="TextButtonNode"/>) is a real native AtkUnitBase widget, so d-pad focus navigation
/// comes from the game itself — no ImGui gamepad-nav stopgap needed (task-B1-report.md).
///
/// Grouping follows task B1's verdict: a flat scroll of section-header <see cref="TextNode"/> rows
/// interleaved with entry blocks, not a collapsible tree. Because native rows have no hover
/// tooltip, every piece of info the ImGui window shows on hover (giver name, accepted quest,
/// live next objective) is rendered inline as a second, dimmed line under each entry's button
/// instead.
///
/// <see cref="NativeAddon"/> fully deallocates its node tree on every close (see its own doc
/// comments), so content is rebuilt from scratch in <see cref="OnSetup"/> on every open. While
/// open, a lightweight framework-tick poll rebuilds the list only when the underlying
/// <see cref="IUnlockProvider.Entries"/> statuses actually changed (a cheap signature comparison,
/// not a timer) — <see cref="Modules.UnlockChecklistModule"/> already keeps that data fresh in the
/// background regardless of whether this window is open, so this class only needs to notice
/// changes, not cause them. This keeps unrelated frames from tearing down and rebuilding (and
/// therefore refocusing) the list while the player is mid-navigation.</summary>
internal sealed class NativeUnlockWindow(
    IUnlockProvider unlocks,
    ModuleRegistry modules,
    IObjectTable objects,
    IClientState clientState,
    IFramework framework) : NativeAddon
{
    // Lumina's Quest sheet offsets row ids by this amount — see UnlockWindow's identical constant
    // for why FollowedOverride/GetAcceptedQuestObjective work in the raw (unoffset) ushort space.
    private const uint QuestRowIdOffset = 65536;

    private static readonly string[] GroupModes = ["Zone", "Level", "Type"];
    private static readonly (string Key, string Label)[] CategoryChips =
        [("content", "Content"), ("system", "Systems"), ("cosmetic", "Cosmetics"), ("zone", "Zones")];

    private static readonly (string Key, string Label)[] PriorityChips =
        [("essential", "Essential"), ("nice", "Nice"), ("optional", "Optional")];

    private readonly FilterState filter = new();
    private int groupMode;

    private ScrollingNode<VerticalListNode>? listArea;
    private TextButtonNode? routeButton;
    private TextNode? routeCaption;
    private int lastEntriesSignature;

    /// <inheritdoc/>
    public override void Dispose()
    {
        // Belt-and-suspenders alongside the OnFinalize unsubscribe below: NativeAddon.Close() only
        // starts the native closing animation (finishes several frames later), but Dispose() must
        // leave nothing subscribed to IFramework the moment it returns, regardless of that timing —
        // Plugin.Dispose() runs modules.Dispose() and drops every other plugin reference right
        // after this call.
        framework.Update -= OnFrameworkUpdate;
        base.Dispose();
    }

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        if (!unlocks.Loaded)
        {
            AddNode(new TextNode
            {
                Position = ContentStartPosition,
                Size = new Vector2(ContentSize.X, 40f),
                String = "Unlock data failed to load - see the Dalamud log.",
            });
            return;
        }

        var contentStart = ContentStartPosition;
        var contentSize = ContentSize;
        var y = contentStart.Y;

        y = BuildGroupTabs(contentStart, contentSize, y);
        y = BuildToggleRow(contentStart, contentSize, y);
        y = BuildRouteRow(contentStart, contentSize, y);

        listArea = new ScrollingNode<VerticalListNode>
        {
            ContentNode = { FitWidth = true, FitContents = true, ItemSpacing = 6f },
            AutoHideScrollBar = true,
            Position = new Vector2(contentStart.X, y),
            Size = new Vector2(contentSize.X, contentSize.Y - (y - contentStart.Y)),
        };
        AddNode(listArea);

        RebuildList();

        framework.Update += OnFrameworkUpdate;
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon) => framework.Update -= OnFrameworkUpdate;

    private static CheckboxNode BuildChipCheckbox(string label, bool isChecked, Action<bool> onClick) => new()
    {
        Height = 20f,
        String = label,
        IsChecked = isChecked,
        OnClick = onClick,
    };

    private static AlignedHorizontalListNode BuildFilterRow(
        Vector2 contentStart, Vector2 contentSize, float y, IEnumerable<CheckboxNode> chips)
    {
        var row = new AlignedHorizontalListNode
        {
            Position = new Vector2(contentStart.X, y),
            Size = new Vector2(contentSize.X, 22f),
            FitToContentHeight = true,
            ItemSpacing = 10f,
        };

        foreach (var chip in chips)
        {
            row.AddNode(chip);
        }

        return row;
    }

    private static void ToggleMembership(HashSet<string> set, string key, bool isOn)
    {
        if (isOn)
        {
            set.Add(key);
        }
        else
        {
            set.Remove(key);
        }
    }

    private static TextNode BuildHeaderNode(string text) => new()
    {
        Height = 22f,
        FontSize = 15,
        TextColor = new Vector4(0.9f, 0.72f, 0.25f, 1f),
        String = text,
    };

    private static (string Label, Vector4 Color) StatusPresentation(UnlockStatus status) => status switch
    {
        UnlockStatus.Done => ("Done", new Vector4(0.5f, 0.8f, 0.5f, 1f)),
        UnlockStatus.Accepted => ("Accepted", new Vector4(0.6f, 0.8f, 1f, 1f)),
        UnlockStatus.Available => ("Available", new Vector4(1f, 0.82f, 0.25f, 1f)),
        UnlockStatus.LockedOut => ("Gone", new Vector4(0.8f, 0.4f, 0.4f, 1f)),
        UnlockStatus.UnknownGate => ("Unknown", new Vector4(0.7f, 0.6f, 0.3f, 1f)),
        _ => ("Locked", new Vector4(0.55f, 0.55f, 0.55f, 1f)),
    };

    /// <summary>Builds the dimmed second line under a row's button: giver name (any status), the
    /// accepted quest's own name, and its live next objective — the same three facts
    /// <see cref="UnlockWindow.DrawRowTooltip"/> shows on hover, folded inline since native rows
    /// have no tooltip.</summary>
    private static string? BuildCaption(ResolvedUnlock u, INavigationProvider? navigator)
    {
        var parts = new List<string>();
        if (u.GiverName is { Length: > 0 } giver)
        {
            parts.Add($"From {giver}");
        }

        if (u.Status == UnlockStatus.Accepted && u.Def.Quest is { Length: > 0 } quest)
        {
            parts.Add($"— {quest}");
        }

        if (u.Status == UnlockStatus.Accepted
            && navigator != null
            && u.QuestRowId is { } questRowId
            && navigator.GetAcceptedQuestObjective(questRowId - QuestRowIdOffset) is { Length: > 0 } objective)
        {
            parts.Add($"Next: {objective}");
        }

        return parts.Count > 0 ? string.Join("\n", parts) : null;
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

    private float BuildGroupTabs(Vector2 contentStart, Vector2 contentSize, float y)
    {
        var tabs = new TabBarNode { Position = new Vector2(contentStart.X, y), Size = new Vector2(contentSize.X, 24f) };
        for (var i = 0; i < GroupModes.Length; i++)
        {
            var index = i; // captured per-tab, not the loop variable
            tabs.AddTab(GroupModes[i], () =>
            {
                groupMode = index;
                RebuildList();
            });
        }

        AddNode(tabs);
        return y + tabs.Height + 6f;
    }

    private float BuildToggleRow(Vector2 contentStart, Vector2 contentSize, float y)
    {
        var doneAndCategory = BuildFilterRow(contentStart, contentSize, y, BuildDoneAndCategoryChips());
        AddNode(doneAndCategory);
        y += doneAndCategory.Height + 4f;

        var priority = BuildFilterRow(contentStart, contentSize, y, BuildPriorityChips());
        AddNode(priority);
        return y + priority.Height + 8f;
    }

    private IEnumerable<CheckboxNode> BuildDoneAndCategoryChips()
    {
        yield return BuildChipCheckbox("Done", filter.ShowDone, isOn =>
        {
            filter.ShowDone = isOn;
            RebuildList();
        });

        foreach (var (key, label) in CategoryChips)
        {
            yield return BuildChipCheckbox(label, filter.Categories.Contains(key), isOn =>
            {
                ToggleMembership(filter.Categories, key, isOn);
                RebuildList();
            });
        }
    }

    private IEnumerable<CheckboxNode> BuildPriorityChips()
    {
        foreach (var (key, label) in PriorityChips)
        {
            yield return BuildChipCheckbox(label, filter.Priorities.Contains(key), isOn =>
            {
                ToggleMembership(filter.Priorities, key, isOn);
                RebuildList();
            });
        }
    }

    private float BuildRouteRow(Vector2 contentStart, Vector2 contentSize, float y)
    {
        routeButton = new TextButtonNode
        {
            Position = new Vector2(contentStart.X, y),
            Size = new Vector2(150f, 26f),
            String = "Route me (0)",
            OnClick = OnRouteClicked,
        };
        AddNode(routeButton);

        routeCaption = new TextNode
        {
            Position = new Vector2(contentStart.X + 160f, y + 5f),
            Size = new Vector2(contentSize.X - 160f, 20f),
            FontSize = 11,
            TextColor = new Vector4(0.65f, 0.65f, 0.65f, 1f),
            String = "chains the arrow through every available pickup shown",
        };
        AddNode(routeCaption);

        return y + 32f;
    }

    /// <summary>Re-evaluated fresh on every call — mirrors <see cref="UnlockWindow"/>'s navigator
    /// field being recomputed every ImGui frame, so a Quest Helper toggle flip between
    /// window-opens is picked up on the very next click/rebuild rather than only on the next
    /// background poll. Returns the concrete type rather than <see cref="INavigationProvider"/>
    /// (CA1859) — every caller only ever consumes it through that interface's members anyway.</summary>
    private QuestNavigator? ResolveNavigator() =>
        modules.Get<QuestHelperModule>() is { Enabled: true } questHelper ? questHelper.Navigator : null;

    private List<ResolvedUnlock> ComputeVisible() =>
        [.. unlocks.Entries.Where(u => u.Status != UnlockStatus.Unverified && UnlockFilters.Matches(u, filter))];

    private void OnFrameworkUpdate(IFramework fw)
    {
        if (ComputeEntriesSignature() == lastEntriesSignature)
        {
            return;
        }

        RebuildList();
    }

    private int ComputeEntriesSignature()
    {
        unchecked
        {
            var hash = 17;
            foreach (var u in unlocks.Entries)
            {
                hash = (hash * 31) + (int)u.Status;
            }

            return hash;
        }
    }

    private void RebuildList()
    {
        if (listArea is null)
        {
            return;
        }

        var navigator = ResolveNavigator();
        var visible = ComputeVisible();

        UpdateRouteRow(visible, navigator);

        listArea.ContentNode.Clear();
        foreach (var group in GroupEntries(visible))
        {
            listArea.ContentNode.AddNode(BuildHeaderNode($"{group.Key} ({group.Count()})"));
            foreach (var u in OrderInGroup(group))
            {
                listArea.ContentNode.AddNode(BuildRowNode(u, navigator));
            }
        }

        AddUnverifiedSection();

        if (listArea.ContentNode.Nodes.Count == 0)
        {
            listArea.ContentNode.AddNode(new TextNode
            {
                Height = 22f,
                String = "No resolved unlocks yet - open the checklist once more to force a recompute.",
            });
        }

        listArea.RecalculateSizes();
        lastEntriesSignature = ComputeEntriesSignature();
    }

    private void UpdateRouteRow(List<ResolvedUnlock> visible, INavigationProvider? navigator)
    {
        if (routeButton is null || routeCaption is null)
        {
            return;
        }

        var routable = visible.Where(u => u.Status == UnlockStatus.Available && u.GiverTerritory != null).ToList();
        routeButton.String = $"Route me ({routable.Count})";
        routeButton.IsEnabled = navigator != null && routable.Count > 0;
        routeCaption.String = navigator == null
            ? "enable Quest Helper to navigate"
            : "chains the arrow through every available pickup shown";
    }

    private void OnRouteClicked()
    {
        var navigator = ResolveNavigator();
        if (navigator is null)
        {
            return;
        }

        var routable = ComputeVisible().Where(u => u.Status == UnlockStatus.Available && u.GiverTerritory != null).ToList();
        if (routable.Count == 0)
        {
            return;
        }

        var player = objects.LocalPlayer;
        var ordered = RoutePlanner.Order(routable, clientState.TerritoryType, player?.Position.X ?? 0, player?.Position.Z ?? 0);
        var targets = ordered.Select(unlocks.ToPickupTarget).Where(t => t != null).Select(t => t!).ToList();
        if (targets.Count > 0)
        {
            navigator.SetRoute(targets);
        }
    }

    private VerticalListNode BuildRowNode(ResolvedUnlock u, INavigationProvider? navigator)
    {
        var row = new VerticalListNode { FitWidth = true, FitContents = true, ItemSpacing = 2f };

        var (label, color) = StatusPresentation(u.Status);
        var button = new TextButtonNode
        {
            Height = 24f,
            String = $"{label}  {u.Def.Unlock}  (lv{u.QuestLevel}{(u.ZoneName is { } z ? $", {z}" : string.Empty)})",
            OnClick = () => OnRowClicked(u),
        };
        button.LabelNode.TextColor = color;
        row.AddNode(button);

        if (BuildCaption(u, navigator) is { } caption)
        {
            var lines = caption.Split('\n');
            row.AddNode(new TextNode
            {
                Height = (lines.Length * 13f) + 4f,
                FontSize = 11,
                LineSpacing = 13,
                TextColor = new Vector4(0.65f, 0.65f, 0.65f, 1f),
                String = caption,
            });
        }

        return row;
    }

    private void OnRowClicked(ResolvedUnlock u)
    {
        var navigator = ResolveNavigator();
        if (navigator is null)
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

    private void AddUnverifiedSection()
    {
        var unverified = unlocks.Entries.Where(u => u.Status == UnlockStatus.Unverified).ToList();
        if (unverified.Count == 0 || listArea is null)
        {
            return;
        }

        listArea.ContentNode.AddNode(BuildHeaderNode($"Unverified ({unverified.Count})"));
        foreach (var u in unverified)
        {
            listArea.ContentNode.AddNode(new TextNode
            {
                Height = 18f,
                FontSize = 11,
                TextColor = new Vector4(0.6f, 0.6f, 0.6f, 1f),
                String = $"{u.Def.Unlock} (lv{u.Def.Level})",
            });
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
        var currentZone = CurrentZoneName();
        return visible.GroupBy(u => u.ZoneName ?? "Unknown location", StringComparer.Ordinal)
                      .OrderByDescending(g => string.Equals(g.Key, currentZone, StringComparison.Ordinal))
                      .ThenBy(g => g.Key, StringComparer.Ordinal);
    }

    private string? CurrentZoneName()
    {
        var here = clientState.TerritoryType;
        return unlocks.Entries.FirstOrDefault(u => u.GiverTerritory == here)?.ZoneName;
    }
}
