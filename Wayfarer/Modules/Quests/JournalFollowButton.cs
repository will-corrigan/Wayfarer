using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
using Wayfarer.App;

using static Wayfarer.GameNodes;

namespace Wayfarer.Modules.Quests;

/// <summary>A button in the quest journal's own row of buttons that follows the quest on show, or
/// stops following it. The row has three slots and the game fills the outer two with Map and
/// Abandon, so ours takes the empty middle one, and only while it really is empty.
///
/// <para>The button hangs off the row itself rather than off the window, so it sits where the row
/// sits whatever the game does with it. It lives only while the quests module is up: the controller
/// is enabled and disabled with the module, and the button is made when the pane opens and freed
/// when it closes, the way every node added to a game window is.</para></summary>
internal sealed class JournalFollowButton(QuestFollowing following, IGameGui gameGui, IFramework framework, IPluginLog log) : IAsyncDisposable
{
    /// <summary>The row of wide buttons at the foot of the pane, node 49, and the two the game
    /// puts in it: Map at one end and Abandon at the other. Ours goes in the space between them,
    /// measured rather than assumed, so it never crowds either.</summary>
    private const uint ButtonRowNodeId = 49;

    /// <inheritdoc cref="ButtonRowNodeId"/>
    private const uint MapNodeId = 50;

    /// <inheritdoc cref="ButtonRowNodeId"/>
    private const uint AbandonNodeId = 52;

    /// <inheritdoc cref="ButtonRowNodeId"/>
    private const float ButtonHeight = 28f;

    /// <summary>Air left either side of our button, so the three read as a row rather than a block.</summary>
    private const float Margin = 10f;

    /// <summary>Narrower than this and the words would not fit, so nothing is shown at all.</summary>
    private const float NarrowestButton = 60f;

    /// <summary>Our stop in the journal's cursor chain, well clear of the game's own.</summary>
    private const int FollowNavIndex = 120;

    private const string FollowLabel = "Follow";
    private const string UnfollowLabel = "Unfollow";
    private const string FollowTooltip = "Guide to this quest with Wayfarer, instead of the main scenario.";
    private const string UnfollowTooltip = "Go back to guiding to the main scenario.";

    private static readonly string AddonName = GameAddon.NameOf<AddonJournalDetail>();

    private AddonController? controller;
    private TextButtonNode? button;
    private ushort? shownQuest;

    /// <summary>The window our button is in. The game keeps more than one of these panes open at
    /// once — the Duty Finder opens its own — and every one of them is announced to us, so the
    /// button has to know which it belongs to or another pane's setup would take it away.</summary>
    private ushort attachedTo;
    private bool shownAsFollowed;
    private bool linked;
    private byte mapRightBefore;
    private byte abandonLeftBefore;

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

    /// <summary>The space the game has left between its own two buttons, or nothing when there is
    /// not enough of it. In the row's own coordinates, which ours shares by hanging off it.</summary>
    private static unsafe (float Left, float Width)? SpaceBetweenTheGamesButtons(AtkUnitBase* addon)
    {
        var map = addon == null ? null : addon->GetNodeById(MapNodeId);
        var abandon = addon == null ? null : addon->GetNodeById(AbandonNodeId);
        if (map == null || abandon == null)
        {
            return null;
        }

        var left = map->X + map->Width + Margin;
        var width = abandon->X - Margin - left;
        return width >= NarrowestButton ? (left, width) : null;
    }

    /// <summary>The quest the pane is showing, or null when it shows a leve, a quest the player has
    /// already finished, or nothing at all.
    ///
    /// <para>The game lends this same pane to other windows: the Duty Finder describes a duty in one
    /// of its own while the journal has another open, and the journal's agent goes on holding
    /// whichever quest was last chosen either way. Which window opened this one is what tells them
    /// apart — the journal's pane hangs off the list the agent is driving, and the Duty Finder's
    /// hangs off the Duty Finder.</para></summary>
    private unsafe ushort? QuestOnShow(AtkUnitBase* addon)
    {
        var agent = AgentQuestJournal.Instance();
        if (agent == null || !OpenedByTheJournal(addon))
        {
            return null;
        }

        return agent->SelectedQuestType != QuestIds.OrdinaryQuest ? null : QuestIds.FromAnyId(agent->SelectedQuestId);
    }

    /// <summary>Whether the quest journal is the thing that opened this pane. The game lends the
    /// same pane to the Duty Finder, which opens one of its own while the journal has another, so
    /// the pane is asked who owns it rather than guessed at from what it is drawing.</summary>
    private unsafe bool OpenedByTheJournal(AtkUnitBase* addon)
    {
        var journal = AgentQuestJournal.Instance();
        return addon != null && journal != null && gameGui.FindAgentInterface(addon).Address == (nint)journal;
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
        // A setup we have already handled can be delivered again, so anything from last time goes
        // before anything new is made: otherwise the game keeps nodes we no longer hold and can
        // never be told to free.
        if (!OpenedByTheJournal(addon))
        {
            return;
        }

        Detach(addon);

        try
        {
            var row = addon == null ? null : addon->GetNodeById(ButtonRowNodeId);
            if (row == null)
            {
                log.Warning("the quest journal has no row of buttons to put the follow button in, so a quest cannot be followed from it this session.");
                return;
            }

            button = new TextButtonNode
            {
                Height = ButtonHeight,
                OnClick = Toggle,
                IsVisible = false,
            };
            button.AttachNode(row);
            attachedTo = addon->Id;
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
        if (button is null || (addon != null && attachedTo != 0 && addon->Id != attachedTo))
        {
            return;
        }

        UnlinkFromCursorChain(addon);
        HandFocusBack(addon);
        button.Dispose();
        button = null;
        attachedTo = 0;
    }

    /// <summary>Points the journal's focus away from our button. The button can go while the
    /// journal is still open, by the module being switched off or its setting unticked, and the
    /// game would otherwise be left holding the pad's cursor on a freed node.</summary>
    private unsafe void HandFocusBack(AtkUnitBase* addon)
    {
        if (button is { } ours)
        {
            GameNodes.HandFocusBack(addon, (AtkResNode*)ours.CollisionNode.Node, addon == null ? null : addon->GetNodeById(ButtonRowNodeId));
        }
    }

    /// <summary>Puts our button between the game's two in the cursor's chain, so the pad reaches it
    /// by moving across the row the way it reaches the others.</summary>
    private unsafe void LinkIntoCursorChain(AtkUnitBase* addon)
    {
        var map = Component(addon, MapNodeId);
        var abandon = Component(addon, AbandonNodeId);
        if (button is null || map == null || abandon == null || linked)
        {
            return;
        }

        linked = true;
        mapRightBefore = map->CursorNavigationInfo.RightIndex;
        abandonLeftBefore = abandon->CursorNavigationInfo.LeftIndex;

        map->CursorNavigationInfo.RightIndex = FollowNavIndex;
        abandon->CursorNavigationInfo.LeftIndex = FollowNavIndex;
        button.NavIndex = FollowNavIndex;
        button.NavLeft = map->CursorNavigationInfo.Index;
        button.NavRight = abandon->CursorNavigationInfo.Index;
        button.NavUp = mapRightBefore;
        button.NavDown = abandonLeftBefore;
    }

    /// <summary>Puts the game's own two records back as they were, finding both again rather than
    /// through pointers kept from an earlier frame: the pane owns them and may have freed them.</summary>
    private unsafe void UnlinkFromCursorChain(AtkUnitBase* addon)
    {
        if (!linked)
        {
            return;
        }

        linked = false;
        if (Component(addon, MapNodeId) is var map && map != null)
        {
            map->CursorNavigationInfo.RightIndex = mapRightBefore;
        }

        if (Component(addon, AbandonNodeId) is var abandon && abandon != null)
        {
            abandon->CursorNavigationInfo.LeftIndex = abandonLeftBefore;
        }
    }

    /// <summary>Shows the button for a quest that can be followed, with the label and tooltip saying
    /// what a press will do. Nothing is written while neither has changed.</summary>
    private unsafe void Refresh(AtkUnitBase* addon)
    {
        if (button is null)
        {
            return;
        }

        if (button is null || addon == null || addon->Id != attachedTo)
        {
            return;
        }

        var space = SpaceBetweenTheGamesButtons(addon);
        var quest = space is null ? null : QuestOnShow(addon);
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
            UnlinkFromCursorChain(addon);
            HandFocusBack(addon);
            return;
        }

        button.Position = new Vector2(space!.Value.Left, 0f);
        button.Size = new Vector2(space.Value.Width, ButtonHeight);
        button.String = followed ? UnfollowLabel : FollowLabel;
        button.TextTooltip = followed ? UnfollowTooltip : FollowTooltip;
        LinkIntoCursorChain(addon);
    }

    /// <summary>Follows or stops following the quest the button is currently offering, which is
    /// the one the last refresh found and showed it for. Read from there rather than asked again:
    /// the press belongs to what the player can see on the button.</summary>
    private void Toggle()
    {
        if (shownQuest is not { } quest)
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
