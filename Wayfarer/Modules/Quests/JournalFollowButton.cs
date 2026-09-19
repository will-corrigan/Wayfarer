using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using Wayfarer.Core.Quests;

namespace Wayfarer.Modules.Quests;

/// <summary>A round button in the quest journal's detail pane, beside the game's own, that follows
/// the quest on show or stops following it. Lives only while the quests module is up: the
/// controller is enabled and disabled with the module, and the button is made when the pane opens
/// and freed when it closes, the way every node added to a game window is.</summary>
internal sealed class JournalFollowButton(QuestFollowing following, IFramework framework, IPluginLog log) : IAsyncDisposable
{
    /// <summary>The detail pane of the quest journal, a separate addon from the list.</summary>
    private const string AddonName = "JournalDetail";

    /// <summary>The game's own round button in the pane sits at 414,582 and is 28 square; ours
    /// goes one button's width to its right, inside the pane's 496 width.</summary>
    private const float ButtonSide = 28f;

    /// <summary>The journal agent's type for an ordinary quest. A leve or a completed quest is
    /// another type and cannot be followed.</summary>
    private const uint OrdinaryQuest = 1;

    private const CircleButtonIcon FollowIcon = CircleButtonIcon.Globe;
    private const CircleButtonIcon FollowingIcon = CircleButtonIcon.Cross;
    private const string FollowTooltip = "Follow with Wayfarer";
    private const string UnfollowTooltip = "Stop following with Wayfarer";

    /// <inheritdoc cref="ButtonSide"/>
    private static readonly Vector2 ButtonPosition = new(446f, 582f);

    private AddonController? controller;
    private CircleButtonNode? button;
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

    /// <summary>The quest the pane is showing, or null when it shows a leve, a completed quest, or
    /// nothing at all.</summary>
    private static unsafe ushort? QuestOnShow()
    {
        var agent = AgentQuestJournal.Instance();
        return agent == null || agent->SelectedQuestType != OrdinaryQuest ? null : QuestIds.FromRowId(agent->SelectedQuestId);
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
            button = new CircleButtonNode
            {
                Position = ButtonPosition,
                Size = new Vector2(ButtonSide, ButtonSide),
                OnClick = Toggle,
                IsVisible = false,
            };
            button.AttachNode(addon);
            shownQuest = null;
        }
        catch (Exception ex)
        {
            button = null;
            log.Error(ex, "the follow button could not be added to the quest journal, so a quest can only be followed from somewhere else this session.");
        }
    }

    private unsafe void Detach(AtkUnitBase* addon)
    {
        button?.Dispose();
        button = null;
    }

    /// <summary>Shows the button for a quest that can be followed, with the icon and tooltip saying
    /// what a press will do. Nothing is written while neither has changed.</summary>
    private unsafe void Refresh(AtkUnitBase* addon)
    {
        if (button is null)
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
        button.IsVisible = quest is not null;
        if (quest is null)
        {
            return;
        }

        button.Icon = followed ? FollowingIcon : FollowIcon;
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
