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
/// visible edge.</para>
///
/// <para><b>Fixed width, free height.</b> <see cref="GameMetrics.JournalFrame.Width"/>, always. Every
/// number on this surface — the border's horizontal run, the 376 banner, the 376 reward tray, the 394
/// canvas column — is authored for that one width, and the border cannot be stretched to any
/// other.</para>
///
/// <para><b>Nothing on this page is positioned by arithmetic.</b> This is the whole of the fix for
/// the defect that kept coming back — a description drawn over the Requirements heading, a title
/// wrapping onto the state line. Every block is a child of a <see cref="SectionStackNode"/>, which
/// places each one after the height the block itself reports; wrapping text reports that height
/// through <see cref="MeasuredTextNode"/>, and nothing else ever reads it. There is no y-cursor in
/// this file. The consequence a player sees is the second one: the page is as tall as its contents,
/// so the foot sits under the last thing on the page rather than at the bottom of a fixed box with a
/// band of empty parchment above it.</para></summary>
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
    private ResNode? pageClip;
    private SectionStackNode? page;

    private HorizontalListNode? header;
    private SimpleImageNode? levelBadgeNode;
    private TextNode? levelNode;
    private MeasuredTextNode? titleNode;
    private TextNode? kindNode;
    private HorizontalListNode? titleRuleRow;

    private HorizontalListNode? statusRow;
    private IconImageNode? statusIconNode;
    private TextNode? statusNode;

    private HorizontalListNode? bannerRow;
    private IconImageNode? bannerNode;

    private JournalSectionNode? rewardSection;
    private SimpleImageNode? rewardTrayNode;
    private IconImageNode? rewardIconNode;
    private TextNode? rewardNameNode;

    private JournalSectionNode? descriptionSection;
    private MeasuredTextNode? descriptionNode;
    private JournalSectionNode? requirementsSection;
    private MeasuredTextNode? requirementsNode;

    private TextNode? giverNode;
    private TextNode? provenanceNode;
    private HorizontalListNode? footerRuleRow;
    private HorizontalListNode? footRow;
    private SimpleImageNode? bossNode;
    private AlignedHorizontalListNode? actionRow;
    private TextButtonNode? backButton;
    private TextButtonNode[] actionButtons = [];

    private HubRowDetail? entry;
    private bool wantsFocus;

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

    /// <summary>Puts the window beside another one, at the offset the game uses, and keeps it inside
    /// the viewport.
    ///
    /// <para><c>Journal.uld</c> reserves its detail page at x=450 y=-40 relative to a 462-wide list
    /// panel, so the page starts twelve pixels inside the list's right edge and forty above its top —
    /// a deliberate overlap that lets the border's ornament cross the seam. Both numbers are authored
    /// in addon units, so both are scaled by the interface scale before they are added to a position
    /// in screen pixels; forgetting that put the page half an ornament out of place at anything other
    /// than 100%.</para>
    ///
    /// <para>The game has no parent-child relationship between two addons — an addon's position is
    /// its own, and <c>AtkUnitBase</c> has no owner field — so this is a per-frame follow rather than
    /// an attachment. The caller runs it every tick and it writes only when the answer has changed,
    /// which is what makes it cheap enough to be the mechanism: it tracks a drag, a resize, a preset
    /// that moves the hub, and a resolution change under an open page, and it also catches the frame
    /// after <c>Open()</c> in which the addon is not open yet.</para></summary>
    public void PlaceBeside(Vector2 hostPosition, Vector2 hostSize)
    {
        if (!IsOpen)
        {
            return;
        }

        var scale = UiScale();
        var wanted = JournalPlacement.Beside(
            hostPosition, hostSize, Size * scale, (Vector2)AtkStage.Instance()->ScreenSize, scale);

        if (Vector2.DistanceSquared(wanted, placedAt) < 1f)
        {
            return;
        }

        placedAt = wanted;
        SetWindowPosition(placedAt);
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
        pageClip = null;
        page = null;
        header = null;
        levelBadgeNode = null;
        levelNode = null;
        titleNode = null;
        kindNode = null;
        titleRuleRow = null;
        statusRow = null;
        statusIconNode = null;
        statusNode = null;
        bannerRow = null;
        bannerNode = null;
        rewardSection = null;
        rewardTrayNode = null;
        rewardIconNode = null;
        rewardNameNode = null;
        descriptionSection = null;
        descriptionNode = null;
        requirementsSection = null;
        requirementsNode = null;
        giverNode = null;
        provenanceNode = null;
        footerRuleRow = null;
        footRow = null;
        bossNode = null;
        actionRow = null;
        backButton = null;
        actionButtons = [];
    }

    private static float UiScale() => Math.Max(AtkUnitBase.GetGlobalUIScale(), 0.1f);

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

        node.IsVisible = true;
        JournalNodes.ApplyIcon(node, iconId, authored);
    }

    /// <summary>A single-line label sized to the room it is in — the state line, the giver, the
    /// footnote. Its height is a constant because a one-line node's height is not a question:
    /// <see cref="TextFlags.Ellipsis"/> makes it one line whatever the string is.</summary>
    private static void SetLine(TextNode node, string text, float width, float height)
    {
        node.Width = width;
        node.Height = height;
        node.String = text;
        node.IsVisible = text.Length > 0;
    }

    /// <summary>What is left of the title band's width once the badge and the kind caption have had
    /// theirs. Static geometry — three fixed widths and two gaps — and the only reason it is computed
    /// at all is that the caption is pinned to the column's right edge.</summary>
    private static float TitleWidth() =>
        JournalWindowLayout.ContentWidth
        - GameMetrics.Journal.BadgeSize
        - GameMetrics.Journal.KindWidth
        - (GameMetrics.Window.RuleGap * 2f);

    private static float StatusTextWidth() =>
        JournalWindowLayout.ContentWidth - GameMetrics.Detail.HeadingIconSize - GameMetrics.Window.RuleGap;

    /// <summary>The giver, right-aligned at the foot — where the game's own journal, and the player's
    /// screenshot, put the name of whoever hands the thing over. In the page's quietest text colour,
    /// which is the fix for the line the player photographed: it was the readout's near-white on
    /// cream parchment.</summary>
    private static TextNode BuildGiver() => new()
    {
        FontType = FontType.Axis,
        FontSize = GameMetrics.Type.BodySize,
        LineSpacing = GameMetrics.Type.BodyLine,
        AlignmentType = AlignmentType.TopRight,
        TextFlags = TextFlags.Ellipsis,
        TextColor = GameColors.JournalPage.Meta,
        Width = JournalWindowLayout.ContentWidth,
        Height = GameMetrics.Row.TextHeight,
    };

    /// <summary>The confidence footnote. JournalCanvas <c>#54</c>'s register: Axis 12, centred,
    /// quiet — the line the game reserves for a caveat.</summary>
    private static TextNode BuildProvenance() => new()
    {
        FontType = FontType.Axis,
        FontSize = GameMetrics.Type.SecondarySize,
        LineSpacing = GameMetrics.Type.SecondaryLine,
        AlignmentType = AlignmentType.Top,
        TextFlags = TextFlags.Ellipsis,
        TextColor = GameColors.JournalPage.Meta,
        Width = JournalWindowLayout.ContentWidth,
        Height = GameMetrics.Journal.FootnoteHeight,
    };

    /// <summary>The requirements block as one string: the game's own "not yet available" sentence when
    /// there is one, then the unmet requirements as bullets.
    ///
    /// <para><c>Addon</c> row 479 is the string <c>AddonJournalDetail</c>'s own requirements label is
    /// authored with, so a quest-gated entry leads with Square Enix's sentence in the player's own
    /// language rather than a paraphrase of it. Offered only for a quest gate: over a duty's or a
    /// mount's requirements the same words would be about something they are not. See
    /// <see cref="HubRowDetail.GatedByQuest"/>.</para></summary>
    private string Requirements(HubRowDetail detail)
    {
        var lead = JournalRequirementText.NotMetLead(words.NotAvailable, detail.GatedByQuest);

        return lead is null
            ? DetailText.Bullets(detail.Requirements, JournalWindowLayout.MaxRequirementLines, out _)
            : DetailText.Led(lead, detail.Requirements, JournalWindowLayout.MaxRequirementLines, out _);
    }

    private void Build()
    {
        frame = new JournalFrameNode(log) { Position = Vector2.Zero };
        AddNode(frame);

        // The window-level clip. A page taller than the window it is in — a viewport too short for the
        // content — is cut off at the window's own foot rather than drawn across the gilt frame's
        // bottom band. Nothing about the layout depends on this; it is the guard for the day something
        // does. The flag set is ScrollingNode's, which is the toolkit's own proven clipping container:
        // a clip node that does not also emit events swallows the mouse for everything inside it.
        pageClip = new ResNode
        {
            Position = new Vector2(JournalWindowLayout.ContentLeft, JournalWindowLayout.ContentTop),
            Size = new Vector2(JournalWindowLayout.ContentWidth, JournalWindowLayout.ContentWidth),
            NodeFlags = NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.Clip | NodeFlags.EmitsEvents,
        };
        pageClip.AttachNode(frame);

        page = new SectionStackNode
        {
            Position = Vector2.Zero,
            Width = JournalWindowLayout.ContentWidth,
            ItemSpacing = JournalWindowLayout.Spacing,
        };

        // The one stack that does NOT clip its own contents, and the reason is the button row. This
        // stack's height is exactly the sum of its children, so its last child's bottom edge is its
        // own bottom edge — and a node the toolkit considers partially clipped is a node the player
        // cannot click. The window-level clip above is the containment guarantee; the per-section
        // clips below it are the anti-overlap one, and neither of them has a control inside it.
        page.ClipListContents = false;
        page.AttachNode(pageClip);

        // Reading order, and this list is the only statement of it: the entry's name, the rule, what
        // state it is in, the picture, what it gives you, what it is, what is still in the way, then
        // who hands it over and the caveat, and the foot. The game's own page reads in that order and
        // so does the player's screenshot.
        JournalNodes.AddOnce(
            page,
            BuildHeader(),
            titleRuleRow = BuildRuleRow(),
            BuildStatusRow(),
            BuildBannerRow(),
            rewardSection = BuildRewardSection(),
            descriptionSection = BuildDescriptionSection(),
            requirementsSection = BuildRequirementsSection(),
            giverNode = BuildGiver(),
            provenanceNode = BuildProvenance(),
            footerRuleRow = BuildRuleRow(),
            BuildFootRow());
    }

    /// <summary>The title band: the level on its disc, the entry's name, and the kind word pinned
    /// right.
    ///
    /// <para>The name is a <see cref="MeasuredTextNode"/> bounded to
    /// <see cref="JournalWindowLayout.MaxTitleLines"/> lines, which is the game's own treatment —
    /// JournalDetail <c>#38</c> is 340x50, two Axis-18 lines, wrapping. So a long name takes a second
    /// line and the row grows by a line, which pushes the rule and everything under it down. It used
    /// to wrap into the space the state line was already in.</para></summary>
    private HorizontalListNode BuildHeader()
    {
        header = new HorizontalListNode
        {
            Alignment = HorizontalListAnchor.Left,
            ItemSpacing = GameMetrics.Window.RuleGap,
            Width = JournalWindowLayout.ContentWidth,
            FitToContentHeight = true,
        };

        // Built detached — the row attaches its own children. See JournalNodes.AddOnce for what
        // attaching one of them here as well would cost.
        levelBadgeNode = JournalNodes.Art(
            null, log, GameMetrics.JournalArt.LevelBadge, GameMetrics.Journal.BadgeSize);

        // The number and its disc are one object: the game centres the numeral on the plate
        // (JournalDetail #9 over #10), so the text is a child of the art and moves with it. The
        // badge is not a layout container, so this is a plain attach and the only one this node
        // ever gets.
        levelNode = JournalNodes.Level(levelBadgeNode);
        levelNode.Position = Vector2.Zero;
        levelNode.Size = new Vector2(GameMetrics.Journal.BadgeSize, GameMetrics.Journal.BadgeSize);

        titleNode = JournalNodes.Title(null);
        kindNode = JournalNodes.Kind(null);
        kindNode.Width = GameMetrics.Journal.KindWidth;
        kindNode.Height = GameMetrics.Detail.HeadingHeight;

        JournalNodes.AddOnce(header, levelBadgeNode, titleNode, kindNode);
        return header;
    }

    /// <summary>One of the page's two rules — <c>Journal_Detail.tex</c> (0,24) 392x4, the image
    /// JournalDetail draws under its title (<c>#39</c>) and above its buttons (<c>#48</c>). A row of
    /// its own so it takes its place in the stack rather than being hung off a coordinate, and inset
    /// to centre the art in the column because the game draws this image and never stretches it.
    /// </summary>
    private HorizontalListNode BuildRuleRow()
    {
        var row = new HorizontalListNode
        {
            Alignment = HorizontalListAnchor.Left,
            FirstItemSpacing = JournalWindowLayout.RuleInset,
            Width = JournalWindowLayout.ContentWidth,
            FitToContentHeight = true,
        };

        var rule = JournalNodes.Art(
            null,
            log,
            GameMetrics.JournalArt.Divider,
            GameMetrics.JournalArt.DividerWidth,
            GameMetrics.JournalArt.DividerHeight);
        rule.IsVisible = true;

        JournalNodes.AddOnce(row, rule);
        return row;
    }

    private HorizontalListNode BuildStatusRow()
    {
        statusRow = new HorizontalListNode
        {
            Alignment = HorizontalListAnchor.Left,
            ItemSpacing = GameMetrics.Window.RuleGap,
            Width = JournalWindowLayout.ContentWidth,
            FitToContentHeight = true,
        };

        statusIconNode = JournalNodes.Marker(
            null, new Vector2(GameMetrics.Detail.HeadingIconSize, GameMetrics.Detail.HeadingIconSize));
        statusNode = JournalNodes.Line(
            null, GameMetrics.Type.BodySize, GameColors.JournalPage.Body, TextFlags.Ellipsis);

        JournalNodes.AddOnce(statusRow, statusIconNode, statusNode);
        return statusRow;
    }

    private HorizontalListNode BuildBannerRow()
    {
        bannerRow = new HorizontalListNode
        {
            Alignment = HorizontalListAnchor.Left,
            FirstItemSpacing = GameMetrics.Journal.SectionInset,
            Width = JournalWindowLayout.ContentWidth,
            FitToContentHeight = true,
        };

        bannerNode = JournalNodes.Marker(
            null, new Vector2(GameMetrics.Journal.BannerWidth, GameMetrics.Journal.BannerHeight));

        JournalNodes.AddOnce(bannerRow, bannerNode);
        return bannerRow;
    }

    /// <summary>The reward section: the chest glyph and heading, then the recessed tray with one
    /// slot's icon and the reward said in words beside it. The icon and the name are children of the
    /// tray art because they are meant to be <i>on</i> it — that is the one intentional overlap on
    /// this page, and making it a parent-child relationship is what stops it being mistaken for the
    /// accidental kind.</summary>
    private JournalSectionNode BuildRewardSection()
    {
        var section = new JournalSectionNode(
            log, GameMetrics.JournalArt.GlyphReward, words.Reward);

        var trayRow = section.BodyRow();
        rewardTrayNode = JournalNodes.Art(
            null,
            log,
            GameMetrics.JournalArt.TrayOneRow,
            GameMetrics.Journal.ColumnWidth,
            GameMetrics.Journal.TrayHeight);
        rewardTrayNode.IsVisible = true;

        var tray = new ScreenRect(
            0f, 0f, GameMetrics.Journal.ColumnWidth, GameMetrics.Journal.TrayHeight);
        var iconRect = JournalTrayLayout.Icon(tray);
        var nameRect = JournalTrayLayout.Name(tray, iconRect);

        rewardIconNode = JournalNodes.Marker(
            rewardTrayNode, new Vector2(GameMetrics.Journal.SlotIconSize, GameMetrics.Journal.SlotIconSize));
        rewardIconNode.Position = new Vector2(iconRect.X, iconRect.Y);

        rewardNameNode = JournalNodes.Line(
            rewardTrayNode, GameMetrics.Type.BodySize, GameColors.JournalPage.Body, TextFlags.Ellipsis);
        rewardNameNode.Position = new Vector2(nameRect.X, nameRect.Y);
        rewardNameNode.Size = new Vector2(nameRect.Width, nameRect.Height);

        JournalNodes.AddOnce(trayRow, rewardTrayNode);
        return section;
    }

    private JournalSectionNode BuildDescriptionSection()
    {
        var section = new JournalSectionNode(
            log, GameMetrics.JournalArt.GlyphDescription, words.Description);
        var row = section.BodyRow();
        descriptionNode = JournalNodes.Paragraph(null, JournalWindowLayout.MaxDescriptionLines);
        JournalNodes.AddOnce(row, descriptionNode);
        return section;
    }

    private JournalSectionNode BuildRequirementsSection()
    {
        var section = new JournalSectionNode(
            log, GameMetrics.JournalArt.GlyphDocument, words.Requirements);
        var row = section.BodyRow();
        requirementsNode = JournalNodes.Paragraph(null, JournalWindowLayout.MaxRequirementLines);
        JournalNodes.AddOnce(row, requirementsNode);
        return section;
    }

    /// <summary>The foot: Back and the entry's actions in one row, with the gold rivet at the far
    /// end.
    ///
    /// <para>One row rather than two because the walker numbers a horizontal container as a single
    /// row that chains left and right and wraps at both ends — so Back is one press from the far end
    /// of the row as well as from its neighbour. The rivet is ornament, not a control: see
    /// <see cref="GameMetrics.JournalFrame.BossSize"/> for why the slot the game gives a button is
    /// given a piece of its border sheet instead.</para></summary>
    private HorizontalListNode BuildFootRow()
    {
        footRow = new HorizontalListNode
        {
            Alignment = HorizontalListAnchor.Left,
            ItemSpacing = GameMetrics.Control.ButtonGap,
            Width = JournalWindowLayout.ContentWidth,
            FitToContentHeight = true,
        };

        actionRow = new AlignedHorizontalListNode
        {
            Width = JournalWindowLayout.ContentWidth
                - GameMetrics.JournalFrame.BossSize
                - GameMetrics.Control.ButtonGap,
            Height = GameMetrics.Control.ButtonHeight,
            ItemSpacing = GameMetrics.Control.ButtonGap,
        };

        backButton = new TextButtonNode
        {
            Width = GameMetrics.Control.ButtonWidthMedium,
            Height = GameMetrics.Control.ButtonHeight,
            String = "Back",
            OnClick = () => OnBack?.Invoke(),
        };
        JournalNodes.AddOnce(actionRow, backButton);

        actionButtons = new TextButtonNode[MaxActions];
        for (var i = 0; i < MaxActions; i++)
        {
            actionButtons[i] = new TextButtonNode
            {
                Width = GameMetrics.Control.ButtonWidthMedium,
                Height = GameMetrics.Control.ButtonHeight,
                IsVisible = false,
            };
            JournalNodes.AddOnce(actionRow, actionButtons[i]);
        }

        bossNode = Boss();
        JournalNodes.AddOnce(footRow, actionRow, bossNode);
        return footRow;
    }

    /// <summary>The gold rivet at the foot of the page.</summary>
    private SimpleImageNode Boss()
    {
        var node = new SimpleImageNode
        {
            Size = new Vector2(GameMetrics.JournalFrame.BossSize, GameMetrics.JournalFrame.BossSize),
            WrapMode = WrapMode.Tile,
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
            node.IsVisible = false;
        }

        return node;
    }

    /// <summary>Writes the stored entry into the tree. There is no layout pass to run afterwards: the
    /// containers place their own children off the heights the children report, so filling the text
    /// <i>is</i> laying the page out. All this has to do at the end is ask the stack how tall it came
    /// out and make the window that size.</summary>
    private void Fill()
    {
        if (page is null || entry is not { } detail)
        {
            return;
        }

        titleNode!.Set(HeadingText.Plain(detail.Title), TitleWidth());
        SetLine(kindNode!, detail.Kind, GameMetrics.Journal.KindWidth, GameMetrics.Detail.HeadingHeight);
        SetBadge(detail.Level);

        ApplyIcon(bannerNode!, detail.BannerIconId, HubJournalFacts.SourceSize);
        bannerRow!.IsVisible = detail.BannerIconId != 0;

        ApplyIcon(statusIconNode!, detail.StatusIconId, new Vector2(GameMetrics.Detail.HeadingIconSize));
        ApplyIcon(rewardIconNode!, detail.RewardIconId, detail.RewardIconSize);
        rewardNameNode!.String = detail.RewardName;
        rewardSection!.IsVisible = detail.RewardName.Length > 0;

        // The requirements are assembled before the state line, because whether there are any is what
        // decides what the state line is allowed to say. See JournalRequirementText.
        var requirements = Requirements(detail);

        SetLine(
            statusNode!,
            JournalRequirementText.StatusLine(
                detail.StatusWord, detail.StatusSentence, requirements.Length > 0),
            StatusTextWidth(),
            GameMetrics.Row.TextHeight);
        statusRow!.IsVisible = statusNode!.IsVisible;

        descriptionSection!.SetBody(descriptionNode!, detail.Body, GameMetrics.Journal.ColumnWidth);
        requirementsSection!.SetBody(requirementsNode!, requirements, GameMetrics.Journal.ColumnWidth);

        SetLine(
            giverNode!,
            GiverLine(detail),
            JournalWindowLayout.ContentWidth,
            GameMetrics.Row.TextHeight);
        SetLine(
            provenanceNode!,
            detail.Provenance,
            JournalWindowLayout.ContentWidth,
            GameMetrics.Journal.FootnoteHeight);

        ApplyActions(detail.Actions);

        page.RecalculateLayout();
        Resize(page.Height);
        ApplyNavigation();
    }

    /// <summary>The level on its disc, or no disc at all. The height goes to zero as well as the
    /// visibility, because a horizontal row takes its own height from the tallest child it holds
    /// whether that child is drawn or not — a hidden 40-pixel badge would otherwise keep the title
    /// band 40 tall.</summary>
    private void SetBadge(string level)
    {
        var show = level.Length > 0;
        levelNode!.String = level;
        levelNode.IsVisible = show;
        levelBadgeNode!.IsVisible = show;
        levelBadgeNode.Height = show ? GameMetrics.Journal.BadgeSize : 0f;
    }

    /// <summary>Makes the window the height its contents came out at, clamped to what the border can
    /// close at and to the viewport.
    ///
    /// <para>This is the other half of the flow container, and the half the player asked about: there
    /// is no fixed content box for the page to leave a gap at the bottom of. The foot is the last
    /// thing in the stack, so it sits under the last thing on the page.</para></summary>
    private void Resize(float contentHeight)
    {
        var wanted = JournalWindowLayout.WindowHeight(contentHeight);
        var viewport = AtkStage.Instance()->ScreenSize.Height;
        var scale = InternalAddon is null || InternalAddon->Scale <= 0f ? 1f : InternalAddon->Scale;
        var cap = viewport <= 0 ? wanted : viewport / scale;

        var height = Math.Clamp(
            wanted,
            GameMetrics.JournalFrame.MinHeight,
            Math.Max(cap, GameMetrics.JournalFrame.MinHeight));

        SetWindowSize(new Vector2(GameMetrics.JournalFrame.Width, height));
        frame!.Size = new Vector2(GameMetrics.JournalFrame.Width, height);
        frame.Layout();

        // To the window's own foot rather than to the content box's, so the button row — which ends at
        // the content box's exact bottom edge — is comfortably inside the clip. A control on a clip
        // boundary is a control the toolkit will not let the player press.
        pageClip!.Size = new Vector2(
            JournalWindowLayout.ContentWidth,
            Math.Max(height - JournalWindowLayout.ContentTop, 0f));
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
