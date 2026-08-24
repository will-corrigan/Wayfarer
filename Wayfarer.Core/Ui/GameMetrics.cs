namespace Wayfarer.Core.Ui;

/// <summary>Every dimension Wayfarer's own windows are built from, read out of the game's own
/// interface definitions rather than chosen.
///
/// <para><b>Why this file exists.</b> The design goal is that Wayfarer be indistinguishable from
/// something Square Enix shipped. That makes the metrics measurable rather than inventable: the
/// game ships its layouts as <c>.uld</c> files, each one a node tree with real rectangles, real
/// font sizes and real line spacing. Every number below was read out of one of those trees, and
/// every number carries the window and node it came from. Nothing here is a preference.</para>
///
/// <para><b>The windows measured.</b> <c>ui/uld/Journal.uld</c> and <c>ui/uld/ContentsFinder.uld</c>
/// for a list with rows and a detail area; <c>ui/uld/MonsterNoteBook.uld</c> — the Hunting Log — for
/// rows carrying artwork, which is the closest analogue to our own; <c>ui/uld/JournalDetail.uld</c>
/// for prose and requirement blocks; <c>ui/uld/ToDoList.uld</c> — the quest tracker — for the
/// heads-up readout. Node numbers below are the <c>NodeId</c>s in those files, and component numbers
/// are the <c>ComponentData.Id</c>s.</para>
///
/// <para><b>Units.</b> ULD units, which are addon units: the same space
/// <c>AtkResNode.Width</c>/<c>Height</c> live in, before the game multiplies by the player's HUD
/// scale. That is the same space KamiToolKit's <c>Size</c> and <c>Position</c> use, so these numbers
/// are used directly and scale with the HUD for free.</para></summary>
public static class GameMetrics
{
    /// <summary>Window content insets, list geometry and everything else a list window is made of.
    /// Measured from <c>Journal.uld</c> and <c>ContentsFinder.uld</c>, which agree on every value.
    /// </summary>
    public static class Window
    {
        /// <summary>Left inset from the window's content edge to anything drawn in it.
        /// Journal <c>#25</c> (the tree list) and ContentsFinder <c>#37</c>/<c>#62</c> all sit at
        /// x=16.</summary>
        public const float InsetLeft = 16f;

        /// <summary>Right inset. Journal's content box is 462 wide and its 432-wide tree list starts
        /// at 16, leaving 14; ContentsFinder's is 482 with a 452-wide block at 16, leaving 14.
        /// </summary>
        public const float InsetRight = 14f;

        /// <summary>A horizontal rule. Journal <c>#26</c>, ContentsFinder <c>#51</c>/<c>#55</c>/
        /// <c>#61</c> and MonsterNoteBook <c>#17</c>/<c>#37</c> are all 4 tall.</summary>
        public const float RuleHeight = 4f;

        /// <summary>The gap the game leaves below a rule before the next block. ContentsFinder's
        /// rule <c>#55</c> ends at y=512 and <c>#56</c> starts at y=516.</summary>
        public const float RuleGap = 4f;

        /// <summary>The gap between two stacked control blocks. ContentsFinder <c>#37</c> ends at
        /// y=156 and the button pair <c>#38</c>/<c>#39</c> starts at y=164.</summary>
        public const float BlockGap = 8f;

        /// <summary>The header band of the game's standard window frame — the title's own line.
        /// The <c>Window</c> component (ContentsFinder <c>1008</c>, MonsterNoteBook <c>1004</c>) is
        /// 38 tall. Wayfarer does not draw this itself; KamiToolKit's <c>NativeAddon</c> reserves it
        /// and reports what is left as <c>ContentSize</c>. Recorded so the reserve can be sanity
        /// checked.</summary>
        public const float HeaderHeight = 38f;
    }

    /// <summary>The type scale. Six roles, each measured where the game uses it. Line spacing is not
    /// one formula — the game leads a wrapping paragraph far more loosely than a dimmed caption — so
    /// each role carries its own, rather than a size-plus-N rule that would be an invention.
    /// </summary>
    public static class Type
    {
        /// <summary>Window title. The <c>Window</c> component's title node (ContentsFinder
        /// <c>1008 #3</c>, MonsterNoteBook <c>1004 #3</c>) is TrumpGothic 23.</summary>
        public const uint TitleSize = 23u;

        /// <summary>A panel heading over a block inside a window. MonsterNoteBook <c>#15</c> is
        /// Jupiter 20 in a 24-tall box; <c>#3</c> is Jupiter 23 in 25.</summary>
        public const uint PanelHeadingSize = 20u;

        /// <summary>The box a panel heading needs. MonsterNoteBook <c>#15</c> is h=24.</summary>
        public const float PanelHeadingHeight = 24f;

        /// <summary>Body and list-row text. Axis 14 everywhere: Journal <c>1023 #3</c>,
        /// ContentsFinder <c>1026 #6</c>, JournalDetail <c>#34</c>, MonsterNoteBook <c>1017 #4</c>.
        /// </summary>
        public const uint BodySize = 14u;

        /// <summary>Line spacing for body text that is not a wrapping paragraph. JournalDetail
        /// <c>#34</c>/<c>#46</c> and its <c>1009</c> component set Axis 14 to 18.</summary>
        public const uint BodyLine = 18u;

        /// <summary>Line spacing for a wrapping paragraph — the game leads these much more loosely.
        /// ContentsFinder <c>#54</c> and Journal <c>#3</c> both set Axis 14 to 21.</summary>
        public const uint ProseLine = 21u;

        /// <summary>Dimmed captions, counts and second lines. Axis 12: Journal <c>1022 #2</c>,
        /// ContentsFinder <c>1025 #2</c>, MonsterNoteBook <c>1009 #3</c>.</summary>
        public const uint SecondarySize = 12u;

        /// <summary>Line spacing for secondary text. ToDoList <c>1008 #6</c> sets Axis 12 to 14.
        /// </summary>
        public const uint SecondaryLine = 14u;

        /// <summary>A detail area's own title, above its prose. JournalDetail <c>#33</c>/<c>#38</c>
        /// are Axis 18.</summary>
        public const uint DetailTitleSize = 18u;

        /// <summary>Line spacing for a detail title. JournalDetail <c>#33</c>/<c>#38</c> set Axis 18
        /// to 20.</summary>
        public const uint DetailTitleLine = 20u;

        /// <summary>A count or numeral pinned to the right of a section row. Journal <c>1021 #3</c>
        /// is TrumpGothic 23.</summary>
        public const uint SectionCountSize = 23u;

        /// <summary>Where a line of text's optical centre sits, as a fraction of its font size
        /// measured down from the top of the em. Not 0.5: Axis draws with its cap height in the
        /// upper part of the em, so the glyphs' visual weight sits above the geometric middle of
        /// the line box, and a control centred on the box reads as sitting a couple of pixels low
        /// beside it.
        ///
        /// <para>Not a <c>.uld</c> measurement like the rest of this file — there is no font-metrics
        /// node to read a cap-height off — but a font-rendering constant established once, against
        /// the readout's direction arrow next to its objective line, and now the one number every
        /// small mark beside a line of text aligns to, so two controls beside two different lines
        /// (the readout's follow switcher beside the quest name, its settings cog beside the
        /// heading) read as siblings rather than each having found its own answer. See
        /// <c>ReadoutBodyNode.LayoutArrow</c> for where this was first tuned in.</para></summary>
        public const float CapHeightCentre = 0.58f;
    }

    /// <summary>List rows. Journal's tree list (<c>1027</c>) and ContentsFinder's (<c>1030</c>) are
    /// the same construction: rows abut, an icon sits two pixels in, and the text starts two pixels
    /// past the icon.</summary>
    public static class Row
    {
        /// <summary>A one-line entry row. Journal <c>1023</c> and ContentsFinder <c>1026</c> are both
        /// 24 tall.</summary>
        public const float Height = 24f;

        /// <summary>Rows abut. Every <c>ListItem</c> under Journal <c>1027</c> and ContentsFinder
        /// <c>1030</c> is placed at y=0 with no separator between instances.
        ///
        /// <para>Wayfarer uses 1 rather than 0, and this is the one deliberate deviation in the row:
        /// the game's row art is a nine-grid with its own bevel, and KamiToolKit's list draws a flat
        /// highlight instead, so a single pixel is what keeps two selected rows from reading as one
        /// block.</para></summary>
        public const float Spacing = 1f;

        /// <summary>Inset from the row's left edge to the icon. Journal <c>1023 #2</c> and
        /// ContentsFinder <c>1026 #2</c> are both at x=2.</summary>
        public const float Padding = 2f;

        /// <summary>The status icon in a list row. Journal <c>1023 #2</c> and ContentsFinder
        /// <c>1026 #3</c> are both 20x20.</summary>
        public const float IconSize = 20f;

        /// <summary>The gap between the icon's right edge and the text. Both windows put the icon at
        /// x=2 with width 20 and the text at x=24.</summary>
        public const float IconGap = 2f;

        /// <summary>The left edge of a row's text once the icon column is accounted for — x=24 in
        /// both windows.</summary>
        public const float TextLeft = Padding + IconSize + IconGap;

        /// <summary>Inset from the row's top to its text node. Journal <c>1023 #3</c> and
        /// ContentsFinder <c>1026 #6</c> are both at y=2.</summary>
        public const float TextTop = 2f;

        /// <summary>The height of a row's text node. Journal <c>1023 #3</c> and ContentsFinder
        /// <c>1026 #6</c> are both h=22 in a 24-tall row.</summary>
        public const float TextHeight = 22f;

        /// <summary>The right-hand caption column. Journal <c>1023 #4</c> and ContentsFinder
        /// <c>1026 #19</c> are both 48 wide.</summary>
        public const float TrailingWidth = 48f;

        /// <summary>The gap the game leaves between a row's name and its right-hand caption. Journal
        /// <c>1023</c>'s name ends at x=336 and the caption starts at x=348.</summary>
        public const float TrailingGap = 12f;

        /// <summary>A section header row. Journal <c>1021</c> and ContentsFinder <c>1024</c> are both
        /// 28 tall.</summary>
        public const float SectionHeight = 28f;

        /// <summary>The icon on a section header row. Journal <c>1021 #2</c> and ContentsFinder
        /// <c>1024 #2</c> are both 24x24 at x=0.</summary>
        public const float SectionIconSize = 24f;

        /// <summary>The left edge of a section header's text. Journal <c>1021 #4</c> is at x=23,
        /// ContentsFinder <c>1024 #4</c> at x=23.</summary>
        public const float SectionTextLeft = 23f;

        /// <summary>A dimmed sub-row — the game's own note line inside a list. Journal <c>1022</c>
        /// and ContentsFinder <c>1025</c> are both 24 tall with an Axis 12 text node h=21 at
        /// y=1.</summary>
        public const float SecondaryHeight = 24f;

        /// <summary>Inset from a sub-row's top to its text. Journal <c>1022 #2</c> is at y=1.
        /// </summary>
        public const float SecondaryTextTop = 1f;

        /// <summary>The height of a sub-row's text node. Journal <c>1022 #2</c> is h=21.</summary>
        public const float SecondaryTextHeight = 21f;

        /// <summary>A two-line row: the game's own Axis-14 line stacked on its own Axis-12 line, with
        /// no invented gap between them. That sum — 48 — is exactly the height of the Hunting Log's
        /// own two-part row (MonsterNoteBook <c>1017</c>), which is the game's only row carrying a
        /// name, a right-hand count and a second element beneath.</summary>
        public const float EntryHeight = Height + SecondaryHeight;

        /// <summary>The icon on the Hunting Log's 48-tall row is 48x48 (MonsterNoteBook
        /// <c>1017 #3</c>) because it is creature art. Wayfarer's row carries a 20px status marker
        /// instead, so it keeps the list-row icon size and centres it over both lines.</summary>
        public const float EntryIconTop = (EntryHeight - IconSize) / 2f;
    }

    /// <summary>The scroll bar, and the width a list has to give up to it.</summary>
    public static class Scroll
    {
        /// <summary>The bar itself. The <c>ScrollBar</c> components in Journal (<c>1004</c>),
        /// ContentsFinder (<c>1004</c>) and MonsterNoteBook (<c>1007</c>) are all 8 wide.</summary>
        public const float BarWidth = 8f;

        /// <summary>What a scrolling list subtracts from its own width for the bar. Journal's tree
        /// list is 432 wide and its rows are 424; ContentsFinder's is 458 with 448-wide rows and an
        /// extra 2 of frame.</summary>
        public const float Gutter = BarWidth;
    }

    /// <summary>Buttons, checkboxes and the rest of the control vocabulary.</summary>
    public static class Control
    {
        /// <summary>Button height. The <c>Button</c> components in Journal (<c>1001</c>),
        /// ContentsFinder (<c>1001</c>) and JournalDetail (<c>1001</c>) are all 28 tall.</summary>
        public const float ButtonHeight = 28f;

        /// <summary>A button's label. Journal <c>1001 #2</c> is Axis 12, centred, h=20 at y=3.
        /// </summary>
        public const uint ButtonLabelSize = Type.SecondarySize;

        /// <summary>The narrow button. Journal's <c>1001</c> is authored at 100 wide.</summary>
        public const float ButtonWidthSmall = 100f;

        /// <summary>The standard button. JournalDetail places three <c>1001</c>s at 120 wide
        /// (<c>#50</c>–<c>#52</c>).</summary>
        public const float ButtonWidthMedium = 120f;

        /// <summary>The wide button. ContentsFinder places three at 144 (<c>#72</c>–<c>#74</c>).
        /// </summary>
        public const float ButtonWidthLarge = 144f;

        /// <summary>The gap between buttons in a row.
        ///
        /// <para>The game abuts them — JournalDetail's three sit at x=12/132/252 at 120 wide — because
        /// its button art carries its own margin inside the nine-grid. KamiToolKit's
        /// <c>TextButtonNode</c> has no such margin, so a small gap stands in for the art the game
        /// gets for free. Four is the smallest value that reads as two buttons.</para></summary>
        public const float ButtonGap = 4f;

        /// <summary>Checkbox height. MonsterNoteBook <c>1009</c> is 24 tall; Journal's <c>1012</c> is
        /// placed at 20.</summary>
        public const float CheckboxHeight = 24f;

        /// <summary>Drop-down height. Journal <c>1017</c> and MonsterNoteBook <c>1010</c> are both
        /// 24.</summary>
        public const float DropDownHeight = 24f;

        /// <summary>A tab or category selector. Journal <c>1025</c> and ContentsFinder <c>1029</c> are
        /// both 36x36 radio buttons.</summary>
        public const float TabHeight = 36f;

        /// <summary>A text input. Journal <c>#7</c> is 28 tall.</summary>
        public const float TextInputHeight = 28f;
    }

    /// <summary>The detail area — the block that says what the cursor is on. Measured from
    /// MonsterNoteBook's own description panel (<c>#40</c>–<c>#45</c>) and from JournalDetail.
    /// </summary>
    public static class Detail
    {
        /// <summary>Inset from the panel's edge to its text. MonsterNoteBook's box <c>#45</c> starts
        /// at x=0 and its body lines <c>#43</c>/<c>#44</c> at x=16.</summary>
        public const float Padding = 16f;

        /// <summary>The icon beside a detail heading. MonsterNoteBook <c>#41</c> is 16x16 at
        /// (6,2).</summary>
        public const float HeadingIconSize = 16f;

        /// <summary>The left edge of a detail heading's text. MonsterNoteBook <c>#42</c> is at x=26,
        /// twenty past the icon's x=6.</summary>
        public const float HeadingTextLeft = 26f;

        /// <summary>A detail heading's own line. MonsterNoteBook <c>#42</c> is h=20.</summary>
        public const float HeadingHeight = 20f;

        /// <summary>The indent the game gives the lines beneath a detail heading. MonsterNoteBook
        /// <c>#43</c>/<c>#44</c> sit at x=16 inside a box whose heading text is at x=26 — the body is
        /// pulled back to the panel padding rather than indented under the heading.</summary>
        public const float BodyIndent = Padding;

        /// <summary>How tall a two-line body block is. MonsterNoteBook <c>#43</c> and <c>#44</c> are
        /// each h=32 carrying Axis 14 at line spacing 14.</summary>
        public const float TwoLineBlock = 32f;

        /// <summary>The detail title's own line. JournalDetail <c>#33</c>/<c>#38</c> reserve h=50 for
        /// two Axis-18 lines at leading 20; one line is half of that.</summary>
        public const float TitleHeight = 25f;

        /// <summary>The narrow caption pinned to the right of a detail title — the game's own
        /// right-hand column is a fixed width, not a fraction. ContentsFinder <c>1024 #3</c> is 64
        /// wide, Journal <c>1023 #4</c> is 48.</summary>
        public const float KindWidth = 64f;
    }

    /// <summary>The heads-up readout. Measured from the quest tracker, <c>ToDoList.uld</c>, which is
    /// the game's own always-on overlay and therefore the only correct model for ours — its register
    /// is much tighter than a window's.</summary>
    public static class Hud
    {
        /// <summary>The tracker's width. ToDoList's root <c>#1</c> and every component in it are 400
        /// wide.</summary>
        public const float Width = 400f;

        /// <summary>The left gutter the tracker reserves for its icons and markers. Every text node
        /// in components <c>1004</c>, <c>1005</c>, <c>1007</c> and <c>1013</c> starts at x=28 with
        /// width 372.</summary>
        public const float Gutter = 28f;

        /// <summary>The tracker's own icons. ToDoList <c>1003 #4</c>, <c>1008 #5</c> and
        /// <c>1012 #4</c> are all 24x24.</summary>
        public const float IconSize = 24f;

        /// <summary>Font size for a tracked quest's title and its objective lines. ToDoList
        /// <c>1004 #3</c>, <c>1005 #2</c> and <c>1007 #2</c> are all Axis 14.</summary>
        public const uint LineSize = Type.BodySize;

        /// <summary>Line spacing on the tracker. Every Axis 14 node in <c>1004</c>, <c>1005</c>,
        /// <c>1007</c> and <c>1013</c> sets it to 16 — two over the font size, against the eighteen
        /// and twenty-one a window uses. This is the single number that makes the readout read as a
        /// heads-up element rather than a floating window.</summary>
        public const uint LineLeading = 16u;

        /// <summary>A quest title's block: two lines at the tracker's leading plus its descender.
        /// ToDoList <c>1004 #3</c> is h=38.</summary>
        public const float TitleBlock = 38f;

        /// <summary>A single objective line's block. ToDoList <c>1007 #2</c> is h=26.</summary>
        public const float LineBlock = 26f;

        /// <summary>Font size for a count or qualifier under a line. ToDoList <c>1008 #7</c> and
        /// <c>1009 #7</c> are Axis 12.</summary>
        public const uint MetaSize = Type.SecondarySize;

        /// <summary>Line spacing for those. ToDoList <c>1008 #6</c> sets Axis 12 to 14.</summary>
        public const uint MetaLeading = Type.SecondaryLine;

        /// <summary>A meta line's block. ToDoList <c>1008 #7</c> is h=22.</summary>
        public const float MetaBlock = 22f;

        /// <summary>The blank the tracker puts between two quests. Component <c>1011</c> is a
        /// 12-tall spacer.</summary>
        public const float Spacer = 12f;

        /// <summary>The tracker's own separator bar. ToDoList <c>1011 #2</c> is h=12 and the progress
        /// bars in <c>1008</c>/<c>1009</c> are h=8; the thinner is the right model for a rule between
        /// readout lines.</summary>
        public const float RuleHeight = 8f;
    }

    /// <summary>The Main Scenario Guide's banner — the framed plate at the top of the screen that
    /// reads "Current Main Scenario Quest", and the lines the game hangs beneath it when a job quest
    /// is active. Measured from <c>ui/uld/ScenarioTree.uld</c> and from the art on
    /// <c>ui/uld/ScenarioTree.tex</c> itself.
    ///
    /// <para><b>Why the readout wears this and not a panel of our own.</b> It is the game's one
    /// always-on element that carries a <i>name</i> rather than a checklist, which is exactly what
    /// the readout carries. Everything below is the game's own arrangement of it: a dark pill above,
    /// a parchment plate, a crest hanging off the plate's left end, and subordinate lines beneath at
    /// a fixed pitch with markers hanging into the gutter to the left of the text.</para>
    ///
    /// <para><b>It is worn at the game's own size, and this is load-bearing.</b> The plate is drawn
    /// at the part's native 300x48 — what the game itself draws it at — so the readout cannot end up
    /// looking bigger than the element it is imitating when the two sit near each other: our whole
    /// box is <see cref="Width"/> = 324 against the game's own 340 root. The nine-grid stays, because
    /// the art does slice cleanly at <see cref="PlateInsetX"/> (rendered at 400, 420 and 600, all
    /// three inspected), but at 300 the slice is the identity and nothing is stretched in either
    /// axis. Vertically it could not be anyway: the longest run of identical rows in the art is two
    /// pixels.</para></summary>
    public static class Banner
    {
        /// <summary>Left edge of the plate part on the sheet. <c>ScenarioTree.uld</c> partlist 4,
        /// part 0.</summary>
        public const float PlateU = 0f;

        /// <inheritdoc cref="PlateU"/>
        public const float PlateV = 0f;

        /// <summary>The plate part's own width — what the game draws it at, and the width the
        /// nine-slice is cut from.</summary>
        public const float PlateWidth = 300f;

        /// <summary>The plate's height, which is not negotiable: the art has no stretchable band
        /// vertically.</summary>
        public const float PlateHeight = 48f;

        /// <summary>Where the plate starts inside the readout's own box — the strip of margin the
        /// emblem hangs into, which is the game's construction rather than a border of ours.
        ///
        /// <para>The game's crest does not sit <i>on</i> its plate: the plate starts at x=39 of the
        /// headline row and the crest at x=12, so four fifths of the emblem is out to the left and it
        /// costs the plate only its last 21 pixels. That is why the game can put a 52-pixel ornament
        /// on a 48-pixel bar without the quest name losing any room — and it is exactly what the
        /// first cut of this got wrong by putting the whole emblem inside the plate, where it ate 64
        /// of the plate's own width.</para>
        ///
        /// <para>Twenty-four rather than the game's 39 because the game's ornament block is 76 wide —
        /// it carries a chapter numeral in a ring, which we deliberately do not draw (see
        /// <see cref="CrestSize"/>). With <see cref="CrestLeft"/> at 1 and a 44-wide emblem the
        /// overlap comes out at 21 — the game's own number.</para></summary>
        public const float PlateLeft = 24f;

        /// <summary>The readout's whole box when it wears the banner: the emblem's margin plus the
        /// plate at its native width. 324, against the game's own 340 root.</summary>
        public const float Width = PlateLeft + PlateWidth;

        /// <summary>The nine-slice's left and right insets. Twenty-four rather than something
        /// smaller because the plate carries a small chevron near its right end, at source x=279-288,
        /// and anything under 24 slices through it.</summary>
        public const float PlateInsetX = 24f;

        /// <summary>Where the plate sits below the top of the banner — the room the header pill needs
        /// above it. <c>ScenarioTree.uld</c> puts the headline row <c>#13</c> at y=20 with the pill
        /// <c>#12</c> at y=3, so the pill overhangs the plate by seventeen and the plate covers its
        /// last seven.</summary>
        public const float PlateTop = 20f;

        /// <summary>The whole banner's height: the pill's overhang plus the plate.</summary>
        public const float Height = PlateTop + PlateHeight;

        /// <summary>The header pill's part on the sheet — partlist 4, part 3, the dark-filled state.
        /// (Part 2 at V=48 is the same pill as an unfilled gold outline; the game swaps between them
        /// by <c>PartId</c>.)</summary>
        public const float StripU = 76f;

        /// <inheritdoc cref="StripU"/>
        public const float StripV = 72f;

        /// <inheritdoc cref="StripU"/>
        public const float StripPartWidth = 116f;

        /// <inheritdoc cref="StripU"/>
        public const float StripHeight = 24f;

        /// <summary>The pill's own nine-slice insets. Declared in the ULD as <c>T=0 B=0 L=20
        /// R=20</c>.</summary>
        public const float StripInsetX = 20f;

        /// <summary>What the game draws the 116-wide pill at — node <c>#12</c> is w=230. Kept rather
        /// than scaled with the readout's width: the pill is a label's backing, not a bar, and the
        /// game's own is a fixed size over a variable-length name.</summary>
        public const float StripWidth = 230f;

        /// <summary>The pill's top edge. ULD node <c>#12</c>, y=3.</summary>
        public const float StripTop = 3f;

        /// <summary>The emblem hanging off the plate's left end. Ours is the plugin's own mark rather
        /// than the game's meteor, for the reason <see cref="WayfarerBitmap"/> gives.
        ///
        /// <para><b>Smaller than the game's 52, on purpose, and by a measurement.</b> The game's part
        /// is 52x52 but its ink only fills 47x50 of that (measured off <c>(228,50)</c>: bounding box
        /// of everything with any alpha, 90% x 96% of the part at 64% coverage — it is a ragged flame
        /// shape, not a disc). Ours is a ring that reaches the edge of its own box in both axes, so at
        /// 52 it drew a solidly bigger mark than the game's does, which is what "the crest icon is too
        /// big" was. At 44 our ink is about 43x43 — inside the game's 47x50 in both axes — and the
        /// ring's stroke is still two pixels, which is the floor for it staying a ring.</para>
        ///
        /// <para>It also fixes the ratio that matters more: with <see cref="PlateLeft"/>, a 44-wide
        /// emblem at <see cref="CrestLeft"/> overlaps the plate by 21 of its 300 — exactly what the
        /// game's crest ink overlaps its own. The plate gives up the same share of itself to an
        /// ornament that the game's does.</para></summary>
        public const float CrestSize = 44f;

        /// <summary>How far in from the readout's own left edge the emblem sits, inside the margin
        /// <see cref="PlateLeft"/> reserves for it. One, so a 44-wide emblem overlaps the plate by
        /// exactly the 21 the game's own crest ink does.</summary>
        public const float CrestLeft = 1f;

        /// <summary>How far the emblem rises above the plate's top edge. The game's sits at absolute
        /// y=15 against a plate at y=20 — a deliberate overhang, which is what makes it read as
        /// pinned <i>to</i> the plate rather than printed on it. Two rather than the game's five
        /// because ours is 44 tall against a 48 plate and the game's is 52: the same idea, scaled to
        /// leave the emblem sitting just proud of the bar rather than towering over it.</summary>
        public const float CrestRise = 2f;

        /// <summary>How much of the plate's own width the emblem costs it, which is the number the
        /// game's own arrangement is really about — see <see cref="PlateLeft"/>. Recorded so the
        /// relationship can be asserted rather than eyeballed.</summary>
        public const float CrestOverlap = CrestLeft + CrestSize - PlateLeft;

        /// <summary>The headline's inset from the plate's own left edge — <b>the game's own 24</b>
        /// (its plate is at x=39 and its text block at x=63), which is what the emblem hanging
        /// outside the plate buys back.</summary>
        public const float PlateTextInset = 24f;

        /// <summary>Where the headline's words start, measured from the readout's left edge.</summary>
        public const float HeadlineLeft = PlateLeft + PlateTextInset;

        /// <summary>What the headline gives back to the plate's right-hand cap. The game's own text
        /// box is 250 wide in a 300 plate starting at 24, which leaves 26 — and at our native plate
        /// width the headline's box comes out at exactly that same 250.</summary>
        public const float HeadlineRight = 26f;

        /// <summary>The room the name actually gets: <c>300 - 24 - 26</c>. The same 250 the game's
        /// own headline node is, so nothing we put in the bar is at more risk of truncating than the
        /// main scenario's own names already are.</summary>
        public const float HeadlineWidth = PlateWidth - PlateTextInset - HeadlineRight;

        /// <summary>The headline's own line inside the plate — <c>(48 - 18) / 2</c>, which is exactly
        /// centred, and exactly what component <c>1001 #5</c> does (y=15, h=18).</summary>
        public const float HeadlineTop = 15f;

        /// <inheritdoc cref="HeadlineTop"/>
        public const float HeadlineHeight = 18f;

        /// <summary>The headline's font. Axis 14 — the same <see cref="Type.BodySize"/> the rest of
        /// the plugin uses for a name.</summary>
        public const uint HeadlineSize = Type.BodySize;

        /// <summary>The header pill's own text. Axis 12, centred, embossed. ULD node <c>#11</c>.
        /// </summary>
        public const uint StripTextSize = Type.SecondarySize;

        /// <summary>The pitch of the subordinate lines beneath the banner. The game's two job-quest
        /// components sit at y=0 and y=26 of their container.</summary>
        public const float SubLinePitch = 26f;

        /// <summary>The marker on a subordinate line — the game's "!" quest medallion, partlist 4
        /// part 7, drawn 1:1. It is taller than the row it belongs to, and deliberately so.</summary>
        public const float MarkerSize = 32f;

        /// <inheritdoc cref="MarkerSize"/>
        public const float MarkerU = 76f;

        /// <inheritdoc cref="MarkerSize"/>
        public const float MarkerV = 96f;

        /// <summary>How far right of the headline's words a subordinate line's own words sit. In the
        /// game's root-absolute coordinates the headline text is at x=63 and a sub-line's at x=72 —
        /// near-flush, with a slight indent. This is the whole visual signature of the relationship:
        /// subordinate lines are not deeply indented, their markers are what sits to the
        /// left.</summary>
        public const float SubLineIndent = 9f;

        /// <summary>How far left of the headline's words a marker hangs. The game's sub-line icons
        /// are at x=44 against a headline text at x=63.</summary>
        public const float MarkerHang = 19f;

        /// <summary>Where a subordinate line's words start, measured from the banner's left
        /// edge.</summary>
        public const float SubLineLeft = HeadlineLeft + SubLineIndent;

        /// <summary>Where a marker's left edge sits.</summary>
        public const float MarkerLeft = HeadlineLeft - MarkerHang;

        /// <summary>The block an unmarked subordinate line gets — an annotation about the tracked
        /// thing rather than a second destination. The quest tracker's own meta block
        /// (<see cref="Hud.MetaBlock"/>), because that is what it is.</summary>
        public const float AnnotationBlock = Hud.MetaBlock;

        /// <summary>Every subordinate line's font, marked or not. Axis 12: the banner has exactly two
        /// type levels and this is the lower one.</summary>
        public const uint SubLineSize = Hud.MetaSize;

        /// <inheritdoc cref="SubLineSize"/>
        public const uint SubLineLeading = Hud.MetaLeading;
    }
}
