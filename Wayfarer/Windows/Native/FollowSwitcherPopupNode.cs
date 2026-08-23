using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;

namespace Wayfarer.Windows.Native;

/// <summary>The list the follow switcher drops — the readout's own on-the-spot answer to "what am I
/// following?", so picking something is a click and a close rather than a click and a window.
///
/// <b>Why this reuses <see cref="ListButtonNode"/> on <c>ui/uld/ListB.tex</c> rather than the whole
/// of KamiToolKit's <c>DropDownNode&lt;T&gt;</c>.</b> That component <i>is</i> the native drop-down
/// idiom end to end — same background, same rows, same collapse arrow — but its background is the
/// bordered <c>DropDownA</c> field built to sit permanently on a settings panel, and the readout
/// already draws that same collapse arrow itself, beside the quest name, in the readout's own
/// chromeless style (see <see cref="ReadoutBodyNode.BuildSwitcher"/>) — a second one on a boxed
/// pill would be two switchers for one control. What this node reuses is the part of the native
/// idiom that <i>is</i> the list: <c>ListB.tex</c>, the sheet every native drop-down and scrolling
/// list in the game draws its popup on, and <see cref="ListButtonNode"/>, the exact row art those
/// same lists use — including its own default ellipsis truncation, so a long quest name in this
/// list is cut the same way every other native list already cuts one.
///
/// <b>Dismissal.</b> A real <c>DropDownNode</c> covers its own addon's window rect with a collision
/// node to catch a click anywhere else in that window — correct for a settings panel that already
/// fills the screen with itself, but this readout's own addon is only ever as big as the readout.
/// <see cref="Reposition"/> covers the whole screen instead, which is the same idea — "elsewhere"
/// means anywhere that is not this list — reached the way a small, screen-floating addon actually
/// needs it reached. Escape is the host's job: see <see cref="ClickableReadoutAddon"/>, which polls
/// it, because this addon is deliberately outside the game's own focus stack and cannot rely on the
/// native escape-closes-the-focused-popup behaviour a real window gets for free.
///
/// <b>Owned by the clickable host, not by <see cref="ReadoutBodyNode"/>.</b> The body is the one
/// definition of what the readout looks like, shared with the click-through overlay; this list only
/// ever exists on the host that can be clicked, so it is a sibling of the body rather than a part of
/// it — see <see cref="ClickableReadoutAddon"/>.</summary>
internal sealed class FollowSwitcherPopupNode : ResNode
{
    /// <summary>The game's own drop-down row height — <c>ListButtonNode</c> everywhere it is used
    /// natively, including KamiToolKit's own <c>DropDownNode</c> popup.</summary>
    private const float RowHeight = 22f;

    /// <summary>Rows shown before the list scrolls. Long enough for every fixed entry (Main
    /// Scenario, Unlock Route, the hunting log) plus a handful of accepted quests without the
    /// dropdown outgrowing the screen it hangs from.</summary>
    private const int MaxVisibleRows = 8;

    /// <summary>The nine-grid's own inset, and the list's inset within it — the exact numbers
    /// KamiToolKit's <c>DropDownNode</c> popup uses for the same sheet, so this list is sized the
    /// way every native one is.</summary>
    private const float Inset = 8f;

    private const float ScrollBarWidth = 8f;

    private readonly SimpleNineGridNode background;
    private readonly VerticalListNode list;
    private readonly ScrollBarNode scrollBar;
    private readonly ListButtonNode[] buttons = new ListButtonNode[MaxVisibleRows];
    private readonly ResNode coverage;

    private IReadOnlyList<FollowChoice> current = [];
    private int scrollPosition;

    public FollowSwitcherPopupNode()
    {
        // Covers the whole screen while open — see the class doc comment for why this, rather than
        // the addon's own (much smaller) window rect, is what "elsewhere" has to mean here.
        // MouseDown, not MouseClick, so the dismiss beats whatever the click underneath it was
        // reaching for — the same choice DropDownNode's own outside-click node makes.
        coverage = new ResNode { IsVisible = false };
        coverage.AddEvent(AtkEventType.MouseDown, Close);
        coverage.AttachNode(this);

        background = new SimpleNineGridNode
        {
            TexturePath = "ui/uld/ListB.tex",
            TextureCoordinates = new Vector2(0f, 0f),
            TextureSize = new Vector2(32f, 32f),
            TopOffset = 10,
            BottomOffset = 12,
            LeftOffset = 10,
            RightOffset = 10,
            IsVisible = false,
        };
        background.AttachNode(this);

        list = new VerticalListNode { FitWidth = true, IsVisible = false };
        list.AttachNode(this);

        for (var i = 0; i < MaxVisibleRows; i++)
        {
            var button = new ListButtonNode { Height = RowHeight, IsVisible = false };
            buttons[i] = button;
            list.AddNode(button);
        }

        scrollBar = new ScrollBarNode
        {
            OnValueChanged = OnScroll,
            ScrollSpeed = (int)RowHeight,
            HideWhenDisabled = true,
            IsVisible = false,
        };
        scrollBar.AttachNode(this);

        IsVisible = false;
    }

    public bool IsOpen { get; private set; }

    /// <summary>Opens the list at <paramref name="topLeft"/>, <paramref name="width"/> wide, showing
    /// <paramref name="choices"/> — the same list <see cref="NativeHubWindow.GetFollowChoices"/>
    /// hands the Following tab. Rebuilds the row content; the button pool itself is fixed size and
    /// built once in the constructor.</summary>
    public void Open(IReadOnlyList<FollowChoice> choices, Vector2 topLeft, float width)
    {
        current = choices;
        scrollPosition = 0;
        IsOpen = true;

        var visibleRows = Math.Min(MaxVisibleRows, Math.Max(choices.Count, 1));
        var needsScroll = choices.Count > MaxVisibleRows;
        var scrollGutter = needsScroll ? ScrollBarWidth + (Inset / 2f) : 0f;

        var listHeight = (visibleRows * RowHeight) + (Inset * 2f);

        background.Position = Vector2.Zero;
        background.Size = new Vector2(width, listHeight);
        background.IsVisible = true;

        list.Position = new Vector2(Inset, Inset);
        list.Size = new Vector2(width - (Inset * 2f) - scrollGutter, listHeight - (Inset * 2f));
        list.IsVisible = true;

        scrollBar.Position = new Vector2(width - Inset - ScrollBarWidth, Inset);
        scrollBar.Size = new Vector2(ScrollBarWidth, listHeight - (Inset * 2f));
        scrollBar.UpdateScrollParams(visibleRows * RowHeight, choices.Count * RowHeight);

        PopulateVisibleRows();

        Position = topLeft;
        IsVisible = true;

        // The caller sizes the outside-click coverage immediately after this returns, and every
        // frame afterwards — see Reposition.
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        IsVisible = false;

        // Every collidable descendant, explicitly — not left to the root's own IsVisible to cascade.
        // This is the same convention ReadoutBodyNode.HideAll follows, and for the same reason: what
        // decides whether a node is still in the addon's collision list is its own flag, not
        // whatever its parent happens to be, and a stray live button behind a hidden popup is
        // exactly the "must not block world clicks when closed" failure this list exists to avoid.
        coverage.IsVisible = false;
        background.IsVisible = false;
        list.IsVisible = false;
        scrollBar.IsVisible = false;
        foreach (var button in buttons)
        {
            button.IsVisible = false;
            button.OnClick = null;
        }

        current = [];
    }

    public void Toggle(IReadOnlyList<FollowChoice> choices, Vector2 topLeft, float width)
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open(choices, topLeft, width);
        }
    }

    /// <summary>Grows the outside-click coverage to the whole screen, in this node's own
    /// coordinate space — which is the addon's own raw-pixel space, one unit per screen pixel (see
    /// <see cref="ClickableReadoutAddon"/>). Called every frame the popup is open, because the
    /// addon — and therefore this node's own screen origin — can move under a drag while the
    /// dropdown is up.</summary>
    /// <param name="addonScreenPosition">Where this node's local origin currently sits on
    /// screen.</param>
    /// <param name="screenSize">The full screen, in the same pixels.</param>
    public void Reposition(Vector2 addonScreenPosition, Vector2 screenSize)
    {
        if (!IsOpen)
        {
            return;
        }

        coverage.Position = -addonScreenPosition;
        coverage.Size = screenSize;
        coverage.IsVisible = true;
    }

    /// <summary>Scrolling is the native scrollbar's own drag thumb only — deliberately not also a
    /// wheel-over-the-list handler. <c>AtkResNode</c> event collision is opt-in per node
    /// (<c>MouseClick</c> is what actually turns it on elsewhere on this readout — see
    /// <see cref="ReadoutBodyNode.BuildCog"/>), and giving a plain <c>VerticalListNode</c> a wheel
    /// handler without a collision rect scoped tightly to the list would either do nothing or —
    /// if attached to something broad enough to be reliably hit, such as the outside-click
    /// coverage — capture the mouse wheel over the whole screen while the dropdown is open, which
    /// is the camera's zoom. The scrollbar's own thumb is a real interactive component with its own
    /// collision already, so it costs nothing extra and cannot make that mistake.</summary>
    private void OnScroll(int newPosition)
    {
        scrollPosition = (int)(newPosition / RowHeight);
        PopulateVisibleRows();
    }

    /// <summary>Fills the button pool from <see cref="current"/> starting at
    /// <see cref="scrollPosition"/>, and re-stacks the list afterwards.
    ///
    /// <para><b>The re-stack is not optional.</b> <c>VerticalListNode.OnRecalculateLayout</c> skips
    /// every node whose <c>IsVisible</c> is false when it hands out Y positions, and it runs only
    /// when <c>RecalculateLayout</c> is actually called — toggling a child's own <c>IsVisible</c>
    /// afterwards does not re-trigger it. The button pool starts entirely invisible in the
    /// constructor (so the one <c>AddNode</c> call there lays out nothing), and every entry made
    /// visible below therefore still carries whatever <c>Y</c> it last had — zero, for a button
    /// that has never been visible before — which is what stacked every row on top of the first
    /// one instead of underneath it.</para></summary>
    private void PopulateVisibleRows()
    {
        for (var i = 0; i < MaxVisibleRows; i++)
        {
            var sourceIndex = scrollPosition + i;
            var button = buttons[i];

            if (sourceIndex >= current.Count)
            {
                button.IsVisible = false;
                button.OnClick = null;
                continue;
            }

            var choice = current[sourceIndex];
            button.String = choice.Detail.Length > 0 ? $"{choice.Label} ({choice.Detail})" : choice.Label;
            button.Selected = choice.IsFollowed;
            button.IsVisible = true;

            var activate = choice.Activate;
            button.OnClick = activate is null ? null : () => Select(activate);
        }

        list.RecalculateLayout();
    }

    private void Select(Action activate)
    {
        activate();
        Close();
    }
}
