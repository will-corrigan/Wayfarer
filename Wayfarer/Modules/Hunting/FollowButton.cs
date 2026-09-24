using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
using Wayfarer.Guidance;

using static Wayfarer.GameNodes;

namespace Wayfarer.Modules.Hunting;

/// <summary>A button on one of the game's hunt windows that follows the hunt on show, or stops
/// following it. It stands beside one of the window's own parts, its anchor, and joins the pad's
/// cursor chain through it.
///
/// <para>Each window says for itself which hunt it shows, where the button hangs and how it is
/// laid out; everything else is here once: making the button when the window opens, showing it
/// only for a hunt that can be followed, what a press does, and freeing it when the window closes,
/// with the pad's cursor handed back and the anchor's cursor record put back as it was.</para></summary>
internal abstract class FollowButton(
    HuntFollowing following,
    HuntReader reader,
    HuntObjectives objectives,
    IGuidance guidance,
    IPluginLog log) : IAsyncDisposable
{
    /// <summary>Our stop in the window's cursor chain, well clear of the game's own.</summary>
    private const int FollowNavIndex = 120;

    private const string FollowLabel = "Follow";
    private const string UnfollowLabel = "Unfollow";

    private AddonController? controller;
    private TextButtonNode? button;
    private Hunt? shown;
    private bool shownAsFollowed;
    private bool linked;
    private byte anchorSideBefore;

    /// <summary>Which side of its anchor the button stands on.</summary>
    protected enum Side
    {
        /// <summary>To the anchor's left.</summary>
        Left,

        /// <summary>To the anchor's right.</summary>
        Right,
    }

    /// <summary>The window's own name for itself.</summary>
    protected abstract string Window { get; }

    /// <summary>The window's own part the button stands beside, whose cursor record leads to it
    /// and where focus lands when the button goes.</summary>
    protected abstract uint AnchorNodeId { get; }

    /// <inheritdoc cref="Side"/>
    protected abstract Side Stands { get; }

    /// <summary>What the pad's down leads to from the button, or null for wherever the anchor's
    /// down leads.</summary>
    protected virtual uint? BelowNodeId => null;

    /// <summary>What hovering the button says while the hunt on show is not followed.</summary>
    protected abstract string FollowTooltip { get; }

    /// <summary>What hovering the button says while the hunt on show is followed.</summary>
    protected abstract string UnfollowTooltip { get; }

    /// <summary>Starts watching the window. Game thread only.</summary>
    public unsafe void Start()
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

    /// <summary>The node the button hangs off, or null when the window does not have it.</summary>
    /// <param name="addon">The window, never null.</param>
    protected abstract unsafe AtkResNode* Parent(AtkUnitBase* addon);

    /// <summary>The hunt the window is showing that the button would follow, or null for none.</summary>
    /// <param name="addon">The window, never null.</param>
    protected abstract unsafe Hunt? OnShow(AtkUnitBase* addon);

    /// <summary>Puts the button where it stands and sizes it.</summary>
    /// <param name="anchor">The anchor, never null.</param>
    /// <param name="shownButton">The button.</param>
    protected abstract unsafe void Arrange(AtkResNode* anchor, TextButtonNode shownButton);

    private unsafe void Attach(AtkUnitBase* addon)
    {
        // A setup already handled can be delivered again, so anything from last time goes first.
        Detach(addon);
        var parent = addon == null ? null : Parent(addon);
        if (parent == null)
        {
            return;
        }

        try
        {
            button = new TextButtonNode
            {
                OnClick = Toggle,
                IsVisible = false,
            };
            button.AttachNode(parent);
            shown = null;
            shownAsFollowed = false;
        }
        catch (Exception ex)
        {
            button = null;
            log.Error(ex, $"the follow button could not be added to {Window}, so nothing can be followed from it this session.");
        }
    }

    private unsafe void Detach(AtkUnitBase* addon)
    {
        if (button is null)
        {
            return;
        }

        Unlink(addon);
        HandFocusBack(addon, (AtkResNode*)button.CollisionNode.Node, addon == null ? null : addon->GetNodeById(AnchorNodeId));
        button.Dispose();
        button = null;
    }

    /// <summary>Shows the button for a hunt on show that can be followed, saying what a press will
    /// do. Nothing is written while neither has changed.</summary>
    private unsafe void Refresh(AtkUnitBase* addon)
    {
        if (button is null || addon == null)
        {
            return;
        }

        var anchor = addon->GetNodeById(AnchorNodeId);
        var hunt = anchor != null && OnShow(addon) is { } onShow && reader.Facts(onShow) is not null ? onShow : null;
        var followed = hunt is not null && following.IsFollowing(hunt);
        if (hunt == shown && followed == shownAsFollowed)
        {
            return;
        }

        shown = hunt;
        shownAsFollowed = followed;
        button.IsVisible = hunt is not null;
        if (hunt is null)
        {
            Unlink(addon);
            HandFocusBack(addon, (AtkResNode*)button.CollisionNode.Node, anchor);
            return;
        }

        Arrange(anchor, button);
        button.String = followed ? UnfollowLabel : FollowLabel;
        button.TextTooltip = followed ? UnfollowTooltip : FollowTooltip;
        Link(addon);
    }

    /// <summary>Puts the button into the pad's cursor chain beside its anchor: the anchor's side
    /// leads to it, it leads back to the anchor and on to wherever the anchor's side led, and up
    /// and down as the anchor does, or down to the part named below it.</summary>
    private unsafe void Link(AtkUnitBase* addon)
    {
        var anchor = Component(addon, AnchorNodeId);
        if (button is null || anchor == null || linked)
        {
            return;
        }

        ref var nav = ref anchor->CursorNavigationInfo;
        var below = BelowNodeId is { } belowId ? Component(addon, belowId) : null;
        linked = true;
        button.NavIndex = FollowNavIndex;
        button.NavUp = nav.UpIndex;
        button.NavDown = below != null ? below->CursorNavigationInfo.Index : nav.DownIndex;
        if (Stands == Side.Left)
        {
            anchorSideBefore = nav.LeftIndex;
            nav.LeftIndex = FollowNavIndex;
            button.NavRight = nav.Index;
            button.NavLeft = anchorSideBefore;
        }
        else
        {
            anchorSideBefore = nav.RightIndex;
            nav.RightIndex = FollowNavIndex;
            button.NavLeft = nav.Index;
            button.NavRight = anchorSideBefore;
        }
    }

    /// <summary>Puts the anchor's cursor record back as it was, found again rather than kept: the
    /// window owns it and may have freed it.</summary>
    private unsafe void Unlink(AtkUnitBase* addon)
    {
        if (!linked)
        {
            return;
        }

        linked = false;
        if (Component(addon, AnchorNodeId) is var anchor && anchor != null)
        {
            if (Stands == Side.Left)
            {
                anchor->CursorNavigationInfo.LeftIndex = anchorSideBefore;
            }
            else
            {
                anchor->CursorNavigationInfo.RightIndex = anchorSideBefore;
            }
        }
    }

    /// <summary>Follows or stops following the hunt the button is offering, which is the one the
    /// last refresh showed it for: the press belongs to what the player can see.</summary>
    private void Toggle()
    {
        if (shown is not { } hunt)
        {
            return;
        }

        if (following.IsFollowing(hunt))
        {
            following.Unfollow();
            guidance.Yield(objectives);
        }
        else
        {
            following.Follow(hunt);
            HuntLog.Followed(log, hunt, reader.Facts(hunt));
            guidance.Claim(objectives);
        }
    }
}
