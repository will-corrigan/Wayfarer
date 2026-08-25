using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using Lumina.Text.ReadOnly;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>The guidance readout itself — the arrow and the lines, drawn with the game's own text
/// nodes, fonts and colours.
///
/// <b>This is the only definition of what the readout looks like.</b> It is a plain
/// <c>ResNode</c> rather than an overlay node or a window so that either surface can contain it: the
/// chromeless addon every player operates (<see cref="ReadoutAddon"/>) and the click-through overlay
/// that stands in when that addon cannot be built (<see cref="GuidanceOverlayNode"/>). They differ
/// in what the player can do to it, never in what it looks like — there is no second layout pass
/// anywhere to drift from this one.
///
/// <b>Scale is not automatic and this is the one thing about overlays that surprises everyone.</b>
/// KamiToolKit deliberately de-scales overlay addons to raw screen pixels
/// (<c>addon-&gt;SetScale(1.0f / GetGlobalUIScale(), true)</c>, reapplied on every resolution
/// change) so overlay nodes can be positioned in absolute screen coordinates. A 14pt font here
/// renders at 14 raw pixels whether the player's interface size is 100% or 200%. Everything below
/// is therefore multiplied by <c>GetGlobalUIScale() * userScale</c> every frame, by hand — which is
/// also why the plugin's own text-size setting had to stay rather than being deleted as redundant.
/// A normal addon is scaled by the game instead, which is why the body is told which kind of host it
/// is in (<c>hostIsHudScaled</c>) and why both produce identical pixels.
///
/// <b>It must never throw.</b> Its hosts run <see cref="Layout"/> from a per-frame update, so an
/// exception here is an exception sixty times a second inside the game's render path. Each host
/// wraps the call and switches itself off on the first failure.</summary>
internal sealed unsafe class ReadoutBodyNode : ResNode
{
    // Enough for a heading plus the deepest readout the composer can produce (objective, step,
    // distance, two routing lines, zone, and the muted context block — a hunting summary, the
    // ambient objective and up to ReadoutComposer.MaxNearbyUnlockLines unlocks). Pooled rather than
    // allocated per frame: nothing in a per-frame path should allocate.
    private const int MaxLines = 12;

    /// <summary>The most rows one line is allowed to wrap into. The layout's own cap, not a second
    /// one: the number that decides how tall a wrapped line's section is has to be the number that
    /// decides how tall its text node is, or the two disagree by exactly one row.</summary>
    private const float MaxWrappedLines = ReadoutBodyLayout.MaxWrappedLines;

    /// <summary>How every ordinary line behaves. Edge is not decoration over the 3D world — without
    /// an outline the text vanishes against bright terrain. WordWrap plus MultiLine is how the
    /// game's own journal and tooltips grow downward instead of truncating.</summary>
    private const TextFlags BodyFlags = TextFlags.Edge | TextFlags.WordWrap | TextFlags.MultiLine;

    /// <summary>One row of letters, for the single purpose of asking a line's own node how tall one
    /// row of it actually draws — see <see cref="WrappedLines"/>. An ascender and a descender, so the
    /// answer is the tallest a row of words gets rather than the height of whichever letters this
    /// particular line happens to start with.</summary>
    private const string RowProbe = "Ag";

    /// <summary>How the line that names what is being followed behaves instead: cut short with the
    /// engine's own ellipsis rather than wrapped.
    ///
    /// <para><b>Why this one line is the exception.</b> Everything else on the readout is a sentence
    /// about the objective and reads fine over two rows. The name is a label — it is what the
    /// switcher is attached to and what the eye lands on first — and a label that reflows the whole
    /// readout downward every time the quest changes is the thing that makes a tracker feel
    /// unsteady. <c>TextFlags.Ellipsis</c> is the game's own flag for exactly this, so the mark at
    /// the end is the mark the game uses everywhere else it runs out of room, at the width this
    /// readout actually has.</para>
    ///
    /// <para>A cut name is not a lost name: the full text is on the node's tooltip, and on a
    /// controller — which has no pointer to hover with — it is in full on the window's Following
    /// tab, on the row that is currently selected.</para></summary>
    private const TextFlags SubjectFlags = TextFlags.Edge | TextFlags.Ellipsis;

    /// <summary>The readout box: the banner's own width — the plate at the size the game draws it,
    /// plus the margin its emblem hangs into. 324, against the game's own 340 root.
    ///
    /// <para><b>Deliberately no longer the quest tracker's 400.</b> That was the right width while
    /// the readout was a bare block of text, because the tracker is the game's other always-on
    /// overlay. It became the wrong one the moment the readout started wearing a specific piece of
    /// art: it meant stretching a 300-wide plate by a third, and the result was visibly larger than
    /// the game's own banner sitting near it. At the plate's native width the nine-slice is the
    /// identity and the readout is exactly the size of the thing it is imitating.</para></summary>
    private const float BaseWidth = GameMetrics.Banner.Width;

    /// <summary>The game's own art sheet for the Main Scenario Guide's banner. Every rectangle taken
    /// out of it is in <see cref="GameMetrics.Banner"/>, which also says which parts are deliberately
    /// NOT used: the crimson meteor crest and the chapter ring are the main scenario's own marks and
    /// would be a claim the readout cannot keep.</summary>
    private const string BannerTexture = "ui/uld/ScenarioTree.tex";

    /// <summary>The name the readout gives whatever is being tracked, on the plate. Axis 14, the
    /// game's own headline size for this element.</summary>
    private const float BaseHeadlineSize = GameMetrics.Banner.HeadlineSize;

    /// <summary>The header pill's own words, and every subordinate line beneath the plate. Axis 12.
    ///
    /// <para><b>The banner has exactly two type levels and this is the lower one.</b> Axis 14 in the
    /// bar, Axis 12 under it — that is what <c>ScenarioTree.uld</c> does and there is no third size
    /// anywhere in it. The readout used to set its objective and its advice in Axis 14 and separate
    /// them by colour; under the banner they are subordinate lines, and subordinate lines are
    /// twelve.</para></summary>
    private const float BaseSubLineSize = GameMetrics.Banner.SubLineSize;

    /// <summary>The arrow's side, before scale.
    ///
    /// <para>The size of the quest tracker's own markers — ToDoList <c>1003 #4</c>, <c>1008 #5</c>
    /// and <c>1012 #4</c> are all 24x24 — sitting in the same 28-wide gutter the tracker reserves for
    /// them. It sits <b>in line</b> with the text, as a left gutter, rather than towering over the
    /// words it belongs to.</para></summary>
    private const float BaseArrow = GameMetrics.Hud.IconSize;

    /// <summary>The readout's spacing unit. Half of what the tracker leaves either side of its icon
    /// column: its gutter is 28 for a 24-wide marker. The layout's own, because the section heights
    /// are made of it and a second copy here would be a second answer.</summary>
    private const float BaseGap = (GameMetrics.Hud.Gutter - GameMetrics.Hud.IconSize) / 2f;

    /// <summary>Where the arrow's centre sits on its line — <see cref="GameMetrics.Type.CapHeightCentre"/>,
    /// shared with every other small mark this readout hangs beside a line of text (the settings
    /// cog, the follow switcher), so all three read as siblings rather than each finding its own
    /// answer. See that constant's own doc comment for why it is not simply half the line box.</summary>
    private const float ArrowOpticalCentre = GameMetrics.Type.CapHeightCentre;

    /// <inheritdoc cref="GameMetrics.Banner.CogSize"/>
    private const float BaseCog = GameMetrics.Banner.CogSize;

    /// <summary>How visible the cog is when the pointer is not on it.
    ///
    /// <para><b>Why it is not simply hidden.</b> Revealing it on hover would be better — the player
    /// asked for exactly that — but the only thing that can tell this readout the pointer is over it
    /// is a collision rectangle, and a collision rectangle over the whole readout would swallow the
    /// world clicks and camera drags underneath it. That is the same trap the drag handle is a
    /// deliberate mode for. So the cog carries its own small collision and nothing else does: it is
    /// barely there until the pointer finds it, and it eats nothing but its own thirteen
    /// pixels.</para></summary>
    private const float CogIdleAlpha = 0.4f;

    /// <summary>What the teleport line's words sit at when the pointer is not on them. Nearer full
    /// than the cog's 0.4 because this is a line of text that has to stay readable at rest — the cog
    /// is a decoration until it is wanted, and the teleport advice is advice whether or not anyone
    /// intends to click it. The lift to 1 is what says the line is pressable, which is the job the
    /// words "(click)" used to do badly.</summary>
    private const float TeleportIdleAlpha = 0.8f;

    /// <summary>Bits of <see cref="ClickTargets"/> — one per clickable node the readout can put on
    /// screen.</summary>
    private const int TeleportTarget = 1;

    /// <inheritdoc cref="TeleportTarget"/>
    private const int CogTarget = 2;

    /// <inheritdoc cref="TeleportTarget"/>
    private const int SwitcherTarget = 4;

    /// <inheritdoc cref="TeleportTarget"/>
    private const int SubjectTarget = 8;

    /// <inheritdoc cref="TeleportTarget"/>
    private const int BannerTarget = 16;

    /// <summary>Where a control's controller anchor sits inside that control's own rectangle: two
    /// units in from its left edge, and half its height down. The same offsets KamiToolKit's own
    /// navigable list rows use (<c>ListItemWithFocusNav</c>), so the game's cursor comes to rest
    /// beside a control here exactly as it does beside a row there.</summary>
    private const float NavAnchorInset = 2f;

    /// <summary>How many controls the readout can offer a controller at once — the cog, the plate,
    /// the switcher's cap and the teleport line.</summary>
    private const int MaxNavTargets = 4;

    /// <summary>Where each control sits in the order a controller walks them, top to bottom as they
    /// are drawn: the cog on the pill, then the plate, then the cap at the plate's right end, then
    /// the teleport line beneath the banner. Down moves along this list and up moves back, both
    /// wrapping, so every control is reachable in at most three presses and no press is a dead
    /// end.</summary>
    private const int NavCog = 0;

    /// <inheritdoc cref="NavCog"/>
    private const int NavBanner = 1;

    /// <inheritdoc cref="NavCog"/>
    private const int NavSwitcher = 2;

    /// <inheritdoc cref="NavCog"/>
    private const int NavTeleport = 3;

    /// <summary>What the pointer is told the plate's own chevron does. The mark is the game's and is
    /// baked into the parchment, so a tooltip is the only thing that can say what WE have made it
    /// mean.</summary>
    private const string SwitcherTooltip = "Choose what to follow";

    /// <summary>What is said when the banner's art cannot be read. Losing it costs the frame and
    /// nothing else — every word the readout was going to say is still said, in the colours it used
    /// before it wore a plate.</summary>
    private const string BannerUnavailable =
        "Wayfarer readout: the game's Main Scenario Guide art (ui/uld/ScenarioTree.tex) could not be read, so the "
        + "readout is drawn without its banner. Everything it says is still on screen, in the plain heads-up "
        + "colours.";

    /// <summary>Padding above and below the teleport line's words, to make its click and cursor
    /// target the height of a row the game would let you press rather than the height of the glyphs
    /// alone.
    ///
    /// <para><b>Why this is a padding and not a fraction of the block any more.</b> It used to be
    /// <c>0.6</c> of the line's block, which was sane only against a block that was eight pixels
    /// taller than its own text. Now that a line's block is its leading
    /// (<see cref="GameMetrics.Banner.AnnotationBlock"/>, 14 at Axis 12), six tenths of it would be
    /// eight pixels — a target shorter than the words inside it, which is poor with a mouse and
    /// worse with a d-pad, where it is also the cursor's anchor.</para>
    ///
    /// <para>Two is what the tracker itself leaves: <c>ToDoList 1008</c>'s collision node
    /// (<c>#9</c>) is h=22 over text (<c>#7</c>) that sits at y=2 in the same 22-tall component, so
    /// the pressable area is the text's own box grown by its inset. Half of that inset on each
    /// side.</para></summary>
    private const float TeleportBoxPadding = GameMetrics.Row.TextTop;

    /// <summary>How far the readout has to change size before the move handle is rebuilt around it.
    /// KamiToolKit sizes the handle once, when move mode is switched on, so a readout that grows a
    /// line would otherwise be dragged by a box that no longer fits it.</summary>
    private const float MoveHandleResizeThreshold = 8f;

    private readonly IPluginLog log;
    private readonly ITextureProvider textures;

    /// <summary>Whether the host already renders this node tree at the player's interface scale, the
    /// way the game renders its own addons — in which case the layout below is in plain ULD units and
    /// must NOT be multiplied by the HUD scale a second time.
    ///
    /// <para><b>This is the fix for "it is still visibly larger than the game's banner".</b> Both
    /// hosts used to force <c>addon-&gt;SetScale(1 / GetGlobalUIScale())</c> and then multiply every
    /// dimension by <c>GetGlobalUIScale()</c> to compensate. That is only self-cancelling if the raw
    /// <c>AtkUnitBase.Scale</c> is a factor applied ON TOP of the HUD scale — and it is not. The
    /// toolkit's own addon-config code settles it: it reads a user-facing scale back as
    /// <c>InternalAddon-&gt;Scale / AtkUnitBase.GetGlobalUIScale()</c>, so a normal addon sitting at
    /// the player's interface size has a raw <c>Scale</c> of exactly <c>GetGlobalUIScale()</c>.
    /// Forcing <c>1/g</c> therefore rendered the readout at <c>1/g</c> where the game's own banner
    /// renders at <c>g</c> — identical only when <c>g</c> is exactly 1, and visibly wrong at every
    /// other interface size. On this player's 5120x1440 display it is not 1.</para>
    ///
    /// <para><b>The addon host now does nothing at all about scale</b>, which is the only
    /// arrangement that is provably right without knowing <c>g</c>: it is an ordinary addon holding
    /// ordinary ULD-unit nodes, exactly like the addon that draws the game's own banner, so the two
    /// cannot disagree. The overlay host is different because the toolkit forces the de-scale on
    /// every overlay addon and will not be talked out of it — see <see cref="GuidanceOverlayNode"/>,
    /// which therefore keeps the old compensation.</para></summary>
    private readonly bool hostIsHudScaled;

    /// <summary>Whether the per-change readout diagnostics should be written. Off by default —
    /// see <see cref="QuestHelperConfig.LogDiagnostics"/> for why.</summary>
    private readonly Func<bool> diagnosticsEnabled;

    /// <summary>The readout, as a stack that grows to fit what is in it.
    ///
    /// <para><b>This is what replaced the readout's y-cursor.</b> Every section below reports its own
    /// height and the container places the next one after it, so nothing on the readout is positioned
    /// from a measurement of anything else. That matters because the measurement in question is a
    /// wrapped string's height, which depends on the loaded font, the column width and where the words
    /// happen to break — so a measurement taken before a node had its real width, or against the
    /// string it drew last frame, used to move every line below it. That was the whole of "text on top
    /// of text". A bad measurement now makes one line the wrong height and moves nothing else.</para>
    ///
    /// <para><b>It does not clip</b>, unlike <see cref="SectionStackNode"/>'s default, for two reasons
    /// that are both about this surface: the game's own "!" medallion is 32 tall in a 26-tall row and
    /// overhangs it on purpose, and a partially clipped node is un-interactable — which on a readout
    /// that is mostly click targets would be a worse failure than a line drawing a few pixels
    /// long.</para></summary>
    private readonly SectionStackNode stack;

    /// <summary>The direction said in words, and the gap under it, as a section of its own.
    ///
    /// <para>Its own section rather than a leading offset, and that is the fix for "everything looks
    /// shifted when the arrow is absent": a section of no height takes no room in the stack, so the
    /// banner beneath it starts in exactly the same place whether these words are there or not. The
    /// old code returned this block's height as the origin of the entire rest of the
    /// readout.</para></summary>
    private readonly ResNode wordsSection;

    /// <summary>The banner — the pill, the plate, the emblem, the name and the three click targets on
    /// it — as one section of fixed height. Fixed because the name is cut short rather than wrapped, so
    /// nothing on the banner can change how tall it is; that is what makes the lines beneath it the
    /// only things on the readout that move.</summary>
    private readonly ResNode bannerSection;

    /// <summary>One section per subordinate line, holding that line's rule, its medallion and its
    /// words. The section's height is the whole of what the line costs — the rule's breathing room
    /// included — so the line after it starts clear of everything in it without anyone adding anything
    /// up.</summary>
    private readonly ResNode[] lineSections = new ResNode[MaxLines];

    /// <summary>The margin under the last line, as a section, so the readout's height stays nothing but
    /// the sum of its sections.</summary>
    private readonly ResNode footSection;

    /// <summary>The name of the tracked thing, written across the plate.
    ///
    /// <para><b>Its own node rather than a slot borrowed from the line pool.</b> It lives in the
    /// banner's section, and a pooled node cannot: the slot that is the name this frame would have to
    /// move between two parents whenever the composer emits its lines in a different order, and
    /// reparenting a node in a per-frame path is exactly the kind of thing that leaves a text node
    /// drawing in two places at once.</para></summary>
    private readonly TextNode headlineNode;

    private readonly TextNode[] lineNodes = new TextNode[MaxLines];
    private readonly HorizontalLineNode[] ruleNodes = new HorizontalLineNode[MaxLines];

    /// <summary>The game's "!" quest medallion, one per line slot — the mark the banner hangs to the
    /// left of a subordinate line that names somewhere the player can go. Pooled alongside the line
    /// nodes and indexed with them, so a line and its own marker cannot get out of step; see
    /// <see cref="ReadoutLine.Marked"/> for which lines get one and why the rest do not.</summary>
    private readonly SimpleImageNode[] markerNodes = new SimpleImageNode[MaxLines];

    /// <summary>The parchment plate — the banner itself, nine-sliced from the game's own 300-wide
    /// part out to the readout's 400. See <see cref="GameMetrics.Banner"/> for the insets and for the
    /// evidence that the art takes them.</summary>
    private readonly SimpleNineGridNode plateNode;

    /// <summary>The dark pill above the plate, where the game prints "Current Main Scenario Quest"
    /// and we print what we are actually tracking.</summary>
    private readonly SimpleNineGridNode stripNode;

    /// <inheritdoc cref="stripNode"/>
    private readonly TextNode stripTextNode;

    /// <summary>Wayfarer's own emblem, in the slot the game hangs the Scions' meteor off. Generated
    /// rather than cropped — see <see cref="WayfarerBitmap"/>, which also says why it is the
    /// installer's mark and not a new one.</summary>
    private readonly ImGuiImageNode crestNode;

    /// <summary>The direction arrow. An <c>ImGuiImageNode</c> rather than a texture-sheet crop
    /// because the arrow is <b>generated</b> — see <see cref="ArrowBitmap"/> for what was wrong with
    /// cropping the minimap's sheet, which is the defect this replaces. The node owns and disposes
    /// the texture wrap it is given.</summary>
    private readonly ImGuiImageNode arrowNode;

    /// <summary>The up/down mark that hangs off the arrow when the target is on a different level of
    /// the world. The game's own minimap does exactly this to a marker on another floor, and copying
    /// the placement convention is the point — a player already reads a badge on a marker without
    /// being told.
    ///
    /// <para><b>It is deliberately not the arrow's art.</b> It used to be: the same generated
    /// arrowhead, half size, turned through half a turn for "below". Direction and elevation are two
    /// different meanings and they were being drawn as the same shape, which the player read as a
    /// second heading to travel in. It is a stacked double chevron now
    /// (<see cref="ChevronBitmap"/>) — line-work where the arrow is a solid mass, so the two
    /// silhouettes have nothing in common at any size or in any colour variant.</para></summary>
    private readonly ImGuiImageNode elevationNode;

    private readonly TextNode arrowWordsNode;
    private readonly string[] lastText = new string[MaxLines];

    /// <summary>Called when the player finishes dragging the readout, with the offset they dragged
    /// it by, in the host's own coordinates. Null in a host that cannot be dragged.</summary>
    private readonly Action<Vector2>? onMoved;

    /// <summary>An invisible click target parked over whichever line is the teleport advice, or
    /// null in a host that cannot be clicked. A <c>ResNode</c> draws nothing of its own, so the
    /// readout looks byte-for-byte the same with or without it — the only difference is a collision
    /// rectangle and the hand cursor over that one line.</summary>
    private readonly ResNode? teleportHitBox;

    /// <summary>The settings cog, or null in a host that takes no input at all. Drawing one on the
    /// fallback overlay would be a lie — an affordance that does nothing is worse than none — but on
    /// the readout's own host it is drawn for everyone, because a pointer and a pad reach it the
    /// same way there.</summary>
    private readonly ImGuiImageNode? cogNode;

    /// <summary>The "choose what to follow" switcher: <b>an invisible click target over the chevron
    /// the plate's own art already carries</b>, at the plate's right end. Null in a host that cannot
    /// be clicked.
    ///
    /// <para><b>Why there is no art of our own here any more.</b> There were two carets on the bar at
    /// once — ours, a tinted crop of <c>DropDownA.tex</c> parked after the name, and the plate's own,
    /// baked into the parchment at source x=279-288 and impossible to remove without slicing through
    /// it (see <see cref="GameMetrics.Banner.PlateInsetX"/>). Two marks for one control is worse than
    /// either alone, and only one of them can be got rid of. So the game's own chevron wins: it is in
    /// the art, in the same place at every width, it already means "there is more behind this" on the
    /// game's own banner, and it cannot be mistinted or misaligned because we do not draw it.</para>
    ///
    /// <para>That also takes with it the one unverified colour on the readout — our caret was
    /// near-white art multiplied into a brown to survive the parchment, which nobody had seen.</para>
    ///
    /// <para><b>What it costs:</b> no hover highlight, because there is no node of ours to light up.
    /// The hand cursor and a tooltip (<see cref="SwitcherTooltip"/>) are what say it is a control —
    /// which is the same pair the readout already relies on for the teleport line.</para>
    ///
    /// <para>A controller reaches this cap the same way it reaches the cog — HUD Select, then along
    /// the ring of anchors — and the same list is still on the window's Following tab and on the
    /// Wayfarer entry in the game's own right-click menu.</para></summary>
    private readonly ResNode? switcherHitBox;

    /// <summary>An invisible box over the words of the subject line, or null in a host that takes no
    /// mouse. It carries the full name as the game's own tooltip when the drawn name has been cut
    /// short, which is the other half of truncating it.
    ///
    /// <para>Its own box rather than the text node's: the text node is as wide as the room the line
    /// was given, including the slot reserved for the switcher, and a hover region that reached
    /// under the switcher would put a tooltip over a control that is not the name. This is exactly
    /// as wide as the words drew.</para>
    ///
    /// <para><b>Its click and the plate's now agree.</b> Since <see cref="bannerHitBox"/> took over
    /// opening the Journal for the whole parchment, this narrower box over the name does the exact
    /// same click when it has one — it is above the plate in hit-test order, so it still catches the
    /// pointer first, but there is no longer a different answer underneath it to disagree with. What
    /// this box is still for on its own is the tooltip: revealing a name the plate cut short.</para>
    /// </summary>
    private readonly ResNode? subjectHitBox;

    /// <summary>The whole plate as a click target for the Journal, at whatever is being followed —
    /// bounded to the parchment itself, not the readout's full width — or null in a host that takes
    /// no mouse.
    ///
    /// <para><b>Why the plate and not only the words of the name.</b> Reported live: the plate used
    /// to open Settings, which was the large, obvious target doing the incidental thing, while the
    /// Journal — the useful thing — was a strip the width of the name's own text. This is the fix:
    /// the parchment is the readout's face and the obvious click on it should be the obvious action,
    /// which is "go look at the thing I am following", not the plugin's own settings.</para>
    ///
    /// <para><b>Settings moved off the plate entirely.</b> It was never anywhere else, but with the
    /// plate now claimed by the Journal it would be a second control fighting the same parchment for
    /// the same click. The cog beside the header pill is the only settings affordance left on the
    /// readout — see <see cref="cogNode"/> — and it was never removed, so there was nothing to
    /// restore.</para>
    ///
    /// <para><b>What it deliberately does not swallow.</b> Sized to stop short of the switcher's cap
    /// on the right — see <see cref="LayoutBannerHitBox"/> — and attached before the switcher and
    /// before the words of the name, so both of those still sit above it in hit-test order: the caret
    /// still drops the follow list, and the words of the name still carry their own, narrower click
    /// (see <see cref="subjectHitBox"/>) for the truncated-name tooltip that lives with them. Hitting
    /// either region opens the same Journal entry, so which one caught the click never matters.</para>
    ///
    /// <para>It is also where the game's cursor lands first when a controller cycles to the readout,
    /// so "HUD Select, then Confirm" opens the Journal at whatever is being followed — the shortest
    /// route the readout has. And it is the one control that answers a second press: the game's own
    /// <b>Display Subcommands</b> drops a menu with everything else Wayfarer can be asked to do —
    /// see <see cref="AddSubcommand"/> and <see cref="ReadoutMenu"/>.</para></summary>
    private readonly ResNode? bannerHitBox;

    /// <summary>Whether this host was given somewhere to send a click on the quest name. False on
    /// the overlay, where nothing is clickable at all.</summary>
    private readonly bool journalClickable;

    /// <summary>The four controls as the game's controller cursor sees them: zero-sized component
    /// nodes parked on the same rectangles the hit boxes above cover, in the order the cursor walks
    /// them. Null in a host that takes no input at all.
    ///
    /// <para><b>Why these exist at all, when the hit boxes are already there.</b> A hit box is a
    /// plain <c>ResNode</c> carrying a collision rectangle, which is everything the mouse needs and
    /// nothing the pad can see: the game's cursor moves between COMPONENTS carrying cursor-navigation
    /// info, which is exactly why KamiToolKit ships <c>NavFocusNode</c> and why its own list rows
    /// grow one when they have no component of their own. These are that node, four times.</para>
    ///
    /// <para><b>They cost nothing on screen.</b> Zero size, so their collision rectangles are zero
    /// too — they draw no pixels, swallow no world click and shift no layout. The visible part of
    /// being focused is the game's own cursor, drawn by the game's own addon.</para>
    ///
    /// <para>Confirm on each does exactly what a click on the control it shadows does. The plate's
    /// alone answers one more press — see <see cref="AddSubcommand"/>.</para></summary>
    private readonly NavFocusNode?[] navTargets = new NavFocusNode?[MaxNavTargets];

    /// <summary>Which inputs the focused plate has already been seen to receive, so the diagnostic
    /// that names them says each one once rather than once a frame — see
    /// <see cref="ReportInput"/>.</summary>
    private readonly HashSet<InputId> seenInputs = [];

    /// <summary>The set of controls the nav chain was last built for, as a
    /// <see cref="ClickTargets"/> bitmask. Starts at -1, which no real set can equal, so the first
    /// frame always builds one.</summary>
    private int lastNavTargets = -1;

    private ArrowIconVariant? loadedVariant;
    private ArrowIconVariant? loadedElevationVariant;
    private bool arrowFailed;
    private bool elevationFailed;
    private bool cogLoaded;
    private bool cogFailed;
    private bool crestLoaded;
    private bool crestFailed;
    private bool bannerFailed;
    private bool warnedBannerOnce;

    /// <summary>The pill's words, as last handed over. Same reasoning as
    /// <see cref="lastSubjectTooltip"/>: assigning <c>String</c> re-runs the engine's text flow and
    /// this is a per-frame path, while the pill's text changes only when the player changes what
    /// they are following.</summary>
    private string lastStripLabel = string.Empty;

    /// <summary>The room the subject line had last frame, and the tooltip it was last given. Both
    /// exist so the per-frame path writes nothing that has not changed: re-handing the engine a
    /// string re-runs its text flow, and re-handing a node a tooltip rebuilds the addon's whole
    /// collision list.</summary>
    private float lastSubjectWidth = -1f;

    /// <inheritdoc cref="lastSubjectWidth"/>
    private string lastSubjectTooltip = string.Empty;

    /// <inheritdoc cref="lastSubjectWidth"/>
    private string lastHeadlineText = string.Empty;

    /// <summary>Which line the teleport box belongs on this frame, or null when no line offered the
    /// click. Written while the lines are laid out and read once they have been placed, which is the
    /// only way a single floating box can follow whichever of twelve lines happens to be the advice.
    /// </summary>
    private LineSlot? teleportSlot;

    private ArrowHiddenReason lastReported = ArrowHiddenReason.None;
    private bool reportedOnce;
    private bool warnedTextureOnce;
    private string? lastBearingWords;
    private bool movable;
    private Vector2 movableSize;

    public ReadoutBodyNode(
        IPluginLog log,
        ITextureProvider textures,
        Func<bool>? diagnosticsEnabled = null,
        Action? onTeleportClicked = null,
        Action<Vector2>? onMoved = null,
        Action? onSettingsClicked = null,
        Action? onFollowClicked = null,
        Action? onQuestNameClicked = null,
        Action? onPlateSubcommand = null,
        bool hostIsHudScaled = false)
    {
        this.log = log;
        this.textures = textures;
        this.diagnosticsEnabled = diagnosticsEnabled ?? (static () => false);
        this.onMoved = onMoved;
        this.hostIsHudScaled = hostIsHudScaled;

        // The stack first, so everything that flows is behind everything that floats over it.
        // Clipping off: see the field's own note — the medallion overhangs its row by design and a
        // partially clipped node cannot be clicked.
        stack = new SectionStackNode { ClipListContents = false, ItemSpacing = 0f };
        stack.AttachNode(this);

        wordsSection = new ResNode { IsVisible = false };
        bannerSection = new ResNode();
        footSection = new ResNode();
        BuildSections();

        // Inside the banner's section, and the order matters here for the same reason it always did: a
        // node attached later is drawn over the ones before it, so the banner's own chrome is laid
        // down before anything that sits on it.
        plateNode = BuildPlate();
        stripNode = BuildStrip();
        stripTextNode = BuildStripText();
        crestNode = BuildCrest();

        // The parchment's own click target: the obvious, large thing on the readout opens the
        // Journal at whatever is being followed — see the field's own note for why settings moved
        // off the plate entirely and lives on the cog alone.
        bannerHitBox = onQuestNameClicked is null ? null : BuildHitBox(onQuestNameClicked, bannerSection);
        headlineNode = BuildHeadline();

        arrowWordsNode = BuildArrowWords();
        BuildLinePool();

        // After the banner's own click target, so the switcher and the cog keep their meanings rather
        // than being swallowed by the plate behind them.
        cogNode = onSettingsClicked is null ? null : BuildCog(onSettingsClicked);

        // Over the chevron the plate's own art carries, at its right end. No art of ours: see the
        // field's own note for why there is now exactly one caret on the bar and it is the game's.
        if (onFollowClicked is not null)
        {
            switcherHitBox = BuildHitBox(onFollowClicked, bannerSection);
            switcherHitBox.TextTooltip = SwitcherTooltip;
        }

        // The floating parts, over the stack. None of them takes vertical room — the arrow sits in a
        // gutter beside a line, the hit boxes sit on top of what they make clickable — so none of them
        // belongs in the flow. They are parked from where the flow put the sections, which is a read
        // of a placed position rather than a measurement of a string.
        arrowNode = BuildArrow(BaseArrow);
        elevationNode = BuildArrow(BaseArrow / 2f);
        teleportHitBox = onTeleportClicked is null ? null : BuildTeleportHitBox(onTeleportClicked);
        subjectHitBox = BuildSubjectHitBox(onFollowClicked, onQuestNameClicked);
        journalClickable = onQuestNameClicked is not null;

        BuildNavAnchors(onSettingsClicked, onQuestNameClicked, onFollowClicked, onTeleportClicked);
        AddSubcommand(navTargets[NavBanner], onPlateSubcommand);
    }

    /// <summary>Where the game should put the controller cursor when this readout is the focused
    /// addon: on the plate, whose press opens the Journal at whatever is being followed. Null in a
    /// host that takes no input, and null before the plate has ever been drawn.
    ///
    /// <para>It is the plate rather than the cog because that is the readout's answer to "what am I
    /// doing" — the same reason the plate rather than the cog is what a mouse click on the parchment
    /// does.</para></summary>
    public NodeBase? ControllerFocusNode => navTargets[NavBanner]?.FocusNode;

    /// <summary>Which clickable targets are on screen right now, as a bit per target — the teleport
    /// advice and the settings cog.
    ///
    /// <para>The host watches this: the game only dispatches mouse events to nodes in its addon's
    /// collision list, and that list has to be rebuilt when the <b>set</b> of live collision nodes
    /// changes. A bool was enough while there was one of them; with two, "something is clickable"
    /// stays true across the teleport line appearing under a cog that was already there, and the
    /// list would never be rebuilt for it — a hit box that is never hit.</para></summary>
    public int ClickTargets { get; private set; }

    /// <summary>Lays the whole readout out for this frame and returns the size it needs, in the
    /// host's own units. The host positions and sizes itself from that; nothing else about the
    /// readout is the host's business.
    ///
    /// <para><b>The shape: the game's own Main Scenario Guide, wearing our content.</b> A dark pill
    /// saying what KIND of thing is being tracked, a parchment plate carrying that thing's real game
    /// name with our emblem pinned to its left end, and subordinate lines beneath it in the exact
    /// arrangement the game hangs job quests in — markers hanging into the gutter, text near-flush
    /// with the name above.
    /// <code>
    ///          +---- CURRENT HUNTING LOG ----+
    ///  +===============================================================+
    ///  | (@)  Highland Goobbue                                    [v]  |
    ///  +===============================================================+
    ///      &gt;  1/3 killed
    ///         56 yalms - above you
    ///    (!)  Unlocks Chocobo Companion (240 yalms)
    /// </code>
    /// Every number in that picture is <see cref="GameMetrics.Banner"/>, read out of
    /// <c>ScenarioTree.uld</c> and off the art itself, and none of it is chosen.</para>
    ///
    /// <para><b>Where the arrow went.</b> Into the marker column — the gutter the banner already
    /// reserves for the medallions that hang left of a subordinate line. That is the same idea the
    /// old free-standing gutter expressed (a mark beside the block, not something the block hangs
    /// off), in the column the game itself puts marks in, and it means the readout has ONE left
    /// gutter rather than one for the arrow and another for the markers. The arrow takes the first
    /// subordinate line, which is the objective — the thing it is pointing at — and that line draws
    /// no medallion of its own while it has the arrow: one mark per line, and the arrow is the
    /// stronger statement.</para></summary>
    public Vector2 Layout(ReadoutFrame frame)
    {
        // See hostIsHudScaled. On a host the game already scales, this is the player's own text-size
        // preference and nothing else, and every number below is a plain ULD unit — the same unit the
        // game's own banner is authored in.
        var hud = hostIsHudScaled ? 1f : AtkUnitBase.GetGlobalUIScale();
        var factor = hud * Math.Clamp(frame.Scale, 0.5f, 3f);
        var width = BaseWidth * factor;
        var arrowSize = BaseArrow * factor * Math.Clamp(frame.ArrowScale, 0.5f, 2f);

        var drawable = frame.ArrowRadians is not null && EnsureArrowTexture(frame.ArrowIcon);

        // Every section is filled in against its OWN top edge — nothing here knows or asks where the
        // section will end up. Then the container places them, in one walk, from the heights they have
        // just reported.
        stack.Width = width;
        stack.Position = Vector2.Zero;
        LayoutWords(frame, drawable, factor, width);
        LayoutBanner(frame, factor, width);
        LayoutCog(factor, width);
        LayoutSwitcher(factor, width);

        var (arrowSlot, subject, subjectContent) = LayoutLines(frame, factor, width, drawable);
        stack.RecalculateLayout();

        // And only now the floating parts, from where the flow actually put the sections. This is a
        // read of a placed position, not a measurement of a string, which is the whole difference.
        LayoutArrow(frame, drawable, arrowSize, factor, ArrowCentre(arrowSlot, factor));
        LayoutElevation(frame, arrowSize);
        SettleTeleportHitBox(LayoutTeleportHitBox());
        LayoutSubjectHitBox(
            subject,
            subjectContent?.Text,
            GameMetrics.Banner.HeadlineLeft * factor,
            journalClickable && subjectContent?.Action == ReadoutLineAction.OpenJournal);

        SettleClickTargets();
        SettleNav();

        // The readout is as tall as the container made itself, which is the sum of its sections. There
        // is no second answer to compare it against and nothing here to get wrong.
        var size = new Vector2(width, stack.Height);
        Size = size;
        ApplyMoveMode(frame.MoveMode, size);
        return size;
    }

    /// <summary>Takes the move handle down, and with it the viewport-level mouse listener behind it.
    ///
    /// <b>Called before the node is disposed.</b> The <c>ViewportEventListener</c> behind the handle
    /// is registered against the game's <i>global</i> viewport rather than against this node, so a
    /// listener that outlives the plugin is the exact shape of the unload crash this plugin has
    /// already shipped once. <c>NodeBase.Dispose()</c> does call <c>DisableEditMode</c> itself, so
    /// this is belt and braces rather than the only thing standing between here and that crash — but
    /// it is belt and braces over a vendored library, on the one failure whose cost is the game, and
    /// it is also what makes the teardown order explicit at the call site instead of implicit in a
    /// submodule.</summary>
    public void StopMoving()
    {
        if (!movable)
        {
            return;
        }

        movable = false;
        OnMoveComplete = null;
        EnableMoving = false;
    }

    /// <summary>Hides every child. Used when there is nothing to say — the readout disappears
    /// rather than drawing a frame around emptiness.</summary>
    public void HideAll()
    {
        arrowNode.IsVisible = false;
        elevationNode.IsVisible = false;
        arrowWordsNode.IsVisible = false;
        plateNode.IsVisible = false;
        stripNode.IsVisible = false;
        stripTextNode.IsVisible = false;
        crestNode.IsVisible = false;

        if (bannerHitBox is not null)
        {
            bannerHitBox.IsVisible = false;
        }

        if (teleportHitBox is not null)
        {
            teleportHitBox.IsVisible = false;
        }

        if (cogNode is not null)
        {
            cogNode.IsVisible = false;
        }

        if (switcherHitBox is not null)
        {
            switcherHitBox.IsVisible = false;
        }

        if (subjectHitBox is not null)
        {
            subjectHitBox.IsVisible = false;
        }

        ClickTargets = 0;
        HideNav();
        headlineNode.IsVisible = false;
        HideLinesFrom(0);

        // The sections as well, and the container told to settle, so a readout that comes back is not
        // briefly the height it was when it went away.
        wordsSection.IsVisible = false;
        wordsSection.Height = 0f;
        bannerSection.IsVisible = false;
        bannerSection.Height = 0f;
        footSection.IsVisible = false;
        footSection.Height = 0f;
        stack.RecalculateLayout();
    }

    /// <summary>What a subordinate line is drawn in. One answer, because the banner has one
    /// subordinate type level — see <see cref="BaseSubLineSize"/>. Emphasis still decides the
    /// COLOUR, which is how a distance and a piece of muted context stay distinguishable without a
    /// third size.</summary>
    private static Vector4 ColorFor(ReadoutEmphasis emphasis) => emphasis switch
    {
        ReadoutEmphasis.Heading => GameColors.Heading,
        ReadoutEmphasis.Primary => GameColors.Body,
        ReadoutEmphasis.Secondary => GameColors.ListText,
        _ => GameColors.Dimmed,
    };

    private static Vector4 OutlineFor(ReadoutEmphasis emphasis) =>
        emphasis == ReadoutEmphasis.Heading ? GameColors.HeadingEdge : GameColors.BodyEdge;

    /// <summary>How many rows this line's text will occupy at the width it has been given.
    ///
    /// <para><c>GetTextDrawSize</c> is the engine's own measurement and answers immediately — it is
    /// not a read of something that has to have been drawn first. Both of its axes are consulted
    /// because a node that is already wrapping reports a wrapped height while one that is not
    /// reports its full unwrapped width, and taking the larger estimate is right in either case. A
    /// measurement that comes back as nothing falls back to one row, which is the old behaviour and
    /// is never worse than it.</para>
    ///
    /// <para><b>Both comparisons here are unit-domain traps, and both of them were sprung.</b> This
    /// is what "the readout is still way too spread out" was, and it was never a metric: a line the
    /// layout says is worth 14 was reporting two rows and taking 28, so every line on the readout
    /// was double-spaced. The two mistakes are called out where they are made below. Neither can be
    /// seen at an interface size of exactly 100% with a face whose rows happen to fit their leading,
    /// which is why the arithmetic in <see cref="ReadoutBodyLayout"/> kept auditing as
    /// correct.</para></summary>
    private static float WrappedLines(TextNode node, float width)
    {
        if (width <= 1f)
        {
            return 1f;
        }

        // Trap one: the measurement's own units. The readout bakes the interface scale into the
        // FontSize, LineSpacing and Size it hands each node and leaves every node's Scale at 1 —
        // see hostIsHudScaled — so `width` and node.LineSpacing below are in the units the node was
        // given. The engine's default is to scale what it hands back, which put the measurement one
        // whole interface scale above the numbers it is divided by; at any size over 100% the
        // ceilings below then rounded every single-row line up to two. MeasuredTextNode.Measure
        // already asks the same question the same way.
        var drawn = node.GetTextDrawSize(considerScale: false);
        if (drawn.X <= 0f && drawn.Y <= 0f)
        {
            return 1f;
        }

        var step = Math.Max(node.LineSpacing, 1f);

        // Trap two: a row is taller than a step. The readout leads its lines two over the face —
        // Axis 12 over 14, the quest tracker's own pairing — while the font's own row carries an
        // ascender and a descender besides, so one drawn row is reliably taller than one leading.
        // That is the tightness that makes this a heads-up element rather than a window, and it
        // means the row count can never be the drawn height over the leading: that reads a single
        // row as two. It is one row, plus however many further steps the drawn height ran on for.
        // Measured off this same node rather than assumed, so it holds at every text size.
        var one = node.GetTextDrawSize(RowProbe, considerScale: false).Y;

        // Rounded rather than ceilinged, which buys half a step of tolerance: a line carrying one of
        // the game's own glyph payloads draws a row slightly taller than a row of letters, and a
        // ceiling would call that a second row. A real second row is a whole step.
        var byHeight = one > 0f
            ? 1f + MathF.Round(Math.Max(drawn.Y - one, 0f) / step)
            : 1f;

        // Only meaningful while the node has not wrapped: once it has, this axis is the widest row
        // rather than the whole string. Kept for the frame a line's words change, where the node
        // still reports the unwrapped width.
        var byWidth = MathF.Ceiling(drawn.X / width);

        // Capped: a line that wants five rows is a content problem, and letting it push the rest of
        // the readout off the bottom of its slot would be trading a clipped line for a lost one.
        return Math.Clamp(Math.Max(byWidth, byHeight), 1f, MaxWrappedLines);
    }

    /// <summary>How far below a line's own top edge a <paramref name="size"/>-square control has to
    /// sit to have its centre land on that line's optical centre —
    /// <see cref="GameMetrics.Type.CapHeightCentre"/> of <paramref name="fontSize"/> down from the
    /// top, then pulled back up by half the control's own size. Shared by the cog and the switcher
    /// so both centre on the text beside them the same way; the arrow uses the same fraction
    /// directly in <see cref="LayoutArrow"/>, where it already has a line centre in hand rather than
    /// a line top.
    ///
    /// <para>Clamped to never go negative: a control taller than the line it sits beside centres as
    /// low as the line's own top rather than climbing above it.</para></summary>
    private static float OpticalCentreOffset(float fontSize, float size) =>
        Math.Max((fontSize * ArrowOpticalCentre) - (size / 2f), 0f);

    /// <summary>An invisible rectangle that turns a region of the readout into a click.
    ///
    /// <para>A <c>ResNode</c> draws nothing of its own, so the readout looks byte-for-byte the same
    /// with or without one — the only difference is a collision rectangle and the hand cursor over
    /// it. <c>MouseClick</c> is also the only event that adds <c>HasCollision</c>, so what swallows a
    /// world click is exactly this rectangle and nothing more.</para></summary>
    private static ResNode BuildHitBox(Action onClicked, NodeBase parent)
    {
        var box = new ResNode { IsVisible = false };
        box.AddEvent(AtkEventType.MouseClick, onClicked);
        box.ShowClickableCursor = true;
        box.AttachNode(parent);
        return box;
    }

    /// <summary>The controller's half of a hit box: a component the game's cursor can come to rest
    /// on, whose Confirm runs the same action the mouse's click does.
    ///
    /// <para><b>Zero size, and one flag off.</b> A component's collision node is built to fill and to
    /// answer the mouse; this one is sized to nothing and has <c>Fill</c> taken away, so whatever
    /// that flag would have made it cover, it covers nothing. That is what keeps a nav anchor from
    /// being a rectangle over the world — the guarantee the readout has always had to hold, and the
    /// reason the drag handle is a mode rather than a permanent listener.</para>
    ///
    /// <para>Null when there is no action to run: a host that was given no callbacks (the fallback
    /// overlay) grows no anchors, exactly as it grows no hit boxes.</para></summary>
    private static NavFocusNode? BuildNavAnchor(Action? onSelected, NodeBase parent)
    {
        if (onSelected is null)
        {
            return null;
        }

        var nav = new NavFocusNode
        {
            OnSelected = onSelected,
            Size = Vector2.Zero,
            IsVisible = false,
        };

        nav.CollisionNode.RemoveNodeFlags(NodeFlags.Fill);
        nav.AttachNode(parent);
        return nav;
    }

    /// <summary>Parks one anchor on one control: visible exactly when that control is, and two units
    /// in from its left edge at half its height, so the game's cursor sits beside the thing it is
    /// about to press rather than in its corner.</summary>
    private static void MirrorNav(NavFocusNode? nav, NodeBase? target)
    {
        if (nav is null || target is null)
        {
            return;
        }

        nav.IsVisible = target.IsVisible;
        if (!target.IsVisible)
        {
            return;
        }

        nav.Position = target.Position + new Vector2(NavAnchorInset, target.Height / 2f);
    }

    /// <summary>The teleport line's target, plus the hover treatment that replaced the words
    /// "(click)".
    ///
    /// <para><b>Two rectangles, deliberately, and they must not be collapsed into one.</b> The hit
    /// box is the full width of the line, because a generous target is right for a pointer and
    /// necessary for a d-pad, which anchors on the same rectangle. The <i>highlight</i> is the line's
    /// own text node — the words and the crystal inside them — and nothing else, so a short place name
    /// does not light a band of empty plate to the right of itself. Lighting the hit box would do
    /// exactly that.</para>
    ///
    /// <para>The treatment is the cog's, so the plate has one vocabulary for "this responds to the
    /// pointer": dimmed at rest, full alpha under it. <c>MouseOver</c> and <c>MouseOut</c> ask only
    /// for <c>EmitsEvents</c> and <c>RespondToMouse</c> — <c>MouseClick</c> is the one that adds
    /// <c>HasCollision</c> — so the hover costs the world no clicks it was not already
    /// costing.</para></summary>
    private ResNode BuildTeleportHitBox(Action onClicked)
    {
        var box = BuildHitBox(onClicked, this);
        box.AddEvent(AtkEventType.MouseOver, () => SetTeleportHighlight(hovered: true));
        box.AddEvent(AtkEventType.MouseOut, () => SetTeleportHighlight(hovered: false));
        return box;
    }

    /// <summary>Lights the teleport line's words, or puts them back. Reads the slot rather than
    /// remembering a node: which of the twelve line nodes is the teleport advice changes as the
    /// composer's output changes, and a remembered node would be last frame's line.</summary>
    private void SetTeleportHighlight(bool hovered)
    {
        if (teleportSlot is { } slot)
        {
            lineNodes[slot.Index].Alpha = hovered ? 1f : TeleportIdleAlpha;
        }
    }

    /// <summary>Gives one anchor a second press: the game's own <b>Display Subcommands</b>, which is
    /// what every focused thing in this game answers when the player wants the list of everything
    /// that can be done to it rather than the obvious one thing.
    ///
    /// <para><b>Why this is the right press, and not a press we chose.</b> It is the game's own, by
    /// name: row 215 of the <c>ConfigKey</c> sheet is "Display Subcommands", keyed <c>MENU</c>, and it
    /// sits in the same UI-navigation category as "Confirm" (row 211, <c>OK</c>), "Cancel" (212,
    /// <c>CANCEL</c>) and "Cycle through HUD Components" (227, <c>PAD_HUDFOCUS</c>) — the presses this
    /// readout already answers. It is therefore bound on the player's own pad already — Square on a
    /// PlayStation pad, X on an Xbox one, which is what the game's own controls guide means by "use A
    /// to confirm, B to cancel, and X to open submenus" — and remapping it in the game's own keybinds
    /// remaps this, because what is matched here is the input's id and not a button.
    ///
    /// <para>Registered as a second handler for the event the anchor already listens to, so Confirm
    /// keeps doing exactly what a mouse click on the plate does — open the Journal — and this is
    /// strictly an addition beside it.</para>
    ///
    /// <para>What no source could settle is whether the game routes this id to a focused node
    /// belonging to a plugin's own addon, the way it routes Confirm there. If it does not, this press
    /// does nothing and everything else on the readout is unaffected — so the diagnostic below names
    /// every id that does arrive, which turns one press in game into the answer.</para></summary>
    private void AddSubcommand(NavFocusNode? nav, Action? onSubcommand)
    {
        if (nav is null || onSubcommand is null)
        {
            return;
        }

        nav.AddEvent(AtkEventType.InputReceived, (_, eventType, _, _, data) =>
        {
            if (eventType is not AtkEventType.InputReceived || data is null)
            {
                return;
            }

            var input = (InputId)data->InputData.InputId;
            if (data->InputData.State is InputState.Down)
            {
                ReportInput(input);
            }

            if (input is InputId.MENU && data->InputData.State is InputState.Down)
            {
                onSubcommand();
            }
        });
    }

    /// <summary>Names each input the focused plate is sent, once per input, and only with diagnostics
    /// on. It exists for one question that reading the game's code cannot answer — which presses reach
    /// a plugin addon's own focused node — and it answers it from inside the game instead.</summary>
    private void ReportInput(InputId input)
    {
        if (!diagnosticsEnabled() || !seenInputs.Add(input))
        {
            return;
        }

        log.Debug($"Wayfarer readout: the focused readout was sent input {input} ({(int)input}).");
    }

    /// <summary>Records which of the readout's click targets are live this frame. The cog, the
    /// switcher, the name and the plate are all collision nodes whenever they are drawn, and the
    /// host watches this to know when the addon's collision list has to be rebuilt.</summary>
    private void SettleClickTargets()
    {
        if (cogNode is { IsVisible: true })
        {
            ClickTargets |= CogTarget;
        }

        if (switcherHitBox is { IsVisible: true })
        {
            ClickTargets |= SwitcherTarget;
        }

        if (subjectHitBox is { IsVisible: true })
        {
            ClickTargets |= SubjectTarget;
        }

        if (bannerHitBox is { IsVisible: true })
        {
            ClickTargets |= BannerTarget;
        }
    }

    /// <summary>Grows one controller anchor per control, each on the same parent as the rectangle it
    /// shadows, because an anchor is positioned from that rectangle's own coordinates. Nothing here
    /// draws, and a host that was given no callbacks grows nothing at all.</summary>
    private void BuildNavAnchors(
        Action? onSettingsClicked, Action? onQuestNameClicked, Action? onFollowClicked, Action? onTeleportClicked)
    {
        navTargets[NavCog] = BuildNavAnchor(onSettingsClicked, bannerSection);
        navTargets[NavBanner] = BuildNavAnchor(onQuestNameClicked, bannerSection);
        navTargets[NavSwitcher] = BuildNavAnchor(onFollowClicked, bannerSection);
        navTargets[NavTeleport] = BuildNavAnchor(onTeleportClicked, this);
    }

    /// <summary>Takes every anchor down with the controls they shadow: a cursor resting on a control
    /// that is no longer drawn would be a press with nothing behind it. The ring is marked stale
    /// rather than rebuilt, so it is rebuilt once when the readout comes back rather than on every
    /// frame it is away.</summary>
    private void HideNav()
    {
        foreach (var nav in navTargets)
        {
            if (nav is not null)
            {
                nav.IsVisible = false;
            }
        }

        lastNavTargets = -1;
    }

    /// <summary>Puts each controller anchor where its control ended up this frame, and rebuilds the
    /// order the cursor walks them in when the set of live controls has changed.
    ///
    /// <para>Run after everything has been placed, for the same reason the hit boxes are: an anchor
    /// is positioned from a rectangle that has already been decided, not from a measurement of
    /// its own.</para>
    ///
    /// <para>The chain is rebuilt only when the set changes — <see cref="ClickTargets"/> is the same
    /// signal the host uses to rebuild the game's collision list — because writing five cursor
    /// indices per frame to say the same thing is the kind of per-frame write this readout is
    /// careful not to do.</para></summary>
    private void SettleNav()
    {
        MirrorNav(navTargets[NavCog], cogNode);
        MirrorNav(navTargets[NavBanner], bannerHitBox);
        MirrorNav(navTargets[NavSwitcher], switcherHitBox);
        MirrorNav(navTargets[NavTeleport], teleportHitBox);

        if (ClickTargets == lastNavTargets)
        {
            return;
        }

        lastNavTargets = ClickTargets;
        LinkNav();
    }

    /// <summary>Numbers the live anchors top to bottom and joins them into a ring: down goes to the
    /// next one, up to the previous, and both wrap. Left and right stay on the anchor they are on —
    /// the readout's controls are a column, and a horizontal press that moved the cursor sideways
    /// off the plate would be a press with nowhere to arrive.
    ///
    /// <para>Indices start at one because zero is the game's "no such target". A control that is not
    /// on screen this frame is left out of the ring entirely rather than left in it pointing at
    /// nothing, which is what makes "press down four times and you are back where you started" true
    /// whether or not there is a teleport line.</para></summary>
    private void LinkNav()
    {
        Span<int> live = stackalloc int[MaxNavTargets];
        var count = 0;

        for (var i = 0; i < MaxNavTargets; i++)
        {
            if (navTargets[i] is { IsVisible: true })
            {
                live[count++] = i;
            }
        }

        for (var i = 0; i < count; i++)
        {
            if (navTargets[live[i]] is not { } nav)
            {
                continue;
            }

            var index = i + 1;
            nav.NavIndex = index;
            nav.NavUp = i == 0 ? count : i;
            nav.NavDown = i == count - 1 ? 1 : index + 1;
            nav.NavLeft = index;
            nav.NavRight = index;
        }
    }

    /// <summary>An image node that holds one of the generated arrow textures — the bearing arrow, or
    /// the smaller up/down chevron beside it.
    ///
    /// <para>The game's own direction indicator is a plain image node whose rotation is written every
    /// frame (<c>AtkImageNode PlayerCone / PlayerConeRotation</c> on the minimap), so this copies the
    /// mechanism rather than inventing one. The origin has to be the arrow's centre or it pivots
    /// around its corner.</para>
    ///
    /// <para><c>FitTexture</c> — AutoFit plus Stretch — means "fit the whole loaded TEXTURE into this
    /// node". That is now exactly right, and it is worth saying why it was catastrophic before: with
    /// a 448x212 texture sheet loaded and a 24x24 part selected, the part was ignored and the whole
    /// sheet was drawn squashed into 34 pixels. The arrow's texture is generated and contains nothing
    /// but the arrow, so there is no part to ignore.</para></summary>
    private ImGuiImageNode BuildArrow(float side)
    {
        var node = new ImGuiImageNode
        {
            TextureSize = new Vector2(ArrowBitmap.Size, ArrowBitmap.Size),
            Size = new Vector2(side, side),
            OriginX = side / 2f,
            OriginY = side / 2f,
            FitTexture = true,
            IsVisible = false,
        };
        node.AttachNode(this);
        return node;
    }

    /// <summary>One piece of the banner's chrome: a nine-grid over a rectangle of the game's own
    /// <c>ScenarioTree</c> sheet, cut so the middle band stretches and the caps do not.
    ///
    /// <para><b>Nine-grid where the game uses a plain image.</b> The game draws the plate at exactly
    /// the part's 300 and ships no wider variant, because its own banner is 340 wide. Ours is 400.
    /// The art takes the slice — that is not an assumption, it was rendered at 400, 420 and 600 and
    /// looked at — and the insets in <see cref="GameMetrics.Banner"/> say why they are 24 rather than
    /// something smaller.</para>
    ///
    /// <para>The texture is read out of the game's resource system rather than generated, so like the
    /// switcher's it can be merely <i>late</i> as well as missing. Loading throws only for a genuinely
    /// bad path; a sheet that is not resident yet simply reports no size for a frame or two, which
    /// <see cref="BannerDrawable"/> is what asks about.</para></summary>
    private SimpleNineGridNode BuildBannerPart(float u, float v, float partWidth, float partHeight, float insetX)
    {
        var node = new SimpleNineGridNode { IsVisible = false };

        try
        {
            node.TexturePath = BannerTexture;
            node.TextureCoordinates = new Vector2(u, v);
            node.TextureSize = new Vector2(partWidth, partHeight);

            // Vector4 order is (Top, Bottom, Left, Right). Top and bottom are zero on purpose: the
            // plate has no stretchable band vertically — the longest run of identical rows in the art
            // is two pixels — so it is drawn at its native height and only ever widened.
            node.Offsets = new Vector4(0f, 0f, insetX, insetX);
        }
        catch (Exception ex)
        {
            bannerFailed = true;
            log.Error(ex, BannerUnavailable);
        }

        node.AttachNode(bannerSection);
        return node;
    }

    /// <summary>The direction in words, for when the arrow cannot be drawn at all. A readout that
    /// says "behind you, to the left" is still guidance; an arrow that silently fails is not.
    /// </summary>
    private TextNode BuildArrowWords()
    {
        var words = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = (uint)BaseSubLineSize,
            AlignmentType = AlignmentType.Center,
            TextFlags = TextFlags.Edge,
            TextColor = GameColors.Heading,
            TextOutlineColor = GameColors.HeadingEdge,
            IsVisible = false,
        };
        words.AttachNode(wordsSection);
        return words;
    }

    /// <summary>The header pill's words. Embossed and nothing else — no edge, because it sits on a
    /// dark pill rather than over the world, which is exactly what <c>ScenarioTree.uld</c> node
    /// <c>#11</c> does.</summary>
    private TextNode BuildStripText()
    {
        var text = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = (uint)BaseSubLineSize,
            AlignmentType = AlignmentType.Center,
            TextFlags = TextFlags.Emboss,
            TextColor = GameColors.Heading,
            TextOutlineColor = GameColors.HeadingEdge,
            IsVisible = false,
        };
        text.AttachNode(bannerSection);
        return text;
    }

    /// <summary>The box over the words of the name, in the banner's own section and last of its
    /// children, so the words of the name sit above the plate's own click target in hit-test order.
    /// Null in a host that takes no mouse.</summary>
    private ResNode? BuildSubjectHitBox(Action? onFollowClicked, Action? onQuestNameClicked)
    {
        if (onFollowClicked is null && onQuestNameClicked is null)
        {
            return null;
        }

        var box = new ResNode { IsVisible = false };

        // Registered once, offered per frame. The cursor is what says whether the click is on offer
        // this frame — see LayoutSubjectHitBox — because the same box is also what a truncated name is
        // hovered over, and a hunt has a name to reveal but no journal entry.
        if (onQuestNameClicked is not null)
        {
            box.AddEvent(AtkEventType.MouseClick, onQuestNameClicked);
        }

        box.AttachNode(bannerSection);
        return box;
    }

    /// <summary>The parchment plate, nine-sliced from the game's own 300-wide part.</summary>
    private SimpleNineGridNode BuildPlate() => BuildBannerPart(
        GameMetrics.Banner.PlateU,
        GameMetrics.Banner.PlateV,
        GameMetrics.Banner.PlateWidth,
        GameMetrics.Banner.PlateHeight,
        GameMetrics.Banner.PlateInsetX);

    /// <summary>The dark pill above the plate.</summary>
    private SimpleNineGridNode BuildStrip() => BuildBannerPart(
        GameMetrics.Banner.StripU,
        GameMetrics.Banner.StripV,
        GameMetrics.Banner.StripPartWidth,
        GameMetrics.Banner.StripHeight,
        GameMetrics.Banner.StripInsetX);

    /// <summary>Puts the readout's sections into the stack, in reading order — which is now what "in
    /// order" means for this surface: the container places them in the order they were added, and
    /// nothing else decides where any of them goes.</summary>
    private void BuildSections()
    {
        for (var i = 0; i < MaxLines; i++)
        {
            lineSections[i] = new ResNode { IsVisible = false };
        }

        stack.AddNode(wordsSection);
        stack.AddNode(bannerSection);
        foreach (var section in lineSections)
        {
            stack.AddNode(section);
        }

        stack.AddNode(footSection);
    }

    /// <summary>The name of the tracked thing, on the plate. Cut short with the engine's own ellipsis
    /// rather than wrapped — see <see cref="SubjectFlags"/> — which is what keeps the banner's height
    /// independent of what is written on it.</summary>
    private TextNode BuildHeadline()
    {
        var node = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = (uint)BaseHeadlineSize,
            AlignmentType = AlignmentType.TopLeft,
            TextFlags = SubjectFlags,
            TextColor = GameColors.Body,
            TextOutlineColor = GameColors.BodyEdge,
            IsVisible = false,
        };
        node.AttachNode(bannerSection);
        return node;
    }

    /// <summary>The emblem in the crest slot. Same generated-texture treatment as the arrow and the
    /// cog, and <c>FitTexture</c> is correct here for the same reason: the whole texture IS the mark,
    /// so there is no part for AutoFit to ignore.</summary>
    private ImGuiImageNode BuildCrest()
    {
        var crest = new ImGuiImageNode
        {
            TextureSize = new Vector2(WayfarerBitmap.Size, WayfarerBitmap.Size),
            Size = new Vector2(GameMetrics.Banner.CrestSize, GameMetrics.Banner.CrestSize),
            FitTexture = true,
            IsVisible = false,
        };
        crest.AttachNode(bannerSection);
        return crest;
    }

    /// <summary>The settings cog, wired to open the window on its Settings tab.
    ///
    /// <para><c>MouseClick</c> is the only one of these events that blocks: <c>MouseOver</c> and
    /// <c>MouseOut</c> ask the toolkit for <c>EmitsEvents</c> and <c>RespondToMouse</c> alone, while
    /// <c>MouseClick</c> adds <c>HasCollision</c>. The rectangle that swallows a world click is
    /// therefore exactly the cog and nothing more — which is the whole reason the readout can carry
    /// one at all.</para></summary>
    private ImGuiImageNode BuildCog(Action onSettingsClicked)
    {
        // Same generated-texture treatment as the arrow — see CogBitmap. FitTexture is correct here
        // for the same reason it is correct there: the whole texture IS the icon, so there is no
        // part for AutoFit to ignore.
        var cog = new ImGuiImageNode
        {
            TextureSize = new Vector2(CogBitmap.Size, CogBitmap.Size),
            Size = new Vector2(BaseCog, BaseCog),
            FitTexture = true,
            IsVisible = false,
            Alpha = CogIdleAlpha,
        };

        cog.AddEvent(AtkEventType.MouseClick, onSettingsClicked);
        cog.AddEvent(AtkEventType.MouseOver, () => cog.Alpha = 1f);
        cog.AddEvent(AtkEventType.MouseOut, () => cog.Alpha = CogIdleAlpha);
        cog.ShowClickableCursor = true;
        cog.AttachNode(bannerSection);
        return cog;
    }

    private void BuildLinePool()
    {
        for (var i = 0; i < MaxLines; i++)
        {
            lastText[i] = string.Empty;

            ruleNodes[i] = new HorizontalLineNode { IsVisible = false };
            ruleNodes[i].AttachNode(lineSections[i]);

            markerNodes[i] = BuildMarker(i);

            lineNodes[i] = new TextNode
            {
                FontType = FontType.Axis,
                FontSize = (uint)BaseSubLineSize,
                AlignmentType = AlignmentType.TopLeft,

                // A starting point only — every line is given its own flags each frame, because the
                // pool is shared and the subject line behaves differently. See BodyFlags.
                TextFlags = BodyFlags,
                TextColor = GameColors.Body,
                TextOutlineColor = GameColors.BodyEdge,
                IsVisible = false,
            };
            lineNodes[i].AttachNode(lineSections[i]);
        }
    }

    /// <summary>One "!" quest medallion — the game's own part, drawn 1:1 at its native 32, which is
    /// what <c>ScenarioTree.uld</c> does with it. No nine-grid and no stretch: it is a glyph, not a
    /// frame.</summary>
    private SimpleImageNode BuildMarker(int index)
    {
        var marker = new SimpleImageNode
        {
            Size = new Vector2(GameMetrics.Banner.MarkerSize, GameMetrics.Banner.MarkerSize),
            WrapMode = WrapMode.Stretch,
            IsVisible = false,
        };

        try
        {
            marker.LoadTexture(BannerTexture);
            marker.TextureCoordinates = new Vector2(GameMetrics.Banner.MarkerU, GameMetrics.Banner.MarkerV);
            marker.TextureSize = new Vector2(GameMetrics.Banner.MarkerSize, GameMetrics.Banner.MarkerSize);
        }
        catch (Exception ex)
        {
            bannerFailed = true;
            log.Error(ex, BannerUnavailable);
        }

        marker.AttachNode(lineSections[index]);
        return marker;
    }

    /// <summary>Turns the game's own HUD-Layout move handle on and off around the readout.
    ///
    /// <para>KamiToolKit's <c>EnableMoving</c> builds the handle once, at the size the node had at
    /// that moment, and registers a viewport-level mouse listener that marks clicks inside it as
    /// handled. Both facts shape what happens here: the handle has to be rebuilt when the readout
    /// changes size, and the whole thing has to be off unless the player asked for it, or the
    /// readout would silently eat world clicks and camera drags underneath itself.</para></summary>
    private void ApplyMoveMode(bool wanted, Vector2 size)
    {
        if (onMoved is null)
        {
            return;
        }

        if (!wanted)
        {
            StopMoving();
            return;
        }

        var resize = movable && Vector2.Distance(size, movableSize) > MoveHandleResizeThreshold;
        if (movable && !resize)
        {
            return;
        }

        if (resize)
        {
            EnableMoving = false;
        }

        movable = true;
        movableSize = size;
        OnMoveComplete = _ =>
        {
            onMoved(Position);
            Position = Vector2.Zero;
        };
        EnableMoving = true;
    }

    /// <summary>Puts the arrow in the marker column — the gutter the banner reserves for the
    /// medallions that hang to the left of a subordinate line — level with the middle of the first
    /// subordinate line, which is the objective it is pointing at. It takes no vertical space of its
    /// own, so this runs after the lines have been placed and simply parks it beside them.
    ///
    /// <para>Centred in the medallion's own 32-wide column rather than given a column of its own, so
    /// the arrow and the markers below it share one left edge whatever the player's arrow-size
    /// setting is.</para></summary>
    private void LayoutArrow(ReadoutFrame frame, bool drawable, float size, float factor, float lineCentre)
    {
        if (frame.ArrowRadians is not { } radians)
        {
            arrowNode.IsVisible = false;
            ReportArrow(frame.ArrowHidden);
            return;
        }

        if (!drawable)
        {
            arrowNode.IsVisible = false;
            ReportArrow(ArrowHiddenReason.TextureUnavailable);
            return;
        }

        // The arrow-size setting has to be applied here as well as the text-size one: before this
        // it moved nothing at all on the readout that is actually on screen, because only the ImGui
        // fallback ever read it.
        // Centred in the medallion's own column, but never allowed to reach the words — see
        // ReadoutBodyLayout's own note. Identical to plain centring at the arrow's authored size and at
        // every size below it; above that, the arrow grows leftward into the empty margin rather than
        // over the sentence it is pointing for.
        var centred = ReadoutBodyLayout.GutterLeft(factor)
            + ((ReadoutBodyLayout.GutterWidth(factor) - size) / 2f);
        var clear = ReadoutBodyLayout.SubLineLeft(factor) - size;

        arrowNode.Size = new Vector2(size, size);
        arrowNode.OriginX = size / 2f;
        arrowNode.OriginY = size / 2f;
        arrowNode.Position = new Vector2(
            Math.Max(Math.Min(centred, clear), 0f), lineCentre - (size / 2f));
        arrowNode.Rotation = radians;
        arrowNode.IsVisible = true;
        ReportArrow(ArrowHiddenReason.None);
        ReportBearing(radians);
    }

    /// <summary>Draws the banner: the plate at the readout's full width, the header pill over its top
    /// edge, the pill's words, the emblem pinned to the plate's left end, and the parchment itself as
    /// a click target for the Journal.
    ///
    /// <para>Every position here is <see cref="GameMetrics.Banner"/> multiplied by the frame's scale
    /// factor and nothing else — the banner's arrangement does not depend on its content, which is
    /// what makes the name and the lines beneath it the only things on the readout that move.</para>
    ///
    /// <para>When the art cannot be read the chrome is simply not drawn and the layout is untouched:
    /// the words all land exactly where they would have, and <see cref="HeadlineColor"/> switches
    /// them back to the plain heads-up colours so a dark name is never left on nothing.</para></summary>
    private void LayoutBanner(ReadoutFrame frame, float factor, float width)
    {
        var drawable = BannerDrawable();

        // The section first, and its height is a constant times the scale: nothing on the banner can
        // change it, because the name is cut short rather than wrapped.
        bannerSection.Size = new Vector2(width, ReadoutBodyLayout.BannerHeight(factor));
        bannerSection.IsVisible = true;

        // Everything below is placed against the section's own top edge, which is what makes it
        // independent of whether the words fallback above it is on screen.
        const float top = 0f;

        // The plate starts past the margin its emblem hangs into, which is the game's own
        // construction — see GameMetrics.Banner.PlateLeft. At the readout's width this comes out at
        // the part's native 300, so the nine-slice is the identity and nothing is stretched.
        var plateLeft = GameMetrics.Banner.PlateLeft * factor;
        plateNode.Size = new Vector2(
            Math.Max(width - plateLeft, factor), GameMetrics.Banner.PlateHeight * factor);
        plateNode.Position = new Vector2(plateLeft, top + (GameMetrics.Banner.PlateTop * factor));
        plateNode.IsVisible = drawable;

        var stripWidth = Math.Min(GameMetrics.Banner.StripWidth * factor, width);
        var stripLeft = (width - stripWidth) / 2f;
        var stripBox = new Vector2(stripWidth, GameMetrics.Banner.StripHeight * factor);
        stripNode.Size = stripBox;
        stripNode.Position = new Vector2(stripLeft, top + (GameMetrics.Banner.StripTop * factor));
        stripNode.IsVisible = drawable;

        // The pill's words sit on the pill whether or not the pill's art turned up — losing the
        // backing is not a reason to stop saying what is being tracked.
        var stripSize = Math.Max(BaseSubLineSize * factor, 8f);
        stripTextNode.FontSize = (uint)stripSize;
        stripTextNode.LineSpacing =
            (uint)Math.Max(stripSize + (GameMetrics.Banner.SubLineLeading - GameMetrics.Banner.SubLineSize), 10f);
        stripTextNode.Size = stripBox;
        stripTextNode.Position = stripNode.Position;
        SetStripLabel(frame.Content.StripLabel);
        stripTextNode.IsVisible = true;

        LayoutCrest(factor, top);
        LayoutBannerHitBox(factor, width, top);
    }

    /// <summary>Pins the emblem to the plate's left end. It rises above the plate on purpose, which is
    /// the slot the game hangs the Scions' meteor in and one of the two reasons the stack does not
    /// clip its contents.</summary>
    private void LayoutCrest(float factor, float top)
    {
        if (!EnsureCrestTexture())
        {
            crestNode.IsVisible = false;
            return;
        }

        var crest = GameMetrics.Banner.CrestSize * factor;
        crestNode.Size = new Vector2(crest, crest);
        crestNode.Position = new Vector2(
            GameMetrics.Banner.CrestLeft * factor,
            top + ((GameMetrics.Banner.PlateTop - GameMetrics.Banner.CrestRise) * factor));
        crestNode.IsVisible = true;
    }

    /// <summary>The parchment itself as a click target for the Journal — the plate's own bounds and
    /// nothing either side of it, so the target reads as "this piece of parchment", not "anywhere
    /// near the banner".
    ///
    /// <para><b>Bounded to the plate, not the readout's full width.</b> It used to reach from the
    /// readout's left edge — the plate AND the emblem's margin beside it — which was fine while it
    /// only ever competed with the switcher's cap. Matching <see cref="plateNode"/>'s own left edge
    /// and trimming the same cap width off the right, the way <see cref="LayoutSwitcher"/> sizes its
    /// own box, is what makes the two provably disjoint rather than merely stacked in hit-test
    /// order.</para></summary>
    private void LayoutBannerHitBox(float factor, float width, float top)
    {
        if (bannerHitBox is null)
        {
            return;
        }

        var plateLeft = GameMetrics.Banner.PlateLeft * factor;
        var cap = GameMetrics.Banner.PlateInsetX * factor;
        var plateWidth = Math.Max(width - plateLeft, factor);

        bannerHitBox.Size = new Vector2(Math.Max(plateWidth - cap, factor), GameMetrics.Banner.PlateHeight * factor);
        bannerHitBox.Position = new Vector2(plateLeft, top + (GameMetrics.Banner.PlateTop * factor));
        bannerHitBox.IsVisible = true;
    }

    /// <summary>Parks the settings cog at the right-hand end of the header pill — the same
    /// relationship it had to the heading the pill replaced, and the one place on the banner that is
    /// never text.
    ///
    /// <para>Pinned to the pill rather than measured off its words, because the pill is a fixed 230
    /// wide whatever it says: there is no "end of the heading" left to measure to.</para></summary>
    private void LayoutCog(float factor, float width)
    {
        if (cogNode is null)
        {
            return;
        }

        if (!EnsureCogTexture())
        {
            cogNode.IsVisible = false;
            return;
        }

        // On the banner's own section, so it stays on the pill whatever is above or below the banner.
        const float top = 0f;
        var size = Math.Max(BaseCog * factor, 9f);
        var gap = BaseGap * factor * 2f;
        var stripWidth = Math.Min(GameMetrics.Banner.StripWidth * factor, width);
        var stripHeight = GameMetrics.Banner.StripHeight * factor;
        var x = ((width + stripWidth) / 2f) + gap;

        cogNode.Size = new Vector2(size, size);

        // Origin follows the size, not only for the rotated controls: a node whose origin is stale
        // pivots around a point that is no longer its centre.
        cogNode.OriginX = size / 2f;
        cogNode.OriginY = size / 2f;
        cogNode.Position = new Vector2(
            Math.Clamp(x, 0f, Math.Max(width - size, 0f)),
            top + (GameMetrics.Banner.StripTop * factor) + ((stripHeight - size) / 2f));
        cogNode.IsVisible = true;
    }

    /// <summary>The words fallback, on its own full-width line above the block. Only ever on screen
    /// when the arrow could not be generated at all — which, since the arrow is now computed rather
    /// than loaded, means something has gone genuinely wrong rather than merely slowly. It keeps its
    /// old full-width line rather than trying to fit "behind you, to the left" into a 25-pixel
    /// gutter.</summary>
    private void LayoutWords(ReadoutFrame frame, bool drawable, float factor, float width)
    {
        if (drawable || frame.ArrowRadians is not { } radians)
        {
            // Both the words and their section, because a section of no height is what takes the
            // block out of the stack entirely rather than leaving a hole where it used to be.
            arrowWordsNode.IsVisible = false;
            wordsSection.IsVisible = false;
            wordsSection.Height = 0f;
            return;
        }

        // The tracker's own leading, two over the font size.
        const float Leading = GameMetrics.Hud.LineLeading - GameMetrics.Hud.LineSize;
        var height = ReadoutBodyLayout.WordsLineHeight(factor);
        arrowWordsNode.FontSize = (uint)Math.Max(BaseHeadlineSize * factor, 8f);
        arrowWordsNode.LineSpacing = (uint)Math.Max((BaseHeadlineSize * factor) + Leading, 10f);
        arrowWordsNode.String = NavMath.DescribeDirection(radians);
        arrowWordsNode.Size = new Vector2(width, height);
        arrowWordsNode.Position = Vector2.Zero;
        arrowWordsNode.IsVisible = true;

        wordsSection.Size = new Vector2(width, ReadoutBodyLayout.WordsHeight(present: true, factor));
        wordsSection.IsVisible = true;
    }

    /// <summary>Hands the header pill its words, but only when they have actually changed. Assigning
    /// <c>String</c> builds a SeString and re-runs the engine's text flow, and this is a per-frame
    /// path; what is being tracked changes when the player changes it and not otherwise.</summary>
    private void SetStripLabel(string label)
    {
        if (string.Equals(lastStripLabel, label, StringComparison.Ordinal))
        {
            return;
        }

        lastStripLabel = label;
        stripTextNode.String = label;
    }

    /// <summary>What the name on the plate is written in. Dark on the parchment when the plate is
    /// actually there, and the readout's ordinary heads-up white when it is not — a dark name on
    /// nothing would be a readout that had silently gone blank rather than one that had lost its
    /// frame.</summary>
    private Vector4 HeadlineColor() => BannerDrawable() ? GameColors.BannerHeadline : GameColors.Body;

    /// <inheritdoc cref="HeadlineColor"/>
    private Vector4 HeadlineEdgeColor() =>
        BannerDrawable() ? GameColors.BannerHeadlineEdge : GameColors.BodyEdge;

    /// <summary>Whether the banner's art is on hand to draw. Like the switcher's and unlike the
    /// generated glyphs, this sheet is read out of the game's own resource system, so it can be
    /// merely <i>late</i> rather than broken — the resource system answers with nothing until the
    /// sheet is resident. Asking every frame is therefore right, and costs a pointer read.</summary>
    private bool BannerDrawable()
    {
        if (bannerFailed)
        {
            return false;
        }

        if (plateNode.PartsList[0]->LoadedTextureSize != Vector2.Zero)
        {
            return true;
        }

        // Said once, and only to somebody who asked for diagnostics — the same policy this file
        // already applies to the arrow and the switcher. A sheet that is merely not resident yet
        // becomes resident a frame or two later, and a warning that retracts itself is worse in a
        // log than no warning.
        if (!warnedBannerOnce && diagnosticsEnabled())
        {
            warnedBannerOnce = true;
            log.Debug(BannerUnavailable);
        }

        return false;
    }

    /// <summary>Generates the emblem once. Same contract as the cog's texture: the pixels are
    /// computed here and uploaded synchronously, so it either works or it throws, and if it throws
    /// there is simply no crest — the plate and everything on it are unaffected.</summary>
    private bool EnsureCrestTexture()
    {
        if (crestFailed)
        {
            return false;
        }

        if (crestLoaded)
        {
            return true;
        }

        try
        {
            var wrap = textures.CreateFromRaw(
                RawImageSpecification.Rgba32(WayfarerBitmap.Size, WayfarerBitmap.Size),
                WayfarerBitmap.Render(),
                "Wayfarer emblem");

            // Takes ownership: the node disposes the wrap with itself.
            crestNode.LoadTexture(wrap);
            crestNode.TextureSize = new Vector2(WayfarerBitmap.Size, WayfarerBitmap.Size);
            crestLoaded = true;
            return true;
        }
        catch (Exception ex)
        {
            crestFailed = true;
            log.Error(ex, "Wayfarer readout: the plugin's emblem could not be generated, so the readout's banner has an empty crest slot. Nothing else about the readout changes.");
            return false;
        }
    }

    /// <summary>Fills in every line's own section, and says which one the arrow belongs beside.
    ///
    /// <para><b>Three fates, decided by what the composer marked the line as, never by where it
    /// happens to be in the list.</b>
    /// <list type="bullet">
    /// <item>the HEADING is not drawn as a line at all — its words are the header pill's, and the
    /// pill is what it became. Nothing else consumes it, so the window's own quest header and the
    /// ImGui fallback keep the fuller mode label they have always shown.</item>
    /// <item>the SUBJECT goes on the plate, at the game's own headline metrics, in the dark fill
    /// cream parchment demands.</item>
    /// <item>everything else is a subordinate line beneath the plate: marked ones at the game's
    /// 26-pixel pitch with a medallion hanging into the gutter, unmarked ones in the tracker's own
    /// annotation block with nothing beside them. See <see cref="ReadoutLine.Marked"/>.</item>
    /// </list></para>
    ///
    /// <para><b>Nothing here knows where anything ends up.</b> Each line's rule, medallion and words
    /// are placed against that line's own section top, and the section is given the height the line
    /// costs. Where the section itself goes is the container's business, decided after this returns.
    /// That is the entire substance of the conversion: there is no cursor to carry a bad measurement
    /// forward, because there is no cursor.</para></summary>
    private (LineSlot? ArrowSlot, SubjectLine? Subject, ReadoutLine? SubjectContent) LayoutLines(
        ReadoutFrame frame, float factor, float width, bool arrowDrawable)
    {
        var count = Math.Min(frame.Content.Lines.Count, MaxLines);
        LineSlot? arrowSlot = null;
        SubjectLine? subject = null;
        ReadoutLine? subjectContent = null;

        // Cleared every frame, because it names a slot in a pool that is reused: last frame's teleport
        // line may be this frame's distance.
        teleportSlot = null;

        var headlineLeft = GameMetrics.Banner.HeadlineLeft * factor;
        var headlineWidth = Math.Max(
            width - headlineLeft - (GameMetrics.Banner.HeadlineRight * factor), factor);
        var subLineLeft = ReadoutBodyLayout.SubLineLeft(factor);
        var subLineWidth = Math.Max(
            width - subLineLeft - (GameMetrics.Banner.HeadlineRight * factor), factor);
        var arrowWanted = arrowDrawable && frame.ArrowRadians is not null;

        // Not i: the lines that go on the banner never reach LayoutSubLine, and it is the first
        // SUBORDINATE line that owns the gutter column. See ReadoutBodyLayout.GutterLine.
        var subLines = 0;
        for (var i = 0; i < count; i++)
        {
            var line = frame.Content.Lines[i];
            markerNodes[i].IsVisible = false;

            if (TryLayoutOnTheBanner(i, line, factor, headlineLeft, headlineWidth, ref subject))
            {
                subjectContent ??= line.Subject ? line : null;
                continue;
            }

            // The arrow takes the first subordinate line — the objective — and that line gives up its
            // own medallion while it has it: one mark per line, and the arrow is the stronger
            // statement about the same thing.
            var takenByArrow = arrowWanted && arrowSlot is null;
            var gutter = ReadoutBodyLayout.GutterLine(subLines++);
            var slot = LayoutSubLine(
                i, line, subLineLeft, subLineWidth, factor, drawMarker: !takenByArrow, gutter: gutter);

            if (takenByArrow)
            {
                arrowSlot = slot;
            }

            if (teleportSlot is null && WantsTeleportBox(frame, line))
            {
                teleportSlot = slot;
                SetTeleportHighlight(hovered: false);
            }
        }

        HideLinesFrom(count);

        footSection.Size = new Vector2(width, ReadoutBodyLayout.FootHeight(factor));
        footSection.IsVisible = true;

        return (arrowSlot, subject, subjectContent);
    }

    /// <summary>Whether this line is the one the teleport box goes on. First match only: there is never
    /// more than one teleport advice, and a second box would be a click target over the wrong words.
    ///
    /// <para>The frame's own offer is what decides, not the line's action mark. With click-to-teleport
    /// turned off the composer still marks the line — the mark describes the line, not the surface —
    /// and placing a hit box on it gave the player a hand cursor over words that would then politely
    /// refuse to do anything.</para></summary>
    private bool WantsTeleportBox(ReadoutFrame frame, ReadoutLine line) =>
        teleportHitBox is not null && frame.ClickableTeleport && line.Action == ReadoutLineAction.Teleport;

    /// <summary>Closes the teleport hit box out for this frame: taken down when no line offered the
    /// click, and recorded either way, because the host rebuilds the addon's collision list from
    /// <see cref="ClickTargets"/> and a hit box that appears without that is one that is never
    /// hit.</summary>
    private void SettleTeleportHitBox(bool placed)
    {
        if (!placed && teleportHitBox is not null)
        {
            teleportHitBox.IsVisible = false;
        }

        ClickTargets = placed ? TeleportTarget : 0;
    }

    /// <summary>Deals with the two lines that belong to the banner itself rather than to the block
    /// beneath it, and says whether this was one of them.
    ///
    /// <para>The HEADING is not drawn at all: its words became the header pill, which is written from
    /// <see cref="ReadoutContent.StripLabel"/> instead. Nothing else consumes the heading line, so
    /// the hub window's own quest header and the ImGui fallback keep the fuller mode label they have
    /// always shown. The first SUBJECT goes on the plate — first only, because the composer emits at
    /// most one and a second would be a second switcher claiming to change the same one
    /// thing.</para></summary>
    private bool TryLayoutOnTheBanner(
        int index, ReadoutLine line, float factor, float left, float available, ref SubjectLine? subject)
    {
        if (line.Emphasis == ReadoutEmphasis.Heading)
        {
            HideLine(index);
            return true;
        }

        if (!line.Subject || subject is not null)
        {
            return false;
        }

        HideLine(index);
        subject = LayoutHeadline(line, factor, left, available);
        return true;
    }

    /// <summary>Takes down every pooled line from <paramref name="first"/> on. The pool is shared and
    /// reused every frame, so a slot that is not used this frame has to be told so, or it keeps drawing
    /// whatever it last held.</summary>
    private void HideLinesFrom(int first)
    {
        for (var i = first; i < MaxLines; i++)
        {
            HideLine(i);
        }
    }

    /// <summary>Takes one line's slot down, <b>including its section</b>, which is what takes it out of
    /// the stack rather than leaving an empty band where it used to be.</summary>
    private void HideLine(int index)
    {
        lineNodes[index].IsVisible = false;
        ruleNodes[index].IsVisible = false;
        markerNodes[index].IsVisible = false;
        lineSections[index].IsVisible = false;
        lineSections[index].Height = 0f;
    }

    /// <summary>Hangs the up/down chevron off the arrow when the target is on a different level of
    /// the world, which is what the game's own minimap does to a marker on another floor.
    ///
    /// <para>Only ever when there is an arrow to hang it on. Whether to claim anything about
    /// elevation at all was decided long before this — see <c>Elevation</c> and <c>GroundHeight</c>
    /// — and by the time it arrives here it is a fact about the frame. The distance line says it in
    /// words as well, because a chevron is a convention the player has to already know.</para>
    ///
    /// <para>The offset parks it clear of the arrow's lower-right corner rather than overlapping it.
    /// The old art overlapped deliberately, to read as one compound mark — which was the mistake:
    /// two meanings fused into one glyph. A small gap is what says "this is a second, separate
    /// thing about the same target".</para></summary>
    private void LayoutElevation(ReadoutFrame frame, float arrowSize)
    {
        if (!arrowNode.IsVisible
            || frame.Content.Elevation == ElevationHint.Level
            || !EnsureElevationTexture(frame.ArrowIcon))
        {
            elevationNode.IsVisible = false;
            return;
        }

        var size = Math.Max(arrowSize * 0.6f, 8f);
        elevationNode.Size = new Vector2(size, size);
        elevationNode.OriginX = size / 2f;
        elevationNode.OriginY = size / 2f;

        // The art points straight up unrotated, so "above" needs no rotation at all and "below" is
        // half a turn. Same guarantee ArrowBitmap gives the bearing arrow.
        elevationNode.Rotation = frame.Content.Elevation == ElevationHint.Above ? 0f : MathF.PI;
        elevationNode.Position = arrowNode.Position + new Vector2(arrowSize * 0.74f, arrowSize * 0.52f);
        elevationNode.IsVisible = true;
    }

    /// <summary>Parks the switcher's click target over the chevron the plate's own art carries, at
    /// the plate's right end.
    ///
    /// <para>The whole right cap rather than the nine drawn pixels of the chevron itself: the mark is
    /// at source x=279-288 of a 300-wide part, the cap it sits in is the last 24, and a target the
    /// size of the glyph would be a nine-pixel button on a television. The cap starts past where the
    /// headline's text box ends (<c>300 - 26 = 274</c> against the cap's 276), so it never covers a
    /// letter.</para>
    ///
    /// <para>Fixed to the plate, not measured off the name — unlike the old floating caret, which
    /// slid along behind the words and ended up sitting right next to the plate's own chevron, which
    /// is how there came to be two. Nothing here knows where the list it opens goes either: that is
    /// the game's own context menu, opened at the cursor — see
    /// <see cref="FollowSwitcherMenu"/>.</para></summary>
    private void LayoutSwitcher(float factor, float width)
    {
        if (switcherHitBox is null)
        {
            return;
        }

        // On the banner's own section, like the cog and the plate's own click target.
        const float top = 0f;

        // Only while the plate is actually drawn: the chevron is part of the plate's art, so with no
        // plate there is no mark to click and an invisible hit box would be a hand cursor over
        // nothing.
        if (!BannerDrawable())
        {
            switcherHitBox.IsVisible = false;
            return;
        }

        var cap = GameMetrics.Banner.PlateInsetX * factor;
        switcherHitBox.Size = new Vector2(cap, GameMetrics.Banner.PlateHeight * factor);
        switcherHitBox.Position = new Vector2(
            Math.Max(width - cap, 0f),
            top + (GameMetrics.Banner.PlateTop * factor));
        switcherHitBox.IsVisible = true;
    }

    /// <summary>Generates the cog once. Same contract as the arrow's texture: the pixels are
    /// computed here and uploaded synchronously, so it either works or it throws, and if it throws
    /// there is simply no cog — the window is still on the info bar entry, the plugin list, the
    /// game's own context menu and <c>/wayfarer settings</c>.</summary>
    private bool EnsureCogTexture()
    {
        if (cogFailed)
        {
            return false;
        }

        if (cogLoaded)
        {
            return true;
        }

        try
        {
            var wrap = textures.CreateFromRaw(
                RawImageSpecification.Rgba32(CogBitmap.Size, CogBitmap.Size),
                CogBitmap.Render(),
                "Wayfarer settings cog");

            // Takes ownership: the node disposes the wrap with itself.
            cogNode!.LoadTexture(wrap);
            cogNode.TextureSize = new Vector2(CogBitmap.Size, CogBitmap.Size);
            cogLoaded = true;
            return true;
        }
        catch (Exception ex)
        {
            cogFailed = true;
            log.Error(ex, "Wayfarer readout: the settings cog could not be generated, so the readout has none. Settings are still on the info bar entry, the plugin list and /wayfarer settings.");
            return false;
        }
    }

    /// <summary>Generates the elevation mark in the current arrow colour, if it is not already
    /// loaded. Its own copy of the pixels rather than a shared wrap: KamiToolKit's image node takes
    /// ownership of the texture it is handed and disposes it with itself, so two nodes sharing one
    /// wrap is a double free waiting for a colour change.
    ///
    /// <para>Failing here costs the mark and nothing else — the distance line still says "above
    /// you" in words, which is the half of this feature that does not need a convention to
    /// read.</para></summary>
    private bool EnsureElevationTexture(ArrowIconVariant variant)
    {
        if (elevationFailed)
        {
            return false;
        }

        if (loadedElevationVariant == variant)
        {
            return true;
        }

        try
        {
            var wrap = textures.CreateFromRaw(
                RawImageSpecification.Rgba32(ChevronBitmap.Size, ChevronBitmap.Size),
                ChevronBitmap.Render(variant),
                $"Wayfarer elevation mark ({variant})");

            elevationNode.LoadTexture(wrap);
            elevationNode.TextureSize = new Vector2(ChevronBitmap.Size, ChevronBitmap.Size);
            loadedElevationVariant = variant;
            return true;
        }
        catch (Exception ex)
        {
            elevationFailed = true;
            log.Error(ex, "Wayfarer readout: the above/below mark could not be generated. The distance line still says which it is in words.");
            return false;
        }
    }

    /// <summary>Draws the rule at the top of a separated line's own section, and says how much of the
    /// section it took — which is nothing at all when the line is not separated.</summary>
    private float LayoutRule(int index, ReadoutLine line, float factor, float left, float width)
    {
        if (!line.Separated)
        {
            ruleNodes[index].IsVisible = false;
            return 0f;
        }

        ruleNodes[index].Size = new Vector2(width, GameMetrics.Window.RuleHeight);
        ruleNodes[index].Position = new Vector2(left, ReadoutBodyLayout.RuleTop(factor));
        ruleNodes[index].IsVisible = true;
        return ReadoutBodyLayout.RuleAdvance(separated: true, factor);
    }

    /// <summary>Writes the name of the tracked thing across the plate, and reports where it landed
    /// so the switcher and the hover box can be parked against the words rather than against the
    /// plate.
    ///
    /// <para>One row, always, cut short with the engine's own ellipsis rather than wrapped — see
    /// <see cref="SubjectFlags"/>. The plate is a fixed 48 tall and does not grow, which is the same
    /// reason the game's own headline is a single <c>Ellipsis</c> node in a 250-wide box.</para>
    ///
    /// <para>The fill is dark, which nothing else on the readout is, because this one line is on
    /// cream parchment rather than over the world — see <see cref="HeadlineColor"/>.</para></summary>
    private SubjectLine LayoutHeadline(ReadoutLine line, float factor, float left, float available)
    {
        var node = headlineNode;
        var fontSize = Math.Max(BaseHeadlineSize * factor, 8f);
        var height = Math.Max(GameMetrics.Banner.HeadlineHeight * factor, fontSize);
        var width = Math.Max(available, fontSize);

        // In the banner's own section, so where the name sits on the plate is a property of the plate
        // and of nothing above it.
        var top = (GameMetrics.Banner.PlateTop + GameMetrics.Banner.HeadlineTop) * factor;

        node.FontSize = (uint)fontSize;
        node.LineSpacing = (uint)height;
        node.TextColor = HeadlineColor();
        node.TextOutlineColor = HeadlineEdgeColor();

        // Sized before the words are handed over: the engine cuts the text to the node's width at
        // the moment it is assigned, so a name given to a node that has not been sized yet would be
        // cut to last frame's width.
        node.Size = new Vector2(width, height);

        // Re-handed over when the room changed as well as when the words did: the engine keeps only
        // what it drew, so a name already shortened to "Sastasha…" would stay shortened after the
        // readout grew, having lost the letters it would now have room for.
        var regrown = Math.Abs(width - lastSubjectWidth) > 0.5f;
        lastSubjectWidth = width;
        if (regrown || !string.Equals(lastHeadlineText, line.Text, StringComparison.Ordinal))
        {
            lastHeadlineText = line.Text;
            node.String = line.Text;
        }

        node.Position = new Vector2(left, top);
        node.IsVisible = true;

        // The UNTRUNCATED width, measured with the font the node has just been given: that overload
        // measures arbitrary text rather than whatever the node last drew, so it answers on the
        // frame the name changes and it answers about the whole name.
        //
        // Unscaled, because `width` above is: the readout hands its nodes sizes that already carry
        // the interface scale and leaves their Scale at 1 — see WrappedLines for the same trap in its
        // more expensive form. Scaled, this called every name truncated at any interface size over
        // 100%, which is what parks the switcher against a shortened name that is not shortened.
        var full = node.GetTextDrawSize(line.Text, considerScale: false).X;
        return new SubjectLine(top, height, fontSize, Math.Min(full, width), full > width);
    }

    /// <summary>Lays out one subordinate line and reports the height it took and where its words
    /// ended up. The height is measured rather than assumed: the readout wraps rather than truncating
    /// (there is no marquee flag anywhere in the engine, and the game's own journal and tooltips grow
    /// downward), so a line's height is a function of its text and the width it has.
    ///
    /// <para><b>Two shapes, and the composer decides which.</b> A marked line is the game's own
    /// job-quest row — a 26-pixel pitch with a 32-pixel medallion hanging into the gutter, taller
    /// than the row and deliberately so. An unmarked line is an annotation and takes the quest
    /// tracker's own meta block instead, with nothing beside it. A line that wraps grows past either,
    /// because a clipped line is worse than a loose one.</para>
    ///
    /// <para><b>The measurement goes into this line's own section height and nowhere else.</b> The row
    /// count still comes from the engine — only the engine knows where a string breaks — but the only
    /// thing that consumes it is the section this line lives in. A count that comes back wrong makes
    /// one line the wrong height; it cannot move the line after it, because nothing after it reads
    /// anything from here.</para></summary>
    private LineSlot LayoutSubLine(
        int index, ReadoutLine line, float left, float width, float factor, bool drawMarker, bool gutter)
    {
        var section = lineSections[index];
        var node = lineNodes[index];
        var fontSize = ReadoutBodyLayout.SubLineFontSize(factor);
        var ruleAdvance = LayoutRule(index, line, factor, left, width);

        node.FontType = FontType.Axis;
        node.FontSize = (uint)fontSize;

        // One number for both the wrap spacing and the advance, so a wrapped line's second row and
        // the line after it cannot disagree about where they are. The banner leads its subordinate
        // lines at Axis 12 over 14 — ScenarioTree 1002 #3 against the same pairing ToDoList uses in
        // 1008/1009 — which is two over the font, exactly as the tracker does.
        var step = ReadoutBodyLayout.SubLineStep(factor);
        node.LineSpacing = (uint)step;
        node.TextColor = ColorFor(line.Emphasis);
        node.TextOutlineColor = OutlineFor(line.Emphasis);
        node.TextFlags = BodyFlags;

        SetGlyphLineText(index, line, forced: false);

        // `gutter` is the arrow's line. It buys the tracker's icon-bearing line height rather than its
        // bare-text height, because a 24px mark in a 14px line hangs out of the readout — see
        // ReadoutBodyLayout.SubLineBlock. The medallion's own lines carry it on line.Marked.
        var block = new ReadoutBlock(line.Marked, line.Separated, WrappedLines(node, width));
        var height = ReadoutBodyLayout.TextHeight(block, factor, gutter);

        // Centred in the block for a single row, top-aligned once it wraps — a wrapped line has to
        // start where the block does or its extra rows push into the line beneath.
        var textTop = ReadoutBodyLayout.TextTop(block, factor, gutter);

        node.Size = new Vector2(width, step * Math.Clamp(block.Rows, 1f, MaxWrappedLines));
        node.Position = new Vector2(left, textTop);
        node.IsVisible = true;

        // Full alpha unless this turns out to be the clickable teleport line, which the caller dims —
        // reset here rather than there because these nodes are a reused pool and last frame's
        // teleport advice is this frame's distance.
        node.Alpha = 1f;

        // The section is worth the whole of what the line costs — the rule's own room included — which
        // is what lets the container place the next line without knowing anything about this one.
        section.Size = new Vector2(stack.Width, ReadoutBodyLayout.LineHeight(block, factor, gutter));
        section.IsVisible = true;

        LayoutMarker(index, line, factor, ruleAdvance, height, drawMarker);
        return new LineSlot(index, ruleAdvance, textTop, height, fontSize, left, width);
    }

    /// <summary>Hangs the game's "!" medallion into the gutter beside a marked line, centred on the
    /// line's own block. It is 32 tall against a 26-tall row and overhangs it either side, which is
    /// what the game does and what makes the marker read as pinned to the line rather than as part of
    /// a column.</summary>
    private void LayoutMarker(
        int index, ReadoutLine line, float factor, float afterRule, float height, bool draw)
    {
        var marker = markerNodes[index];
        if (!draw || !line.Marked || !BannerDrawable())
        {
            marker.IsVisible = false;
            return;
        }

        var size = GameMetrics.Banner.MarkerSize * factor;
        marker.Size = new Vector2(size, size);
        marker.Position = new Vector2(
            ReadoutBodyLayout.GutterLeft(factor),
            afterRule + ((Math.Min(height, GameMetrics.Banner.SubLinePitch * factor) - size) / 2f));
        marker.IsVisible = true;
    }

    /// <summary>Hands the words to a line's node, but only when something about them has actually
    /// changed. Assigning <c>String</c> builds a SeString and re-runs the engine's text flow, and
    /// this is a per-frame path.</summary>
    private void SetLineText(int index, string text, bool forced)
    {
        if (!forced && string.Equals(lastText[index], text, StringComparison.Ordinal))
        {
            return;
        }

        lastText[index] = text;
        lineNodes[index].String = text;
    }

    /// <summary>Writes a line whose words have one of the game's own bitmap-font glyphs inside them.
    ///
    /// <para>The glyph and where it goes arrive as a mark on the line
    /// (<see cref="ReadoutLine.Glyph"/>), not as a character in the string — this is the only place
    /// the abstract mark becomes a concrete <c>BitmapFontIcon</c>, exactly as
    /// <see cref="DtrEntry"/> does for the server info bar. <c>SeStringBuilder</c> puts the icon in
    /// the same text run as the words either side of it, so it wraps and ellipsises with them.</para>
    ///
    /// <para>The cache key includes the glyph, because two lines whose words are identical and whose
    /// glyphs differ are not the same line.</para></summary>
    private void SetGlyphLineText(int index, ReadoutLine line, bool forced)
    {
        // The one mapping from the readout's abstract glyph mark to the game's own icon. Deliberately
        // not shared with DtrEntry's copy: they read the same enum, and if the two surfaces ever want
        // different marks for the same meaning neither has to be untangled from the other first.
        if (line.Glyph != DtrGlyph.Aetheryte)
        {
            SetLineText(index, line.Text, forced);
            return;
        }

        // Clamped rather than trusted: a composer that named an index past the end of its own words
        // must not be able to throw on the drawing path.
        var at = Math.Clamp(line.GlyphAt, 0, line.Text.Length);
        var key = $"{line.Glyph}@{at}:{line.Text}";
        if (!forced && string.Equals(lastText[index], key, StringComparison.Ordinal))
        {
            return;
        }

        var builder = new SeStringBuilder();
        builder.AddText(line.Text[..at]).AddIcon(BitmapFontIcon.Aetheryte).AddText(line.Text[at..]);

        lastText[index] = key;
        lineNodes[index].String = new ReadOnlySeString(builder.Build().Encode());
    }

    /// <summary>Parks the box over the words of the subject line — the one region that is both the
    /// hover that reveals a cut-short name and the click that opens the game's Journal at it.
    ///
    /// <para><b>One box for both, and exactly as wide as the words.</b> They are the same gesture
    /// aimed at the same thing, and two overlapping rectangles over one line would be two answers to
    /// the question of what the pointer is on. It stops where the text stops, so it reaches neither
    /// the switcher to its right nor the cog on the line above, and it is one row tall, so it never
    /// touches the teleport advice below.</para>
    ///
    /// <para><b>It is only there when it does something.</b> A name that fits and has no journal
    /// entry gets no box at all rather than an invisible rectangle sitting over the quest name of
    /// every readout, swallowing the world clicks underneath it. The hand cursor is separate again:
    /// hovering to read a hunt's full name is not an offer to open anything.</para></summary>
    private void LayoutSubjectHitBox(SubjectLine? subject, string? fullText, float left, bool journalOffered)
    {
        if (subjectHitBox is null)
        {
            return;
        }

        var truncated = subject is { Truncated: true } && fullText is { Length: > 0 };
        if (subject is not { } line || (!truncated && !journalOffered))
        {
            subjectHitBox.IsVisible = false;
            SetSubjectTooltip(string.Empty);
            return;
        }

        SetSubjectTooltip(truncated ? fullText! : string.Empty);
        subjectHitBox.ShowClickableCursor = journalOffered;
        subjectHitBox.Size = new Vector2(Math.Max(line.TextWidth, 1f), line.Height);
        subjectHitBox.Position = new Vector2(left, line.Top);
        subjectHitBox.IsVisible = true;
    }

    /// <summary>Gives the hover box the whole name to show, or takes the tooltip away again. Written
    /// only on a change: handing a node a tooltip rebuilds the addon's entire collision list, and
    /// this runs sixty times a second.</summary>
    private void SetSubjectTooltip(string text)
    {
        if (subjectHitBox is null || string.Equals(lastSubjectTooltip, text, StringComparison.Ordinal))
        {
            return;
        }

        lastSubjectTooltip = text;
        subjectHitBox.TextTooltip = text.Length == 0 ? default : text;
    }

    /// <summary>Parks the invisible click target over the teleport line, from where the flow actually
    /// put that line's section.
    ///
    /// <para><b>Why it is not in the section with the line.</b> There is one box and any of twelve
    /// lines could be the teleport advice, so a box inside a section would have to be reparented as
    /// the composer's output changes — in a per-frame path, which is how a node comes to draw in two
    /// places at once. It floats over the stack instead and is placed by adding its line's section's
    /// own placed position to an offset within that section. Both of those are facts, not
    /// measurements.</para></summary>
    private bool LayoutTeleportHitBox()
    {
        if (teleportHitBox is null || teleportSlot is not { } slot)
        {
            return false;
        }

        // The words' own run, grown by the tracker's text inset, and never taller than the block it
        // sits in. Derived from the line's measured text rather than from a fraction of its block, so
        // tightening the readout's pitch cannot shrink the target below the words it belongs to.
        var padding = TeleportBoxPadding * 2f;
        var height = Math.Min(slot.FontSize + padding, slot.Height);
        var top = stack.Y + lineSections[slot.Index].Y + slot.RuleAdvance
            + Math.Max((slot.Height - height) / 2f, 0f);

        teleportHitBox.Size = new Vector2(slot.Width, height);
        teleportHitBox.Position = new Vector2(slot.Left, top);
        teleportHitBox.IsVisible = true;
        return true;
    }

    /// <summary>Where the arrow's centre lands: the optical centre of the line it belongs to, in the
    /// readout's own coordinates. The section's placed position plus an offset inside it — see
    /// <see cref="LineSlot"/> for why that is not the same kind of thing as the old cursor.
    ///
    /// <para>No subordinate line at all and still something to point at: the arrow parks where the
    /// first one would have been — the top of the block after the banner — rather than climbing onto
    /// the plate and colliding with the emblem already in that column.</para></summary>
    private float ArrowCentre(LineSlot? slot, float factor) =>
        slot is { } line
            ? stack.Y + lineSections[line.Index].Y + line.TextTop + (line.FontSize * ArrowOpticalCentre)
            : stack.Y + bannerSection.Y + bannerSection.Height
              + (GameMetrics.Banner.AnnotationBlock * factor / 2f);

    /// <summary>Generates the arrow for the chosen colour and hands it to the image node, if it is
    /// not already loaded. Reloading on a variant change is what makes the setting apply live.
    ///
    /// <para>Unlike a texture read out of the game's resource system, this one cannot be merely
    /// <i>late</i> — the pixels are computed here and uploaded synchronously — so there is no retry
    /// loop and no "not ready yet" state. It either works or it throws, and if it throws the readout
    /// falls back to saying the direction in words and never tries again this session.</para></summary>
    private bool EnsureArrowTexture(ArrowIconVariant variant)
    {
        if (arrowFailed)
        {
            return false;
        }

        if (loadedVariant == variant)
        {
            return true;
        }

        try
        {
            var pixels = ArrowBitmap.Render(variant);
            var wrap = textures.CreateFromRaw(
                RawImageSpecification.Rgba32(ArrowBitmap.Size, ArrowBitmap.Size),
                pixels,
                $"Wayfarer arrow ({variant})");

            // Takes ownership: the node disposes the previous wrap and this one with itself.
            arrowNode.LoadTexture(wrap);
            arrowNode.TextureSize = new Vector2(ArrowBitmap.Size, ArrowBitmap.Size);
            loadedVariant = variant;

            if (arrowNode.ActualTextureSize == Vector2.Zero)
            {
                log.Warning(
                    "Wayfarer readout: the generated direction arrow reports no texture size after being "
                    + "uploaded. It is still being drawn; if there is a blank space where the arrow should "
                    + "be, this line is why.");
            }

            return true;
        }
        catch (Exception ex)
        {
            arrowFailed = true;
            log.Error(ex, "Wayfarer readout: the direction arrow could not be generated — showing the direction in words instead.");
            return false;
        }
    }

    /// <summary>Logs why there is (or is no longer) an arrow, once per change of reason. Deliberately
    /// not rate-limited by time: the interesting event is the transition, and this runs every frame.</summary>
    private void ReportArrow(ArrowHiddenReason reason)
    {
        if (reportedOnce && reason == lastReported)
        {
            return;
        }

        lastReported = reason;
        reportedOnce = true;

        if (reason != ArrowHiddenReason.TextureUnavailable && !diagnosticsEnabled())
        {
            return;
        }

        var message = reason switch
        {
            ArrowHiddenReason.None => "Wayfarer readout: the direction arrow is being drawn.",
            ArrowHiddenReason.NotRequested => "Wayfarer readout: no direction arrow — nothing active has a direction to point at.",
            ArrowHiddenReason.NoTargetCoordinates => "Wayfarer readout: no direction arrow — the active objective has no target coordinates.",
            ArrowHiddenReason.NoPlayer => "Wayfarer readout: no direction arrow — there is no local player to measure a bearing from.",
            _ => "Wayfarer readout: the direction arrow could not be generated — showing the direction in words instead.",
        };

        if (reason == ArrowHiddenReason.TextureUnavailable)
        {
            // Once for the whole session, not once per change of reason: guidance stopping and
            // starting again re-enters this reason every time, and the answer never changes.
            if (!warnedTextureOnce)
            {
                warnedTextureOnce = true;
                log.Warning(message);
            }

            return;
        }

        log.Debug(message);
    }

    /// <summary>Writes down what the arrow is currently claiming, so the one thing about it that
    /// cannot be settled by reading the code can be settled by looking at the screen once.
    ///
    /// <para><c>NavMath.ArrowAngle</c> is defined as "0 = straight up", and the words fallback is
    /// built from the same number. The arrow art is now generated by <see cref="ArrowBitmap"/>,
    /// which draws it pointing straight up and centred in its own image, so the rest orientation is
    /// a property of this codebase rather than an assumption about a game asset — there is no offset
    /// to get wrong. This line stays anyway, because "the arrow and the words disagree" is still the
    /// cheapest possible check that the bearing itself is right.</para></summary>
    private void ReportBearing(float radians)
    {
        if (!diagnosticsEnabled())
        {
            return;
        }

        var words = NavMath.DescribeDirection(radians);
        if (string.Equals(words, lastBearingWords, StringComparison.Ordinal))
        {
            return;
        }

        lastBearingWords = words;
        var degrees = radians * 180f / MathF.PI;
        log.Debug(
            $"Wayfarer readout: arrow rotation {degrees:F0}° = {words}. The arrow is drawn pointing straight " +
            "up unrotated; if the arrow on screen and these words disagree, the bearing is what is wrong.");
    }
}
