using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

using static Wayfarer.GameNodes;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Our lines spliced into the guide's cursor chain where they sit on screen: after the
/// last visible job row and before the cursor wraps back to the plate. Two of the game's own
/// records are rewritten for that, the row's "down" and the plate's "up", and both are put back
/// the moment the block's stops change or the block goes.
///
/// <para>Nothing but node ids and the two bytes taken from them is kept between frames. The nodes
/// themselves are found again every time, because the addon owns them and may have freed them
/// since: putting a record back through a pointer kept from an earlier frame is how a plugin
/// writes into memory the game has moved on from.</para></summary>
internal sealed unsafe class NavSplice
{
    private uint? aboveNodeId;
    private byte aboveDownBefore;
    private byte plateUpBefore;
    private int firstStop;

    private bool Applied => aboveNodeId is not null;

    /// <summary>Links the block in after the node with <paramref name="aboveNodeId"/>, which is the
    /// last row showing above us. Re-links only when the stops or the row have changed.</summary>
    public void Splice(AtkUnitBase* addon, uint aboveNodeId, GuidanceBlockNode block, CircleButtonNode cog)
    {
        var plate = Component(addon, ScenarioTreeMetrics.PlateNodeId);
        var above = Component(addon, aboveNodeId);
        if (plate == null || above == null)
        {
            Restore(addon);
            return;
        }

        // The cog is always there to be reached, so it is always the last of our stops; the lines
        // come before it when there is anything on them to press.
        var first = block.FirstStop ?? GuideStops.Settings;
        if (Applied && this.aboveNodeId == aboveNodeId && firstStop == first)
        {
            return;
        }

        Restore(addon);
        this.aboveNodeId = aboveNodeId;
        firstStop = first;

        ref var aboveNav = ref above->CursorNavigationInfo;
        ref var plateNav = ref plate->CursorNavigationInfo;
        aboveDownBefore = aboveNav.DownIndex;
        plateUpBefore = plateNav.UpIndex;
        aboveNav.DownIndex = (byte)first;
        plateNav.UpIndex = GuideStops.Settings;
        block.LinkNav(aboveNav.Index, GuideStops.Settings);
        cog.NavIndex = GuideStops.Settings;
        cog.NavUp = block.LastStop ?? aboveNav.Index;
        cog.NavDown = plateNav.Index;
    }

    /// <summary>Puts the game's two records back as they were, looking both nodes up again.</summary>
    public void Restore(AtkUnitBase* addon)
    {
        if (this.aboveNodeId is not { } nodeId)
        {
            return;
        }

        this.aboveNodeId = null;
        var above = Component(addon, nodeId);
        if (above != null)
        {
            above->CursorNavigationInfo.DownIndex = aboveDownBefore;
        }

        var plate = Component(addon, ScenarioTreeMetrics.PlateNodeId);
        if (plate != null)
        {
            plate->CursorNavigationInfo.UpIndex = plateUpBefore;
        }
    }
}
