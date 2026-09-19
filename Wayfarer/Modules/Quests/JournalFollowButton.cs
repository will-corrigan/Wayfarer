using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
using Wayfarer.App;
using Wayfarer.Core.Quests;
using Wayfarer.Ui;

namespace Wayfarer.Modules.Quests;

/// <summary>Wayfarer's own mark in the corner of the quest journal's detail pane, which follows the
/// quest on show or stops following it. It sits opposite the level badge, the same size and the
/// same inset from its own edge, so the head of the page reads as the game laid it out.
///
/// <para>It lives only while the quests module is up: the controller is enabled and disabled with
/// the module, and the mark is made when the pane opens and freed when it closes, the way every
/// node added to a game window is.</para></summary>
internal sealed class JournalFollowButton(QuestFollowing following, ITextureProvider textures, IFramework framework, IPluginLog log) : IAsyncDisposable
{
    /// <summary>The level badge at the head of the pane, node 8: 40 square, inset 24 from the left.
    /// Our mark is its opposite number, the same size and inset from the right of the pane's 496.</summary>
    private const float MarkSide = 40f;

    /// <inheritdoc cref="MarkSide"/>
    private const float PaneWidth = 496f;

    /// <inheritdoc cref="MarkSide"/>
    private const float HeadInset = 24f;

    /// <inheritdoc cref="MarkSide"/>
    private const float HeadTop = 62f;

    private const string FollowTooltip = "Follow this quest with Wayfarer, instead of the main scenario.";
    private const string UnfollowTooltip = "Stop following this quest, and go back to the main scenario.";

    private static readonly string AddonName = GameAddon.NameOf<AddonJournalDetail>();

    /// <inheritdoc cref="MarkSide"/>
    private static readonly Vector2 MarkPosition = new(PaneWidth - HeadInset - MarkSide, HeadTop);

    private AddonController? controller;
    private WayfarerMark? mark;
    private ushort? shownQuest;
    private bool shownAsFollowed;

    /// <summary>Starts watching the journal. Safe to call off the framework thread.</summary>
    public void Start() => _ = framework.RunOnFrameworkThread(Enable);

    /// <summary>Stops watching the journal and frees the button if the pane is open.</summary>
    public async Task StopAsync()
    {
        if (controller is { } owned)
        {
            controller = null;
            await owned.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    /// <summary>The quest the pane is showing, or null when it shows a leve, a quest the player has
    /// already finished, or nothing at all.</summary>
    private static unsafe ushort? QuestOnShow()
    {
        var agent = AgentQuestJournal.Instance();
        return agent == null || agent->SelectedQuestType != QuestIds.OrdinaryQuest ? null : QuestIds.FromAnyId(agent->SelectedQuestId);
    }

    private unsafe void Enable()
    {
        controller ??= new AddonController
        {
            AddonName = AddonName,
            OnSetup = Attach,
            OnFinalize = Detach,
            OnUpdate = Refresh,
        };
        controller.Enable();
    }

    private unsafe void Attach(AtkUnitBase* addon)
    {
        try
        {
            mark = new WayfarerMark(textures, log, Toggle) { Position = MarkPosition, IsVisible = false };
            mark.AttachNode(addon);
            shownQuest = null;
        }
        catch (Exception ex)
        {
            mark = null;
            log.Error(ex, "Wayfarer's mark could not be added to the quest journal, so a quest cannot be followed from it this session.");
        }
    }

    private unsafe void Detach(AtkUnitBase* addon)
    {
        mark?.Dispose();
        mark = null;
    }

    /// <summary>Shows the button for a quest that can be followed, with the label and tooltip saying
    /// what a press will do. Nothing is written while neither has changed.</summary>
    private unsafe void Refresh(AtkUnitBase* addon)
    {
        if (mark is null)
        {
            return;
        }

        var quest = QuestOnShow();
        var followed = quest is { } id && following.IsFollowing(id);
        if (quest == shownQuest && followed == shownAsFollowed)
        {
            return;
        }

        shownQuest = quest;
        shownAsFollowed = followed;
        if (quest is null)
        {
            mark.IsVisible = false;
            return;
        }

        mark.Show(MarkSide);
        mark.Lit = followed;
        mark.Tooltip = followed ? UnfollowTooltip : FollowTooltip;
    }

    private void Toggle()
    {
        if (QuestOnShow() is not { } quest)
        {
            return;
        }

        if (following.IsFollowing(quest))
        {
            following.Unfollow();
        }
        else
        {
            following.Follow(quest);
        }
    }
}
