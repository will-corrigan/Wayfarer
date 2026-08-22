using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace Wayfarer.Spike;

/// <summary>THROWAWAY SPIKE CODE — see <see cref="SpikeNavTarget"/>. Experiment 1: prove the
/// controller cursor can cross a <see cref="TabBarNode"/> → <c>ListNode</c> of
/// <c>ListItemWithFocusNav</c> rows → settings controls, and back, when every element carries an
/// explicit up/down/left/right index.
///
/// Index plan (one flat byte space per addon, 0 means "no target" so nothing may be left at 0):
/// <list type="bullet">
/// <item><description>1..3 — tab bar radio buttons (<c>TabBarNode.NavIndex = 1</c>, one index per tab).</description></item>
/// <item><description>10..11 — settings block (checkbox, slider), numbered by the walker.</description></item>
/// <item><description>100 — the list's upward scroll sentinel; 101, 105, 109… — rows (four slots
/// reserved per row by <c>ListNode</c>); (rows × 4) + 101 — the downward scroll sentinel.</description></item>
/// </list>
/// The graph is recomputed in full on every tab switch, filter toggle and row-count change, because
/// indices are absolute and dense — patching them is not viable.</summary>
internal sealed unsafe class SpikeNavWindow(InputModeService inputMode, IFramework framework, IPluginLog log) : NativeAddon
{
    private const int TabBarNavIndex = 1;
    private const int TabCount = 3;
    private const int SettingsNavStart = 10;
    private const int SettingsNavCount = 2;
    private const int ListNavIndex = 100;
    private const int SettingsBlockHeight = 96;

    private static readonly string[] TabLabels = ["Alpha", "Bravo", "Charlie"];

    private readonly List<SpikeRowModel> allRows = [];

    private TabBarNode? tabBar;
    private TextNode? modeNode;
    private TextNode? hintNode;
    private TextNode? actionNode;
    private ListNode<SpikeRowModel, SpikeRowNode>? list;
    private VerticalListNode? settingsList;
    private CheckboxNode? evenOnlyCheckbox;
    private SliderNode? rowCountSlider;

    private int activeTab;
    private int rowCount = 24;
    private bool evenOnly;

    /// <inheritdoc/>
    public override void Dispose()
    {
        // Same framework-thread marshalling NativeHubWindow uses: base.Dispose() calls Close(),
        // which asserts the main thread, but Dalamud unloads plugins off it.
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
            log.Warning(ex, "SpikeNavWindow: dispose on the framework thread failed or timed out.");
        }
    }

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        var contentStart = ContentStartPosition;
        var contentSize = ContentSize;
        var y = contentStart.Y;

        // Nav properties must be set BEFORE Size/AddTab: TabBarNode.RecalculateLayout() is private
        // and only runs from OnSizeChanged/AddTab, so a NavIndex assigned afterwards never reaches
        // the radio buttons. This ordering trap is the single easiest way to get a silent no-op.
        tabBar = new TabBarNode
        {
            NavIndex = TabBarNavIndex,
            NavUp = SettingsNavStart + SettingsNavCount - 1,
            NavDown = ListNavIndex,
            Position = new Vector2(contentStart.X, y),
            Size = new Vector2(contentSize.X, 26.0f),
        };

        for (var i = 0; i < TabCount; i++)
        {
            var index = i;
            tabBar.AddTab(TabLabels[i], () => SelectTab(index));
        }

        AddNode(tabBar);
        y += 32.0f;

        modeNode = BuildLine(new Vector2(contentStart.X, y), contentSize.X, 14, new Vector4(0.85f, 0.85f, 0.85f, 1f));
        AddNode(modeNode);
        y += 20.0f;

        hintNode = BuildLine(new Vector2(contentStart.X, y), contentSize.X, 14, new Vector4(0.75f, 0.82f, 1f, 1f));
        AddNode(hintNode);
        y += 22.0f;

        var listHeight = Math.Max(contentSize.Y - (y - contentStart.Y) - SettingsBlockHeight, 60.0f);
        list = new ListNode<SpikeRowModel, SpikeRowNode>
        {
            NavIndex = ListNavIndex,
            NavUp = TabBarNavIndex,
            NavDown = SettingsNavStart,
            ItemSpacing = 2.0f,
            Position = new Vector2(contentStart.X, y),
            Size = new Vector2(contentSize.X, listHeight),
            OptionsList = [],
            OnItemSelected = OnRowSelected,
        };
        AddNode(list);
        y += listHeight + 6.0f;

        settingsList = new VerticalListNode
        {
            FitWidth = true,
            ItemSpacing = 6.0f,
            Position = new Vector2(contentStart.X, y),
            Size = new Vector2(contentSize.X, 56.0f),
        };

        evenOnlyCheckbox = new CheckboxNode
        {
            Height = 22.0f,
            String = "Show even-numbered rows only",
            IsChecked = evenOnly,
            OnClick = isOn =>
            {
                evenOnly = isOn;
                RefreshList();
            },
        };
        settingsList.AddNode(evenOnlyCheckbox);

        rowCountSlider = new SliderNode
        {
            Height = 22.0f,
            Width = contentSize.X - 40.0f,
            Min = 4,
            Max = 60,
            Step = 4,
            Value = rowCount,
        };
        rowCountSlider.OnValueChanged = value =>
        {
            rowCount = value;
            RefreshList();
        };
        settingsList.AddNode(rowCountSlider);

        AddNode(settingsList);
        y += 62.0f;

        actionNode = BuildLine(new Vector2(contentStart.X, y), contentSize.X, 13, new Vector4(0.95f, 0.8f, 0.35f, 1f));
        AddNode(actionNode);

        SelectTab(0);
        UpdateHintLine();
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        if (modeNode is null)
        {
            return;
        }

        var pad = inputMode.IsPlayStationPad ? "PlayStation" : "Xbox or unknown";
        modeNode.String = $"Input mode: {inputMode.Mode}   ·   pad icons: {pad}   ·   confirm: {inputMode.Glyphs.Confirm}   cancel: {inputMode.Glyphs.Cancel}";
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        tabBar = null;
        modeNode = null;
        hintNode = null;
        actionNode = null;
        list = null;
        settingsList = null;
        evenOnlyCheckbox = null;
        rowCountSlider = null;
    }

    private static TextNode BuildLine(Vector2 position, float width, uint fontSize, Vector4 color) => new()
    {
        Position = position,
        Size = new Vector2(width, 18.0f),
        FontSize = fontSize,
        LineSpacing = fontSize,
        TextColor = color,
        TextFlags = TextFlags.Ellipsis,
    };

    /// <summary>Builds the button-hint line out of <see cref="BitmapFontIcon"/> payloads rather
    /// than literal "A"/"B" text. The game swaps the glyph atlas for the player's own
    /// PadSelectButtonIcon setting, so one string renders correctly on both pad families — which is
    /// the point being tested here.</summary>
    private void UpdateHintLine()
    {
        if (hintNode is null)
        {
            return;
        }

        var builder = new SeStringBuilder();
        builder.AddIcon(BitmapFontIcon.ControllerDPadAll).AddText(" Move   ");
        builder.AddIcon(BitmapFontIcon.ControllerButton1).AddText(" Select   ");
        builder.AddIcon(BitmapFontIcon.ControllerButton0).AddText(" Back   ");
        builder.AddIcon(BitmapFontIcon.ControllerShoulderLeft).AddText("/");
        builder.AddIcon(BitmapFontIcon.ControllerShoulderRight).AddText(" Cycle windows");

        hintNode.String = new ReadOnlySeString(builder.Build().Encode());
    }

    private void SelectTab(int index)
    {
        activeTab = index;

        allRows.Clear();
        for (var i = 1; i <= rowCount; i++)
        {
            allRows.Add(new SpikeRowModel { Number = i, Label = $"{TabLabels[activeTab]} item {i}" });
        }

        RefreshList();
    }

    /// <summary>Rebuilds the list contents and then renumbers the ENTIRE nav graph — the list's
    /// own indices are reapplied by <c>ListNode</c> when <c>OptionsList</c> is set, and the settings
    /// block is renumbered by the walker afterwards because its up-neighbour depends on how many
    /// row nodes currently exist.</summary>
    private void RefreshList()
    {
        if (list is null)
        {
            return;
        }

        list.OptionsList = evenOnly
            ? [.. allRows.Where(row => row.Number % 2 == 0)]
            : [.. allRows];

        foreach (var node in list.OptionNodes)
        {
            node.OnPadConfirmed = OnRowPadConfirmed;
        }

        ApplyNavigationGraph();

        if (InternalAddon is not null)
        {
            InternalAddon->UpdateCollisionNodeList(false);
        }
    }

    private void ApplyNavigationGraph()
    {
        if (list is null || settingsList is null)
        {
            return;
        }

        // ListNode reserves four nav slots per row so a row can carry left/right sub-actions later;
        // the last row therefore sits at (count - 1) * 4 + NavIndex + 1, and that is what "up" out
        // of the settings block has to point at (the sentinel one past it only scrolls).
        var visibleRows = list.OptionNodes.Count;
        var lastRowNavIndex = visibleRows > 0
            ? ((visibleRows - 1) * 4) + ListNavIndex + 1
            : ListNavIndex;

        var next = SpikeNavigationWalker.Apply(settingsList, SettingsNavStart, lastRowNavIndex, TabBarNavIndex);

        if (next != SettingsNavStart + SettingsNavCount)
        {
            log.Warning($"Spike nav: settings block numbered {SettingsNavStart}..{next - 1}, expected {SettingsNavCount} entries — tab bar wrap-around index is stale.");
        }

        log.Information($"Spike nav graph: tabs 1..{TabCount}, list sentinel {ListNavIndex}, rows {ListNavIndex + 1}..{lastRowNavIndex} ({visibleRows} row nodes), settings {SettingsNavStart}..{next - 1}.");
    }

    private void OnRowSelected(SpikeRowModel? model)
    {
        if (model is null)
        {
            return;
        }

        model.Selections++;
        list?.Update();
        SetActionLine($"Selected: {model.Label} (selection #{model.Selections})");
    }

    private void OnRowPadConfirmed(SpikeRowModel model)
    {
        model.PadConfirms++;
        list?.Update();
        SetActionLine($"Controller confirm on: {model.Label} (pad confirm #{model.PadConfirms})");
    }

    private void SetActionLine(string text)
    {
        if (actionNode is not null)
        {
            actionNode.String = text;
        }

        log.Information($"Spike nav: {text}");
    }
}
