using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.BaseTypes.ComponentNode;
using KamiToolKit.Nodes;

namespace Wayfarer.App.Settings;

/// <summary>The settings window, built the way VanillaPlus builds its config windows: a scrolling
/// tabbed list sized to the window's content, a category heading, and one game checkbox per
/// module with its description as the tooltip. It knows the modules only through
/// <see cref="IModuleHost"/>, so a new module appears here by being registered and nothing else.
///
/// <para>A module's own settings, when it has some, go under its checkbox one tab in — the same
/// list, the next tab index. The list is rebuilt on every open and freed on every close, because
/// the game frees the addon's node tree when it closes.</para></summary>
internal sealed class SettingsAddon(IModuleHost host) : NativeAddon
{
    private const string ModulesHeading = "Modules";
    private const float WindowWidth = 400f;
    private const float TallestWindow = 400f;
    private const float CheckboxHeight = 24f;
    private const float BottomPadding = 24f;
    private const int HeadingTab = 0;
    private const int ModuleTab = 1;

    private ScrollingNode<TabbedVerticalListNode>? list;

    /// <inheritdoc/>
    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        list = new ScrollingNode<TabbedVerticalListNode>
        {
            ContentNode =
            {
                FitContents = true,
                FitWidth = true,
                NavIndex = 1,
            },
            AutoHideScrollBar = true,
        };
        list.AttachNode(this);

        list.ContentNode.AddNode(HeadingTab, new CategoryTextNode { String = ModulesHeading });
        foreach (var module in host.Modules)
        {
            list.ContentNode.AddNode(ModuleTab, Toggle(module));
        }

        if (list.ContentNode.GetNodes<ComponentNode>().FirstOrDefault()?.FocusNode is { } focus)
        {
            addon->FocusNode = focus;
        }

        FitWindowToList();
    }

    /// <inheritdoc/>
    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        list?.Dispose();
        list = null;
    }

    private CheckboxNode Toggle(IModule module)
    {
        var toggle = new CheckboxNode
        {
            Height = CheckboxHeight,
            String = module.Name,
            IsChecked = host.IsEnabled(module),
            TextTooltip = module.Description,
        };
        toggle.OnClick = enabled => _ = host.SetEnabledAsync(module, enabled);
        return toggle;
    }

    /// <summary>Sizes the window to the list, up to a ceiling past which the list scrolls, then
    /// fits the list into the content area that gives.</summary>
    private void FitWindowToList()
    {
        if (list is null)
        {
            return;
        }

        list.RecalculateSizes();
        var listHeight = Math.Min(list.ContentNode.Height, TallestWindow);
        SetWindowSize(new Vector2(WindowWidth, listHeight + ContentStartPosition.Y + BottomPadding));

        list.Size = ContentSize + new Vector2(0f, ContentPadding.Y);
        list.Position = ContentStartPosition - new Vector2(0f, ContentPadding.Y);
        list.RecalculateSizes();
        list.ContentNode.RecalculateLayout();
    }
}
