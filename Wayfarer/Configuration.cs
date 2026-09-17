using Dalamud.Configuration;
using Wayfarer.Core.Input;
using Wayfarer.Core.Ui;

namespace Wayfarer;

/// <summary>Gates when <see cref="ContextMenuActions"/> registers its "Wayfarer" submenu on the
/// game's Default context menu. <see cref="ControllerOnly"/> is the default: an entry in every
/// right-click menu is noise for a mouse player, who has the plugin list and the window itself,
/// but it is the one cursor-free way into Wayfarer's actions on a controller.</summary>
public enum ContextMenuMode
{
    Never,
    ControllerOnly,
    Always,
}

/// <summary>Everything Wayfarer remembers between sessions, grouped by the feature that owns it.
/// Dalamud serialises this whole object, so a property removed here is silently dropped from an
/// existing config file rather than breaking it — and one added here defaults for everyone until
/// they change it.</summary>
public sealed class Configuration : IPluginConfiguration
{
    /// <summary>The version this build writes. Versions 4 and 5 carry no migration step of their
    /// own — see <see cref="Migrate"/> for why they exist anyway. Version 5 is the build that
    /// removed the unlock checklist, the hunting log, the aether-current routes and the old
    /// readout, along with every setting that only configured them.</summary>
    public const int CurrentVersion = 5;

    /// <summary>What the unlocks module was called before the rename, and therefore the key its
    /// saved enabled flag is under in an existing config.</summary>
    private const string OldUnlocksModuleName = "Unlock Checklist";

    /// <summary>What it was renamed to. A literal rather than a reference to the module, which this
    /// build no longer has: the version-3 step has to keep working for a config written before the
    /// module was removed, and the name it wrote is a fact about that file, not about the code.</summary>
    private const string NewUnlocksModuleName = "Unlocks";

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Per-module enabled flag, keyed by <see cref="Modules.IModule.Name"/>. A missing key
    /// means "use the module's own default" — see <see cref="Modules.ModuleRegistry.Register"/>.
    /// Nested per-module config classes are added alongside the modules that need them.</summary>
    public Dictionary<string, bool> ModuleEnabled { get; set; } = [];

    public QuestHelperConfig QuestHelper { get; set; } = new();

    public InputModeConfig InputMode { get; set; } = new();

    public GuidanceConfig Guidance { get; set; } = new();

    /// <summary>Brings a config written by an older build up to date, and reports whether anything
    /// changed so the caller can save it.
    ///
    /// <para><b>Version 2 has no step any more.</b> It moved the readout off "follow the quest
    /// tracker", and the readout and its position setting are both gone; a step that rewrites a
    /// property no build has is a step that cannot run. Dalamud drops the property from the file on
    /// the next save either way.</para>
    ///
    /// <para>Version 3 renames the unlocks feature from "Unlock Checklist" to "Unlocks", because
    /// the player could not tell what a checklist was. <see cref="ModuleEnabled"/> is keyed by that
    /// name, so a player who had deliberately switched the feature off would silently have had it
    /// switched back on. The flag moves with the name.</para>
    ///
    /// <para><b>Version 4 has no step, because the change it was bumped for was reverted before it
    /// shipped.</b> Four defaults were briefly going to move from on to off for a fresh install, and
    /// then were kept on. The number stays where it is rather than going backwards, and it is a
    /// stamp and nothing more.</para>
    ///
    /// <para><b>Version 5 has no step either.</b> It is the build that removed the unlock checklist,
    /// the hunting log, the aether-current routes and the old readout. Removing a property needs no
    /// migration: Dalamud serialises this object, so a property that no longer exists here is simply
    /// dropped from the file on the next save. The number is a stamp saying which build wrote the
    /// file, which is what makes the next step that does need one able to tell.</para>
    ///
    /// <para>Worth keeping the reasoning that came out of it, because it is the shape every future
    /// default change has to take. A migration must not re-assert a default. By the time this method
    /// runs, every one of those properties already holds this player's own answer — Dalamud writes
    /// every public property on every save, all four are older than the <see cref="Version"/> field
    /// itself, and deserialisation has put the saved value back before anything here is reached — so
    /// "restoring" a default would switch a setting back on for everybody who had deliberately turned
    /// it off. Only <c>new Configuration()</c>, which is exactly and only what a first run gets, ever
    /// sees a declared default at all. Which is also why moving a default needs no step: it applies
    /// itself to new installs and to nobody else.</para></summary>
    public bool Migrate()
    {
        if (Version >= CurrentVersion)
        {
            return false;
        }

        if (Version < 3 && ModuleEnabled.Remove(OldUnlocksModuleName, out var unlocksEnabled))
        {
            ModuleEnabled[NewUnlocksModuleName] = unlocksEnabled;
        }

        // Versions 4 and 5 have no step of their own — see the notes above.
        Version = CurrentVersion;
        return true;
    }
}

/// <summary>Settings for the guidance framework itself — the part that decides what the arrow
/// follows, shared by every feature that can own it.</summary>
public sealed class GuidanceConfig
{
    /// <summary>Marks the current target with the game's own map flag while an explicit mode is
    /// engaged, moving it as the plan advances — the map pin, minimap pin and compass marker the
    /// game itself uses.
    ///
    /// <b>On for a new install</b>, because a player who has just installed a plugin for being guided
    /// somewhere should be guided somewhere, and the flag is the guidance the game itself already
    /// draws. It is safe to have on: the game stores exactly ONE flag and setting it destroys the
    /// player's, so Wayfarer snapshots theirs before taking it and puts it back the moment the route
    /// ends. It only ever writes while an explicit mode is engaged — nothing marks anything
    /// while the plugin is idle — and turning it off means nothing ever writes the flag at
    /// all.</summary>
    public bool MarkObjectiveWithMapFlag { get; set; } = true;
}

/// <summary>Settings for <see cref="InputModeService"/>, shared by every window that adapts to
/// the player's input device.</summary>
public sealed class InputModeConfig
{
    public InputModeOverride Override { get; set; } = InputModeOverride.Auto;

    /// <summary>Wires the game's own cursor-navigation graph through the Wayfarer window, so a
    /// controller drives it the way it drives every other game window.
    ///
    /// On by default — before it existed the window had no graph at all and the cursor was stranded
    /// on whichever button took initial focus, so this can only add reachable edges. It stays a
    /// setting purely as an escape hatch: if a graph ever traps the cursor somewhere, this turns
    /// the whole mechanism off without a new build. Esc and the window's own close button are
    /// never disabled and never depend on the graph.</summary>
    public bool CursorNavigation { get; set; } = true;
}

/// <summary>Settings for <see cref="Modules.QuestHelperModule"/>. There is no "show widget"
/// flag here — the widget's visibility while the module is enabled is the module-level
/// <see cref="WidgetHidden"/> toggle (bound to <c>/way</c>); the module's own enabled state
/// (see <see cref="Modules.IModule.Enabled"/>) governs whether it runs at all.</summary>
public sealed class QuestHelperConfig
{
    /// <summary>Writes the readout's per-change diagnostics (why there is no arrow, what rotation
    /// the chevron is being given) to the log. Off by default: the compass direction changes every
    /// time the camera swings past a compass point, so a play session would write thousands of lines
    /// nobody asked for. Real failures — a texture that will not load — are warned about regardless.</summary>
    public bool LogDiagnostics { get; set; }

    public float ArrowScale { get; set; } = 1.0f;

    /// <summary>Multiplies the readout's text size. Still required after the move to a native
    /// overlay, and this is worth stating plainly because the opposite is the intuitive guess:
    /// KamiToolKit's overlay addons are <b>deliberately de-scaled</b> to raw screen pixels
    /// (<c>addon-&gt;SetScale(1.0f / GetGlobalUIScale(), true)</c>) so overlay nodes can be
    /// positioned in absolute screen coordinates. Nothing under an overlay follows the player's
    /// interface size unless the plugin multiplies it in itself, every frame — which the readout
    /// does, as <c>GetGlobalUIScale() * TextScale</c>. 0.8–2.0.</summary>
    public float TextScale { get; set; } = 1.0f;

    /// <summary>Which colour the readout's arrow is drawn in. Applied on the next frame, with no
    /// reload — see <see cref="ArrowIconVariant"/>.</summary>
    public ArrowIconVariant ArrowIcon { get; set; } = ArrowIconVariant.Amber;

    public bool ArrowHideInCombat { get; set; } = true;

    public bool ArrowHideInDuty { get; set; } = true;

    public bool ClickTeleportEnabled { get; set; } = true;

    /// <summary>Hides the readout entirely, whichever host is drawing it. Toggled by <c>/way</c>;
    /// checked by <see cref="Windows.ReadoutFeed.ShouldShow"/>.</summary>
    public bool WidgetHidden { get; set; }

    /// <summary>Hides Wayfarer's entry in Dalamud's server info bar. Off by default: the bar entry
    /// is the plugin's one surface that is on screen whatever else is hidden — see
    /// <see cref="Windows.DtrEntry"/>.</summary>
    public bool DtrHidden { get; set; }

    /// <summary>Controls <see cref="ContextMenuActions"/>'s gating. Defaults to
    /// <see cref="ContextMenuMode.ControllerOnly"/>: the game's own context menu is one native,
    /// d-pad-navigable place for Wayfarer's actions, with no new chrome and no cursor. Left off for
    /// mouse players by default, where an entry in every right-click menu is noise.</summary>
    public ContextMenuMode MenuMode { get; set; } = ContextMenuMode.ControllerOnly;
}
