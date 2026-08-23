using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>A fixed pane across the bottom of the hub window that says what the cursor is on, and
/// carries the buttons that act on it.
///
/// <para><b>Why a pane and not the three obvious alternatives.</b> A <i>tooltip</i> is structurally
/// mouse-only — KamiToolKit registers <c>MouseOver</c>/<c>MouseOut</c> and nothing else — so it
/// fails a controller outright, and the ten-foot guidance says to avoid tooltips on a television
/// anyway. A <i>pop-out window</i> is the truest mirror of the game (Journal and JournalDetail
/// really are two addons), but window focus and cursor navigation are separate systems and there is
/// no way to move a pad cursor from one addon into another: a second window a controller cannot
/// enter is exactly the trap this whole pass exists to remove. <i>Expanding in place</i> reflows the
/// list under the cursor on every open, which is the precise condition that trips the vendored
/// <c>ListNode</c>'s recycling defect, and it makes the list's length depend on what you have
/// opened.</para>
///
/// <para><b>What it mirrors instead.</b> <c>AddonContentsFinder</c> — the Duty Finder keeps its
/// detail strip and its Join button <i>inside the same window as the list</i>, below it, and moving
/// the cursor over a duty updates the strip live while confirm acts. That contract works on both
/// devices without either being a port of the other: cursor moves, detail updates; confirm,
/// act.</para>
///
/// <para><b>The empty state is the legend.</b> Before the cursor has touched a row, the pane lists
/// the status vocabulary. That is where a key belongs — one press away, not competing with content
/// — and it is why there is no permanent legend anywhere else.</para></summary>
internal sealed class HubDetailPaneNode : ResNode
{
    /// <summary>How much of the window the pane takes. A little over the design's "lower third" at
    /// the default height, and a fixed number rather than a fraction so the list above it does not
    /// change size as the window is resized.</summary>
    public const float PaneHeight = 158f;

    private const float Padding = 8f;
    private const float TitleHeight = 24f;
    private const float StatusHeight = 18f;
    private const float LineHeight = 16f;
    private const float ButtonHeight = 24f;
    private const float ButtonWidth = 150f;
    private const float StatusIconSize = 20f;
    private const int MaxBodyLines = 4;
    private const int MaxRequirementLines = 3;
    private const int MaxActions = 3;

    private readonly HorizontalLineNode rule;
    private readonly TextNode titleNode;
    private readonly TextNode kindNode;
    private readonly IconImageNode statusIconNode;
    private readonly TextNode statusNode;
    private readonly TextNode bodyNode;
    private readonly TextNode requirementsLabelNode;
    private readonly TextNode requirementsNode;
    private readonly TextNode fromNode;
    private readonly TextNode provenanceNode;
    private readonly AlignedHorizontalListNode actionRow;
    private readonly TextButtonNode[] actionButtons = new TextButtonNode[MaxActions];

    private HubRowDetail? current;

    public HubDetailPaneNode()
    {
        rule = new HorizontalLineNode();
        rule.AttachNode(this);

        titleNode = new TextNode
        {
            FontType = FontType.TrumpGothic,
            FontSize = 20,
            AlignmentType = AlignmentType.TopLeft,
            TextFlags = TextFlags.Edge | TextFlags.Ellipsis,
            TextColor = GameColors.Heading,
            TextOutlineColor = GameColors.HeadingEdge,
        };
        titleNode.AttachNode(this);

        kindNode = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = 12,
            AlignmentType = AlignmentType.TopRight,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.Dimmed,
        };
        kindNode.AttachNode(this);

        statusIconNode = new IconImageNode
        {
            Size = new Vector2(StatusIconSize, StatusIconSize),
            FitTexture = true,
            IsVisible = false,
        };
        statusIconNode.AttachNode(this);

        statusNode = BuildBodyText(13, GameColors.ListText, TextFlags.Ellipsis);
        statusNode.AttachNode(this);

        bodyNode = BuildBodyText(13, GameColors.Body, TextFlags.WordWrap | TextFlags.MultiLine);
        bodyNode.AttachNode(this);

        requirementsLabelNode = BuildBodyText(12, GameColors.Heading, TextFlags.Ellipsis);
        requirementsLabelNode.String = "Requirements not met";
        requirementsLabelNode.AttachNode(this);

        requirementsNode = BuildBodyText(12, GameColors.Dimmed, TextFlags.WordWrap | TextFlags.MultiLine);
        requirementsNode.AttachNode(this);

        fromNode = BuildBodyText(12, GameColors.ListText, TextFlags.Ellipsis);
        fromNode.AttachNode(this);

        provenanceNode = BuildBodyText(12, GameColors.Dimmed, TextFlags.Ellipsis);
        provenanceNode.AttachNode(this);

        actionRow = BuildActionRow();
    }

    /// <summary>The action row, so the window can number it into the cursor graph. Numbered
    /// separately from the tab's control region because it sits below the list rather than above
    /// it, and the two must not share indices.</summary>
    public NodeBase ActionRow => actionRow;

    /// <summary>Shows a row's detail. Idempotent by reference: a held d-pad fires the hover callback
    /// once per step and every assignment here builds <c>SeString</c>s, so re-publishing the row
    /// that is already shown has to cost nothing.</summary>
    public void Show(HubRowDetail? detail)
    {
        if (ReferenceEquals(detail, current))
        {
            return;
        }

        current = detail;
        if (detail is null)
        {
            ShowEmptyState();
            return;
        }

        titleNode.String = HeadingText.Plain(detail.Title);
        kindNode.String = detail.Kind;

        statusIconNode.IsVisible = detail.StatusIconId != 0;
        if (detail.StatusIconId != 0)
        {
            statusIconNode.IconId = detail.StatusIconId;
        }

        statusNode.String = detail.StatusSentence;
        bodyNode.String = detail.Body;

        var requirements = Join(detail.Requirements, MaxRequirementLines);
        requirementsLabelNode.IsVisible = requirements.Length > 0;
        requirementsNode.IsVisible = requirements.Length > 0;
        requirementsNode.String = requirements;

        fromNode.IsVisible = detail.From.Length > 0;
        fromNode.String = detail.From;

        provenanceNode.IsVisible = detail.Provenance.Length > 0;
        provenanceNode.String = detail.Provenance;

        ApplyActions(detail.Actions);
        Layout();
    }

    /// <summary>Re-runs the layout after a resize. The pane is positioned by the window, so its own
    /// contents have to be re-flowed against the new width.</summary>
    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        Layout();
    }

    /// <summary>Joins up to <paramref name="max"/> lines into one wrapping block, with an honest
    /// tail when there are more. Truncating silently would be the same defect as the row's
    /// ellipsised gutter: the player cannot tell there was more.</summary>
    private static string Join(IReadOnlyList<string> lines, int max)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var shown = Math.Min(lines.Count, max);
        var text = string.Join('\n', lines.Take(shown).Select(line => $"• {line}"));
        return lines.Count > shown ? $"{text}\n• and {lines.Count - shown} more" : text;
    }

    private static TextNode BuildBodyText(uint size, Vector4 color, TextFlags flags) => new()
    {
        FontType = FontType.Axis,
        FontSize = size,
        AlignmentType = AlignmentType.TopLeft,
        TextFlags = flags,
        TextColor = color,
        LineSpacing = size + 3,
    };

    /// <summary>The status vocabulary, shown before the cursor has touched anything. This is the
    /// legend, and it is the only one — it lives where it costs nothing and disappears the moment
    /// there is something real to say.</summary>
    private void ShowEmptyState()
    {
        titleNode.String = HeadingText.Plain("Wayfarer");
        kindNode.String = string.Empty;
        statusIconNode.IsVisible = false;
        statusNode.String = "Move the cursor over an entry to see what it is and what it needs.";

        bodyNode.String =
            $"{UnlockStatusDisplay.Word(Core.Unlocks.UnlockStatus.Available)} — you can start this now\n"
            + $"{UnlockStatusDisplay.Word(Core.Unlocks.UnlockStatus.Accepted)} — you have already taken it\n"
            + $"{UnlockStatusDisplay.Word(Core.Unlocks.UnlockStatus.Done)} — nothing left to do\n"
            + $"{UnlockStatusDisplay.Word(Core.Unlocks.UnlockStatus.LevelLocked)} — the entry says what is missing\n"
            + $"{UnlockStatusDisplay.Word(Core.Unlocks.UnlockStatus.UnknownGate)} — Wayfarer isn't certain about this one";

        requirementsLabelNode.IsVisible = false;
        requirementsNode.IsVisible = false;
        fromNode.IsVisible = false;
        provenanceNode.IsVisible = false;

        ApplyActions([]);
        Layout();
    }

    /// <summary>A fixed pool of buttons, hidden until a row needs them. Allocating nodes per hover
    /// would mean building and destroying components on every d-pad step.</summary>
    private AlignedHorizontalListNode BuildActionRow()
    {
        var row = new AlignedHorizontalListNode
        {
            Height = ButtonHeight,
            FitToContentHeight = true,
            ItemSpacing = 8f,
        };

        for (var i = 0; i < MaxActions; i++)
        {
            actionButtons[i] = new TextButtonNode
            {
                Width = ButtonWidth,
                Height = ButtonHeight,
                IsVisible = false,
            };
            row.AddNode(actionButtons[i]);
        }

        row.AttachNode(this);
        return row;
    }

    private void ApplyActions(IReadOnlyList<HubDetailAction> actions)
    {
        for (var i = 0; i < MaxActions; i++)
        {
            var button = actionButtons[i];
            if (i < actions.Count)
            {
                button.String = actions[i].Label;
                button.OnClick = actions[i].Act.Invoke;

                // Enabled, always: a button that is on screen here is one this row can actually
                // do. The inapplicable ones are absent, not greyed.
                button.IsEnabled = true;
                button.IsVisible = true;
            }
            else
            {
                button.OnClick = null;
                button.IsVisible = false;
            }
        }

        actionRow.RecalculateLayout();
    }

    private void Layout()
    {
        var inner = Math.Max(Width - (Padding * 2f), 0f);
        var y = 0f;

        rule.Position = new Vector2(0f, y);
        rule.Size = new Vector2(Width, 4f);
        y += 8f;

        titleNode.Position = new Vector2(Padding, y);
        titleNode.Size = new Vector2(Math.Max(inner * 0.62f, 0f), TitleHeight);
        kindNode.Position = new Vector2(Padding + (inner * 0.62f), y + 4f);
        kindNode.Size = new Vector2(Math.Max(inner * 0.38f, 0f), LineHeight);
        y += TitleHeight;

        // The status icon and its sentence read as one line: the shape and the words for the same
        // fact, side by side, which is the pairing the whole status vocabulary rests on.
        statusIconNode.Position = new Vector2(Padding, y - 1f);
        statusNode.Position = new Vector2(Padding + StatusIconSize + 6f, y);
        statusNode.Size = new Vector2(Math.Max(inner - StatusIconSize - 6f, 0f), StatusHeight);
        y += StatusHeight + 4f;

        var bodyHeight = LineHeight * MaxBodyLines;
        bodyNode.Position = new Vector2(Padding, y);
        bodyNode.Size = new Vector2(inner, bodyHeight);
        y += bodyHeight + 2f;

        if (requirementsLabelNode.IsVisible)
        {
            requirementsLabelNode.Position = new Vector2(Padding, y);
            requirementsLabelNode.Size = new Vector2(inner, LineHeight);
            y += LineHeight;

            var requirementHeight = LineHeight * MaxRequirementLines;
            requirementsNode.Position = new Vector2(Padding + 8f, y);
            requirementsNode.Size = new Vector2(Math.Max(inner - 8f, 0f), requirementHeight);
            y += requirementHeight;
        }

        if (fromNode.IsVisible)
        {
            fromNode.Position = new Vector2(Padding, y);
            fromNode.Size = new Vector2(inner, LineHeight);
            y += LineHeight;
        }

        if (provenanceNode.IsVisible)
        {
            provenanceNode.Position = new Vector2(Padding, y);
            provenanceNode.Size = new Vector2(inner, LineHeight);
        }

        // The button row is pinned to the bottom edge rather than flowed after the text: it is the
        // one thing on the pane whose position must not depend on how much this particular entry
        // had to say, because a d-pad reaching it should not have to look for it.
        actionRow.Position = new Vector2(Padding, Math.Max(Height - ButtonHeight - Padding, 0f));
        actionRow.Size = new Vector2(inner, ButtonHeight);
        actionRow.RecalculateLayout();
    }
}
