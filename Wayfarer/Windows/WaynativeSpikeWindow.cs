using System;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Windows;

/// <summary>
/// TEMPORARY task-B1 spike — NOT part of the shipped module surface. Proves a real native
/// (KamiToolKit-authored AtkUnitBase) addon window can host a title, a scrolling list of
/// <see cref="TextButtonNode"/> rows interleaved with plain <see cref="TextNode"/> section
/// headers, bound to live <see cref="IUnlockProvider"/> data — and that the standard
/// components get native d-pad focus navigation for free (spec §3, task-B1-brief.md).
/// Opened only via the debug command <c>/waynative</c> wired in <see cref="Plugin"/>. Delete
/// this file, its command handler and its field once task B2 lands the real native checklist
/// window (spec §3, "Native checklist window (KamiToolKit port)").
///
/// Grouping verdict (task-B1-brief.md): flat list + section-header <see cref="TextNode"/> rows
/// reads fine here with a handful of groups/entries per group — no need to reach for
/// TabBarNode for the filter set. Revisit only if the real dataset's group count makes the
/// flat scroll unwieldy (task B2's call, with the full entry count in front of it).
/// </summary>
internal sealed class WaynativeSpikeWindow(IUnlockProvider unlocks, IPluginLog log) : NativeAddon
{
    // "A handful of entries" (task-B1-brief.md) - this is a navigation/binding spike, not a
    // real list; task B2 owns pagination/virtualization for the full dataset.
    private const int MaxGroups = 4;
    private const int MaxEntriesPerGroup = 5;

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        var scrollable = new ScrollingNode<VerticalListNode>
        {
            ContentNode = { FitWidth = true, FitContents = true, ItemSpacing = 4.0f },
            AutoHideScrollBar = true,
            Size = ContentSize,
            Position = ContentStartPosition,
        };

        var groups = unlocks.Entries
            .Where(u => u.Status != UnlockStatus.Unverified)
            .GroupBy(u => u.Def.Type, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Take(MaxGroups)
            .ToList();

        if (groups.Count == 0)
        {
            scrollable.ContentNode.AddNode(new TextNode
            {
                String = unlocks.Loaded
                    ? "No resolved unlocks yet - open the Unlocks window once to force a recompute."
                    : "Unlock data failed to load - see the Dalamud log.",
                Height = 22.0f,
            });
        }

        foreach (var group in groups)
        {
            scrollable.ContentNode.AddNode(new TextNode
            {
                String = $"{group.Key} ({group.Count()})",
                Height = 22.0f,
                FontSize = 16,
            });

            foreach (var entry in group.Take(MaxEntriesPerGroup))
            {
                var label = $"{entry.Def.Unlock} (lv{entry.QuestLevel})";
                scrollable.ContentNode.AddNode(new TextButtonNode
                {
                    Height = 28.0f,
                    String = label,

                    // Read-only invariant: the spike never routes or teleports, it only proves
                    // the row is focusable/clickable via native d-pad nav.
                    OnClick = () => log.Information($"[waynative spike] selected: {label}"),
                });
            }
        }

        scrollable.RecalculateSizes();
        scrollable.AttachNode(this);
    }
}
