using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Unlocks;
using Wayfarer.Modules;

namespace Wayfarer.Windows;

/// <summary>Single native (KamiToolKit <see cref="NativeAddon"/>) window that is the Controller-mode
/// home for the whole plugin (spec: controller wave task 4) — replaces the earlier separate
/// per-module native windows with one hub carrying a native <see cref="TabBarNode"/>
/// (Checklist | Hunting Log | Settings), so d-pad focus navigation comes from the game itself, same
/// as the tabs it replaces. Owned directly by <see cref="Plugin"/> (not by either module) since it
/// now serves both; <see cref="UnlockChecklistModule"/> and <see cref="HuntingLogModule"/> only ever
/// call <see cref="OpenTab"/> to land on their own tab.
///
/// Every piece of content below is a straight port of the former <c>NativeUnlockWindow</c> /
/// <c>NativeHuntingWindow</c> classes' row-building logic, unchanged in behavior, just re-homed
/// under one addon with a bucketed <see cref="NodeBase.IsVisible"/> toggle per tab instead of one
/// window each. <see cref="NativeAddon"/> fully deallocates its node tree on every close, so all
/// three tabs' content is rebuilt from scratch in <see cref="OnSetup"/> on every open, then the
/// active tab's list is force-refreshed again on every <see cref="SelectTab"/> switch so stale data
/// never lingers behind a tab that was built once and left alone.</summary>
internal sealed unsafe class NativeHubWindow(
    IUnlockProvider unlocks,
    HuntingLogService hunting,
    ModuleRegistry modules,
    IObjectTable objects,
    IClientState clientState,
    IFramework framework,
    Configuration config,
    Action saveConfig,
    IPluginLog log) : NativeAddon
{
    // Lumina's Quest sheet offsets row ids by this amount — see UnlockWindow's identical constant
    // for why FollowedOverride/GetAcceptedQuestObjective work in the raw (unoffset) ushort space.
    private const uint QuestRowIdOffset = 65536;

    private static readonly string[] GroupModes = ["Zone", "Level", "Type"];
    private static readonly (string Key, string Label)[] CategoryChips =
        [("content", "Content"), ("system", "Systems"), ("cosmetic", "Cosmetics"), ("zone", "Zones")];

    private static readonly (string Key, string Label)[] PriorityChips =
        [("essential", "Essential"), ("nice", "Nice"), ("optional", "Optional")];

    private static readonly (string Label, float Value)[] TextScalePresets =
        [("Small", 0.8f), ("Normal", 1.0f), ("Large", 1.3f)];

    private static readonly (string Label, HubPositionPreset Preset)[] PositionPresets =
    [
        ("Top-left", HubPositionPreset.TopLeft),
        ("Top-right", HubPositionPreset.TopRight),
        ("Center", HubPositionPreset.Center),
        ("Bottom-left", HubPositionPreset.BottomLeft),
        ("Bottom-right", HubPositionPreset.BottomRight),
    ];

    private readonly FilterState filter = new();
    private readonly List<NodeBase> checklistNodes = [];
    private readonly List<NodeBase> huntingNodes = [];
    private readonly List<NodeBase> settingsNodes = [];
    private readonly List<(TextNode Node, Core.Hunting.HuntingMonster Monster)> distanceRows = [];

    private int groupMode;
    private HubTab pendingTab = HubTab.Checklist;
    private HubTab currentTab = HubTab.Checklist;
    private Vector2 tabContentStart;
    private Vector2 tabContentSize;

    private TabBarNode? hubTabs;

    private ScrollingNode<VerticalListNode>? checklistListArea;
    private TextButtonNode? routeButton;
    private TextNode? routeCaption;
    private int lastChecklistSignature;

    private ScrollingNode<VerticalListNode>? huntingListArea;
    private TextNode? huntingHeaderNode;
    private TextButtonNode? huntHereButton;
    private int lastHuntingSignature;

    private ScrollingNode<VerticalListNode>? settingsArea;

    private enum HubPositionPreset
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center,
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        // Belt-and-suspenders alongside the OnFinalize unsubscribe below: NativeAddon.Close() only
        // starts the native closing animation (finishes several frames later), but Dispose() must
        // leave nothing subscribed to IFramework the moment it returns, regardless of that timing.
        framework.Update -= OnFrameworkUpdate;

        // Dalamud unloads plugins on a thread-pool thread (game exit); base.Dispose() calls
        // Close(), which asserts the main thread and throws otherwise. Marshal onto the framework
        // thread and block for it — IFramework's own docs call this the correct use of
        // RunOnFrameworkThread's returned Task.Wait(). Bounded by a timeout so a truly-unloading
        // framework (game process exiting, tick loop already stopped) can never hang plugin
        // teardown — Plugin.Dispose()'s finally still reaches KamiToolKitLibrary.Cleanup() either way.
        if (framework.IsInFrameworkUpdateThread)
        {
            base.Dispose();
            return;
        }

        try
        {
            framework.RunOnFrameworkThread(() => base.Dispose()).Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            log.Warning(ex, "NativeHubWindow: dispose on the framework thread failed or timed out.");
        }
    }

    /// <summary>Opens the hub on <paramref name="tab"/>, or — if already open — just switches to
    /// it. Used by both <see cref="UnlockChecklistModule.OpenChecklist"/> and
    /// <see cref="HuntingLogModule.OpenLog"/>, and by the widget's "Open Wayfarer ▸" entry point.</summary>
    public void OpenTab(HubTab tab)
    {
        if (IsOpen)
        {
            SelectTab(tab);
            return;
        }

        pendingTab = tab;
        Size = ComputeDefaultSize();
        Open();
    }

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        if (!unlocks.Loaded && !hunting.Loaded)
        {
            AddNode(new TextNode
            {
                Position = ContentStartPosition,
                Size = new Vector2(ContentSize.X, 40f),
                String = "Wayfarer data failed to load - see the Dalamud log.",
            });
            return;
        }

        var contentStart = ContentStartPosition;
        var contentSize = ContentSize;

        hubTabs = new TabBarNode { Position = contentStart, Size = new Vector2(contentSize.X, 26f) };
        hubTabs.AddTab("Checklist", () => SelectTab(HubTab.Checklist));
        hubTabs.AddTab("Hunting Log", () => SelectTab(HubTab.Hunting));
        hubTabs.AddTab("Settings", () => SelectTab(HubTab.Settings));
        AddNode(hubTabs);

        var y = contentStart.Y + hubTabs.Height + 6f;
        tabContentStart = new Vector2(contentStart.X, y);
        tabContentSize = new Vector2(contentSize.X, contentSize.Y - (y - contentStart.Y));

        BuildChecklistTab();
        BuildHuntingTab();
        BuildSettingsTab();

        SelectTab(pendingTab);

        framework.Update += OnFrameworkUpdate;
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        framework.Update -= OnFrameworkUpdate;
        distanceRows.Clear();
        checklistNodes.Clear();
        huntingNodes.Clear();
        settingsNodes.Clear();
        hubTabs = null;
        checklistListArea = null;
        routeButton = null;
        routeCaption = null;
        huntingListArea = null;
        huntingHeaderNode = null;
        huntHereButton = null;
        settingsArea = null;
    }

    // ----- Private static helpers (grouped together — SA1204) ------------------------------
    private static unsafe Vector2 ComputeDefaultSize()
    {
        var screen = new Vector2(AtkStage.Instance()->ScreenSize.Width, AtkStage.Instance()->ScreenSize.Height);
        var scale = AtkUnitBase.GetGlobalUIScale();
        var width = Math.Clamp(560f * scale, 420f, screen.X * 0.55f);
        var height = Math.Clamp(screen.Y * 0.65f, 420f, screen.Y * 0.85f);
        return new Vector2(width, height);
    }

    private static void SetBucketVisible(List<NodeBase> bucket, bool visible)
    {
        foreach (var node in bucket)
        {
            node.IsVisible = visible;
        }
    }

    private static string TabLabel(HubTab tab) => tab switch
    {
        HubTab.Checklist => "Checklist",
        HubTab.Hunting => "Hunting Log",
        _ => "Settings",
    };

    private static CheckboxNode BuildChipCheckbox(string label, bool isChecked, Action<bool> onClick) => new()
    {
        Height = 20f,
        String = label,
        IsChecked = isChecked,
        OnClick = onClick,
    };

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

    // Builds the dimmed second line under a row's button: giver name (any status), the accepted
    // quest's own name, and its live next objective — the same three facts UnlockWindow's hover
    // tooltip shows, folded inline since native rows have no tooltip.
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

    private static void OpenDuty(uint? cfcId)
    {
        if (cfcId is not { } id)
        {
            return;
        }

        var agent = AgentContentsFinder.Instance();
        if (agent != null)
        {
            agent->OpenRegularDuty(id, false);
        }
    }

    // ----- Tab switching / background polling -----------------------------------------------
    private void AddTabNode(List<NodeBase> bucket, NodeBase node)
    {
        AddNode(node);
        bucket.Add(node);
    }

    /// <summary>Switches the visible tab, force-refreshing its content so nothing stale is shown
    /// (the background poll in <see cref="OnFrameworkUpdate"/> only refreshes the currently active
    /// tab, so the other two can be arbitrarily out of date by the time the player switches to
    /// them), then focuses the first content control (task 4c) so d-pad navigation lands in the
    /// tab's content rather than staying on the title bar / tab strip.</summary>
    private void SelectTab(HubTab tab)
    {
        currentTab = tab;
        hubTabs?.SelectTab(TabLabel(tab));

        SetBucketVisible(checklistNodes, tab == HubTab.Checklist);
        SetBucketVisible(huntingNodes, tab == HubTab.Hunting);
        SetBucketVisible(settingsNodes, tab == HubTab.Settings);

        switch (tab)
        {
            case HubTab.Checklist:
                RebuildChecklist();
                routeButton?.SetFocus();
                break;
            case HubTab.Hunting:
                RebuildHunting();
                huntHereButton?.SetFocus();
                break;
            default:
                RebuildSettings();
                break;
        }
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        switch (currentTab)
        {
            case HubTab.Checklist:
                if (ComputeChecklistSignature() != lastChecklistSignature)
                {
                    RebuildChecklist();
                }

                break;
            case HubTab.Hunting:
                if (ComputeHuntingSignature() != lastHuntingSignature)
                {
                    RebuildHunting();
                }
                else
                {
                    RefreshHuntingDistances();
                }

                break;
        }
    }

    // Re-evaluated fresh on every call — mirrors the ImGui windows' navigator field being
    // recomputed every frame, so a Quest Helper toggle flip between opens is picked up on the
    // very next click/rebuild rather than only on the next background poll. Returns the concrete
    // type rather than INavigationProvider (CA1859) — every caller only ever consumes it through
    // that interface's members anyway.
    private QuestNavigator? ResolveNavigator() =>
        modules.Get<QuestHelperModule>() is { Enabled: true } questHelper ? questHelper.Navigator : null;

    // ----- Checklist tab (ported from the former NativeUnlockWindow) -----------------------
    private void BuildChecklistTab()
    {
        var y = tabContentStart.Y;
        y = BuildGroupTabs(y);
        y = BuildToggleRow(y);
        y = BuildRouteRow(y);

        checklistListArea = new ScrollingNode<VerticalListNode>
        {
            ContentNode = { FitWidth = true, FitContents = true, ItemSpacing = 6f },
            AutoHideScrollBar = true,
            Position = new Vector2(tabContentStart.X, y),
            Size = new Vector2(tabContentSize.X, tabContentSize.Y - (y - tabContentStart.Y)),
        };
        AddTabNode(checklistNodes, checklistListArea);
    }

    private AlignedHorizontalListNode BuildFilterRow(float y, IEnumerable<CheckboxNode> chips)
    {
        var row = new AlignedHorizontalListNode
        {
            Position = new Vector2(tabContentStart.X, y),
            Size = new Vector2(tabContentSize.X, 22f),
            FitToContentHeight = true,
            ItemSpacing = 10f,
        };

        foreach (var chip in chips)
        {
            row.AddNode(chip);
        }

        return row;
    }

    private float BuildGroupTabs(float y)
    {
        var tabs = new TabBarNode { Position = new Vector2(tabContentStart.X, y), Size = new Vector2(tabContentSize.X, 24f) };
        for (var i = 0; i < GroupModes.Length; i++)
        {
            var index = i; // captured per-tab, not the loop variable
            tabs.AddTab(GroupModes[i], () =>
            {
                groupMode = index;
                RebuildChecklist();
            });
        }

        AddTabNode(checklistNodes, tabs);
        return y + tabs.Height + 6f;
    }

    private float BuildToggleRow(float y)
    {
        var doneAndCategory = BuildFilterRow(y, BuildDoneAndCategoryChips());
        AddTabNode(checklistNodes, doneAndCategory);
        y += doneAndCategory.Height + 4f;

        var priority = BuildFilterRow(y, BuildPriorityChips());
        AddTabNode(checklistNodes, priority);
        return y + priority.Height + 8f;
    }

    private IEnumerable<CheckboxNode> BuildDoneAndCategoryChips()
    {
        yield return BuildChipCheckbox("Done", filter.ShowDone, isOn =>
        {
            filter.ShowDone = isOn;
            RebuildChecklist();
        });

        foreach (var (key, label) in CategoryChips)
        {
            yield return BuildChipCheckbox(label, filter.Categories.Contains(key), isOn =>
            {
                ToggleMembership(filter.Categories, key, isOn);
                RebuildChecklist();
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
                RebuildChecklist();
            });
        }
    }

    private float BuildRouteRow(float y)
    {
        routeButton = new TextButtonNode
        {
            Position = new Vector2(tabContentStart.X, y),
            Size = new Vector2(150f, 26f),
            String = "Route me (0)",
            OnClick = OnRouteClicked,
        };
        AddTabNode(checklistNodes, routeButton);

        routeCaption = new TextNode
        {
            Position = new Vector2(tabContentStart.X + 160f, y + 5f),
            Size = new Vector2(tabContentSize.X - 160f, 20f),
            FontSize = 11,
            TextColor = new Vector4(0.65f, 0.65f, 0.65f, 1f),
            String = "chains the arrow through every available pickup shown",
        };
        AddTabNode(checklistNodes, routeCaption);

        return y + 32f;
    }

    private List<ResolvedUnlock> ComputeVisibleUnlocks() =>
        [.. unlocks.Entries.Where(u => u.Status != UnlockStatus.Unverified && UnlockFilters.Matches(u, filter))];

    private int ComputeChecklistSignature()
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

    private void RebuildChecklist()
    {
        if (checklistListArea is null)
        {
            return;
        }

        var navigator = ResolveNavigator();
        var visible = ComputeVisibleUnlocks();

        UpdateRouteRow(visible, navigator);

        checklistListArea.ContentNode.Clear();
        foreach (var group in GroupUnlockEntries(visible))
        {
            checklistListArea.ContentNode.AddNode(BuildHeaderNode($"{group.Key} ({group.Count()})"));
            foreach (var u in OrderInGroup(group))
            {
                checklistListArea.ContentNode.AddNode(BuildChecklistRowNode(u, navigator));
            }
        }

        AddUnverifiedSection();

        if (checklistListArea.ContentNode.Nodes.Count == 0)
        {
            checklistListArea.ContentNode.AddNode(new TextNode
            {
                Height = 22f,
                String = "No resolved unlocks yet - open the checklist once more to force a recompute.",
            });
        }

        checklistListArea.RecalculateSizes();
        lastChecklistSignature = ComputeChecklistSignature();
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

        var routable = ComputeVisibleUnlocks().Where(u => u.Status == UnlockStatus.Available && u.GiverTerritory != null).ToList();
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

    private VerticalListNode BuildChecklistRowNode(ResolvedUnlock u, INavigationProvider? navigator)
    {
        var row = new VerticalListNode { FitWidth = true, FitContents = true, ItemSpacing = 2f };

        var (label, color) = StatusPresentation(u.Status);
        var button = new TextButtonNode
        {
            Height = 24f,
            String = $"{label}  {u.Def.Unlock}  (lv{u.QuestLevel}{(u.ZoneName is { } z ? $", {z}" : string.Empty)})",
            OnClick = () => OnChecklistRowClicked(u),
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

    private void OnChecklistRowClicked(ResolvedUnlock u)
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
        if (unverified.Count == 0 || checklistListArea is null)
        {
            return;
        }

        checklistListArea.ContentNode.AddNode(BuildHeaderNode($"Unverified ({unverified.Count})"));
        foreach (var u in unverified)
        {
            checklistListArea.ContentNode.AddNode(new TextNode
            {
                Height = 18f,
                FontSize = 11,
                TextColor = new Vector4(0.6f, 0.6f, 0.6f, 1f),
                String = $"{u.Def.Unlock} (lv{u.Def.Level})",
            });
        }
    }

    private IEnumerable<IGrouping<string, ResolvedUnlock>> GroupUnlockEntries(List<ResolvedUnlock> visible) =>
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

    // ----- Hunting tab (ported from the former NativeHuntingWindow) ------------------------
    private void BuildHuntingTab()
    {
        var y = tabContentStart.Y;

        huntingHeaderNode = new TextNode
        {
            Position = new Vector2(tabContentStart.X, y),
            Size = new Vector2(tabContentSize.X, 22f),
            FontSize = 15,
            TextColor = new Vector4(0.9f, 0.72f, 0.25f, 1f),
        };
        AddTabNode(huntingNodes, huntingHeaderNode);
        y += 26f;

        huntHereButton = new TextButtonNode
        {
            Position = new Vector2(tabContentStart.X, y),
            Size = new Vector2(160f, 26f),
            String = "Hunt here (0)",
            OnClick = OnHuntHereClicked,
        };
        AddTabNode(huntingNodes, huntHereButton);
        y += 32f;

        huntingListArea = new ScrollingNode<VerticalListNode>
        {
            ContentNode = { FitWidth = true, FitContents = true, ItemSpacing = 6f },
            AutoHideScrollBar = true,
            Position = new Vector2(tabContentStart.X, y),
            Size = new Vector2(tabContentSize.X, tabContentSize.Y - (y - tabContentStart.Y)),
        };
        AddTabNode(huntingNodes, huntingListArea);
    }

    private int ComputeHuntingSignature()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + (hunting.ActiveLogLabel?.GetHashCode(StringComparison.Ordinal) ?? 0);
            hash = (hash * 31) + (hunting.CurrentRank ?? 0);
            foreach (var m in hunting.RemainingOnPage)
            {
                hash = (hash * 31) + (int)m.BNpcNameId;
            }

            return hash;
        }
    }

    // Per-tick refresh of the distance captions only: player movement and live-tracking position
    // updates change distances without changing ComputeHuntingSignature, so the rows would
    // otherwise show the distance from whenever the list was last rebuilt.
    private void RefreshHuntingDistances()
    {
        var player = objects.LocalPlayer;
        if (player is null)
        {
            return;
        }

        foreach (var (node, monster) in distanceRows)
        {
            var view = hunting.CurrentTarget is { } current && current.Monster == monster
                ? current
                : hunting.HuntHereOrder.FirstOrDefault(t => t.Monster == monster);
            if (view is null)
            {
                continue;
            }

            var distance = NavMath.Distance(view.WorldX - player.Position.X, view.WorldY - player.Position.Y, view.WorldZ - player.Position.Z);
            node.String = view.IsLivePosition ? $"{NavMath.FormatDistance(distance)} (live)" : NavMath.FormatDistance(distance);
        }
    }

    private void RebuildHunting()
    {
        if (huntingListArea is null || huntingHeaderNode is null || huntHereButton is null)
        {
            return;
        }

        huntingHeaderNode.String = hunting.ActiveLogLabel is { } label
            ? $"{label} — rank {hunting.CurrentRank}"
            : hunting.NoLogReason ?? "No hunting log active.";

        var navigator = ResolveNavigator();
        huntHereButton.String = $"Hunt here ({hunting.HuntHereOrder.Count})";
        huntHereButton.IsEnabled = navigator != null && hunting.HuntHereOrder.Count > 0;

        huntingListArea.ContentNode.Clear();
        distanceRows.Clear();
        foreach (var target in hunting.HuntHereOrder)
        {
            huntingListArea.ContentNode.AddNode(BuildHuntingRowNode(target, navigator));
        }

        var shown = hunting.HuntHereOrder.Select(t => t.Monster).ToHashSet();
        if (hunting.CurrentTarget is { } current && !shown.Contains(current.Monster))
        {
            huntingListArea.ContentNode.AddNode(BuildHuntingRowNode(current, navigator));
        }

        if (huntingListArea.ContentNode.Nodes.Count == 0)
        {
            huntingListArea.ContentNode.AddNode(new TextNode { Height = 22f, String = "Nothing remaining on this page." });
        }

        huntingListArea.RecalculateSizes();
        lastHuntingSignature = ComputeHuntingSignature();
    }

    private VerticalListNode BuildHuntingRowNode(HuntingTargetView target, QuestNavigator? navigator)
    {
        var row = new VerticalListNode { FitWidth = true, FitContents = true, ItemSpacing = 2f };

        if (target.IsRoutable)
        {
            var button = new TextButtonNode
            {
                Height = 24f,
                String = $"{target.MonsterName}  ({target.Killed}/{target.Required})",
                IsEnabled = navigator != null,
                OnClick = () =>
                {
                    if (navigator != null && hunting.ToPickupTarget(target) is { } pickup)
                    {
                        navigator.SetPickup(pickup);
                    }
                },
            };
            row.AddNode(button);

            var player = objects.LocalPlayer;
            if (player != null)
            {
                var distance = NavMath.Distance(target.WorldX - player.Position.X, target.WorldY - player.Position.Y, target.WorldZ - player.Position.Z);
                var distanceNode = new TextNode
                {
                    Height = 16f,
                    FontSize = 11,
                    TextColor = new Vector4(0.65f, 0.65f, 0.65f, 1f),
                    String = target.IsLivePosition ? $"{NavMath.FormatDistance(distance)} (live)" : NavMath.FormatDistance(distance),
                };
                row.AddNode(distanceNode);
                distanceRows.Add((distanceNode, target.Monster));
            }

            return row;
        }

        row.AddNode(new TextNode
        {
            Height = 24f,
            String = $"{target.MonsterName}  ({target.Killed}/{target.Required})",
        });
        row.AddNode(new TextButtonNode
        {
            Height = 24f,
            String = $"Open Duty Finder: {target.DutyName}",
            IsEnabled = target.DutyContentFinderConditionId is not null,
            OnClick = () => OpenDuty(target.DutyContentFinderConditionId),
        });

        return row;
    }

    private void OnHuntHereClicked()
    {
        var navigator = ResolveNavigator();
        if (navigator is null || hunting.HuntHereOrder.Count == 0)
        {
            return;
        }

        var targets = hunting.HuntHereOrder.Select(hunting.ToPickupTarget).Where(t => t != null).Select(t => t!).ToList();
        if (targets.Count > 0)
        {
            navigator.SetRoute(targets);
        }
    }

    // ----- Settings tab (new — task 4: controller-navigable essentials) --------------------
    private void BuildSettingsTab()
    {
        settingsArea = new ScrollingNode<VerticalListNode>
        {
            ContentNode = { FitWidth = true, FitContents = true, ItemSpacing = 8f },
            AutoHideScrollBar = true,
            Position = tabContentStart,
            Size = tabContentSize,
        };
        AddTabNode(settingsNodes, settingsArea);
    }

    /// <summary>Rebuilt every time the Settings tab is selected (not polled every frame like the
    /// other two tabs) so module-enabled checkboxes always reflect the latest state — e.g. after
    /// flipping a module in the ImGui <see cref="ConfigWindow"/> and then opening the hub.</summary>
    private void RebuildSettings()
    {
        if (settingsArea is null)
        {
            return;
        }

        settingsArea.ContentNode.Clear();

        var firstModuleToggle = RebuildSettingsModules();
        RebuildSettingsPresets();

        settingsArea.RecalculateSizes();
        firstModuleToggle?.SetFocus();
    }

    private CheckboxNode? RebuildSettingsModules()
    {
        settingsArea!.ContentNode.AddNode(BuildHeaderNode("Modules"));

        CheckboxNode? first = null;
        foreach (var module in modules.Modules)
        {
            var checkbox = new CheckboxNode
            {
                Height = 22f,
                String = module.Name,
                IsChecked = module.Enabled,
                OnClick = isOn =>
                {
                    modules.SetEnabled(module, isOn);
                    config.ModuleEnabled[module.Name] = isOn;
                    saveConfig();
                },
            };
            first ??= checkbox;
            settingsArea.ContentNode.AddNode(checkbox);
        }

        return first;
    }

    private void RebuildSettingsPresets()
    {
        settingsArea!.ContentNode.AddNode(BuildHeaderNode("Text size"));
        var scaleRow = new AlignedHorizontalListNode { Height = 26f, FitToContentHeight = true, ItemSpacing = 8f };
        foreach (var (label, value) in TextScalePresets)
        {
            scaleRow.AddNode(new TextButtonNode
            {
                Width = 90f,
                Height = 24f,
                String = label,
                OnClick = () =>
                {
                    config.QuestHelper.TextScale = value;
                    saveConfig();
                },
            });
        }

        settingsArea.ContentNode.AddNode(scaleRow);

        settingsArea.ContentNode.AddNode(BuildHeaderNode("Window position"));
        var positionRow = new AlignedHorizontalListNode { Height = 26f, FitToContentHeight = true, ItemSpacing = 8f };
        foreach (var (label, preset) in PositionPresets)
        {
            positionRow.AddNode(new TextButtonNode
            {
                Width = 90f,
                Height = 24f,
                String = label,
                OnClick = () => ApplyPositionPreset(preset),
            });
        }

        settingsArea.ContentNode.AddNode(positionRow);
    }

    // Immediate reposition (no drag needed — task 4, "moving it was a pain"); NativeAddon already
    // persists whatever position is current when the window is next hidden (SaveAddonConfig in
    // Hide()), so no extra config field is needed for this to survive a reopen.
    private unsafe void ApplyPositionPreset(HubPositionPreset preset)
    {
        const float margin = 12f;
        var screen = new Vector2(AtkStage.Instance()->ScreenSize.Width, AtkStage.Instance()->ScreenSize.Height);
        var position = preset switch
        {
            HubPositionPreset.TopLeft => new Vector2(margin, margin),
            HubPositionPreset.TopRight => new Vector2(screen.X - Size.X - margin, margin),
            HubPositionPreset.BottomLeft => new Vector2(margin, screen.Y - Size.Y - margin),
            HubPositionPreset.BottomRight => new Vector2(screen.X - Size.X - margin, screen.Y - Size.Y - margin),
            _ => (screen - Size) / 2f,
        };
        SetWindowPosition(position);
    }
}
