using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows;

/// <summary>The plugin's one entry in Dalamud's server info bar (the "DTR" — the row of addon
/// text beside the clock).
///
/// It does two jobs. It is the way back into Wayfarer that is always on screen and always
/// clickable: the readout can be hidden, hidden in combat, or — on a controller — click-through,
/// and before this existed the only remaining routes in were the plugin installer's buttons, the
/// slash commands and the context menu. And it is where the ambient loop's alert lives: an
/// exclamation marker whenever this zone has an unlock available, which keeps showing while a route
/// or a hunt is running, because walking past a pickup mid-route is exactly when it is worth
/// knowing. It sits in the corner the game itself reserves for addon status text and never depends
/// on the readout's own visibility.
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
            const string message =
                "Wayfarer: removing the server info bar entry failed, so a dead Wayfarer entry may sit on the "
                + "bar until the game is restarted. Nothing else is affected.";
            log.Warning(ex, message);
        }

        entry = null;
    }

    /// <summary>Says what every part of the entry means, because a glyph on the info bar has no
    /// room to explain itself and the player asked — reasonably — why there was an aetheryte on it
    /// while the target was fifty-six yalms away in the same zone.</summary>
    private static SeString BuildTooltip() =>
        new SeStringBuilder()
            .AddText("Wayfarer. The crystal means the next step uses the aetheryte network - the words beside it say "
                + "where. No crystal means walk there, and the numbers are how far through and how far left. "
                + "An exclamation mark means there is an unlock you can pick up in this zone. "
                + "Left-click opens your unlocks, right-click opens settings, shift-click stops the current hunt or route.")
            .Build();

    private static SeString BuildText(DtrText text)
    {
        var builder = new SeStringBuilder();

        // The alert first, because it is the thing that has to catch the eye without being read.
        // ExclamationRectangle is the game's own "there is a quest here" marker — the same shape a
        // player already scans for over an NPC's head, which is exactly the association wanted.
        // It is emitted from exactly one condition — DtrComposer sets UnlocksNearby only when the
        // unlock count the READOUT is given is non-zero — so the bar cannot claim a pickup the
        // readout has been told to keep quiet about.
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

    // One glyph, one meaning: the aetheryte crystal appears when, and only when, the next step is a
    // teleport or an aethernet hop. It used to mean "a route is in progress", which is how it came
    // to sit beside a target fifty-six yalms away in the same zone. Everything else is words, and
    // that is deliberate — see DtrGlyph.
    private static BitmapFontIcon? Icon(DtrGlyph glyph) =>
        glyph == DtrGlyph.Aetheryte ? BitmapFontIcon.Aetheryte : null;

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
            const string message =
                "Wayfarer: clicking the server info bar entry did nothing — the window it should have opened "
                + "is still reachable from the plugin list or /wayfarer.";
            log.Error(ex, message);
        }
    }

    private void LogFailureOnce(Exception ex)
    {
        if (loggedFailure)
        {
            return;
        }

        loggedFailure = true;
        const string message =
            "Wayfarer: the server info bar entry is unavailable, so there is no Wayfarer icon on the bar for "
            + "the rest of the session. Every other way in — the readout, the plugin list, /wayfarer, the "
            + "game's own menus — keeps working. Reported once.";
        log.Warning(ex, message);
    }
}
