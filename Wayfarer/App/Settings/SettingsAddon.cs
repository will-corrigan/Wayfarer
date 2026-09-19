using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;
using Wayfarer.App.Modules;
using Wayfarer.Surfaces.ScenarioTree;

namespace Wayfarer.App.Settings;

/// <summary>The settings window, laid out like the game's own journal: pages listed down the left,
/// the chosen page on the right under a ruled title. The Guide Block page draws the block itself,
/// with the same node the game draws, re-laid live as the sliders move. The Modules page switches
/// modules and knows them only through <see cref="IModuleHost"/>, so a new module appears here by
/// being registered and nothing else.
///
/// <para>Both panes scroll and clip: every page is built inside a scrolling list that is exactly
/// the pane's size, and each block of words is sized to the words it drew, so nothing a page holds
/// can spill past the window's frame however long it turns out to be.</para>
///
/// <para>Everything is built on open and freed on close, because the game frees the addon's node
/// tree when it closes; a page is rebuilt whenever it is chosen.</para></summary>
internal sealed class SettingsAddon(IModuleHost host, ScenarioTreeStyleStore styles, ITextureProvider textures, IPluginLog log) : NativeAddon
{
    private const float WindowWidth = 680f;
    private const float WindowHeight = 520f;

    /// <summary>How the window's content is divided across: the pages list, then a gutter, then
    /// the page. Shares rather than widths, so the panes hold together at any window size.</summary>
    private const float PagesShare = 0.26f;
    private const float Gutter = 16f;

    /// <summary>How a settings row is divided across: its label, then its control.</summary>
    private const float RowLabelShare = 0.38f;

    /// <summary>Room kept clear at a scrolling list's right edge for its scroll bar.</summary>
    private const float ScrollBarWidth = 16f;

    // Heights are the game's own: its list buttons are 28 tall, its checkboxes 24, its radio
    // buttons 24, and its slider track 20, whatever width they are given.
    private const float PageButtonHeight = 28f;
    private const float TitleHeight = 28f;
    private const float RowHeight = 28f;
    private const float SliderHeight = 20f;
    private const float RadioButtonHeight = 24f;
    private const float CheckboxHeight = 24f;

    private const float FramePadding = 12f;
    private const float SectionGap = 10f;

    /// <summary>What the preview block shows: a step, a route with a press, and a compass reading.</summary>
    private const string SampleEntry = "Speak with Minfilia at the Waking Sands.";
    private const string SampleRoute = "Teleport to Vesper Bay, then Walk to the Waking Sands";
    private const float SampleNeedle = 0.6f;
    private const float SampleYalms = 143f;
    private const float SampleRise = 2f;

    private readonly Dictionary<Page, ListButtonNode> pageButtons = [];
    private ScrollingNode<VerticalListNode>? pane;
    private VerticalListNode? pages;
    private ResNode? previewStage;
    private GuidanceBlockNode? preview;
    private NineGridNode? previewFrame;

    private enum Page
    {
        GuideBlock,
        Modules,
    }

    /// <summary>The pages list down the left.</summary>
    private float PagesWidth => ContentSize.X * PagesShare;

    /// <summary>The pane the chosen page is built in, gutter aside.</summary>
    private float PaneWidth => ContentSize.X - PagesWidth - Gutter;

    /// <summary>The width a page's own nodes may take: the pane, less its scroll bar.</summary>
    private float PageWidth => PaneWidth - ScrollBarWidth;

    /// <summary>A settings row's label, and the control beside it.</summary>
    private float RowLabelWidth => PageWidth * RowLabelShare;

    /// <inheritdoc cref="RowLabelWidth"/>
    private float ControlWidth => PageWidth - RowLabelWidth;

    /// <inheritdoc/>
    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        SetWindowSize(new Vector2(WindowWidth, WindowHeight));

        pages = new VerticalListNode
        {
            Position = ContentStartPosition,
            Size = new Vector2(PagesWidth, ContentSize.Y),
            FitWidth = true,
            ClipListContents = true,
            ItemSpacing = 2f,
        };
        pages.AttachNode(this);
        AddPageButton(Page.GuideBlock);
        AddPageButton(Page.Modules);
        pages.RecalculateLayout();

        Show(Page.GuideBlock);
    }

    /// <inheritdoc/>
    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        ForgetPage();
        pages?.Dispose();
        pages = null;
        pageButtons.Clear();
    }

    /// <summary>What a page is called in its button and in its own title.</summary>
    private static string PageTitle(Page page) => page switch
    {
        Page.GuideBlock => "Guide Block",
        _ => "Modules",
    };

    private static string PlacementLabel(CompassPlacement placement) => placement switch
    {
        CompassPlacement.Right => "Right of the words",
        CompassPlacement.Left => "Left of the words",
        _ => "Hidden",
    };

    private SliderNode Slider(int min, int max, int value, Action<int> onChanged) => new()
    {
        Size = new Vector2(ControlWidth, SliderHeight),
        Min = min,
        Max = max,
        Step = 1,
        Value = value,
        OnValueChanged = onChanged,
    };

    /// <summary>A labelled row: the label at its share of the page, the control beside it.</summary>
    private HorizontalListNode Row(string label, NodeBase control, float height = RowHeight)
    {
        var row = new HorizontalListNode { Height = height };
        row.AddNode(new LabelTextNode { String = label, Size = new Vector2(RowLabelWidth, height) });
        row.AddNode(control);
        return row;
    }

    private Paragraph Words(string words)
    {
        var paragraph = new Paragraph();
        paragraph.Set(words, PageWidth);
        return paragraph;
    }

    private void AddPageButton(Page page)
    {
        var button = new ListButtonNode
        {
            Height = PageButtonHeight,
            String = PageTitle(page),
            OnClick = () => Show(page),
        };
        pageButtons[page] = button;
        pages!.AddNode(button);
    }

    private void ForgetPage()
    {
        pane?.Dispose();
        pane = null;
        preview = null;
        previewFrame = null;
        previewStage = null;
    }

    /// <summary>Rebuilds the right-hand pane for a page and marks its button as the chosen one.</summary>
    private void Show(Page page)
    {
        foreach (var (kind, button) in pageButtons)
        {
            button.Selected = kind == page;
        }

        ForgetPage();

        pane = new ScrollingNode<VerticalListNode>
        {
            Position = new Vector2(ContentStartPosition.X + PagesWidth + Gutter, ContentStartPosition.Y),
            Size = new Vector2(PaneWidth, ContentSize.Y),
            AutoHideScrollBar = true,
            ContentNode =
            {
                FitContents = true,
                ItemSpacing = SectionGap,
            },
        };
        pane.AttachNode(this);

        var blurb = page switch
        {
            Page.GuideBlock => "How Wayfarer's lines look under the Main Scenario Guide. The preview below is the real thing, drawn by the same node the game draws, so what you set is what you get.",
            _ => "What Wayfarer follows. Each module is switched on its own and remembers your choice.",
        };

        var body = pane.ContentNode;
        body.AddNode(new UnderlinedTextNode { String = PageTitle(page), Size = new Vector2(PageWidth, TitleHeight) });
        body.AddNode(Words(blurb));

        switch (page)
        {
            case Page.GuideBlock:
                BuildGuideBlockPage(body);
                break;
            default:
                BuildModulesPage(body);
                break;
        }

        Refit();
    }

    /// <summary>The preview block in a frame, then one row per style value, then a reset.</summary>
    private void BuildGuideBlockPage(VerticalListNode body)
    {
        previewStage = new ResNode();
        previewFrame = new BorderNineGridNode();
        previewFrame.AttachNode(previewStage);
        preview = new GuidanceBlockNode(textures, log, () => { }, () => { }) { Position = new Vector2(FramePadding, FramePadding) };
        preview.AttachNode(previewStage);
        preview.SetWords(
            new LineContent(new ReadOnlySeString(SampleEntry), null, false),
            new LineContent(new ReadOnlySeString(SampleRoute), null, true));
        preview.SetHeading(SampleNeedle, SampleYalms, SampleRise);
        body.AddNode(previewStage);

        var style = styles.Current;
        body.AddNode(Row("Entry text size", Slider((int)ScenarioTreeStyle.SmallestFont, (int)ScenarioTreeStyle.LargestFont, (int)style.EntryFontSize, size => Change(s => s.EntryFontSize = (uint)size))));
        body.AddNode(Row("Route text size", Slider((int)ScenarioTreeStyle.SmallestFont, (int)ScenarioTreeStyle.LargestFont, (int)style.RouteFontSize, size => Change(s => s.RouteFontSize = (uint)size))));
        body.AddNode(Row("Line spacing", Slider((int)ScenarioTreeStyle.SmallestLineGap, (int)ScenarioTreeStyle.LargestLineGap, (int)style.LineGap, gap => Change(s => s.LineGap = gap))));
        body.AddNode(Row("Compass size", Slider((int)ScenarioTreeStyle.SmallestCompass, (int)ScenarioTreeStyle.LargestCompass, (int)style.CompassSize, size => Change(s => s.CompassSize = size))));

        var placements = Enum.GetValues<CompassPlacement>();
        var placement = new RadioButtonGroupNode { Size = new Vector2(ControlWidth, RadioButtonHeight * placements.Length) };
        foreach (var option in placements)
        {
            placement.AddButton(PlacementLabel(option), () => Change(s => s.Compass = option));
        }

        placement.SelectedOption = PlacementLabel(style.Compass);
        body.AddNode(Row("Compass", placement, placement.Height));

        body.AddNode(new TextButtonNode
        {
            Size = new Vector2(ControlWidth, PageButtonHeight),
            String = "Reset to defaults",
            OnClick = () =>
            {
                styles.Reset();
                Show(Page.GuideBlock);
            },
        });
    }

    /// <summary>One game checkbox per module, its description under it.</summary>
    private void BuildModulesPage(VerticalListNode body)
    {
        foreach (var module in host.Modules)
        {
            var toggle = new CheckboxNode
            {
                Size = new Vector2(PageWidth, CheckboxHeight),
                String = module.Name,
                IsChecked = host.IsEnabled(module),
                TextTooltip = module.Description,
            };
            toggle.OnClick = enabled => _ = host.SetEnabledAsync(module, enabled);
            body.AddNode(toggle);
            body.AddNode(Words(module.Description));
        }
    }

    /// <summary>Edits a copy of the current style, makes it current, and re-lays the preview.</summary>
    private void Change(Action<ScenarioTreeStyle> edit)
    {
        var next = styles.Current.Clamped();
        edit(next);
        styles.Apply(next);
        preview?.Restyle(styles.Current);
        Refit();
    }

    /// <summary>Sizes the preview's frame to the block it holds, which changes height with the
    /// style, then re-lays the page and its scroll bar around it.</summary>
    private void Refit()
    {
        if (preview is not null && previewFrame is not null && previewStage is not null)
        {
            previewStage.Size = new Vector2(preview.Width + (2f * FramePadding), preview.Height + (2f * FramePadding));
            previewFrame.Size = previewStage.Size;
        }

        pane?.ContentNode.RecalculateLayout();
        pane?.RecalculateSizes();
    }
}
