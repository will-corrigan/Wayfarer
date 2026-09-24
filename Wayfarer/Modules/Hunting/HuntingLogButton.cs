using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
using Wayfarer.Guidance;

using static Wayfarer.GameNodes;

namespace Wayfarer.Modules.Hunting;

/// <summary>A button on the Hunting Log that follows the page on show, or stops following it.
///
/// <para>The window has no buttons of its own, only the filter above the list of monsters. Ours
/// takes the empty space left of the filter, and only on the page the character is working on:
/// a finished page has nothing left to guide, and a later one cannot be worked yet.</para>
///
/// <para>It lives only while the setting is on, and is made when the window opens and freed when
/// it closes, handing the pad's cursor back before it goes.</para></summary>
internal sealed class HuntingLogButton(
    HuntFollowing following,
    HuntReader reader,
    HuntObjectives objectives,
    IGuidance guidance,
    IFramework framework,
    IPluginLog log) : IAsyncDisposable
{
    /// <summary>The Hunting Log's own name for itself.</summary>
    private const string Window = "MonsterNote";

    /// <summary>The filter's dropdown, whose left the button stands on.</summary>
    private const uint FilterNodeId = 38;

    /// <summary>The list of monsters under both.</summary>
    private const uint ListNodeId = 46;

    /// <summary>Our stop in the window's cursor chain, well clear of the game's own.</summary>
    private const int FollowNavIndex = 120;

    private const string FollowLabel = "Follow";
    private const string UnfollowLabel = "Unfollow";
    private const string FollowTooltip = "Guide to this page's monsters with Wayfarer, one at a time, in order.";
    private const string UnfollowTooltip = "Stop guiding to this page.";

    /// <summary>Where the button sits, in the window's own coordinates: left of the filter, level
    /// with it, and the filter's height. Measured off the window's layout file.</summary>
    private static readonly Vector2 At = new(164f, 120f);

    /// <inheritdoc cref="At"/>
    private static readonly Vector2 Size = new(156f, 24f);

    private AddonController? controller;
    private TextButtonNode? button;
    private Hunt? shownPage;
    private bool shownAsFollowed;
    private bool linked;
    private byte filterLeftBefore;

    /// <summary>Starts watching the Hunting Log. The wait is handed back so a stop asked for
    /// straight afterwards cannot overtake it.</summary>
    public Task StartAsync() => framework.RunOnFrameworkThread(Enable);

    /// <summary>Stops watching and frees the button if the window is open.</summary>
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

    /// <summary>The page on show, when it is the one this character is working on, or null.</summary>
    private static unsafe Hunt? PageOnShow()
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

    private unsafe void Enable()
    {
        controller ??= new AddonController
        {
            AddonName = Window,
            OnSetup = Attach,
            OnFinalize = Detach,
            OnUpdate = Refresh,
        };
        controller.Enable();
    }

    private unsafe void Attach(AtkUnitBase* addon)
    {
        // A setup already handled can be delivered again, so anything from last time goes first.
        Detach(addon);
        if (addon == null)
        {
            return;
        }

        try
        {
            button = new TextButtonNode
            {
                Position = At,
                Size = Size,
                OnClick = Toggle,
                IsVisible = false,
            };
            button.AttachNode(addon);
            shownPage = null;
        }
        catch (Exception ex)
        {
            button = null;
            log.Error(ex, "the follow button could not be added to the Hunting Log, so a page cannot be followed from it this session.");
        }
    }

    private unsafe void Detach(AtkUnitBase* addon)
    {
        if (button is null)
        {
            return;
        }

        Unlink(addon);
        HandFocusBack(addon, (AtkResNode*)button.CollisionNode.Node, addon == null ? null : addon->GetNodeById(FilterNodeId));
        button.Dispose();
        button = null;
    }

    /// <summary>Shows the button for the page being worked, saying what a press will do. Nothing
    /// is written while neither has changed.</summary>
    private unsafe void Refresh(AtkUnitBase* addon)
    {
        if (button is null || addon == null)
        {
            return;
        }

        var page = PageOnShow() is { } worked && reader.Facts(worked) is not null ? worked : null;
        var followed = page is not null && following.IsFollowing(page);
        if (page == shownPage && followed == shownAsFollowed)
        {
            return;
        }

        shownPage = page;
        shownAsFollowed = followed;
        button.IsVisible = page is not null;
        if (page is null)
        {
            Unlink(addon);
            HandFocusBack(addon, (AtkResNode*)button.CollisionNode.Node, addon->GetNodeById(FilterNodeId));
            return;
        }

        button.String = followed ? UnfollowLabel : FollowLabel;
        button.TextTooltip = followed ? UnfollowTooltip : FollowTooltip;
        Link(addon);
    }

    /// <summary>Puts the button into the pad's cursor chain: the filter's left leads to it, it
    /// leads right back to the filter, left to wherever the filter's left led, and down into the
    /// list.</summary>
    private unsafe void Link(AtkUnitBase* addon)
    {
        var filter = Component(addon, FilterNodeId);
        var list = Component(addon, ListNodeId);
        if (button is null || filter == null || list == null || linked)
        {
            return;
        }

        linked = true;
        filterLeftBefore = filter->CursorNavigationInfo.LeftIndex;
        filter->CursorNavigationInfo.LeftIndex = FollowNavIndex;
        button.NavIndex = FollowNavIndex;
        button.NavRight = filter->CursorNavigationInfo.Index;
        button.NavLeft = filterLeftBefore;
        button.NavDown = list->CursorNavigationInfo.Index;
        button.NavUp = filter->CursorNavigationInfo.UpIndex;
    }

    /// <summary>Puts the filter's record back as it was, found again rather than kept: the window
    /// owns it and may have freed it.</summary>
    private unsafe void Unlink(AtkUnitBase* addon)
    {
        if (!linked)
        {
            return;
        }

        linked = false;
        if (Component(addon, FilterNodeId) is var filter && filter != null)
        {
            filter->CursorNavigationInfo.LeftIndex = filterLeftBefore;
        }
    }

    /// <summary>Follows or stops following the page the button is offering, which is the one the
    /// last refresh showed it for: the press belongs to what the player can see.</summary>
    private void Toggle()
    {
        if (shownPage is not { } page)
        {
            return;
        }

        if (following.IsFollowing(page))
        {
            following.Unfollow();
            guidance.Yield(objectives);
        }
        else
        {
            following.Follow(page);
            HuntLog.Followed(log, page, reader.Facts(page));
            guidance.Claim(objectives);
        }
    }
}
