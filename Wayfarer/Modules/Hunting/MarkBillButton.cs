using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using Wayfarer.Guidance;

namespace Wayfarer.Modules.Hunting;

/// <summary>A button on one expansion's mark bill window that follows the bill on show, or stops
/// following it. It stands beside the game's own Close button.
///
/// <para>Which bill the window shows is asked of the game's mark bill agent, which keeps a record
/// of it while the window is open: whether it is a bill the character holds or one a board is
/// offering, which kind of bill, and which bill. The button is offered only for a held bill of one
/// of this window's kinds, and only when the record names the very bill the character holds, so a
/// record that is not what it seems shows no button rather than follows a bill that is not on
/// show.</para></summary>
internal sealed class MarkBillButton(
    string window,
    IReadOnlyList<byte> markIndexes,
    HuntFollowing following,
    HuntReader reader,
    HuntObjectives objectives,
    IGuidance guidance,
    IPluginLog log) : FollowButton(following, reader, objectives, guidance, log)
{
    /// <summary>The panel under the bill that holds its words and buttons.</summary>
    private const uint PanelNodeId = 2;

    /// <summary>The game's own button in the panel: Close, for a bill already held.</summary>
    private const uint CloseNodeId = 21;

    /// <summary>Air between the game's button and ours.</summary>
    private const float Gap = 8f;

    /// <summary>Where the agent keeps its pointer to the record of the bill on show: the first
    /// field after the common agent header, null while the window is hidden. The game's own code
    /// reads it there to choose which of the six windows to open.</summary>
    private const int ShownBillOffset = 0x28;

    /// <summary>The record's mode for a bill the character holds; a board's offer is another.</summary>
    private const uint HeldMode = 0;

    /// <inheritdoc/>
    protected override string Window => window;

    /// <inheritdoc/>
    protected override uint AnchorNodeId => CloseNodeId;

    /// <inheritdoc/>
    protected override Side Stands => Side.Right;

    /// <inheritdoc/>
    protected override string FollowTooltip => "Guide to this bill's marks with Wayfarer, one at a time, in order.";

    /// <inheritdoc/>
    protected override string UnfollowTooltip => "Stop guiding to this bill.";

    /// <inheritdoc/>
    protected override unsafe AtkResNode* Parent(AtkUnitBase* addon) => addon->GetNodeById(PanelNodeId);

    /// <inheritdoc/>
    /// <remarks>The bill on show, when the character holds it.</remarks>
    protected override unsafe Hunt? OnShow(AtkUnitBase* addon)
    {
        var agents = AgentModule.Instance();
        var agent = agents == null ? null : agents->GetAgentByInternalId(AgentId.MobHunt);
        var record = agent == null ? null : *(ShownBill**)((byte*)agent + ShownBillOffset);
        if (record == null || record->Mode != HeldMode || record->MarkIndex > byte.MaxValue)
        {
            return null;
        }

        var markIndex = (byte)record->MarkIndex;
        if (!markIndexes.Contains(markIndex))
        {
            return null;
        }

        var held = HuntReader.HeldBill(markIndex);
        return held != 0 && held == record->OrderRowId ? Hunt.Bill(markIndex, held) : null;
    }

    /// <inheritdoc/>
    protected override unsafe void Arrange(AtkResNode* anchor, TextButtonNode shownButton)
    {
        shownButton.Position = new Vector2(anchor->X + anchor->Width + Gap, anchor->Y);
        shownButton.Size = new Vector2(anchor->Width, anchor->Height);
    }

    /// <summary>The mark bill agent's record of the bill on show. The game's headers have no
    /// shape for it; these three were read off the game's own code, which picks the window from
    /// the kind and looks the bill up by its row.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 0x14)]
    private struct ShownBill
    {
        /// <summary>Whether the bill is held or offered by a board.</summary>
        [FieldOffset(0x4)]
        public uint Mode;

        /// <summary>Which of the game's kinds of bill it is.</summary>
        [FieldOffset(0x8)]
        public uint MarkIndex;

        /// <summary>Which bill of that kind, as its row in the bills sheet.</summary>
        [FieldOffset(0x10)]
        public uint OrderRowId;
    }
}
