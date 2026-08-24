using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>One entry's journal page: the full-height view that opens when a row is activated and
/// takes the list's place in the window.
///
/// <para><b>Why it replaces the list rather than sitting beside it.</b> The game really does draw
/// its journal as two addons in one rectangle — <c>Journal.uld</c> reserves an empty 496x650 node at
/// x=450 that is exactly where <c>JournalDetail</c> lands — but a cursor cannot be moved from one
/// addon into another through KamiToolKit, and the window is 760 wide where the game's pair is 946.
/// Replacing the list is what the width allows and what a pad can navigate, and it buys the list its
/// height back: the strip cost it 291 pixels on every frame whether anyone was reading it or
/// not.</para>
///
/// <para><b>What is on it.</b> The journal's own anatomy, in its own metrics: a title in Axis 18
/// over a level on the game's 40x40 black disc, the 376x120 banner the game authors for exactly this
/// slot, the reward tray at its authored width, and — because they are in two columns rather than
/// one strip — the requirements and the description at the same time. The gilt
/// <c>Journal_Frame</c> is deliberately absent: it is assembled from fourteen nodes at hard-coded
/// positions inside a 496-wide page and there is no honest way to stretch it, so the window's own
/// chrome carries the frame and the glyphs, the type and the tray carry the journal.</para></summary>
internal sealed class HubJournalPageNode : ResNode
{
    private const int MaxActions = 3;

    private readonly IPluginLog log;
    private readonly HorizontalLineNode rule;
    private readonly SimpleImageNode levelBadgeNode;
    private readonly TextNode levelNode;
    private readonly TextNode titleNode;
    private readonly TextNode kindNode;
    private readonly HorizontalLineNode titleRule;
    private readonly IconImageNode bannerNode;
    private readonly SimpleImageNode rewardGlyphNode;
    private readonly TextNode rewardLabelNode;
    private readonly SimpleImageNode rewardTrayNode;
    private readonly IconImageNode rewardIconNode;
    private readonly TextNode rewardNameNode;
    private readonly IconImageNode statusIconNode;
    private readonly TextNode statusNode;
    private readonly SimpleImageNode requirementsGlyphNode;
    private readonly TextNode requirementsLabelNode;
    private readonly TextNode requirementsNode;
    private readonly SimpleImageNode descriptionGlyphNode;
    private readonly TextNode descriptionLabelNode;
    private readonly TextNode descriptionNode;
    private readonly SimpleImageNode informationGlyphNode;
    private readonly TextNode informationLabelNode;
    private readonly TextNode informationNode;
    private readonly TextNode provenanceNode;
    private readonly HorizontalLineNode footerRule;
    private readonly AlignedHorizontalListNode actionRow;
    private readonly TextButtonNode[] actionButtons = new TextButtonNode[MaxActions];

    private bool hasLevel;
    private bool hasBanner;
    private bool hasStatusIcon;
    private bool hasReward;
    private bool hasRewardIcon;
    private bool hasProvenance;
    private int wantedDescriptionLines;
    private int wantedRequirementLines;
    private int wantedInformationLines;

    public HubJournalPageNode(IPluginLog log)
    {
        this.log = log;

        rule = NewRule();
        levelBadgeNode = JournalNodes.Art(
            this, log, GameMetrics.JournalArt.LevelBadge, GameMetrics.Journal.BadgeSize);
        levelNode = JournalNodes.Level(this);

        // Two Axis-18 lines, wrapping, which is what JournalDetail #38 reserves h=50 for. The strip
        // ellipsises its title because it has one line; the page does not have to.
        titleNode = JournalNodes.Title(this, TextFlags.MultiLine | TextFlags.WordWrap);
        kindNode = JournalNodes.Kind(this);
        titleRule = NewRule();

        bannerNode = JournalNodes.Marker(
            this, new Vector2(GameMetrics.Journal.BannerWidth, GameMetrics.Journal.BannerHeight));

        (rewardGlyphNode, rewardLabelNode, rewardTrayNode, rewardIconNode, rewardNameNode) =
            BuildRewardSection();

        statusIconNode = JournalNodes.Marker(
            this,
            new Vector2(GameMetrics.Detail.HeadingIconSize, GameMetrics.Detail.HeadingIconSize));
        statusNode = JournalNodes.Line(
            this, GameMetrics.Type.BodySize, GameColors.ListText, TextFlags.WordWrap | TextFlags.MultiLine);

        // Section #22, and the game's own words for it: Addon 479 is the sentence a locked quest
        // gets, and this pair is the heading form of the same thing.
        (requirementsGlyphNode, requirementsLabelNode, requirementsNode) =
            Section(GameMetrics.JournalArt.GlyphDocument, "Requirements not met");

        // Section #5: the open book over the description.
        (descriptionGlyphNode, descriptionLabelNode, descriptionNode) =
            Section(GameMetrics.JournalArt.GlyphDescription, "Description");

        // Section #18: the game puts a document over Information; the person silhouette in the same
        // strip of art is the better glyph for a block whose first line names a quest giver.
        (informationGlyphNode, informationLabelNode, informationNode) =
            Section(GameMetrics.JournalArt.GlyphPerson, "Information");

        provenanceNode = JournalNodes.Line(
            this, GameMetrics.Type.SecondarySize, GameColors.Dimmed, TextFlags.Ellipsis);
        provenanceNode.AlignmentType = AlignmentType.Top;

        footerRule = NewRule();

        var (row, back) = BuildActionRow();
        actionRow = row;
        Back = back;
    }

    /// <summary>What Back does. Set by the window, which owns "the page is open".</summary>
    public Action? OnBack { get; set; }

    /// <summary>The action row, so the window can number it into the cursor graph. Back is the first
    /// node in it, and therefore the block's first index — see <see cref="HubNavPlan.JournalPage"/>.
    /// </summary>
    public NodeBase ActionRow => actionRow;

    /// <summary>The Back button, so the window can put the cursor on it when the page opens.
    /// </summary>
    public TextButtonNode Back { get; }

    /// <summary>Fills the page with one entry. Not guarded by reference like the strip's
    /// <c>Show</c>: the page is opened by a deliberate press rather than by the cursor passing over a
    /// row, so it is written once per open rather than once per d-pad step.</summary>
    public void Show(HubRowDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        titleNode.String = HeadingText.Plain(detail.Title);
        kindNode.String = detail.Kind;

        hasLevel = detail.Level.Length > 0;
        levelNode.String = detail.Level;

        ApplyBanner(detail);
        ApplyReward(detail);
        ApplyStatus(detail);

        requirementsNode.String = DetailText.Bullets(
            detail.Requirements, JournalPageLayout.MaxRequirementLines, out wantedRequirementLines);

        descriptionNode.String = detail.Body;
        wantedDescriptionLines = detail.Body.Length == 0 ? 0 : JournalPageLayout.MaxDescriptionLines;

        informationNode.String = DetailText.Lines(
            Information(detail), JournalPageLayout.MaxInformationLines, out wantedInformationLines);

        hasProvenance = detail.Provenance.Length > 0;
        provenanceNode.String = detail.Provenance;

        ApplyActions(detail.Actions);
        Layout();
    }

    /// <summary>Re-runs the layout after a resize. The page is positioned by the window, so its own
    /// contents have to be re-flowed against the new width — and the page's width is what decides
    /// whether it is two columns or one.</summary>
    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        Layout();
    }

    /// <summary>The Information section's lines: who and where, at what coordinates, from which
    /// quest. Three facts a player standing in the wrong zone actually needs, and none of them fits
    /// on the strip.</summary>
    private static List<string> Information(HubRowDetail detail)
    {
        var lines = new List<string>(2);
        var where = detail.Coordinates.Length > 0 && detail.From.Length > 0
            ? $"{detail.From} {detail.Coordinates}"
            : detail.From + detail.Coordinates;

        if (where.Length > 0)
        {
            lines.Add(where);
        }

        if (detail.QuestName.Length > 0)
        {
            lines.Add($"Quest: {detail.QuestName}");
        }

        return lines;
    }

    private static void Place(NodeBase node, ScreenRect rect)
    {
        node.IsVisible = !rect.IsEmpty;
        if (rect.IsEmpty)
        {
            return;
        }

        node.Position = new Vector2(rect.X, rect.Y);
        node.Size = new Vector2(rect.Width, rect.Height);
    }

    private static HorizontalLineNode NewRule() => new();

    /// <summary>Journal canvas section <c>#26</c>: the treasure chest over Reward, the tray it sits
    /// on, and one slot's icon and name.</summary>
    private (SimpleImageNode Glyph, TextNode Label, SimpleImageNode Tray, IconImageNode Icon, TextNode Name)
        BuildRewardSection() =>
        (JournalNodes.Art(this, log, GameMetrics.JournalArt.GlyphReward, GameMetrics.Journal.GlyphSize),
            JournalNodes.Heading(this, "Reward"),
            JournalNodes.Art(
                this,
                log,
                GameMetrics.JournalArt.TrayOneRow,
                GameMetrics.Journal.ColumnWidth,
                GameMetrics.Journal.TrayHeight),
            JournalNodes.Marker(
                this, new Vector2(GameMetrics.Journal.SlotIconSize, GameMetrics.Journal.SlotIconSize)),
            JournalNodes.Line(this, GameMetrics.Type.BodySize, GameColors.ListText, TextFlags.Ellipsis));

    private (SimpleImageNode Glyph, TextNode Label, TextNode Body) Section(
        (float U, float V) glyph, string heading) =>
        (JournalNodes.Art(this, log, glyph, GameMetrics.Journal.GlyphSize),
            JournalNodes.Heading(this, heading),
            JournalNodes.Line(
                this, GameMetrics.Type.BodySize, GameColors.Body, TextFlags.WordWrap | TextFlags.MultiLine));

    /// <summary>The banner: the duty's own art when there is one, the gate quest's otherwise, and
    /// nothing at all for the entries the game ships no picture for — 166 of the 587 in the
    /// catalogue, almost all of them system unlocks, which are features rather than places. The
    /// block is dropped whole rather than filled with a placeholder.</summary>
    private void ApplyBanner(HubRowDetail detail)
    {
        hasBanner = detail.BannerIconId != 0;
        if (hasBanner)
        {
            JournalNodes.ApplyIcon(bannerNode, detail.BannerIconId, HubJournalFacts.SourceSize);
        }
    }

    private void ApplyStatus(HubRowDetail detail)
    {
        hasStatusIcon = detail.StatusIconId != 0;
        if (hasStatusIcon)
        {
            JournalNodes.ApplyIcon(
                statusIconNode, detail.StatusIconId, HubStatusIcons.SourceSize(detail.StatusIconId));
        }

        statusNode.String = detail.StatusSentence;
    }

    /// <summary>The tray's contents, at page scale — the same resolution and the same graceful
    /// absence the strip already has. The tray and the name are drawn whether or not there is a
    /// picture, so an entry with no icon reads as "here is what you get" rather than as a slot that
    /// failed to load.</summary>
    private void ApplyReward(HubRowDetail detail)
    {
        hasReward = detail.RewardName.Length > 0;
        rewardNameNode.String = detail.RewardName;

        hasRewardIcon = hasReward && detail.RewardIconId != 0;
        if (hasRewardIcon)
        {
            JournalNodes.ApplyIcon(rewardIconNode, detail.RewardIconId, detail.RewardIconSize);
        }
    }

    /// <summary>Back, then the entry's actions, in one row. One row rather than two because the
    /// walker numbers a horizontal container as a single row that chains left and right and wraps at
    /// both ends — so Back is one press from the far end of the row as well as from its
    /// neighbour.</summary>
    private (AlignedHorizontalListNode Row, TextButtonNode Back) BuildActionRow()
    {
        var row = new AlignedHorizontalListNode
        {
            Height = GameMetrics.Control.ButtonHeight,
            FitToContentHeight = true,
            ItemSpacing = GameMetrics.Control.ButtonGap,
        };

        var back = new TextButtonNode
        {
            Width = GameMetrics.Control.ButtonWidthMedium,
            Height = GameMetrics.Control.ButtonHeight,
            String = "Back",
            OnClick = () => OnBack?.Invoke(),
        };
        row.AddNode(back);

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
        return (row, back);
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

                // Enabled, always: a button on screen here is one this entry can actually do. The
                // inapplicable ones are absent, not greyed.
                button.IsEnabled = true;
                button.IsVisible = true;
            }
            else
            {
                button.OnClick = null;
                button.IsVisible = false;
            }
        }

        Back.IsVisible = true;
        actionRow.RecalculateLayout();
    }

    private void Layout()
    {
        var blocks = JournalPageLayout.Compose(
            Width,
            Height,
            hasLevel,
            hasBanner,
            hasStatusIcon,
            wantedRequirementLines,
            hasReward,
            wantedDescriptionLines,
            wantedInformationLines,
            hasProvenance);

        Place(rule, blocks.Rule);
        Place(titleRule, blocks.TitleRule);
        Place(footerRule, blocks.FooterRule);

        // The number and its disc share one rectangle: the game centres the numeral on the plate
        // (JournalDetail #9 over #10) rather than setting it beside.
        Place(levelBadgeNode, blocks.LevelBadge);
        Place(levelNode, blocks.LevelBadge);
        Place(titleNode, blocks.Title);
        Place(kindNode, blocks.Kind);

        Place(bannerNode, blocks.Banner);
        Place(rewardGlyphNode, blocks.RewardGlyph);
        Place(rewardLabelNode, blocks.RewardLabel);
        Place(rewardTrayNode, blocks.RewardTray);
        Place(rewardIconNode, hasRewardIcon ? blocks.RewardIcon : default);
        Place(rewardNameNode, blocks.RewardName);

        Place(statusIconNode, blocks.StatusIcon);
        Place(statusNode, blocks.Status);
        Place(requirementsGlyphNode, blocks.RequirementsGlyph);
        Place(requirementsLabelNode, blocks.RequirementsLabel);
        Place(requirementsNode, blocks.Requirements);
        Place(descriptionGlyphNode, blocks.DescriptionGlyph);
        Place(descriptionLabelNode, blocks.DescriptionLabel);
        Place(descriptionNode, blocks.Description);
        Place(informationGlyphNode, blocks.InformationGlyph);
        Place(informationLabelNode, blocks.InformationLabel);
        Place(informationNode, blocks.Information);
        Place(provenanceNode, blocks.Provenance);

        // Pinned to the bottom edge rather than flowed after the text: where Back is must not depend
        // on how much this particular entry had to say.
        actionRow.Position = new Vector2(blocks.Actions.X, blocks.Actions.Y);
        actionRow.Size = new Vector2(blocks.Actions.Width, blocks.Actions.Height);
        actionRow.RecalculateLayout();
    }
}
