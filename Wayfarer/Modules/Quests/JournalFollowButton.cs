using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
using Wayfarer.App;
using Wayfarer.Core.Quests;

namespace Wayfarer.Modules.Quests;

/// <summary>A button in the quest journal's own row of buttons that follows the quest on show, or
/// stops following it. The row has three slots and the game fills the outer two with Map and
/// Abandon, so ours takes the empty middle one, and only while it really is empty.
///
/// <para>The button hangs off the row itself rather than off the window, so it sits where the row
/// sits whatever the game does with it. It lives only while the quests module is up: the controller
/// is enabled and disabled with the module, and the button is made when the pane opens and freed
/// when it closes, the way every node added to a game window is.</para></summary>
internal sealed class JournalFollowButton(QuestFollowing following, IFramework framework, IPluginLog log) : IAsyncDisposable
{
    /// <summary>The row of wide buttons at the foot of the pane, node 49: three slots of 120 by 28
    /// at x=12, 132 and 252 within it. Map takes the first, Abandon the last.</summary>
    private const uint ButtonRowNodeId = 49;

    /// <inheritdoc cref="ButtonRowNodeId"/>
    private const uint MiddleSlotNodeId = 51;

    /// <inheritdoc cref="ButtonRowNodeId"/>
    private const float ButtonWidth = 120f;

    /// <inheritdoc cref="ButtonRowNodeId"/>
    private const float ButtonHeight = 28f;

    /// <inheritdoc cref="ButtonRowNodeId"/>
    private const float MiddleSlotLeft = 132f;

    private const string FollowLabel = "Follow";
    private const string UnfollowLabel = "Unfollow";
    private const string FollowTooltip = "Guide to this quest with Wayfarer, instead of the main scenario.";
    private const string UnfollowTooltip = "Go back to guiding to the main scenario.";

    private static readonly string AddonName = GameAddon.NameOf<AddonJournalDetail>();

    private AddonController? controller;
    private TextButtonNode? button;
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

    /// <summary>Whether the middle slot of the button row is the game's to use right now.</summary>
    private static unsafe bool MiddleSlotIsFree(AtkUnitBase* addon)
    {
        var slot = addon->GetNodeById(MiddleSlotNodeId);
        return slot == null || !slot->IsVisible();
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
            var row = addon->GetNodeById(ButtonRowNodeId);
            if (row == null)
            {
                log.Warning("the quest journal has no row of buttons to put the follow button in, so a quest cannot be followed from it this session.");
                return;
            }

            button = new TextButtonNode
            {
                Position = new Vector2(MiddleSlotLeft, 0f),
                Size = new Vector2(ButtonWidth, ButtonHeight),
                OnClick = Toggle,
                IsVisible = false,
            };
            button.AttachNode(row);
            shownQuest = null;
        }
        catch (Exception ex)
        {
            button = null;
            log.Error(ex, "the follow button could not be added to the quest journal, so a quest cannot be followed from it this session.");
        }
    }

    private unsafe void Detach(AtkUnitBase* addon)
    {
        button?.Dispose();
        button = null;
    }

    /// <summary>Shows the button for a quest that can be followed, with the label and tooltip saying
    /// what a press will do. Nothing is written while neither has changed.</summary>
    private unsafe void Refresh(AtkUnitBase* addon)
    {
        if (button is null)
        {
            return;
        }

        var quest = MiddleSlotIsFree(addon) ? QuestOnShow() : null;
        var followed = quest is { } id && following.IsFollowing(id);
        if (quest == shownQuest && followed == shownAsFollowed)
        {
            return;
        }

        shownQuest = quest;
        shownAsFollowed = followed;
        button.IsVisible = quest is not null;
        if (quest is null)
        {
            return;
        }

        button.String = followed ? UnfollowLabel : FollowLabel;
        button.TextTooltip = followed ? UnfollowTooltip : FollowTooltip;
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
