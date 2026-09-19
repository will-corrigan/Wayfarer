using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;
using Wayfarer.App.Modules;
using Wayfarer.Core.Presentation;
using Wayfarer.Surfaces.ScenarioTree;
using Wayfarer.Ui;

namespace Wayfarer.App.Settings;

/// <summary>The settings window, laid out like the game's journal: pages listed down the left,
/// the chosen page on the right under a ruled title. The Guide Block page shows the block itself,
/// drawn by the same node the game draws, re-laid live as its sliders move. The Modules page
/// switches modules and knows them only through <see cref="IModuleHost"/>, so a new module
/// appears by being registered and nothing else.
///
/// <para>Everything is built on open and freed on close, because the game frees the addon's node
/// tree when it closes; a page is rebuilt whenever it is chosen.</para></summary>
internal sealed class SettingsAddon(IModuleHost host, ScenarioTreeStyleStore styles, ITextureProvider textures, IPluginLog log) : NativeAddon
{
    private const float WindowWidth = 660f;
    private const float WindowHeight = 500f;
    private const float PagesWidth = 180f;
    private const float Gutter = 16f;
    private const float PageButtonHeight = 28f;
    private const float TitleHeight = 32f;
    private const float BlurbHeight = 40f;
    private const float RowHeight = 30f;
    private const float RowLabelWidth = 150f;
    private const float SliderWidth = 200f;
    private const float SliderHeight = 20f;
    private const float FramePadding = 14f;
    private const float SectionGap = 12f;
    private const float ButtonWidth = 160f;
    private const float CheckboxHeight = 24f;
    private const float DescriptionHeight = 20f;
    private const uint BlurbFontSize = 12;
    private const uint HeadingFontSize = 14;

    /// <summary>What the preview block shows: a step, a route with a press, and a compass reading.</summary>
    private const string SampleEntry = "Speak with Minfilia.";
    private const string SampleRoute = "Teleport to Mor Dhona, then Walk to the Rising Stones";
    private const float SampleNeedle = 0.6f;
    private const float SampleYalms = 143f;
    private const float SampleRise = 2f;

    private readonly Dictionary<Page, ListButtonNode> pageButtons = [];
    private VerticalListNode? pages;
    private VerticalListNode? body;
    private ResNode? pane;
    private ResNode? previewStage;
    private GuidanceBlockNode? preview;
    private NineGridNode? previewFrame;

    private enum Page
    {
        GuideBlock,
        Modules,
    }

    /// <inheritdoc/>
    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        SetWindowSize(new Vector2(WindowWidth, WindowHeight));

        pages = new VerticalListNode
        {
            Position = ContentStartPosition,
            Size = new Vector2(PagesWidth, ContentSize.Y),
            FitWidth = true,
            ItemSpacing = 2f,
        };
        pages.AttachNode(this);
        AddPageButton(Page.GuideBlock, "Guide Block");
        AddPageButton(Page.Modules, "Modules");

        Show(Page.GuideBlock);
    }

    /// <inheritdoc/>
    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        pane?.Dispose();
        pane = null;
        preview = null;
        previewFrame = null;
        previewStage = null;
        pages?.Dispose();
        pages = null;
        pageButtons.Clear();
    }

    private static HorizontalListNode Row(string label, NodeBase control)
    {
        var row = new HorizontalListNode { Height = RowHeight, FitHeight = true };
        row.AddNode(new LabelTextNode { String = label, Size = new Vector2(RowLabelWidth, RowHeight) });
        row.AddNode(control);
        return row;
    }

    private static SliderNode Slider(int min, int max, int value, Action<int> onChanged) => new()
    {
        Size = new Vector2(SliderWidth, SliderHeight),
        Min = min,
        Max = max,
        Step = 1,
        Value = value,
        OnValueChanged = onChanged,
    };

    private static TextNode Blurb(string words, Vector2 size) => new()
    {
        Size = size,
        FontType = FontType.Axis,
        FontSize = BlurbFontSize,
        LineSpacing = (uint)(BlurbHeight / 2f),
        AlignmentType = AlignmentType.TopLeft,
        TextFlags = TextFlags.WordWrap | TextFlags.MultiLine,
        TextColor = GameColors.ListText,
        TextOutlineColor = GameColors.ListTextEdge,
        String = words,
    };

    private static string PlacementLabel(CompassPlacement placement) => placement switch
    {
        CompassPlacement.Right => "Right of the words",
        CompassPlacement.Left => "Left of the words",
        _ => "Hidden",
    };

    private void AddPageButton(Page page, string label)
    {
        var button = new ListButtonNode
        {
            Height = PageButtonHeight,
            String = label,
            OnClick = () => Show(page),
        };
        pageButtons[page] = button;
        pages!.AddNode(button);
    }

    /// <summary>Rebuilds the right-hand pane for a page and marks its button as the chosen one.</summary>
    private void Show(Page page)
    {
        foreach (var (kind, button) in pageButtons)
        {
            button.Selected = kind == page;
        }

        pane?.Dispose();
        preview = null;
        previewFrame = null;
        previewStage = null;

        var left = ContentStartPosition.X + PagesWidth + Gutter;
        pane = new ResNode
        {
            Position = new Vector2(left, ContentStartPosition.Y),
            Size = new Vector2(ContentSize.X - PagesWidth - Gutter, ContentSize.Y),
        };
        pane.AttachNode(this);

        var (title, blurb) = page switch
        {
            Page.GuideBlock => ("Guide Block", "How Wayfarer's lines look under the Main Scenario Guide. The preview is the real thing, so what you set is what you get."),
            _ => ("Modules", "What Wayfarer follows. Each module is switched on its own and remembers your choice."),
        };

        body = new VerticalListNode
        {
            Size = pane.Size,
            FitWidth = true,
            ItemSpacing = SectionGap,
        };
        body.AttachNode(pane);
        body.AddNode(new UnderlinedTextNode { String = title, Size = new Vector2(pane.Width, TitleHeight) });
        body.AddNode(Blurb(blurb, new Vector2(pane.Width, BlurbHeight)));

        switch (page)
        {
            case Page.GuideBlock:
                BuildGuideBlockPage(body);
                break;
            default:
                BuildModulesPage(body);
                break;
        }

        body.RecalculateLayout();
    }

    /// <summary>The preview block in a frame, then one row per style value, then a reset.</summary>
    private void BuildGuideBlockPage(VerticalListNode body)
    {
        var frame = new ResNode { Height = 0f };
        previewStage = frame;
        previewFrame = new BorderNineGridNode();
        previewFrame.AttachNode(frame);
        preview = new GuidanceBlockNode(textures, log, () => { }, () => { }) { Position = new Vector2(FramePadding, FramePadding) };
        preview.AttachNode(frame);
        preview.SetWords(
            new LineContent(new ReadOnlySeString(SampleEntry), null, false),
            new LineContent(new ReadOnlySeString(SampleRoute), null, true));
        preview.SetHeading(SampleNeedle, SampleYalms, SampleRise);
        body.AddNode(frame);
        RefitPreview();

        var style = styles.Current;
        body.AddNode(Row("Entry text size", Slider((int)ScenarioTreeStyle.SmallestFont, (int)ScenarioTreeStyle.LargestFont, (int)style.EntryFontSize, size => Change(s => s.EntryFontSize = (uint)size))));
        body.AddNode(Row("Route text size", Slider((int)ScenarioTreeStyle.SmallestFont, (int)ScenarioTreeStyle.LargestFont, (int)style.RouteFontSize, size => Change(s => s.RouteFontSize = (uint)size))));
        body.AddNode(Row("Line spacing", Slider((int)ScenarioTreeStyle.SmallestLineGap, (int)ScenarioTreeStyle.LargestLineGap, (int)style.LineGap, gap => Change(s => s.LineGap = gap))));
        body.AddNode(Row("Compass size", Slider((int)ScenarioTreeStyle.SmallestCompass, (int)ScenarioTreeStyle.LargestCompass, (int)style.CompassSize, size => Change(s => s.CompassSize = size))));

        var placement = new RadioButtonGroupNode { Width = SliderWidth };
        foreach (var option in Enum.GetValues<CompassPlacement>())
        {
            placement.AddButton(PlacementLabel(option), () => Change(s => s.Compass = option));
        }

        placement.SelectedOption = PlacementLabel(style.Compass);
        placement.Height = RowHeight * Enum.GetValues<CompassPlacement>().Length;
        body.AddNode(Row("Compass", placement));

        body.AddNode(new TextButtonNode
        {
            Size = new Vector2(ButtonWidth, PageButtonHeight),
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
                Height = CheckboxHeight,
                String = module.Name,
                IsChecked = host.IsEnabled(module),
                TextTooltip = module.Description,
            };
            toggle.OnClick = enabled => _ = host.SetEnabledAsync(module, enabled);
            body.AddNode(toggle);
            body.AddNode(Blurb(module.Description, new Vector2(body.Width, DescriptionHeight)));
        }
    }

    /// <summary>Edits a copy of the current style, makes it current, and re-lays the preview.</summary>
    private void Change(Action<ScenarioTreeStyle> edit)
    {
        var next = styles.Current.Clamped();
        edit(next);
        styles.Apply(next);
        preview?.Restyle(styles.Current);
        RefitPreview();
    }

    /// <summary>Sizes the frame to the block it holds, which changes height with the style.</summary>
    private void RefitPreview()
    {
        if (preview is null || previewFrame is null || previewStage is not { } frame)
        {
            return;
        }

        frame.Height = preview.Height + (2f * FramePadding);
        frame.Width = preview.Width + (2f * FramePadding);
        previewFrame.Size = frame.Size;
        body?.RecalculateLayout();
    }
}
