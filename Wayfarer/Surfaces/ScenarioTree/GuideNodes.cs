using FFXIVClientStructs.FFXIV.Component.GUI;
using static Wayfarer.Surfaces.ScenarioTree.ScenarioTreeMetrics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Every part of the game's Main Scenario Guide that Wayfarer reads or writes, looked up
/// by the ids in <see cref="ScenarioTreeMetrics"/>. Each one is found fresh and guarded here, so
/// no caller holds a game pointer between frames or repeats the same null checks.
///
/// <para>Every method answers null for a part the guide does not have right now, which is ordinary:
/// the job rows come and go with the player's job, and the hint strip only exists while the guide
/// has the controller's attention.</para></summary>
internal static unsafe class GuideNodes
{
    /// <summary>The headline plate: the quest name, its icon, and the button around them.</summary>
    public static AtkComponentNode* Plate(AtkUnitBase* addon) => ComponentNode(addon, PlateNodeId);

    /// <summary>The words on the plate: the name of the quest the guide is about.</summary>
    public static AtkTextNode* PlateTitle(AtkUnitBase* addon) => Text(Component(addon, PlateNodeId), PlateTitleTextNodeId);

    /// <summary>The words above the plate: what kind of quest it is about.</summary>
    public static AtkTextNode* Header(AtkUnitBase* addon)
    {
        var node = addon->GetNodeById(HeaderTextNodeId);
        return node == null ? null : node->GetAsAtkTextNode();
    }

    /// <summary>A job-quest row, whether or not it is showing anything.</summary>
    public static AtkComponentNode* JobRow(AtkUnitBase* addon, uint rowNodeId) => ComponentNode(addon, rowNodeId);

    /// <summary>Whether a job-quest row is showing a quest. A row can stay flagged visible with
    /// nothing written in it, and a blank row kept in the block's way is a blank gap on screen.</summary>
    public static bool JobRowShown(AtkUnitBase* addon, uint rowNodeId) =>
        Shown(addon, rowNodeId) && HasWords(Component(addon, rowNodeId), JobRowTextNodeId);

    /// <summary>Whether the controller hint strip is showing, which it does only while the guide
    /// has the cursor.</summary>
    public static bool HintBarShown(AtkUnitBase* addon) =>
        Shown(addon, HintBarNodeId) && HasWords(Component(addon, HintBarComponentNodeId), HintBarTextNodeId);

    /// <summary>Whether a node of the guide exists and is being drawn.</summary>
    private static bool Shown(AtkUnitBase* addon, uint nodeId)
    {
        var node = addon->GetNodeById(nodeId);
        return node != null && node->IsVisible();
    }

    private static AtkComponentBase* Component(AtkUnitBase* addon, uint nodeId) => addon->GetComponentByNodeId(nodeId);

    private static AtkComponentNode* ComponentNode(AtkUnitBase* addon, uint nodeId)
    {
        var node = addon->GetNodeById(nodeId);
        return node == null ? null : node->GetAsAtkComponentNode();
    }

    private static AtkTextNode* Text(AtkComponentBase* component, uint textNodeId) =>
        component == null ? null : component->GetTextNodeById(textNodeId);

    /// <summary>Whether a component has anything written in one of its text nodes.</summary>
    private static bool HasWords(AtkComponentBase* component, uint textNodeId) =>
        Text(component, textNodeId) is var text && text != null && text->NodeText.Length > 0;
}
