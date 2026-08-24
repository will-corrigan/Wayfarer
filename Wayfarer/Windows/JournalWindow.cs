using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using Wayfarer.Core.Ui;
using Wayfarer.Windows.Native;

namespace Wayfarer.Windows;

/// <summary>The journal's page, as its own window beside the Wayfarer window.
///
/// <para><b>Why a second window.</b> The game's Journal is two addons in one composition:
/// <c>Journal</c> is a plain list on the left and <c>JournalDetail</c> is an ornate parchment page on
/// the right. <c>Journal.uld</c> proves the pairing outright — its node <c>#9</c> is an empty
/// <c>Res</c> at (450,-40) sized 496x650, which is exactly where <c>JournalDetail</c>'s own root
/// lands. Drawing the page inside the hub window instead cost three things at once: the page had to
/// fit whatever width the player had dragged the hub to, so it could not wear the gilt border; the
/// list had to be hidden to make room, so the game's own "cursor moves, page updates" contract was
/// impossible; and Cancel on a pad closed the whole window rather than the page. A separate addon
/// fixes all three, and the third one for free — Cancel closes the addon that has focus, and now the
/// addon that has focus is the page.</para>
///
/// <para><b>Chromeless on purpose.</b> <c>JournalDetail</c> has no window component at all: its
/// chrome <i>is</i> the parchment nine-grid and the gilt border, and a standard window frame around
/// that would be a frame inside a frame. So the window node is supplied already invisible — the same
/// trick the readout's clickable host uses — and <see cref="JournalFrameNode"/> is the whole of the
/// visible edge. The consequence is that this window is not dragged and has no title-bar close box,
/// which is also true of the game's: <c>JournalDetail</c> is positioned by <c>Journal</c> and closed
/// with it.</para>
///
/// <para><b>Fixed width, free height.</b> <see cref="GameMetrics.JournalFrame.Width"/>, always. Every
/// number on this surface — the border's horizontal run, the 376 banner, the 376 reward tray, the 394
/// canvas column — is authored for that one width, and the border cannot be stretched to any
/// other.</para>
///
/// <para><b>Text is measured, not counted.</b> Every wrapping block is set, measured with
/// <c>GetTextDrawSize</c> against this column's width, and shortened until it fits the room the
/// layout granted it. The page this replaces sized its blocks by a line <i>count</i>, which is how a
/// one-line requirement that wrapped to five came to be drawn under a description.</para></summary>
internal sealed unsafe class JournalWindow(JournalWords words, IFramework framework, IPluginLog log)
    : NativeAddon
{
    /// <summary>How many action buttons the row can hold: the entry's three, plus Back.</summary>
    private const int MaxActions = 3;

    /// <summary>Where this window's own cursor graph starts. Its own addon, so its own index space —
    /// nothing here shares a byte with the hub's <see cref="HubNavPlan"/>, and 1 is simply the first
    /// index that is not the reserved "no navigation".</summary>
    private const int NavStart = 1;

    private JournalFrameNode? frame;
    private SimpleImageNode? levelBadgeNode;
    private TextNode? levelNode;
    private TextNode? titleNode;
    private TextNode? kindNode;
    private SimpleImageNode? titleRuleNode;
    private IconImageNode? statusIconNode;
    private TextNode? statusNode;
    private IconImageNode? bannerNode;
    private SimpleImageNode? rewardGlyphNode;
    private TextNode? rewardLabelNode;
    private SimpleImageNode? rewardTrayNode;
    private IconImageNode? rewardIconNode;
    private TextNode? rewardNameNode;
    private SimpleImageNode? descriptionGlyphNode;
    private TextNode? descriptionLabelNode;
    private TextNode? descriptionNode;
    private SimpleImageNode? requirementsGlyphNode;
    private TextNode? requirementsLabelNode;
    private TextNode? requirementsNode;
    private TextNode? giverNode;
    private TextNode? provenanceNode;
    private SimpleImageNode? footerRuleNode;
    private SimpleImageNode? bossNode;
    private AlignedHorizontalListNode? actionRow;
    private TextButtonNode? backButton;
    private TextButtonNode[] actionButtons = [];

    private HubRowDetail? entry;
    private bool wantsFocus;
    private string giverLine = string.Empty;

    /// <summary>Where the window was last put, so PlaceBeside can be called every tick without
    /// writing a position every tick.</summary>
    private Vector2 placedAt = new(float.NaN, float.NaN);

    /// <summary>What Back does — set by the hub, which owns "which row this page is for".</summary>
    public Action? OnBack { get; set; }

    /// <summary>Called when the window has gone away by any route: its own Back button, Cancel on a
    /// pad, or the game's close-all. The hub uses it to put the cursor back on the row.</summary>
    public Action? OnClosed { get; set; }

    /// <summary>Shows one entry, opening the window if it is not already open.
    /// <paramref name="takeFocus"/> is what a controller wants and a mouse does not: a pad has to be
    /// moved into the page deliberately, but a mouse player who clicked a row is still holding the
    /// mouse and must not have the keyboard focus taken from under them.</summary>
    public void Show(HubRowDetail detail, bool takeFocus)
    {
        ArgumentNullException.ThrowIfNull(detail);

        entry = detail;
        wantsFocus = takeFocus;

        if (!IsOpen)
        {
            // The tree is rebuilt on every open — NativeAddon deallocates it on close — so the fill
            // happens in OnSetup instead, from the entry just stored.
            Open();
            return;
        }

        Fill();
        if (takeFocus)
        {
            FocusFirstControl();
        }
    }

    /// <summary>Puts the window beside another one, at the offset the game uses. <c>Journal.uld</c>
    /// reserves its detail page at x=450 y=-40 relative to a 462-wide list panel, so the page starts
    /// twelve pixels inside the list's right edge and forty above its top — a deliberate overlap that
    /// lets the border's ornament cross the seam.</summary>
    public void PlaceBeside(Vector2 hostPosition, Vector2 hostSize)
    {
        if (!IsOpen)
        {
            return;
        }

        var wanted = new Vector2(
            hostPosition.X + hostSize.X - GameMetrics.JournalFrame.BesideOverlapX,
            hostPosition.Y - GameMetrics.JournalFrame.BesideOverlapY);

        // Idempotent, because the caller runs this every tick — which is how the page follows a
        // window that is being dragged, and also how it catches the frame after Open() in which the
        // addon has only just become open. Writing a position every frame regardless would be a
        // window nothing else could move.
        if (Vector2.DistanceSquared(wanted, placedAt) < 1f)
        {
            return;
        }

        placedAt = wanted;
        SetWindowPosition(wanted);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        // Same marshalling as the hub window: Dalamud unloads plugins on a thread-pool thread while
        // Close() asserts the main thread.
        if (framework.IsInFrameworkUpdateThread)
        {
            base.Dispose();
            return;
        }

        try
        {
            framework.RunOnFrameworkThread(() => base.Dispose()).Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            const string message =
                "Wayfarer journal: disposing the journal window on the framework thread failed or timed out, so "
                + "it may remain on screen until the game is restarted.";
            log.Warning(ex, message);
        }
    }

    /// <inheritdoc/>
    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        Build();
        Fill();

        if (wantsFocus)
        {
            FocusFirstControl();
        }
    }

    /// <summary>Called as the game takes the addon away, by whatever route. This is where Cancel on
    /// a pad is handled: there is no hook for the button itself, but the addon closing <i>is</i> the
    /// event, and on this window that means exactly one thing — the player is done with the page.
    /// </summary>
    protected override void OnHide(AtkUnitBase* addon) => OnClosed?.Invoke();

    /// <inheritdoc/>
    protected override void OnFinalize(AtkUnitBase* addon)
    {
        placedAt = new Vector2(float.NaN, float.NaN);
        frame = null;
        levelBadgeNode = null;
        levelNode = null;
        titleNode = null;
        kindNode = null;
        titleRuleNode = null;
        statusIconNode = null;
        statusNode = null;
        bannerNode = null;
        rewardGlyphNode = null;
        rewardLabelNode = null;
        rewardTrayNode = null;
        rewardIconNode = null;
        rewardNameNode = null;
        descriptionGlyphNode = null;
        descriptionLabelNode = null;
        descriptionNode = null;
        requirementsGlyphNode = null;
        requirementsLabelNode = null;
        requirementsNode = null;
        giverNode = null;
        provenanceNode = null;
        footerRuleNode = null;
        bossNode = null;
        actionRow = null;
        backButton = null;
        actionButtons = [];
    }

    private static void Place(NodeBase? node, ScreenRect rect)
    {
        if (node is null)
        {
            return;
        }

        node.IsVisible = !rect.IsEmpty;
        if (rect.IsEmpty)
        {
            return;
        }

        node.Position = new Vector2(rect.X, rect.Y);
        node.Size = new Vector2(rect.Width, rect.Height);
    }

    /// <summary>The one line at the foot: who hands it over, and where they stand. The quest's own
    /// name is not on it — the quest is <i>how you get this</i> and the page is about <i>what it
    /// is</i>.</summary>
    private static string GiverLine(HubRowDetail detail)
    {
        if (detail.From.Length == 0)
        {
            return detail.Coordinates;
        }

        return detail.Coordinates.Length == 0
            ? detail.From
            : $"{detail.From} {detail.Coordinates}";
    }

    private static void ApplyIcon(IconImageNode node, uint iconId, Vector2 authored)
    {
        if (iconId == 0)
        {
            node.IsVisible = false;
            return;
        }

        JournalNodes.ApplyIcon(node, iconId, authored);
    }

    /// <summary>The rule, at the width its art is authored at, centred in the block the layout
    /// gave it. The game draws this image and never stretches it, so neither does this.</summary>
    private static ScreenRect Rule(ScreenRect block)
    {
        if (block.IsEmpty)
        {
            return default;
        }

        var width = Math.Min(GameMetrics.JournalArt.DividerWidth, block.Width);
        return new ScreenRect(
            block.X + ((block.Width - width) / 2f),
            block.Y,
            width,
            GameMetrics.JournalArt.DividerHeight);
    }

    /// <summary>Sets the requirements block — the game's own "not yet available" sentence when there
    /// is one, then the unmet requirements as bullets — and shortens it until it measures inside
    /// <paramref name="allowance"/>, returning the height it ended up at.</summary>
    private static float FitBullets(
        TextNode node, string? lead, IReadOnlyList<string> lines, float allowance)
    {
        var wanted = lines.Count + (lead is null ? 0 : 1);
        if (wanted == 0 || allowance <= 0f)
        {
            node.String = string.Empty;
            return 0f;
        }

        for (var budget = Math.Min(wanted, JournalWindowLayout.MaxRequirementLines); budget >= 1; budget--)
        {
            var measured = Measure(node, Compose(lead, lines, budget), allowance);
            if (measured <= allowance)
            {
                return measured;
            }
        }

        // Nothing fits even one wrapped line. One ellipsised line is the honest floor: it says there
        // is a requirement and that it did not fit, rather than running off the page.
        return Truncate(node, Compose(lead, lines, 1), allowance);
    }

    private static string Compose(string? lead, IReadOnlyList<string> lines, int budget) =>
        lead is null
            ? DetailText.Bullets(lines, budget, out _)
            : DetailText.Led(lead, lines, budget, out _);

    /// <summary>The same for a paragraph, shortened a sentence at a time from the end.</summary>
    private static float Fit(TextNode node, string text, float allowance)
    {
        if (text.Length == 0 || allowance <= 0f)
        {
            node.String = string.Empty;
            return 0f;
        }

        var measured = Measure(node, text, allowance);
        if (measured <= allowance)
        {
            return measured;
        }

        // Trim words off the end until it fits, then mark the cut. A paragraph has no line
        // structure to give up, so this is the only unit there is.
        var pieces = text.Split(' ');
        for (var take = pieces.Length - 1; take >= 1; take--)
        {
            var shortened = string.Join(' ', pieces.Take(take)) + "…";
            measured = Measure(node, shortened, allowance);
            if (measured <= allowance)
            {
                return measured;
            }
        }

        return Truncate(node, text, allowance);
    }

    /// <summary>The last resort: drop the wrap, keep one line, let the node ellipsise it. Bounded by
    /// construction, which is what makes the fitting loops above safe to give up on.</summary>
    private static float Truncate(TextNode node, string text, float allowance)
    {
        node.RemoveTextFlags(TextFlags.MultiLine, TextFlags.WordWrap);
        node.AddTextFlags(TextFlags.Ellipsis);
        node.String = text.ReplaceLineEndings(" ");
        return Math.Min(JournalWindowLayout.BlockHeight(1), allowance);
    }

    /// <summary>Sets the text and asks the game how tall it draws at this column's width.
    ///
    /// <para>The node's own width has to be right before the measurement, because that is what the
    /// wrap is computed against — which is the whole reason this is done here rather than guessed
    /// from a character count in Core. Restores the wrapping flags first: an earlier
    /// <see cref="Truncate"/> may have taken them away.</para></summary>
    private static float Measure(TextNode node, string text, float allowance)
    {
        node.RemoveTextFlags(TextFlags.Ellipsis);
        node.AddTextFlags(TextFlags.MultiLine, TextFlags.WordWrap);
        node.Width = GameMetrics.Journal.ColumnWidth;
        node.Height = Math.Max(allowance, JournalWindowLayout.BlockHeight(1));
        node.String = text;

        var drawn = node.GetTextDrawSize(considerScale: false).Y;
        return drawn > 0f ? drawn : JournalWindowLayout.BlockHeight(1);
    }

    private void Build()
    {
        frame = new JournalFrameNode(log) { Position = Vector2.Zero };
        AddNode(frame);

        levelBadgeNode = JournalNodes.Art(
            frame, log, GameMetrics.JournalArt.LevelBadge, GameMetrics.Journal.BadgeSize);
        levelNode = JournalNodes.Level(frame);
        titleNode = JournalNodes.Title(frame, TextFlags.MultiLine | TextFlags.WordWrap);
        kindNode = JournalNodes.Kind(frame);
        titleRuleNode = Divider();

        statusIconNode = JournalNodes.Marker(
            frame, new Vector2(GameMetrics.Detail.HeadingIconSize, GameMetrics.Detail.HeadingIconSize));
        statusNode = JournalNodes.Line(
            frame, GameMetrics.Type.BodySize, GameColors.ListText, TextFlags.Ellipsis);

        bannerNode = JournalNodes.Marker(
            frame, new Vector2(GameMetrics.Journal.BannerWidth, GameMetrics.Journal.BannerHeight));

        rewardGlyphNode = JournalNodes.Art(
            frame, log, GameMetrics.JournalArt.GlyphReward, GameMetrics.Journal.GlyphSize);
        rewardLabelNode = JournalNodes.Heading(frame, words.Reward);
        rewardTrayNode = JournalNodes.Art(
            frame,
            log,
            GameMetrics.JournalArt.TrayOneRow,
            GameMetrics.Journal.ColumnWidth,
            GameMetrics.Journal.TrayHeight);
        rewardIconNode = JournalNodes.Marker(
            frame, new Vector2(GameMetrics.Journal.SlotIconSize, GameMetrics.Journal.SlotIconSize));
        rewardNameNode = JournalNodes.Line(
            frame, GameMetrics.Type.BodySize, GameColors.ListText, TextFlags.Ellipsis);

        (descriptionGlyphNode, descriptionLabelNode, descriptionNode) =
            Section(GameMetrics.JournalArt.GlyphDescription, words.Description);
        (requirementsGlyphNode, requirementsLabelNode, requirementsNode) =
            Section(GameMetrics.JournalArt.GlyphDocument, words.Requirements);

        giverNode = Giver();
        provenanceNode = Provenance();
        footerRuleNode = Divider();
        bossNode = Boss();

        BuildActionRow();
    }

    /// <summary>The journal's own rule — <c>Journal_Detail.tex</c> (0,24) 392x4, the image
    /// JournalDetail draws under its title (<c>#39</c>) and above its buttons (<c>#48</c>). The
    /// codebase's <c>HorizontalLineNode</c> is the same four pixels of different art; this is the
    /// page's own, and on this surface that is the point.</summary>
    private SimpleImageNode Divider() =>
        JournalNodes.Art(
            frame!,
            log,
            GameMetrics.JournalArt.Divider,
            GameMetrics.JournalArt.DividerWidth,
            GameMetrics.JournalArt.DividerHeight);

    /// <summary>The gold rivet at the foot of the page. Ornament, not a control: see
    /// <see cref="GameMetrics.JournalFrame.BossSize"/> for why the slot the game gives a button is
    /// given a piece of its border sheet instead.</summary>
    private SimpleImageNode Boss()
    {
        var node = new SimpleImageNode
        {
            Size = new Vector2(GameMetrics.JournalFrame.BossSize, GameMetrics.JournalFrame.BossSize),
            WrapMode = WrapMode.Tile,
            IsVisible = false,
        };

        try
        {
            node.LoadTexture(GameMetrics.JournalFrame.Texture);
            node.TextureCoordinates = new Vector2(
                GameMetrics.JournalFrame.Boss.U, GameMetrics.JournalFrame.Boss.V);
            node.TextureSize = node.Size;
        }
        catch (Exception ex)
        {
            const string why =
                "Wayfarer journal: the journal border's own sheet could not be read, so the rivet at the foot of "
                + "the page is not drawn. Nothing else is affected.";
            log.Warning(ex, why);
        }

        node.AttachNode(frame!);
        return node;
    }

    /// <summary>The giver, right-aligned at the foot — where the game's own journal, and the
    /// player's screenshot, put the name of whoever hands the thing over.</summary>
    private TextNode Giver()
    {
        var node = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.BodySize,
            LineSpacing = GameMetrics.Type.BodyLine,
            AlignmentType = AlignmentType.TopRight,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.ListText,
        };
        node.AttachNode(frame!);
        return node;
    }

    /// <summary>The confidence footnote. JournalCanvas <c>#54</c>'s register: Axis 12, centred,
    /// dimmed — the line the game reserves for a caveat.</summary>
    private TextNode Provenance()
    {
        var node = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = GameMetrics.Type.SecondarySize,
            LineSpacing = GameMetrics.Type.SecondaryLine,
            AlignmentType = AlignmentType.Top,
            TextFlags = TextFlags.Ellipsis,
            TextColor = GameColors.Dimmed,
        };
        node.AttachNode(frame!);
        return node;
    }

    private (SimpleImageNode Glyph, TextNode Label, TextNode Body) Section(
        (float U, float V) glyph, string heading) =>
        (JournalNodes.Art(frame!, log, glyph, GameMetrics.Journal.GlyphSize),
            JournalNodes.Heading(frame!, heading),
            JournalNodes.Line(
                frame!,
                GameMetrics.Type.BodySize,
                GameColors.Body,
                TextFlags.WordWrap | TextFlags.MultiLine));

    /// <summary>Back and the entry's actions, in one row. One row rather than two because the
    /// walker numbers a horizontal container as a single row that chains left and right and wraps at
    /// both ends — so Back is one press from the far end of the row as well as from its
    /// neighbour.</summary>
    private void BuildActionRow()
    {
        actionRow = new AlignedHorizontalListNode
        {
            Height = GameMetrics.Control.ButtonHeight,
            FitToContentHeight = true,
            ItemSpacing = GameMetrics.Control.ButtonGap,
        };

        backButton = new TextButtonNode
        {
            Width = GameMetrics.Control.ButtonWidthMedium,
            Height = GameMetrics.Control.ButtonHeight,
            String = "Back",
            OnClick = () => OnBack?.Invoke(),
        };
        actionRow.AddNode(backButton);

        actionButtons = new TextButtonNode[MaxActions];
        for (var i = 0; i < MaxActions; i++)
        {
            actionButtons[i] = new TextButtonNode
            {
                Width = GameMetrics.Control.ButtonWidthMedium,
                Height = GameMetrics.Control.ButtonHeight,
                IsVisible = false,
            };
            actionRow.AddNode(actionButtons[i]);
        }

        actionRow.AttachNode(frame!);
    }

    /// <summary>Writes the stored entry into the tree and lays it out. Separate from
    /// <see cref="Build"/> so re-showing a different entry into an open window is a fill and a layout
    /// rather than a rebuild — which is what lets the page follow the list's cursor.</summary>
    private void Fill()
    {
        if (frame is null || entry is not { } detail)
        {
            return;
        }

        titleNode!.String = HeadingText.Plain(detail.Title);
        kindNode!.String = detail.Kind;
        levelNode!.String = detail.Level;

        ApplyIcon(bannerNode!, detail.BannerIconId, HubJournalFacts.SourceSize);
        ApplyIcon(statusIconNode!, detail.StatusIconId, new Vector2(GameMetrics.Detail.HeadingIconSize));
        ApplyIcon(rewardIconNode!, detail.RewardIconId, detail.RewardIconSize);
        rewardNameNode!.String = detail.RewardName;

        // The requirements are assembled before the state line, because whether there are any is what
        // decides what the state line is allowed to say. See JournalRequirementText.
        var requirements = detail.Requirements;
        var lead = RequirementsLead(detail);
        statusNode!.String = JournalRequirementText.StatusLine(
            detail.StatusWord, detail.StatusSentence, requirements.Count > 0 || lead is not null);

        descriptionNode!.String = detail.Body;
        giverLine = GiverLine(detail);
        giverNode!.String = giverLine;
        provenanceNode!.String = detail.Provenance;

        ApplyActions(detail.Actions);
        Resize();
        Layout(lead, requirements);
        ApplyNavigation();
    }

    /// <summary>The requirements block's lines, led by the game's own sentence for this shape of gate
    /// when there is one.
    ///
    /// <para><c>Addon</c> row 479 — "This quest is not yet available." — is the string
    /// <c>AddonJournalDetail</c>'s own <c>RequirementsNotMetLabelTextNode</c> (<c>#33</c>) is authored
    /// with, so leading with it is the game's own idiom in the game's own words, already localised.
    /// It is offered only for a quest gate: see <see cref="HubRowDetail.GatedByQuest"/>.</para>
    /// </summary>
    private string? RequirementsLead(HubRowDetail detail) =>
        JournalRequirementText.NotMetLead(words.NotAvailable, detail.GatedByQuest);

    /// <summary>Asks the game for the height a fully populated entry wants, clamped to the border's
    /// own minimum and to the viewport. The width is never negotiated.</summary>
    private void Resize()
    {
        var wanted = Math.Max(JournalWindowLayout.NaturalHeight, GameMetrics.JournalFrame.MinHeight);
        var viewport = AtkStage.Instance()->ScreenSize.Height;
        var scale = InternalAddon is null || InternalAddon->Scale <= 0f ? 1f : InternalAddon->Scale;
        var cap = viewport <= 0 ? wanted : viewport / scale;

        var height = Math.Clamp(wanted, GameMetrics.JournalFrame.MinHeight, Math.Max(cap, GameMetrics.JournalFrame.MinHeight));
        SetWindowSize(new Vector2(GameMetrics.JournalFrame.Width, height));
        frame!.Size = new Vector2(GameMetrics.JournalFrame.Width, height);
        frame.Layout();
    }

    /// <summary>Places everything, having first shortened the two wrapping blocks to what the layout
    /// is willing to give them.
    ///
    /// <para>Two passes, and the order matters. The first composes with what the text <i>wants</i>,
    /// which tells each block how much room it is actually getting; the second re-measures the
    /// shortened strings and composes again, so the rectangles the nodes are given are the
    /// rectangles the text really occupies. Without the second pass a block that had to give up two
    /// lines would leave a two-line hole and the block under it would sit in the wrong place — which
    /// is the visible half of the same defect as drawing text on top of text.</para></summary>
    private void Layout(string? lead, IReadOnlyList<string> requirements)
    {
        var height = frame!.Height;
        var hasReward = entry!.RewardName.Length > 0;
        var hasBanner = entry.BannerIconId != 0;

        var requirementAllowance = JournalWindowLayout.TextAllowance(
            height, JournalWindowLayout.MaxRequirementLines);
        var descriptionAllowance = JournalWindowLayout.TextAllowance(
            height, JournalWindowLayout.MaxDescriptionLines);

        var requirementHeight = FitBullets(requirementsNode!, lead, requirements, requirementAllowance);
        var descriptionHeight = Fit(descriptionNode!, entry.Body, descriptionAllowance);

        var blocks = JournalWindowLayout.Compose(
            height,
            hasLevel: entry.Level.Length > 0,
            hasStatusIcon: entry.StatusIconId != 0,
            hasBanner,
            hasReward,
            requirementHeight,
            descriptionHeight,
            hasGiver: giverLine.Length > 0,
            hasProvenance: entry.Provenance.Length > 0);

        // Second pass: whatever the ladder granted is the real budget, so re-fit against it and
        // compose once more. Cheap — two measures and one more pass of arithmetic.
        requirementHeight = FitBullets(requirementsNode!, lead, requirements, blocks.Requirements.Height);
        descriptionHeight = Fit(descriptionNode!, entry.Body, blocks.Description.Height);
        blocks = JournalWindowLayout.Compose(
            height,
            hasLevel: entry.Level.Length > 0,
            hasStatusIcon: entry.StatusIconId != 0,
            hasBanner,
            hasReward,
            requirementHeight,
            descriptionHeight,
            hasGiver: giverLine.Length > 0,
            hasProvenance: entry.Provenance.Length > 0);

        Apply(blocks);
    }

    private void Apply(JournalWindowBlocks blocks)
    {
        // The number and its disc share one rectangle: the game centres the numeral on the plate
        // (JournalDetail #9 over #10) rather than setting it beside.
        Place(levelBadgeNode, blocks.LevelBadge);
        Place(levelNode, blocks.LevelBadge);
        Place(titleNode, blocks.Title);
        Place(kindNode, blocks.Kind);
        Place(titleRuleNode, Rule(blocks.TitleRule));

        Place(statusIconNode, entry!.StatusIconId == 0 ? default : blocks.StatusIcon);
        Place(statusNode, blocks.Status);
        Place(bannerNode, entry.BannerIconId == 0 ? default : blocks.Banner);

        Place(rewardGlyphNode, blocks.RewardGlyph);
        Place(rewardLabelNode, blocks.RewardLabel);
        Place(rewardTrayNode, blocks.RewardTray);
        Place(rewardIconNode, entry.RewardIconId == 0 ? default : blocks.RewardIcon);
        Place(rewardNameNode, blocks.RewardName);

        Place(descriptionGlyphNode, blocks.DescriptionGlyph);
        Place(descriptionLabelNode, blocks.DescriptionLabel);
        Place(descriptionNode, blocks.Description);
        Place(requirementsGlyphNode, blocks.RequirementsGlyph);
        Place(requirementsLabelNode, blocks.RequirementsLabel);
        Place(requirementsNode, blocks.Requirements);

        Place(giverNode, blocks.Giver);
        Place(provenanceNode, blocks.Provenance);
        Place(footerRuleNode, Rule(blocks.FooterRule));
        Place(bossNode, blocks.Boss);

        if (actionRow is not null)
        {
            actionRow.Position = new Vector2(blocks.Actions.X, blocks.Actions.Y);
            actionRow.Size = new Vector2(blocks.Actions.Width, blocks.Actions.Height);
            actionRow.IsVisible = !blocks.Actions.IsEmpty;
            actionRow.RecalculateLayout();
        }
    }

    private void ApplyActions(IReadOnlyList<HubDetailAction> actions)
    {
        for (var i = 0; i < actionButtons.Length; i++)
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

        if (backButton is not null)
        {
            backButton.IsVisible = true;
        }

        actionRow?.RecalculateLayout();
    }

    /// <summary>Numbers this window's own cursor graph. Its own addon, so the graph is its own and
    /// starts at 1: the action row is one row that chains left and right and wraps at both ends, and
    /// there is nothing else here a pad can land on — the reward slot is an image with no collision
    /// node, exactly as the game's own is.</summary>
    private void ApplyNavigation()
    {
        if (actionRow is null)
        {
            return;
        }

        NavigationWalker.Apply(actionRow, NavStart, NavStart, NavStart, NavGraphPlanner.MaxIndex);

        if (InternalAddon is not null)
        {
            InternalAddon->UldManager.UpdateDrawNodeList();
            InternalAddon->UpdateCollisionNodeList(false);
        }
    }

    /// <summary>Puts the cursor on Back. Back rather than the first action, deliberately: the cursor
    /// lands on the way out, so a reflex confirm returns to the list rather than firing a navigation
    /// change nobody asked for.
    ///
    /// <para>This is also the whole of the cross-addon focus move. <c>ComponentNode.SetFocus</c>
    /// resolves the addon from the node itself and hands it to the game's input manager, so focusing
    /// a button in this window moves the game's focus into this window — which is exactly what the
    /// game does when it opens its own journal page, and the reason a pad can reach a second addon at
    /// all.</para></summary>
    private void FocusFirstControl()
    {
        try
        {
            backButton?.SetFocus();
        }
        catch (Exception ex)
        {
            const string why =
                "Wayfarer journal: the journal window opened but the controller cursor could not be moved into "
                + "it, so its buttons have to be clicked this time. Closing and reopening the page usually "
                + "recovers it.";
            log.Warning(ex, why);
        }
    }
}
