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

    private const float SectionGap = 10f;

    /// <summary>The gap between a switch and the sentence under it, which is much smaller than the
    /// gap between one switch and the next: what belongs together has to sit together, or a page of
    /// evenly spaced lines reads as one list of unrelated things.</summary>
    private const float AsideGap = 1f;

    /// <summary>The gap between one settings row and the next. They are a block of one kind of
    /// thing, so they sit closer together than the blocks of the page do.</summary>
    private const float RowGap = 2f;

    /// <summary>How thick the game draws the line it separates one thing from another with.</summary>
    private const float RuleHeight = 4f;

    /// <summary>How far back an aside is written from what it is about.</summary>
    private const float AsideText = 0.7f;

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
    private const string SampleRoute = "Teleport to Limsa Lominsa Lower Decks, then Aethernet to Arcanists' Guild";
    private const string SampleRouteKeyword = "Teleport to Limsa Lominsa Lower Decks";
    private const float SampleNeedle = 0.6f;
    private const float SampleYalms = 143f;
    private const float SampleRise = 2f;

    /// <summary>How many of the thing a search area holds, in the sample: one, so the preview does
    /// not show a count the player would only see while searching.</summary>

    /// <summary>The one page that is not a module's: what the guide itself looks like. Every other
    /// page is a module, named by the module, so a new module brings its own page with it.</summary>
    private const string MainPage = "Main";

    /// <summary>The dark the game fills its own framed panels with.</summary>
    private static readonly Vector4 PanelColor = new(0f, 0f, 0f, 0.35f);

    private readonly Dictionary<string, ListButtonNode> pageButtons = [];
    private readonly List<ComponentNode> controls = [];
    private ScrollingNode<VerticalListNode>? pane;
    private ListBoxNode? pages;
    private GuidanceBlockNode? preview;
    private DropDownNode<CompassPlacement>? placement;
    private string? wanted;

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
        AddPageButton(MainPage);
        foreach (var module in host.Modules)
        {
            AddPageButton(module.Name);
        }

        pages.RecalculateLayout();

        Show(MainPage);
    }

    /// <inheritdoc/>
    /// <remarks>Choosing a page frees the page that was showing, and the control that was pressed
    /// is part of it. The game is still inside that control's own event when the press runs, so
    /// the choice is remembered here and acted on next frame, when nothing is holding it.</remarks>
    protected override unsafe void OnUpdate(AtkUnitBase* addon)
    {
        if (wanted is { } page)
        {
            wanted = null;
            Show(page);
        }
    }

    /// <inheritdoc/>
    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        wanted = null;
        ForgetPage();
        pages?.Dispose();
        pages = null;
        pageButtons.Clear();
    }

    /// <summary>What each place for the compass is called, in the dropdown and on its list.</summary>
    private static string PlacementLabel(CompassPlacement placement) => placement switch
    {
        CompassPlacement.Right => "Right of the words",
        CompassPlacement.Left => "Left of the words",
        _ => "Hidden",
    };

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
    private void ChainForTheCursor(string page)
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
        row.AddNode(Inset.Middle(control, height));
        return row;
    }

    /// <summary>A block of words wrapped to a width.</summary>
    /// <param name="words">What to write.</param>
    /// <param name="width">How wide to wrap it, or the whole page.</param>
    /// <param name="aside">Whether this is a remark about something else rather than a thing in its
    /// own right. An aside is set leaning and dropped back, so a switch and the sentence explaining
    /// it are told apart at a glance instead of reading as two switches.</param>
    private Paragraph Words(string words, float? width = null, bool aside = false)
    {
        var paragraph = new Paragraph();
        if (aside)
        {
            paragraph.TextFlags |= TextFlags.Italic;
            paragraph.TextColor = GameColors.ListText with { W = AsideText };
        }

        paragraph.Set(words, width ?? PageWidth);
        return paragraph;
    }

    private void AddPageButton(string page)
    {
        var button = new ListButtonNode
        {
            Size = new Vector2(PagesWidth - (2f * PanelPadding), PageButtonHeight),
            String = page,
            OnClick = () => wanted = page,
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
    }

    /// <summary>Rebuilds the right-hand pane for a page and marks its button as the chosen one.</summary>
    private void Show(string page)
    {
        foreach (var (kind, button) in pageButtons)
        {
            button.Selected = string.Equals(kind, page, StringComparison.Ordinal);
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

        var body = pane.ContentNode;
        body.AddNode(new UnderlinedTextNode { String = page, Size = new Vector2(PageWidth, TitleHeight) });

        if (host.Modules.FirstOrDefault(module => string.Equals(module.Name, page, StringComparison.Ordinal)) is { } chosen)
        {
            BuildModulePage(body, chosen);
        }
        else
        {
            BuildMainPage(body);
        }

        ChainForTheCursor(page);
        Refit();
    }

    /// <summary>The block itself, with nothing else around it: on the page's own left margin, in
    /// line with the settings under it, and as wide as the page rather than the width the game
    /// happens to give it.
    ///
    /// <para>Put on the page directly, so the list measures it. The block is as tall as the style
    /// makes it and does not know how tall that is until it has been styled; anything holding it
    /// would have to be told that height by hand, which is what had the sliders drawn over it.</para>
    /// </summary>
    private void Preview(VerticalListNode body)
    {
        preview = new GuidanceBlockNode(textures, log, () => { }, () => { }) { Width = PageWidth };
        body.AddNode(preview);
        preview.SetWords(
            new LineContent(SampleEntry),
            new LineContent(SampleRoute, SampleRouteKeyword, Glyph: BitmapFontIcon.Aetheryte, Pressable: true));
        preview.SetHeading(SampleNeedle, SampleYalms, SampleRise);
    }

    /// <summary>The game's own guide with ours under it, then one row per style value, then a
    /// reset. The block is shown where it actually appears -- beneath the window's title and the
    /// quest being followed -- rather than alone in a frame, because how it reads depends entirely
    /// on what it sits under.</summary>
    private void BuildMainPage(VerticalListNode body)
    {
        Preview(body);

        // Styled only once the block is part of the window's own tree. A style applied before then
        // is laid out against nothing and lost when the tree is joined, which is why the preview
        // showed the defaults until a slider moved and restyled it in place.
        preview!.Restyle(styles.Current);

        // The block is what the settings under it change. The rule says so: above it is the thing,
        // below it is what it is made of.
        body.AddNode(new HorizontalLineNode { Width = PageWidth, Height = RuleHeight });

        var style = styles.Current;

        var rows = new VerticalListNode { Width = PageWidth, FitContents = true, ItemSpacing = RowGap };
        rows.AddNode(Row("Entry text size", Control(Slider(ScenarioTreeStyle.SmallestFont, ScenarioTreeStyle.LargestFont, style.EntryFontSize, size => Change(s => s.EntryFontSize = (uint)size)))));
        rows.AddNode(Row("Route text size", Control(Slider(ScenarioTreeStyle.SmallestFont, ScenarioTreeStyle.LargestFont, style.RouteFontSize, size => Change(s => s.RouteFontSize = (uint)size)))));
        rows.AddNode(Row("Line spacing", Control(Slider(ScenarioTreeStyle.SmallestLineGap, ScenarioTreeStyle.LargestLineGap, style.LineGap, gap => Change(s => s.LineGap = gap)))));
        rows.AddNode(Row("Text left edge", Control(Slider(ScenarioTreeStyle.SmallestInset, ScenarioTreeStyle.LargestInset, style.ContentLeft, left => Change(s => s.ContentLeft = left)))));
        rows.AddNode(Row("Compass size", Control(Slider(ScenarioTreeStyle.SmallestCompass, ScenarioTreeStyle.LargestCompass, style.CompassSize, size => Change(s => s.CompassSize = size)))));

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
        rows.AddNode(Row("Compass", Control(placement)));
        rows.RecalculateLayout();
        body.AddNode(rows);

        var reset = new TextButtonNode
        {
            Size = new Vector2(ControlWidth, PageButtonHeight),
            String = "Reset to defaults",
        };
        reset.OnClick = () =>
        {
            styles.Reset();
            wanted = MainPage;
        };
        body.AddNode(Row(string.Empty, Control(reset)));
    }

    /// <summary>What one module is for, then a checkbox for each thing it offers.
    ///
    /// <para>The module itself has no checkbox. It is on while any of what it offers is on, so the
    /// player switches the thing they want rather than a thing called a module — and the module is
    /// told to come into line with its switches each time one of them moves.</para></summary>
    private void BuildModulePage(VerticalListNode body, IModule module)
    {
        body.AddNode(Words(module.Description, aside: true));

        foreach (var setting in module.Settings)
        {
            var moved = (bool on) =>
            {
                setting.Write(on);
                GameThread.Let(() => host.RefreshAsync(module), log, $"bring {module.Name} into line with its settings");
            };

            // Each switch and its sentence are one thing on the page rather than two, so the page's
            // own spacing falls between settings and not between a setting and what it means.
            var together = new VerticalListNode
            {
                Width = PageWidth,
                FitContents = true,
                ItemSpacing = AsideGap,
            };

            together.AddNode(Control(Switch(setting.Name, setting.Description, PageWidth, setting.Read, moved)));
            together.AddNode(Inset.From(Words(setting.Description, PageWidth - SettingIndent, aside: true), SettingIndent));
            together.RecalculateLayout();
            body.AddNode(together);
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
        pane?.ContentNode.RecalculateLayout();
        pane?.RecalculateSizes();
    }
}
