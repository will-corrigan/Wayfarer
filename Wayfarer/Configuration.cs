using Dalamud.Configuration;
using Wayfarer.Core.Input;

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

/// <summary>Where the guidance readout sits. A plugin cannot register with the game's HUD Layout
/// editor — <c>AddonHudLayoutScreen</c>'s tables are fixed-size with no registration API, and
/// KamiToolKit explicitly opts its own addons out — so presets are the substitute for "drag it
/// where you want in HUD Layout".</summary>
public enum ReadoutPosition
{
    /// <summary>Follows the game's own quest tracker, mirroring the way it flips sides when the
    /// player moves it across the screen. The default, because it needs no configuration and puts
    /// Wayfarer's guidance exactly where the player already looks for objectives.</summary>
    FollowQuestTracker,

    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>Which of the minimap's own direction chevrons the readout's arrow is cut from.
///
/// The game keeps five of them side by side on <c>ui/uld/NaviMap.tex</c> — the same sheet, the same
/// 24x24 part size, the same shape, differing only in colour — and <c>ui/uld/NaviMap.uld</c> declares
/// all five as real parts. They are offered as a setting because which one reads as "go this way"
/// against a particular player's terrain, HUD theme and screen is the sort of question only an eye
/// can settle, and changing it must not need a new build.</summary>
public enum ArrowIconVariant
{
    /// <summary>The game's own quest-direction chevron. The default.</summary>
    Amber,

    Green,

    Blue,

    Red,

    White,
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
    public int Version { get; set; } = 1;

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
    /// On by default because it is what makes a chained route usable at a glance, and safe to
    /// default on only because of the guarantee around it: the game stores exactly ONE flag and
    /// setting it destroys the player's, so Wayfarer snapshots theirs before taking it and puts it
    /// back the moment the route or hunt ends. Turn this off and nothing ever writes the
    /// flag.</summary>
    public bool MarkObjectiveWithMapFlag { get; set; } = true;

    /// <summary>Puts the game's own quest-marker icon over the heads of hunting-log targets and
    /// unlock quest givers, through the same nameplate channel the game uses for quest availability.
    ///
    /// <b>On by default.</b> This is the strongest form of "it should be obvious there is something
    /// near me I can grab" — a marker over the giver's head is read without opening anything, in the
    /// place the player already looks. Three guarantees are what make defaulting it on safe: the
    /// icon id is validated against the game's own texture table before it is ever written (a bad
    /// value degrades to no marker), a plate the game has already marked is never overwritten, and
    /// the match set is limited to the current zone's targets. See <see cref="NamePlateMarkerIcon"/>
    /// for the companion escape hatch.</summary>
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

    /// <summary>Where the readout sits on screen.</summary>
    public ReadoutPosition ReadoutPosition { get; set; } = ReadoutPosition.FollowQuestTracker;

    /// <summary>Which of the minimap's direction chevrons the readout's arrow uses. Applied on the
    /// next frame, with no reload — see <see cref="ArrowIconVariant"/>.</summary>
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
    /// lines under the readout — the glance that makes opening the checklist optional. On by
    /// default; absent regardless when the module itself is disabled.</summary>
    public bool ShowOnWidget { get; set; } = true;
}

/// <summary>Settings for <see cref="Modules.HuntingLogModule"/>.</summary>
public sealed class HuntingLogConfig
{
    /// <summary>Shows the current hunting-log target and its kill count as a muted line under the
    /// readout, for when a hunt is running but is not what the arrow is following. On by default;
    /// absent regardless when the module itself is disabled.</summary>
    public bool ShowOnWidget { get; set; } = true;
}
