using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
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
    /// <summary>How much of the window the pane takes: everything a fully populated entry needs at
    /// the game's own block heights, and a fixed number rather than a fraction so the list above it
    /// does not change size as the window is resized.</summary>
    public static readonly float PaneHeight = DetailPaneLayout.NaturalHeight;

    private const int MaxActions = 3;

    private readonly IPluginLog log;
    private readonly HorizontalLineNode rule;
    private readonly SimpleImageNode levelBadgeNode;
    private readonly TextNode levelNode;
    private readonly TextNode titleNode;
    private readonly TextNode kindNode;
    private readonly IconImageNode statusIconNode;
    private readonly TextNode statusNode;
    private readonly SimpleImageNode bodyGlyphNode;
    private readonly TextNode bodyNode;
    private readonly SimpleImageNode requirementsGlyphNode;
    private readonly TextNode requirementsLabelNode;
    private readonly TextNode requirementsNode;
    private readonly SimpleImageNode rewardGlyphNode;
    private readonly TextNode rewardLabelNode;
    private readonly SimpleImageNode rewardTrayNode;
    private readonly IconImageNode rewardIconNode;
    private readonly TextNode rewardNameNode;
    private readonly TextNode fromNode;
    private readonly TextNode provenanceNode;
    private readonly AlignedHorizontalListNode actionRow;
    private readonly TextButtonNode[] actionButtons = new TextButtonNode[MaxActions];

    private HubRowDetail? current;
    private bool hasStatusIcon;
    private bool hasLevel;
    private bool hasReward;
    private bool hasRewardIcon;
    private bool hasFrom;
    private bool hasProvenance;
    private int wantedBodyLines = DetailPaneLayout.MaxBodyLines;
    private int wantedRequirementLines;

    public HubDetailPaneNode(IPluginLog log)
    {
        this.log = log;

        rule = new HorizontalLineNode();
        rule.AttachNode(this);

        // The Journal's own level treatment: a TrumpGothic numeral centred on a 40x40 black disc,
        // beside the title rather than inside it. It is the one place on the page the game uses
        // that face, and it is why a journal entry reads as a level first and a name second.
        levelBadgeNode = BuildArt(GameMetrics.JournalArt.LevelBadge, GameMetrics.Journal.BadgeSize);
        levelNode = BuildLevel();

        titleNode = BuildTitle();
        kindNode = BuildKind();

        // The Journal's category-image slot: the shape that says what state this is in, beside the
        // sentence that says it in words.
        statusIconNode = BuildMarker(GameMetrics.Detail.HeadingIconSize);
        statusNode = BuildLine(GameMetrics.Type.BodySize, GameColors.ListText, TextFlags.Ellipsis);

        // Journal canvas section #5: the open book over the description.
        bodyGlyphNode = BuildArt(GameMetrics.JournalArt.GlyphDescription, GameMetrics.Journal.GlyphSize);
        bodyNode = BuildLine(
            GameMetrics.Type.BodySize, GameColors.Body, TextFlags.WordWrap | TextFlags.MultiLine);

        // Section #22: the document, which the game puts over Requirements, Information and Summary
        // alike. Addon text 2835 is its heading, and it is the word used here.
        requirementsGlyphNode = BuildArt(GameMetrics.JournalArt.GlyphDocument, GameMetrics.Journal.GlyphSize);
        requirementsLabelNode = BuildHeading("Requirements");
        requirementsNode = BuildLine(
            GameMetrics.Type.SecondarySize, GameColors.Dimmed, TextFlags.WordWrap | TextFlags.MultiLine);

        // Section #26: the treasure chest over Reward, the tray it sits on, and one slot. Built in
        // this order because it is the order they stack — the tray behind, the icon and the name on
        // top of it. The name is always drawn, never replaced by the icon: a KamiToolKit tooltip
        // registers on mouse events only, so an icon on its own is unreadable with a pad in your
        // hands, which is the exact failure this pane exists to fix.
        rewardGlyphNode = BuildArt(GameMetrics.JournalArt.GlyphReward, GameMetrics.Journal.GlyphSize);
        rewardLabelNode = BuildHeading("Reward");
        rewardTrayNode = BuildArt(
            GameMetrics.JournalArt.TrayOneRow,
            GameMetrics.Journal.ColumnWidth,
            GameMetrics.Journal.TrayHeight);
        rewardIconNode = BuildMarker(GameMetrics.Journal.SlotIconSize);
        rewardNameNode = BuildLine(GameMetrics.Type.BodySize, GameColors.ListText, TextFlags.Ellipsis);

        fromNode = BuildLine(GameMetrics.Type.SecondarySize, GameColors.ListText, TextFlags.Ellipsis);
        provenanceNode = BuildLine(GameMetrics.Type.SecondarySize, GameColors.Dimmed, TextFlags.Ellipsis);
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

        hasLevel = detail.Level.Length > 0;
        levelNode.String = detail.Level;

        ApplyReward(detail);

        hasStatusIcon = detail.StatusIconId != 0;
        if (hasStatusIcon)
        {
            statusIconNode.IconId = detail.StatusIconId;

            // Same rule as the row's icon column: the part rectangle has to match the size the icon
            // is authored at, and the Hunting Log's creature art is 48x48 where a status marker is
            // 32x32, so a fixed rectangle would crop one or pad the other.
            var actual = statusIconNode.ActualTextureSize;
            statusIconNode.TextureSize = actual.X > 0f && actual.Y > 0f
                ? actual
                : HubStatusIcons.SourceSize(detail.StatusIconId);
        }

        statusNode.String = detail.StatusSentence;
        bodyNode.String = detail.Body;
        wantedBodyLines = detail.Body.Length == 0 ? 0 : DetailPaneLayout.MaxBodyLines;

        requirementsNode.String = JoinRequirements(detail.Requirements, out wantedRequirementLines);

        hasFrom = detail.From.Length > 0;
        fromNode.String = detail.From;

        hasProvenance = detail.Provenance.Length > 0;
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

    /// <summary>Joins the requirement lines into one wrapping block, with an honest tail when there
    /// are more than fit. Truncating silently would be the same defect as the row's ellipsised
    /// gutter: the player cannot tell there was more.</summary>
    private static string JoinRequirements(IReadOnlyList<string> lines, out int drawn)
    {
        if (lines.Count == 0)
        {
            drawn = 0;
            return string.Empty;
        }

        var shown = Math.Min(lines.Count, DetailPaneLayout.MaxRequirementLines);
        var text = string.Join('\n', lines.Take(shown).Select(line => $"• {line}"));
        if (lines.Count <= shown)
        {
            drawn = shown;
            return text;
        }

        // The tail costs a line of its own, so it replaces the last bullet rather than being added
        // past the budget — which is how the block used to run out of the bottom of the pane.
        drawn = shown;
        return shown <= 1
            ? $"• and {lines.Count} more"
            : $"{string.Join('\n', lines.Take(shown - 1).Select(line => $"• {line}"))}"
                + $"\n• and {lines.Count - shown + 1} more";
    }

    private static void Place(TextNode node, ScreenRect rect)
    {
        node.IsVisible = !rect.IsEmpty;
        if (rect.IsEmpty)
        {
            return;
        }

        node.Position = new Vector2(rect.X, rect.Y);
        node.Size = new Vector2(rect.Width, rect.Height);
    }

    /// <summary>Places a crop of the journal's page art. The node's own size is set <b>and</b> its
    /// part rectangle is left where <see cref="BuildArt"/> put it: the art is authored at one size
    /// and the game never stretches it, so the tray narrows by showing less of itself rather than
    /// by distorting.</summary>
    private static void PlaceArt(SimpleImageNode node, ScreenRect rect)
    {
        node.IsVisible = !rect.IsEmpty;
        if (rect.IsEmpty)
        {
            return;
        }

        node.Position = new Vector2(rect.X, rect.Y);
        node.Size = new Vector2(rect.Width, rect.Height);
    }

    private static void PlaceIcon(IconImageNode node, ScreenRect rect)
    {
        node.IsVisible = !rect.IsEmpty;
        if (rect.IsEmpty)
        {
            return;
        }

        node.Position = new Vector2(rect.X, rect.Y);
        node.Size = new Vector2(rect.Width, rect.Height);
    }

    private static TextNode BuildBodyText(uint size, Vector4 color, TextFlags flags) => new()
    {
        FontType = FontType.Axis,
        FontSize = size,
        AlignmentType = AlignmentType.TopLeft,
        TextFlags = flags,
        TextColor = color,
        LineSpacing = size == GameMetrics.Type.BodySize
            ? GameMetrics.Type.BodyLine
            : GameMetrics.Type.SecondaryLine,
    };

    /// <summary>A crop of the Journal's own page art — a section glyph, the level disc, the reward
    /// tray. All of them live in <c>ui/uld/Journal_Detail.tex</c>, and the part rectangle has to be
    /// the authored one or the node samples past the edge of the texture and draws a band of
    /// nothing.
    ///
    /// <para>The load is guarded the same way the readout's own art is: a texture that a patch has
    /// moved must cost the pane a glyph, not the whole pane. A failed node stays invisible, and the
    /// text it decorates is drawn regardless — the glyphs are the journal's accent, never its
    /// content.</para></summary>
    private SimpleImageNode BuildArt((float U, float V) at, float width, float height = 0f)
    {
        var size = new Vector2(width, height > 0f ? height : width);
        var node = new SimpleImageNode { Size = size, WrapMode = WrapMode.Stretch, IsVisible = false };

        try
        {
            node.LoadTexture(GameMetrics.JournalArt.Texture);
            node.TextureCoordinates = new Vector2(at.U, at.V);
            node.TextureSize = size;
        }
        catch (Exception ex)
        {
            var why = $"Wayfarer hub: {GameMetrics.JournalArt.Texture} could not be read, so the detail "
                + "pane will draw its sections without the journal's glyphs. Nothing else is affected.";
            log.Warning(ex, why);
        }

        node.AttachNode(this);
        return node;
    }

    /// <summary>The reward tray's contents.
    ///
    /// <para>Half the reward kinds the game ships have no artwork at all — there is no picture of a
    /// title, an Aether Current, a folklore book or a hunt board anywhere in the files. The tray and
    /// the name are drawn either way and only the icon is dropped, so an entry with no icon reads as
    /// "here is what you get" rather than as a slot that failed to load. The layout puts the name at
    /// the tray's own left inset when there is no icon, so there is not even a gap where one would
    /// have been.</para></summary>
    private void ApplyReward(HubRowDetail detail)
    {
        hasReward = detail.RewardName.Length > 0;
        rewardNameNode.String = detail.RewardName;

        hasRewardIcon = hasReward && detail.RewardIconId != 0;
        if (!hasRewardIcon)
        {
            return;
        }

        rewardIconNode.IconId = detail.RewardIconId;

        // Same rule as the row's icon column and the status marker: the part rectangle has to match
        // the size the art is authored at, or the node samples past the edge of the texture and
        // draws a band of nothing. The loaded texture answers when it can; the reward's own kind is
        // the seed for when it cannot.
        var actual = rewardIconNode.ActualTextureSize;
        rewardIconNode.TextureSize = actual.X > 0f && actual.Y > 0f ? actual : detail.RewardIconSize;
    }

    /// <summary>A line of the pane's body text, attached and ready to be placed.</summary>
    private TextNode BuildLine(uint size, Vector4 color, TextFlags flags)
    {
        var node = BuildBodyText(size, color, flags);
        node.AttachNode(this);
        return node;
    }

    /// <summary>The entry's name. The game's own treatment — JournalDetail sets its heading in Axis
    /// 18 at leading 20, not in the window-title face. TrumpGothic belongs on the window's own title
    /// bar and on the level badge, nowhere else.</summary>
    private TextNode BuildTitle()
    {
        var node = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.DetailTitleSize,
            LineSpacing = GameMetrics.Type.DetailTitleLine,
            AlignmentType = AlignmentType.TopLeft,
            TextFlags = TextFlags.Edge | TextFlags.Ellipsis,
            TextColor = GameColors.Heading,
            TextOutlineColor = GameColors.HeadingEdge,
        };
        node.AttachNode(this);
        return node;
    }

    /// <summary>The kind word, pinned to the right of the title — the game's own dimmed caption
    /// column. The level used to share this line and now has the badge to itself.</summary>
    private TextNode BuildKind()
    {
        var node = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.SecondarySize,
            LineSpacing = GameMetrics.Type.SecondaryLine,
            AlignmentType = AlignmentType.TopRight,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.Dimmed,
        };
        node.AttachNode(this);
        return node;
    }

    /// <summary>The number on the level badge. TrumpGothic is the Journal's own face for it, and
    /// the only place the page uses that face.</summary>
    private TextNode BuildLevel()
    {
        var node = new TextNode
        {
            FontType = FontType.TrumpGothic,
            FontSize = GameMetrics.Journal.BadgeTextSize,
            AlignmentType = AlignmentType.Center,
            TextFlags = TextFlags.Edge,
            TextColor = GameColors.Heading,
            TextOutlineColor = GameColors.HeadingEdge,
            IsVisible = false,
        };
        node.AttachNode(this);
        return node;
    }

    /// <summary>A section heading in the journal's register: Axis over the glyph beside it, in the
    /// heading colour. The words are the game's own — Addon 2835 "Requirements", 463 "Reward".
    /// </summary>
    private TextNode BuildHeading(string text)
    {
        var node = BuildBodyText(GameMetrics.Type.SecondarySize, GameColors.Heading, TextFlags.Ellipsis);
        node.String = text;
        node.AttachNode(this);
        return node;
    }

    /// <summary>A square of runtime-chosen icon art — the status marker, or a reward's own picture.
    /// Hidden until something fills it, and sized by the caller because the two slots are different
    /// sizes.</summary>
    private IconImageNode BuildMarker(float size)
    {
        var node = new IconImageNode
        {
            Size = new Vector2(size, size),
            FitTexture = true,
            IsVisible = false,
        };
        node.AttachNode(this);
        return node;
    }

    /// <summary>The status vocabulary, shown before the cursor has touched anything. This is the
    /// legend, and it is the only one — it lives where it costs nothing and disappears the moment
    /// there is something real to say.</summary>
    private void ShowEmptyState()
    {
        titleNode.String = HeadingText.Plain("Wayfarer");
        kindNode.String = string.Empty;
        hasStatusIcon = false;
        hasLevel = false;
        hasReward = false;
        hasRewardIcon = false;
        statusNode.String = "Move the cursor over an entry.";

        // The gloss for each word is the same one UnlockStatusDisplay uses when a row is actually
        // selected. Two competing definitions of the same five words, seconds apart in the same
        // pane, is worse than none.
        bodyNode.String =
            $"{UnlockStatusDisplay.Word(Core.Unlocks.UnlockStatus.Available)} — start it now\n"
            + $"{UnlockStatusDisplay.Word(Core.Unlocks.UnlockStatus.Accepted)} — already taken\n"
            + $"{UnlockStatusDisplay.Word(Core.Unlocks.UnlockStatus.Done)} — nothing left\n"
            + $"{UnlockStatusDisplay.Word(Core.Unlocks.UnlockStatus.LevelLocked)} — see what it needs\n"
            + $"{UnlockStatusDisplay.Word(Core.Unlocks.UnlockStatus.UnknownGate)} — cannot be checked";

        wantedBodyLines = DetailPaneLayout.MaxBodyLines;
        wantedRequirementLines = 0;
        hasFrom = false;
        hasProvenance = false;

        ApplyActions([]);
        Layout();
    }

    /// <summary>A fixed pool of buttons, hidden until a row needs them. Allocating nodes per hover
    /// would mean building and destroying components on every d-pad step.</summary>
    private AlignedHorizontalListNode BuildActionRow()
    {
        var row = new AlignedHorizontalListNode
        {
            Height = GameMetrics.Control.ButtonHeight,
            FitToContentHeight = true,
            ItemSpacing = GameMetrics.Control.ButtonGap,
        };

        for (var i = 0; i < MaxActions; i++)
        {
            actionButtons[i] = new TextButtonNode
            {
                Width = GameMetrics.Control.ButtonWidthMedium,
                Height = GameMetrics.Control.ButtonHeight,
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
        // Every rectangle comes from DetailPaneLayout, which allocates the pane's blocks into a
        // fixed content box in priority order and returns nothing outside it. Blocks that did not
        // fit come back empty and are hidden rather than drawn off the bottom edge, which is what
        // used to happen to the requirement bullets.
        var blocks = DetailPaneLayout.Compose(
            Width,
            Height,
            hasStatusIcon,
            hasLevel,
            wantedBodyLines,
            wantedRequirementLines,
            hasReward,
            hasFrom,
            hasProvenance);

        rule.Position = new Vector2(blocks.Rule.X, blocks.Rule.Y);
        rule.Size = new Vector2(blocks.Rule.Width, blocks.Rule.Height);

        // The number and its disc share one rectangle: the game centres the numeral on the plate
        // (JournalDetail #9 over #10) rather than setting it beside.
        PlaceArt(levelBadgeNode, blocks.LevelBadge);
        Place(levelNode, blocks.LevelBadge);

        Place(titleNode, blocks.Title);
        Place(kindNode, blocks.Kind);

        PlaceIcon(statusIconNode, blocks.StatusIcon);

        Place(statusNode, blocks.Status);
        PlaceArt(bodyGlyphNode, blocks.BodyGlyph);
        Place(bodyNode, blocks.Body);
        PlaceArt(requirementsGlyphNode, blocks.RequirementsGlyph);
        Place(requirementsLabelNode, blocks.RequirementsLabel);
        Place(requirementsNode, blocks.Requirements);

        PlaceArt(rewardGlyphNode, blocks.RewardGlyph);
        Place(rewardLabelNode, blocks.RewardLabel);
        PlaceArt(rewardTrayNode, blocks.RewardTray);
        PlaceIcon(rewardIconNode, hasRewardIcon ? blocks.RewardIcon : default);
        Place(rewardNameNode, blocks.RewardName);

        Place(fromNode, blocks.From);
        Place(provenanceNode, blocks.Provenance);

        // The button row is pinned to the bottom edge rather than flowed after the text: it is the
        // one thing on the pane whose position must not depend on how much this particular entry
        // had to say, because a d-pad reaching it should not have to look for it.
        actionRow.Position = new Vector2(blocks.Actions.X, blocks.Actions.Y);
        actionRow.Size = new Vector2(blocks.Actions.Width, blocks.Actions.Height);
        actionRow.RecalculateLayout();
    }
}
