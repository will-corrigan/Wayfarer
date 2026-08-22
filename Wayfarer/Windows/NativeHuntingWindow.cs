using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Wayfarer.Core.Navigation;
using Wayfarer.Modules;

namespace Wayfarer.Windows;

/// <summary>Native (KamiToolKit <see cref="NativeAddon"/>) presentation of the hunting log — the
/// Controller-mode counterpart to <see cref="HuntingWindow"/> (spec §5). Same data source
/// (<see cref="HuntingLogService"/>), same row actions (SetPickup/SetRoute through the same
/// <see cref="INavigationProvider"/> the ImGui window uses). Every component here
/// (<see cref="TextButtonNode"/>, <see cref="TextNode"/>) is a real native AtkUnitBase widget, so
/// d-pad focus navigation comes from the game itself (task-B1-report.md).
///
/// <see cref="NativeAddon"/> fully deallocates its node tree on every close, so content is rebuilt
/// from scratch in <see cref="OnSetup"/> on every open. While open, a lightweight framework-tick
/// poll rebuilds the list only when a cheap signature of the live data actually changed — same
/// idiom as <see cref="NativeUnlockWindow"/>.</summary>
internal sealed unsafe class NativeHuntingWindow(
    HuntingLogService hunting,
    ModuleRegistry modules,
    IObjectTable objects,
    IFramework framework) : NativeAddon
{
    /// <summary>The distance caption under each routable row, keyed by the row's monster so the
    /// text can be refreshed every tick from the freshest view (the player moves and live
    /// tracking updates positions without changing <see cref="ComputeSignature"/> — rebuilding
    /// the whole list for that would steal native focus mid-navigation).</summary>
    private readonly List<(TextNode Node, Core.Hunting.HuntingMonster Monster)> distanceRows = [];

    private ScrollingNode<VerticalListNode>? listArea;
    private TextNode? headerNode;
    private TextButtonNode? huntHereButton;
    private int lastSignature;

    public override void Dispose()
    {
        // Belt-and-suspenders alongside OnFinalize below — see NativeUnlockWindow's identical
        // comment for why both unsubscribes exist.
        framework.Update -= OnFrameworkUpdate;
        base.Dispose();
    }

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        if (!hunting.Loaded)
        {
            AddNode(new TextNode
            {
                Position = ContentStartPosition,
                Size = new Vector2(ContentSize.X, 40f),
                String = "Hunting log data failed to load - see the Dalamud log.",
            });
            return;
        }

        var contentStart = ContentStartPosition;
        var contentSize = ContentSize;
        var y = contentStart.Y;

        headerNode = new TextNode
        {
            Position = new Vector2(contentStart.X, y),
            Size = new Vector2(contentSize.X, 22f),
            FontSize = 15,
            TextColor = new Vector4(0.9f, 0.72f, 0.25f, 1f),
        };
        AddNode(headerNode);
        y += 26f;

        huntHereButton = new TextButtonNode
        {
            Position = new Vector2(contentStart.X, y),
            Size = new Vector2(160f, 26f),
            String = "Hunt here (0)",
            OnClick = OnHuntHereClicked,
        };
        AddNode(huntHereButton);
        y += 32f;

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

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        framework.Update -= OnFrameworkUpdate;
        distanceRows.Clear(); // the node tree is deallocated with the addon — drop the wrappers
    }

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

    // Returns the concrete type rather than INavigationProvider (CA1859) — same reasoning as
    // NativeUnlockWindow.ResolveNavigator.
    private QuestNavigator? ResolveNavigator() =>
        modules.Get<QuestHelperModule>() is { Enabled: true } questHelper ? questHelper.Navigator : null;

    private void OnFrameworkUpdate(IFramework fw)
    {
        var signature = ComputeSignature();
        if (signature == lastSignature)
        {
            RefreshDistances();
            return;
        }

        RebuildList();
    }

    /// <summary>Per-tick refresh of the distance captions only: player movement and live-tracking
    /// position updates change distances without changing <see cref="ComputeSignature"/>, so the
    /// rows would otherwise show the distance from whenever the list was last rebuilt.</summary>
    private void RefreshDistances()
    {
        var player = objects.LocalPlayer;
        if (player is null)
        {
            return;
        }

        foreach (var (node, monster) in distanceRows)
        {
            // CurrentTarget is the only view live tracking rewrites in place; every other row's
            // position is the curated coordinate captured at rebuild time.
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

    private int ComputeSignature()
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

    private void RebuildList()
    {
        if (listArea is null || headerNode is null || huntHereButton is null)
        {
            return;
        }

        headerNode.String = hunting.ActiveLogLabel is { } label
            ? $"{label} — rank {hunting.CurrentRank}"
            : hunting.NoLogReason ?? "No hunting log active.";

        var navigator = ResolveNavigator();
        huntHereButton.String = $"Hunt here ({hunting.HuntHereOrder.Count})";
        huntHereButton.IsEnabled = navigator != null && hunting.HuntHereOrder.Count > 0;

        listArea.ContentNode.Clear();
        distanceRows.Clear();
        foreach (var target in hunting.HuntHereOrder)
        {
            listArea.ContentNode.AddNode(BuildRowNode(target, navigator));
        }

        var shown = hunting.HuntHereOrder.Select(t => t.Monster).ToHashSet();
        if (hunting.CurrentTarget is { } current && !shown.Contains(current.Monster))
        {
            listArea.ContentNode.AddNode(BuildRowNode(current, navigator));
        }

        if (listArea.ContentNode.Nodes.Count == 0)
        {
            listArea.ContentNode.AddNode(new TextNode { Height = 22f, String = "Nothing remaining on this page." });
        }

        listArea.RecalculateSizes();
        lastSignature = ComputeSignature();
    }

    private VerticalListNode BuildRowNode(HuntingTargetView target, QuestNavigator? navigator)
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
}
