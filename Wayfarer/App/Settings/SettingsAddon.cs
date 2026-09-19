using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.BaseTypes.ComponentNode;
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
    private const float CheckboxHeight = 24f;
    private const float DropDownHeight = 24f;

    private const float FramePadding = 12f;
    private const float SectionGap = 10f;

    /// <summary>How far a module's own settings are stepped in from the module itself.</summary>
    private const float SettingIndent = 24f;

    /// <summary>Where the cursor's stops begin: the pages down the left, then the page's own
    /// controls well clear of them.</summary>
    private const int FirstPageStop = 1;

    /// <inheritdoc cref="FirstPageStop"/>
    private const int FirstControlStop = 20;

    /// <summary>The inset that keeps a framed panel's contents off its own border.</summary>
    private const float PanelPadding = 8f;

    /// <summary>What the preview block shows: a step, a route with a press, and a compass reading.</summary>
    private const string SampleEntry = "Speak with Minfilia at the Waking Sands.";
    private const string SampleRoute = "Teleport to Vesper Bay, then Walk to the Waking Sands";
    private const string SampleRouteKeyword = "Teleport to Vesper Bay";
    private const float SampleNeedle = 0.6f;
    private const float SampleYalms = 143f;
    private const float SampleRise = 2f;

    /// <summary>The dark the game fills its own framed panels with.</summary>
    private static readonly Vector4 PanelColor = new(0f, 0f, 0f, 0.35f);

    private readonly Dictionary<Page, ListButtonNode> pageButtons = [];
    private readonly List<ComponentNode> controls = [];
    private ScrollingNode<VerticalListNode>? pane;
    private ListBoxNode? pages;
    private ResNode? previewStage;
    private GuidanceBlockNode? preview;
    private NineGridNode? previewFrame;
    private DropDownNode<CompassPlacement>? placement;

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

        pages = new ListBoxNode
        {
            Position = ContentStartPosition,
            Size = new Vector2(PagesWidth, ContentSize.Y),
            ClipListContents = true,
            ShowBorder = true,
            ShowBackground = true,
            BackgroundColor = PanelColor,
            ItemSpacing = 2f,
            FirstItemSpacing = PanelPadding,
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

    /// <summary>What each place for the compass is called, in the dropdown and on its list.</summary>
    private static string PlacementLabel(CompassPlacement placement) => placement switch
    {
        CompassPlacement.Right => "Right of the words",
        CompassPlacement.Left => "Left of the words",
        _ => "Hidden",
    };

    /// <summary>Steps a node in from the left, to show it belongs to the one above it.</summary>
    private static T Indented<T>(T node)
        where T : NodeBase
    {
        node.Position = new Vector2(SettingIndent, node.Position.Y);
        return node;
    }

    /// <summary>A game checkbox over something that is on or off.</summary>
    private static CheckboxNode Switch(string name, string description, float width, Func<bool> read, Action<bool> write) => new()
    {
        Size = new Vector2(width, CheckboxHeight),
        String = name,
        IsChecked = read(),
        TextTooltip = description,
        OnClick = write,
    };

    /// <summary>Marks a control as one the cursor stops on, in the order they are added.</summary>
    private T Control<T>(T node)
        where T : ComponentNode
    {
        controls.Add(node);
        return node;
    }

    /// <summary>Chains the cursor through the pages and then through the page's own controls: down
    /// and up within each column, right from a page into its controls, left from a control back to
    /// the page it belongs to.</summary>
    private void ChainForTheCursor(Page page)
    {
        var buttons = pageButtons.Values.ToList();
        var here = FirstPageStop + pageButtons.Keys.ToList().IndexOf(page);
        var firstControl = controls.Count > 0 ? FirstControlStop : here;
        for (var i = 0; i < buttons.Count; i++)
        {
            buttons[i].NavIndex = FirstPageStop + i;
            buttons[i].NavUp = FirstPageStop + ((i + buttons.Count - 1) % buttons.Count);
            buttons[i].NavDown = FirstPageStop + ((i + 1) % buttons.Count);
            buttons[i].NavRight = firstControl;
            buttons[i].NavLeft = FirstPageStop + i;
        }

        for (var i = 0; i < controls.Count; i++)
        {
            controls[i].NavIndex = FirstControlStop + i;
            controls[i].NavUp = FirstControlStop + ((i + controls.Count - 1) % controls.Count);
            controls[i].NavDown = FirstControlStop + ((i + 1) % controls.Count);
            controls[i].NavLeft = here;
            controls[i].NavRight = FirstControlStop + i;
        }
    }

    /// <summary>A slider over a whole-number range. The range is set through the game's own
    /// component rather than by writing its data, which is what actually moves the end stops.</summary>
    private SliderNode Slider(float min, float max, float value, Action<int> onChanged) => new()
    {
        Size = new Vector2(ControlWidth, SliderHeight),
        Range = (int)min..(int)max,
        Step = 1,
        Value = (int)value,
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

    private Paragraph Words(string words, float? width = null)
    {
        var paragraph = new Paragraph();
        paragraph.Set(words, width ?? PageWidth);
        return paragraph;
    }

    private void AddPageButton(Page page)
    {
        var button = new ListButtonNode
        {
            Size = new Vector2(PagesWidth - (2f * PanelPadding), PageButtonHeight),
            String = PageTitle(page),
            OnClick = () => Show(page),
        };
        pageButtons[page] = button;
        pages!.AddNode(button);
    }

    private void ForgetPage()
    {
        // An open list is re-parented to the addon's own root while it shows, so it has to be shut
        // before the page holding it is freed, rather than left behind on an owner that is gone.
        placement?.Collapse(playSoundEffect: false);
        placement = null;
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
        controls.Clear();

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

        ChainForTheCursor(page);
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
            new LineContent(SampleEntry),
            new LineContent(SampleRoute, SampleRouteKeyword, Glyph: BitmapFontIcon.Aetheryte, Pressable: true));
        preview.SetHeading(SampleNeedle, SampleYalms, SampleRise);
        body.AddNode(previewStage);

        var style = styles.Current;
        body.AddNode(Row("Entry text size", Control(Slider(ScenarioTreeStyle.SmallestFont, ScenarioTreeStyle.LargestFont, style.EntryFontSize, size => Change(s => s.EntryFontSize = (uint)size)))));
        body.AddNode(Row("Route text size", Control(Slider(ScenarioTreeStyle.SmallestFont, ScenarioTreeStyle.LargestFont, style.RouteFontSize, size => Change(s => s.RouteFontSize = (uint)size)))));
        body.AddNode(Row("Line spacing", Control(Slider(ScenarioTreeStyle.SmallestLineGap, ScenarioTreeStyle.LargestLineGap, style.LineGap, gap => Change(s => s.LineGap = gap)))));
        body.AddNode(Row("Text left edge", Control(Slider(ScenarioTreeStyle.SmallestInset, ScenarioTreeStyle.LargestInset, style.ContentLeft, left => Change(s => s.ContentLeft = left)))));
        body.AddNode(Row("Compass size", Control(Slider(ScenarioTreeStyle.SmallestCompass, ScenarioTreeStyle.LargestCompass, style.CompassSize, size => Change(s => s.CompassSize = size)))));

        var places = Enum.GetValues<CompassPlacement>();
        placement = new DropDownNode<CompassPlacement>
        {
            Size = new Vector2(ControlWidth, DropDownHeight),
            MaxListOptions = places.Length,
            Options = [.. places],
            GetLabelFunction = place => new ReadOnlySeString(PlacementLabel(place)),
            SelectedOption = style.Compass,
            OnOptionSelected = place => Change(s => s.Compass = place),
        };
        body.AddNode(Row("Compass", Control(placement)));

        var reset = new TextButtonNode
        {
            Size = new Vector2(ControlWidth, PageButtonHeight),
            String = "Reset to defaults",
        };
        reset.OnClick = () =>
        {
            styles.Reset();
            Show(Page.GuideBlock);
        };
        body.AddNode(Row(string.Empty, Control(reset)));
    }

    /// <summary>One game checkbox per module with its description under it, and under that whatever
    /// the module itself lets the player switch.</summary>
    private void BuildModulesPage(VerticalListNode body)
    {
        foreach (var module in host.Modules)
        {
            body.AddNode(Control(Switch(module.Name, module.Description, PageWidth, () => host.IsEnabled(module), on => _ = host.SetEnabledAsync(module, on))));
            body.AddNode(Words(module.Description));
            foreach (var setting in module.Settings)
            {
                body.AddNode(Indented(Control(Switch(setting.Name, setting.Description, PageWidth - SettingIndent, setting.Read, setting.Write))));
                body.AddNode(Indented(Words(setting.Description, PageWidth - SettingIndent)));
            }
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
