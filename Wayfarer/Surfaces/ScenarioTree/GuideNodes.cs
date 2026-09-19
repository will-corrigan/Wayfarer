using FFXIVClientStructs.FFXIV.Component.GUI;
using static Wayfarer.GameNodes;
using static Wayfarer.Surfaces.ScenarioTree.ScenarioTreeMetrics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Every part of the game's Main Scenario Guide that Wayfarer reads or writes, named
/// once here against the ids in <see cref="ScenarioTreeMetrics"/>. The looking up and the
/// guarding are <see cref="GameNodes"/>'s; this only says which parts the guide has.
///
/// <para>Every method answers null or false for a part the guide does not have right now, which is
/// ordinary: the job rows come and go with the player's job, and the hint strip only exists while
/// the guide has the controller's attention.</para></summary>
internal static unsafe class GuideNodes
{
    /// <summary>The headline plate: the quest name, its icon, and the button around them.</summary>
    public static AtkComponentNode* Plate(AtkUnitBase* addon) => ComponentNode(addon, PlateNodeId);

    /// <summary>The words on the plate: the name of the quest the guide is about.</summary>
    public static AtkTextNode* PlateTitle(AtkUnitBase* addon) => Text(Component(addon, PlateNodeId), PlateTitleTextNodeId);

    /// <summary>The words above the plate: what kind of quest it is about.</summary>
    public static AtkTextNode* Header(AtkUnitBase* addon) => Text(addon, HeaderTextNodeId);

    /// <summary>Whether a job-quest row is showing a quest.</summary>
    public static bool JobRowShown(AtkUnitBase* addon, uint rowNodeId) =>
        Shown(addon, rowNodeId) && HasWords(Component(addon, rowNodeId), JobRowTextNodeId);

    /// <summary>Whether the controller hint strip is showing, which it does only while the guide
    /// has the cursor.</summary>
    public static bool HintBarShown(AtkUnitBase* addon) =>
        Shown(addon, HintBarNodeId) && HasWords(Component(addon, HintBarComponentNodeId), HintBarTextNodeId);
}
