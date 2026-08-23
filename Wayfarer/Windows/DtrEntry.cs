using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows;

/// <summary>The plugin's one entry in Dalamud's server info bar (the "DTR" — the row of addon
/// text beside the clock).
///
/// This is the fix for a specific gap: the guidance readout is click-through by design (see
/// <c>Native.GuidanceOverlay</c>'s doc comment), so on a default setup it carries no affordances
/// at all, and the ImGui fallback it replaces only ever appears once that readout is off. Before
/// this existed the only ways back into Wayfarer once its window was closed were the plugin
/// installer's own buttons, the slash commands, and — for a controller only — the game's context
/// menu; none of those is a visible, mouse-reachable, always-on-screen affordance. The bar entry
/// is exactly that: it sits where the player is already looking, in the corner the game itself
/// reserves for addon status text, and it never depends on the readout's own visibility.
///
/// Text is refreshed from <see cref="ReadoutFeed.ComposeDtr"/>, so it can never say something the
/// readout itself disagrees with, and the decision of what to say lives in the tested
/// <see cref="DtrComposer"/> rather than here.</summary>
internal sealed class DtrEntry(
    IDtrBar dtrBar,
    ReadoutFeed feed,
    QuestHelperConfig cfg,
    IFramework framework,
    Action openChecklist,
    Action openSettings,
    Action stop,
    IPluginLog log) : IDisposable
{
    // Also the title Dalamud stores the entry under (IDtrBar.Get's first argument is a key, not
    // just a label) — see IDtrBar.Remove(string). Namespaced implicitly by being a Dalamud-wide
    // key, so it deliberately matches the plugin's own name rather than something generic.
    private const string Title = "Wayfarer";

    private IDtrBarEntry? entry;
    private DtrText? lastText;
    private bool loggedFailure;

    /// <summary>Creates the entry and starts refreshing it every frame. Never throws — a bar that
    /// is unavailable, or that throws creating the entry, is logged once and the plugin carries
    /// on with every other entry point intact.</summary>
    public void Start()
    {
        try
        {
            entry = dtrBar.Get(Title);
            entry.Tooltip = BuildTooltip();
            entry.OnClick = OnClick;
            entry.Shown = !cfg.DtrHidden;
            framework.Update += OnFrame;
        }
        catch (Exception ex)
        {
            LogFailureOnce(ex);
        }
    }

    public void Dispose()
    {
        framework.Update -= OnFrame;

        try
        {
            entry?.Remove();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Wayfarer: removing the server info bar entry failed — it may linger until the next full reload.");
        }

        entry = null;
    }

    private static SeString BuildTooltip() =>
        new SeStringBuilder()
            .AddText("Wayfarer — left-click opens the checklist, right-click opens settings, shift-click stops the current hunt or route. "
                + "An exclamation mark means there is something you can pick up in this zone.")
            .Build();

    private static SeString BuildText(DtrText text)
    {
        var builder = new SeStringBuilder();

        // The alert first, because it is the thing that has to catch the eye without being read.
        // ExclamationRectangle is the game's own "there is a quest here" marker — the same shape a
        // player already scans for over an NPC's head, which is exactly the association wanted.
        if (text.UnlocksNearby)
        {
            builder.AddIcon(BitmapFontIcon.ExclamationRectangle);
        }

        if (Icon(text.Glyph) is { } icon)
        {
            builder.AddIcon(icon);
        }

        return builder.AddText(text.Text).Build();
    }

    // Every value here is an icon the game itself already uses elsewhere for the same idea, kept
    // to a plain "no icon" default rather than guessing at glyphs nobody has seen on the bar.
    private static BitmapFontIcon? Icon(DtrGlyph glyph) => glyph switch
    {
        DtrGlyph.Hunting => BitmapFontIcon.NotoriousMonster,
        DtrGlyph.Route => BitmapFontIcon.Aetheryte,
        _ => null,
    };

    private void OnFrame(IFramework tick)
    {
        if (entry is null)
        {
            return;
        }

        try
        {
            entry.Shown = !cfg.DtrHidden;
            if (!entry.Shown)
            {
                return;
            }

            var text = feed.ComposeDtr();
            if (text.Equals(lastText))
            {
                return;
            }

            lastText = text;
            entry.Text = BuildText(text);
        }
        catch (Exception ex)
        {
            LogFailureOnce(ex);
        }
    }

    private void OnClick(DtrInteractionEvent evt)
    {
        try
        {
            // Shift beats which button was clicked — the universal exit needs exactly one gesture
            // regardless of hand position, and MouseClickType only ever distinguishes Left/Right
            // (there is no middle-click on the bar), so a modifier is the only third option cheap
            // enough to add without a whole submenu.
            if (evt.ModifierKeys == ClickModifierKeys.Shift)
            {
                stop();
            }
            else if (evt.ClickType == MouseClickType.Right)
            {
                openSettings();
            }
            else
            {
                openChecklist();
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Wayfarer: the server info bar entry's click handler failed.");
        }
    }

    private void LogFailureOnce(Exception ex)
    {
        if (loggedFailure)
        {
            return;
        }

        loggedFailure = true;
        log.Error(ex, "Wayfarer: the server info bar entry is unavailable — every other way into Wayfarer keeps working.");
    }
}
