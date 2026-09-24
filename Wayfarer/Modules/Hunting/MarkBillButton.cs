using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
using Wayfarer.Guidance;

using static Wayfarer.GameNodes;

namespace Wayfarer.Modules.Hunting;

/// <summary>A button on one expansion's mark bill window that follows the bill on show, or stops
/// following it. It stands beside the game's own Close button.
///
/// <para>The window says which bill it shows only in its words, so the bill is found by asking
/// which of the bills the character holds for that expansion has the monster on show on that
/// page. When the character holds only one of them, that one is it.</para>
///
/// <para>Made when the window opens and freed when it closes, handing the pad's cursor back
/// before it goes.</para></summary>
internal sealed class MarkBillButton(
    string window,
    IReadOnlyList<byte> markIndexes,
    HuntFollowing following,
    HuntReader reader,
    HuntObjectives objectives,
    IGuidance guidance,
    IPluginLog log) : IAsyncDisposable
{
    /// <summary>The panel under the bill that holds its words and buttons.</summary>
    private const uint PanelNodeId = 2;

    /// <summary>The text naming the monster on show.</summary>
    private const uint NameNodeId = 7;

    /// <summary>The game's own button in the panel: Close, for a bill already held.</summary>
    private const uint CloseNodeId = 21;

    /// <summary>Air between the game's button and ours.</summary>
    private const float Gap = 8f;

    /// <summary>Our stop in the window's cursor chain, well clear of the game's own.</summary>
    private const int FollowNavIndex = 120;

    private const string FollowLabel = "Follow";
    private const string UnfollowLabel = "Unfollow";
    private const string FollowTooltip = "Guide to this bill's marks with Wayfarer, one at a time, in order.";
    private const string UnfollowTooltip = "Stop guiding to this bill.";

    private AddonController? controller;
    private TextButtonNode? button;
    private Hunt? shownBill;
    private bool shownAsFollowed;
    private bool linked;
    private byte closeRightBefore;

    /// <summary>Starts watching the window. Game thread only.</summary>
    public unsafe void Start()
    {
        controller ??= new AddonController
        {
            AddonName = window,
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

    private unsafe void Attach(AtkUnitBase* addon)
    {
        Detach(addon);
        var panel = addon == null ? null : addon->GetNodeById(PanelNodeId);
        if (panel == null)
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
            button.AttachNode(panel);
            shownBill = null;
        }
        catch (Exception ex)
        {
            button = null;
            log.Error(ex, $"the follow button could not be added to {window}, so its bills cannot be followed from it this session.");
        }
    }

    private unsafe void Detach(AtkUnitBase* addon)
    {
        if (button is null)
        {
            return;
        }

        Unlink(addon);
        HandFocusBack(addon, (AtkResNode*)button.CollisionNode.Node, addon == null ? null : addon->GetNodeById(CloseNodeId));
        button.Dispose();
        button = null;
    }

    /// <summary>The bill on show, when the character holds it, or null.</summary>
    private unsafe Hunt? BillOnShow(AtkUnitBase* addon)
    {
        var page = ((AddonMobHunt*)addon)->CurrentPage;
        var name = Text(addon, NameNodeId) is var text && text != null ? text->NodeText.ToString() : string.Empty;

        Hunt? only = null;
        var held = 0;
        foreach (var markIndex in markIndexes)
        {
            var order = HuntReader.HeldBill(markIndex);
            if (order == 0)
            {
                continue;
            }

            var bill = Hunt.Bill(markIndex, order);
            held++;
            only = bill;
            if (reader.Facts(bill)?.Quarries.FirstOrDefault(quarry => quarry.Entry == page) is { } shown
                && string.Equals(shown.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return bill;
            }
        }

        return held == 1 ? only : null;
    }

    /// <summary>Shows the button beside Close for a bill the character holds, saying what a press
    /// will do. Nothing is written while neither has changed.</summary>
    private unsafe void Refresh(AtkUnitBase* addon)
    {
        if (button is null || addon == null)
        {
            return;
        }

        var close = addon->GetNodeById(CloseNodeId);
        var bill = close == null ? null : BillOnShow(addon);
        var followed = bill is not null && following.IsFollowing(bill);
        if (bill == shownBill && followed == shownAsFollowed)
        {
            return;
        }

        shownBill = bill;
        shownAsFollowed = followed;
        button.IsVisible = bill is not null;
        if (bill is null)
        {
            Unlink(addon);
            HandFocusBack(addon, (AtkResNode*)button.CollisionNode.Node, close);
            return;
        }

        button.Position = new Vector2(close->X + close->Width + Gap, close->Y);
        button.Size = new Vector2(close->Width, close->Height);
        button.String = followed ? UnfollowLabel : FollowLabel;
        button.TextTooltip = followed ? UnfollowTooltip : FollowTooltip;
        Link(addon);
    }

    /// <summary>Puts the button beside Close in the pad's cursor chain.</summary>
    private unsafe void Link(AtkUnitBase* addon)
    {
        var close = Component(addon, CloseNodeId);
        if (button is null || close == null || linked)
        {
            return;
        }

        linked = true;
        closeRightBefore = close->CursorNavigationInfo.RightIndex;
        close->CursorNavigationInfo.RightIndex = FollowNavIndex;
        button.NavIndex = FollowNavIndex;
        button.NavLeft = close->CursorNavigationInfo.Index;
        button.NavRight = closeRightBefore;
        button.NavUp = close->CursorNavigationInfo.UpIndex;
        button.NavDown = close->CursorNavigationInfo.DownIndex;
    }

    /// <summary>Puts Close's record back as it was, found again rather than kept.</summary>
    private unsafe void Unlink(AtkUnitBase* addon)
    {
        if (!linked)
        {
            return;
        }

        linked = false;
        if (Component(addon, CloseNodeId) is var close && close != null)
        {
            close->CursorNavigationInfo.RightIndex = closeRightBefore;
        }
    }

    private void Toggle()
    {
        if (shownBill is not { } bill)
        {
            return;
        }

        if (following.IsFollowing(bill))
        {
            following.Unfollow();
            guidance.Yield(objectives);
        }
        else
        {
            following.Follow(bill);
            HuntLog.Followed(log, bill, reader.Facts(bill));
            guidance.Claim(objectives);
        }
    }
}
