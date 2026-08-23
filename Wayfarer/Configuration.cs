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

/// <summary>Corner presets for the Wayfarer window. The game's own title-bar right-click menu
/// already offers Move/Scale/Reset for mouse users; this exists so a controller player, who cannot
/// reach that menu, can still put the window somewhere it does not cover the action.</summary>
public enum HubPositionPreset
{
    Center,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>Everything Wayfarer remembers between sessions, grouped by the feature that owns it.
/// Dalamud serialises this whole object, so a property removed here is silently dropped from an
/// existing config file rather than breaking it — and one added here defaults for everyone until
/// they change it.</summary>
public sealed class Configuration : IPluginConfiguration
{
    /// <summary>The version this build writes. Bumped to 4 for the leaner first-run defaults — see
    /// <see cref="Migrate"/>.</summary>
    public const int CurrentVersion = 4;

    /// <summary>What <see cref="Modules.UnlockChecklistModule"/> was called before the rename, and
    /// therefore the key its saved enabled flag is under in an existing config.</summary>
    private const string OldUnlocksModuleName = "Unlock Checklist";

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Per-module enabled flag, keyed by <see cref="Modules.IModule.Name"/>. A missing key
    /// means "use the module's own default" — see <see cref="Modules.ModuleRegistry.Register"/>.
    /// Nested per-module config classes are added alongside the modules that need them.</summary>
    public Dictionary<string, bool> ModuleEnabled { get; set; } = [];

    public QuestHelperConfig QuestHelper { get; set; } = new();

    public UnlockChecklistConfig UnlockChecklist { get; set; } = new();

    public HuntingLogConfig HuntingLog { get; set; } = new();

    public InputModeConfig InputMode { get; set; } = new();

    public GuidanceConfig Guidance { get; set; } = new();

    public HubConfig Hub { get; set; } = new();

    /// <summary>Brings a config written by an older build up to date, and reports whether anything
    /// changed so the caller can save it.
    ///
    /// <para>Version 2 moves the readout off "follow the quest tracker". That was the shipped
    /// default and it was wrong on a television: on a 16:9 layout it put the readout underneath the
    /// minimap, and the second line — the one with the objective on it — was drawn behind the map
    /// and could not be read. Anyone still on the old default is moved to top centre; anyone who
    /// went into the settings and chose the tracker deliberately keeps it, because a migration that
    /// overrides a deliberate choice is a bug of its own.</para>
    ///
    /// <para>Version 3 renames the unlocks feature from "Unlock Checklist" to "Unlocks", because
    /// the player could not tell what a checklist was. <see cref="ModuleEnabled"/> is keyed by that
    /// name, so a player who had deliberately switched the feature off would silently have had it
    /// switched back on. The flag moves with the name.</para>
    ///
    /// <para><b>Version 4 makes a fresh install leaner, and deliberately copies nothing.</b> Four
    /// shipped defaults moved from on to off — the map flag, the nameplate markers, the
    /// nearby-unlock lines and the hunting summary — and this step is the guarantee that only NEW
    /// installs get them. It has nothing to copy because the config file already holds this player's
    /// own answer for each of the four: Dalamud writes every public property on every save, all four
    /// properties are older than the <see cref="Version"/> field itself, and deserialisation has
    /// therefore already put the player's value back before this method is reached. Only
    /// <c>new Configuration()</c> — which is exactly and only what a first run gets — ever sees a
    /// declared default at all.</para>
    ///
    /// <para>Which is also why the step is a version stamp and NOT a re-assertion of the old values.
    /// Setting them back to true here would be the bug it looks like a fix for: by this point the
    /// four properties hold what the player chose, so "restoring" them would switch the markers back
    /// on for everybody who had gone into the settings and turned them off.</para></summary>
    public bool Migrate()
    {
        if (Version >= CurrentVersion)
        {
            return false;
        }

        if (Version < 2 && QuestHelper.ReadoutPosition == ReadoutPosition.FollowQuestTracker)
        {
            QuestHelper.ReadoutPosition = ReadoutPosition.TopCentre;
        }

        if (Version < 3 && ModuleEnabled.Remove(OldUnlocksModuleName, out var unlocksEnabled))
        {
            ModuleEnabled[Modules.UnlockChecklistModule.FeatureName] = unlocksEnabled;
        }

        // Version 4 has no step of its own, on purpose. See the note above: the file this object was
        // loaded from already carries the player's own value for every default that moved, so the
        // only thing version 4 has to do is record that the shipped defaults are no longer what an
        // old file would have been written against.
        Version = CurrentVersion;
        return true;
    }
}

/// <summary>Settings for the one Wayfarer window.</summary>
public sealed class HubConfig
{
    public HubPositionPreset Position { get; set; } = HubPositionPreset.Center;
}

/// <summary>Settings for the guidance framework itself — the part that decides what the arrow
/// follows, shared by every feature that can own it.</summary>
public sealed class GuidanceConfig
{
    /// <summary>Marks the current target with the game's own map flag while an explicit mode (an
    /// unlock route, a hunt) is engaged, moving it as the plan advances — the map pin, minimap pin
    /// and compass marker the game itself uses.
    ///
    /// <b>Off for a new install.</b> It is genuinely useful once a player is running chained routes,
    /// and it is safe — the game stores exactly ONE flag and setting it destroys the player's, so
    /// Wayfarer snapshots theirs before taking it and puts it back the moment the route or hunt ends
    /// — but it writes three marks onto surfaces the player did not ask Wayfarer to touch, and
    /// someone meeting the plugin for the first time did not ask for their own flag to move. Turn it
    /// on and it is exactly as it was; leave it off and nothing ever writes the flag. See
    /// <see cref="Configuration.Migrate"/> for why nobody who already has it on loses it.</summary>
    public bool MarkObjectiveWithMapFlag { get; set; } = true;

    /// <summary>Puts the game's own quest-marker icon over the heads of hunting-log targets and
    /// unlock quest givers, through the same nameplate channel the game uses for quest availability.
    ///
    /// <b>Off for a new install.</b> This is the strongest form of "it should be obvious there is
    /// something near me I can grab" — a marker over the giver's head is read without opening
    /// anything, in the place the player already looks — and that strength is exactly why it is not
    /// the first thing a player should meet. It puts the plugin's marks into the world itself, on
    /// characters the game did not mark, and someone who has not decided they want that has not
    /// asked for it. Three guarantees still make it safe once it is switched on: the icon id is
    /// validated against the game's own texture table before it is ever written (a bad value
    /// degrades to no marker), a plate the game has already marked is never overwritten, and the
    /// match set is limited to the current zone's targets. See <see cref="NamePlateMarkerIcon"/> for
    /// the companion escape hatch, and <see cref="Configuration.Migrate"/> for why nobody who
    /// already has it on loses it.</summary>
    public bool MarkTargetsOnNameplates { get; set; } = true;

    /// <summary>Which icon the nameplate marker uses. A setting rather than a constant because
    /// whether an icon "looks right" above a monster is the one thing that cannot be settled
    /// without seeing it: 71223 is the game's own quest-in-progress marker (EventIconType row 3's
    /// base 71200 plus the in-progress offset of 3, corroborated by EventIconPriority row 40), and
    /// every alternative offered is an id the game itself emits. Validated against the game's own
    /// texture table before it is ever written, so a bad value degrades to "no marker" rather than
    /// to a broken nameplate.</summary>
    public int NamePlateMarkerIcon { get; set; } = 71223;
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

    /// <summary>Where the readout sits on screen. Top centre by default: it is what the player
    /// asked for, and it is the one part of a default 16:9 HUD that is clear of both the minimap and
    /// the quest tracker. See <see cref="Configuration.Migrate"/> for why the old
    /// tracker-following default was retired.</summary>
    public ReadoutPosition ReadoutPosition { get; set; } = ReadoutPosition.TopCentre;

    /// <summary>The readout's own position, as a fraction (0..1) of the usable screen — 0 hard
    /// against the left safe margin, 1 hard against the right one. Live in every mode: while a
    /// preset is selected these mirror wherever the preset put the readout, so nudging one of them
    /// (or dragging the readout) continues from where it already is rather than jumping. Persisted
    /// only when the player actually moves it, at which point <see cref="ReadoutPosition"/> becomes
    /// <see cref="Core.Ui.ReadoutPosition.Custom"/>.
    ///
    /// <para>A fraction rather than a pixel count so that changing resolution — windowed to
    /// full-screen, a different monitor, a television — moves the readout to the same <i>place</i>
    /// instead of stranding it off the edge.</para></summary>
    public float ReadoutFractionX { get; set; } = 0.5f;

    /// <inheritdoc cref="ReadoutFractionX"/>
    public float ReadoutFractionY { get; set; }

    /// <summary>Puts the readout into the game's own HUD-Layout-style move mode: a translucent
    /// handle appears over it and it can be dragged with the mouse, exactly as the game's HUD Layout
    /// editor moves its own elements.
    ///
    /// <b>A mode rather than always-on, deliberately.</b> Dragging is implemented by a viewport-level
    /// mouse listener that marks a click inside the readout's box as handled, so leaving it on
    /// permanently would swallow world clicks and camera drags under the readout — which is exactly
    /// the click-through guarantee that makes the readout safe to park over the world in the first
    /// place. It would also mean permanently painting the HUD-Layout handle over the readout and
    /// putting the hand cursor on it. Off by default; a controller never needs it, because the two
    /// position sliders in Settings move the readout with no cursor at all.</summary>
    public bool ReadoutMoveMode { get; set; }

    /// <summary>Which colour the readout's arrow is drawn in. Applied on the next frame, with no
    /// reload — see <see cref="ArrowIconVariant"/>.</summary>
    public ArrowIconVariant ArrowIcon { get; set; } = ArrowIconVariant.Amber;

    /// <summary>Draws the readout with the game's own text nodes, fonts and colours instead of the
    /// old ImGui widget. On by default; the ImGui widget remains only as the automatic fallback
    /// for when the overlay cannot be created, and this setting is the manual version of the same
    /// escape hatch.</summary>
    public bool UseNativeReadout { get; set; } = true;

    public bool ArrowHideInCombat { get; set; } = true;

    public bool ArrowHideInDuty { get; set; } = true;

    public bool ClickTeleportEnabled { get; set; } = true;

    /// <summary>Hides the readout entirely, whichever host is drawing it. Toggled by <c>/way</c>;
    /// checked by <see cref="Windows.ReadoutFeed.ShouldShow"/>.</summary>
    public bool WidgetHidden { get; set; }

    /// <summary>Hides Wayfarer's entry in Dalamud's server info bar. Off by default: the bar entry
    /// is the plugin's one surface that is on screen whatever else is hidden, and it is where the
    /// "there is something to pick up here" marker lives — see <see cref="Windows.DtrEntry"/>.</summary>
    public bool DtrHidden { get; set; }

    /// <summary>Controls <see cref="ContextMenuActions"/>'s gating. Defaults to
    /// <see cref="ContextMenuMode.ControllerOnly"/>: a controller gets the click-through readout,
    /// which by construction carries no affordances, so it needs one native, d-pad-navigable place
    /// to start a hunt, reach the checklist and take the teleport the readout is recommending — and
    /// the game's own context menu is exactly that, with no new chrome and no cursor. Left off for
    /// mouse players by default, where the readout itself is clickable and an entry in every
    /// right-click menu is noise.</summary>
    public ContextMenuMode MenuMode { get; set; } = ContextMenuMode.ControllerOnly;
}

/// <summary>Settings for <see cref="Modules.UnlockChecklistModule"/>.</summary>
public sealed class UnlockChecklistConfig
{
    /// <summary>Shows the nearest few available unlocks in this zone, with live distances, as muted
    /// lines under the readout — the glance that makes opening the checklist optional.
    ///
    /// <para><b>Off for a new install</b>, and absent regardless when the module itself is disabled.
    /// It is up to three extra lines on a readout whose whole appeal is that it is short, and it
    /// answers a question ("what else is around here?") the player has not asked while they are
    /// being guided somewhere else. The unlocks are not switched off with it — they are still the
    /// window's own tab, which is what the plugin list's main button opens. See
    /// <see cref="Configuration.Migrate"/> for why nobody who already has it on loses it.</para></summary>
    public bool ShowOnWidget { get; set; } = true;
}

/// <summary>Settings for <see cref="Modules.HuntingLogModule"/>.</summary>
public sealed class HuntingLogConfig
{
    /// <summary>Shows the current hunting-log target and its kill count as a muted line under the
    /// readout, for when a hunt is running but is not what the arrow is following.
    ///
    /// <para><b>Off for a new install</b>, and absent regardless when the module itself is disabled.
    /// It is a second thing on a readout that is about one thing, and it is only ever about a hunt
    /// the player is not currently being guided through — which is exactly the state somebody who
    /// has just installed the plugin is not in. The hunting log is not switched off with it. See
    /// <see cref="Configuration.Migrate"/> for why nobody who already has it on loses
    /// it.</para></summary>
    public bool ShowOnWidget { get; set; } = true;
}
