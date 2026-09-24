using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using Wayfarer.Guidance;

namespace Wayfarer.Modules.Hunting;

/// <summary>A button on the Hunting Log that follows the page on show, or stops following it.
///
/// <para>The window has no buttons of its own, only the filter above the list of monsters. Ours
/// takes the empty space left of the filter, and only on the page the character is working on:
/// a finished page has nothing left to guide, and a later one cannot be worked yet.</para>
///
/// <para>It lives only while the setting is on.</para></summary>
internal sealed class HuntingLogButton(
    HuntFollowing following,
    HuntReader reader,
    HuntObjectives objectives,
    IGuidance guidance,
    IPluginLog log) : FollowButton(following, reader, objectives, guidance, log)
{
    /// <summary>The filter's dropdown, whose left the button stands on.</summary>
    private const uint FilterNodeId = 38;

    /// <summary>The list of monsters under both.</summary>
    private const uint ListNodeId = 46;

    /// <summary>Where the button sits, in the window's own coordinates: left of the filter, level
    /// with it, and the filter's height. Measured off the window's layout file.</summary>
    private static readonly Vector2 At = new(164f, 120f);

    /// <inheritdoc cref="At"/>
    private static readonly Vector2 Across = new(156f, 24f);

    /// <inheritdoc/>
    protected override string Window => "MonsterNote";

    /// <inheritdoc/>
    protected override uint AnchorNodeId => FilterNodeId;

    /// <inheritdoc/>
    protected override Side Stands => Side.Left;

    /// <inheritdoc/>
    protected override uint? BelowNodeId => ListNodeId;

    /// <inheritdoc/>
    protected override string FollowTooltip => "Guide to this page's monsters with Wayfarer, one at a time, in order.";

    /// <inheritdoc/>
    protected override string UnfollowTooltip => "Stop guiding to this page.";

    /// <inheritdoc/>
    protected override unsafe AtkResNode* Parent(AtkUnitBase* addon) => addon->RootNode;

    /// <inheritdoc/>
    /// <remarks>The page on show, when it is the one this character is working on.</remarks>
    protected override unsafe Hunt? OnShow(AtkUnitBase* addon)
    {
        var agent = AgentMonsterNote.Instance();
        if (agent == null || agent->IsLocked || agent->BaseId == 0 || agent->ClassId == 0)
        {
            return null;
        }

        return HuntReader.RankWorked(agent->MonsterNote) == agent->Rank
            ? Hunt.Page(agent->ClassId * agent->BaseId, agent->Rank, agent->MonsterNote)
            : null;
    }

    /// <inheritdoc/>
    protected override unsafe void Arrange(AtkResNode* anchor, TextButtonNode shownButton)
    {
        shownButton.Position = At;
        shownButton.Size = Across;
    }
}
