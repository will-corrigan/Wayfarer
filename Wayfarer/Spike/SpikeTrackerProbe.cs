using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.UiOverlay;

namespace Wayfarer.Spike;

/// <summary>THROWAWAY SPIKE CODE — see <see cref="SpikeNavTarget"/>. Experiment 4a: a strictly
/// read-only structural dump of the game's quest tracker (<c>_ToDoList</c>). No injection is
/// attempted — the research settled that as a no (rows are rebuilt on every requested-update and
/// the ecosystem retreated from it after crashes). The dump exists so the follow-up overlay can
/// copy the tracker's real row heights, spacing, font sizes and colours instead of guessing.</summary>
internal sealed unsafe class SpikeTrackerProbe(IPluginLog log)
{
    private const int MaxNodes = 220;
    private const int MaxDepth = 8;

    private int emitted;

    public string Dump(string addonName)
    {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName(addonName);
        if (addon is null)
        {
            return $"{addonName} is not loaded right now.";
        }

        emitted = 0;
        log.Information($"===== Spike dump of {addonName} =====");
        log.Information($"{addonName}: visible={addon->IsVisible}, position=({addon->X}, {addon->Y}), size=({addon->GetScaledWidth(true)}x{addon->GetScaledHeight(true)}), scale={addon->Scale}, depthLayer={addon->DepthLayer}");

        Walk(addon->RootNode, 0);
        log.Information($"===== end dump of {addonName} ({emitted} nodes) =====");

        return $"Dumped {emitted} nodes of {addonName} to the Dalamud log.";
    }

    private void Walk(AtkResNode* node, int depth)
    {
        while (node is not null && emitted < MaxNodes)
        {
            Emit(node, depth);

            if (depth < MaxDepth)
            {
                if ((ushort)node->Type >= 1000)
                {
                    var component = ((AtkComponentNode*)node)->Component;
                    if (component is not null)
                    {
                        Walk(component->UldManager.RootNode, depth + 1);
                    }
                }
                else
                {
                    Walk(node->ChildNode, depth + 1);
                }
            }

            node = node->PrevSiblingNode;
        }
    }

    private void Emit(AtkResNode* node, int depth)
    {
        emitted++;
        var indent = new string(' ', depth * 2);
        var text = node->Type == NodeType.Text ? $" text=\"{((AtkTextNode*)node)->NodeText}\" fontSize={((AtkTextNode*)node)->FontSize} align={((AtkTextNode*)node)->AlignmentType} flags={((AtkTextNode*)node)->TextFlags}" : string.Empty;
        log.Information($"{indent}#{node->NodeId} {node->Type} pos=({node->X}, {node->Y}) size=({node->Width}x{node->Height}) scale=({node->ScaleX}, {node->ScaleY}) visible={node->IsVisible()} alpha={node->Color.A}{text}");
    }
}

/// <summary>THROWAWAY SPIKE CODE — see <see cref="SpikeNavTarget"/>. Experiment 4b: the smallest
/// possible proof that a HUD overlay node renders in the game's own font at the player's own HUD
/// scale, which ImGui text does not. Two lines, non-interactive, no collision — the shape the real
/// glanceable readout should take.</summary>
internal sealed class SpikeOverlayNode : OverlayNode
{
    private readonly TextNode titleNode;
    private readonly TextNode bodyNode;

    public SpikeOverlayNode()
    {
        Size = new Vector2(360.0f, 60.0f);
        Position = new Vector2(60.0f, 300.0f);

        titleNode = new TextNode
        {
            Position = new Vector2(0.0f, 0.0f),
            Size = new Vector2(360.0f, 24.0f),
            FontType = FontType.TrumpGothic,
            FontSize = 24,
            LineSpacing = 24,
            TextColor = new Vector4(1f, 0.93f, 0.72f, 1f),
            TextOutlineColor = new Vector4(0.15f, 0.11f, 0.06f, 1f),
            TextFlags = TextFlags.Edge,
            String = "Hunting Log",
        };
        titleNode.AttachNode(this);

        bodyNode = new TextNode
        {
            Position = new Vector2(0.0f, 26.0f),
            Size = new Vector2(360.0f, 34.0f),
            FontType = FontType.Axis,
            FontSize = 14,
            LineSpacing = 17,
            TextColor = new Vector4(1f, 1f, 1f, 1f),
            TextOutlineColor = new Vector4(0.15f, 0.11f, 0.06f, 1f),
            TextFlags = TextFlags.Edge | TextFlags.WordWrap | TextFlags.MultiLine,
            String = "Little Ladybug 2/3\nWind Shard 0/3 — Central Shroud, teleport to Bentbranch Meadows",
        };
        bodyNode.AttachNode(this);
    }

    /// <inheritdoc/>
    public override OverlayLayer OverlayLayer => OverlayLayer.BehindUserInterface;

    public void SetBody(string text) => bodyNode.String = text;

    /// <inheritdoc/>
    protected override void OnUpdate()
    {
        // Nothing to animate — the base Update() already handles hiding with nameplates, with the
        // UI-toggle hotkey and during cutscenes, which is the whole point of using this instead of
        // an ImGui window.
    }
}
