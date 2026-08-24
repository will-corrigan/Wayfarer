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

    /// <summary>The Journal's own page vocabulary — the section glyphs, the level badge and the
    /// reward tray. Measured from <c>ui/uld/JournalDetail.uld</c> and its
    /// <c>AtkComponentJournalCanvas</c> (component <c>1010</c>), which is the game's own answer to
    /// "tell me what this thing is and what it gives you".
    ///
    /// <para>Everything here is a <b>page</b> measurement. The list side needs none of it: every
    /// number a list row is built from is already in <see cref="Row"/>, measured from the same
    /// file.</para></summary>
    public static class Journal
    {
        /// <summary>The page's inner column. JournalDetail <c>#45</c> (the banner), <c>#46</c> (the
        /// prose) and the canvas's own banner and reward tray are all authored at 376 inside a
        /// 496-wide page.</summary>
        public const float ColumnWidth = 376f;

        /// <summary>The inset from a canvas section's left edge to the art inside it. JournalCanvas
        /// <c>#4</c> (the banner) and <c>#31</c> (the reward tray) are both at x=18, with the
        /// section's 24x24 glyph at x=0 and its heading at x=22.</summary>
        public const float SectionInset = 18f;

        /// <summary>A canvas section's own width. JournalCanvas <c>#2</c> (the banner section) and
        /// <c>#26</c> (the reward section) are both 394 — the authored 376 column plus the inset in
        /// front of it.</summary>
        public const float SectionWidth = ColumnWidth + SectionInset;

        /// <summary>The banner. JournalDetail <c>#45</c> and JournalCanvas <c>#4</c> are both
        /// 376x120, and so is every piece of art the game puts in that slot: measured against the
        /// live install, all 2,519 non-zero <c>Quest.Icon</c> rows and all 773 non-zero
        /// <c>ContentFinderCondition.Image</c> rows are exactly 376x120, with no file missing.
        /// </summary>
        public const float BannerWidth = ColumnWidth;

        /// <inheritdoc cref="BannerWidth"/>
        public const float BannerHeight = 120f;

        /// <summary>The page title's own block. JournalDetail <c>#38</c> is 340x50 — two Axis-18
        /// lines at leading 20, which is twice <see cref="Detail.TitleHeight"/>.</summary>
        public const float PageTitleHeight = Detail.TitleHeight * 2f;

        /// <summary>The caption beside the page title.
        ///
        /// <para>One JournalDetail button wide (<c>#50</c>–<c>#52</c> are 120), which is the only
        /// horizontal module that page has. The strip's caption column is
        /// <see cref="Detail.KindWidth"/> — 64, from ContentsFinder <c>1024 #3</c>, measured in a
        /// 462-wide list — and at Axis 12 that ellipsises "Alliance Raid". A 730-wide page does not
        /// have to.</para></summary>
        public const float KindWidth = Control.ButtonWidthMedium;

        /// <summary>The footnote line under the page's body. JournalCanvas <c>#54</c> is 368x22,
        /// Axis 12 at line spacing 12, centred — the register the game reserves for a caveat.
        /// </summary>
        public const float FootnoteHeight = 22f;

        /// <summary>Narrowest the page's second column may be before the page gives up on two
        /// columns and stacks everything into one.
        ///
        /// <para>Derived rather than measured, and said so: the game's own journal is one fixed
        /// width and never has to answer this. Half the authored column is the threshold because a
        /// text column narrower than that beside a 376-wide one reads as a gutter rather than as a
        /// column, and stacking is the honest fallback — the same "drop it whole rather than draw it
        /// wrong" rule the rest of the layout follows.</para></summary>
        public const float MinTextColumn = ColumnWidth / 2f;

        /// <summary>A section glyph. JournalCanvas <c>#6</c>/<c>#10</c>/<c>#19</c>/<c>#28</c> are all
        /// 24x24 at x=0 — the open book over Description, the document over Requirements, the
        /// treasure chest over Reward.</summary>
        public const float GlyphSize = 24f;

        /// <summary>Where a section's heading text starts, past its glyph. JournalCanvas
        /// <c>#7</c>/<c>#11</c>/<c>#20</c>/<c>#29</c> are all at x=22 — two pixels under the glyph's
        /// right edge, because the glyph art carries its own transparent margin.</summary>
        public const float GlyphTextLeft = 22f;

        /// <summary>The reward tray, one row of slots. <c>Journal_Detail.tex</c> (0,28) 376x52,
        /// drawn by JournalCanvas <c>1013 #2</c>. It is a plain image in the game, not a nine-grid:
        /// it is drawn at its authored width and never stretched.</summary>
        public const float TrayHeight = 52f;

        /// <summary>The tray's left inset to the first slot. JournalCanvas <c>#34</c>/<c>#35</c>/
        /// <c>#36</c> — all three slot templates — are authored at x=15.</summary>
        public const float TrayInset = 15f;

        /// <summary>A reward icon. JournalCanvas <c>1012 #3</c> is 36x36.</summary>
        public const float SlotIconSize = 36f;

        /// <summary>Where that icon sits inside its slot. JournalCanvas <c>1012 #3</c> is at y=8 in
        /// a 44-tall slot.</summary>
        public const float SlotIconTop = 8f;

        /// <summary>The level badge's black disc. JournalDetail <c>#10</c>,
        /// <c>Journal_Detail.tex</c> (420,124) 40x40, with <c>#9</c>'s TrumpGothic 20 centred over
        /// it.</summary>
        public const float BadgeSize = 40f;

        /// <summary>The face of the number on that badge. JournalDetail <c>#9</c> is TrumpGothic 20,
        /// centred, with an edge.</summary>
        public const uint BadgeTextSize = 20u;
    }

    /// <summary>Where in <c>ui/uld/Journal_Detail.tex</c> each piece of the journal's page art
    /// lives. Coordinates and sizes both, because an image node samples a rectangle: give it the
    /// right origin and the wrong size and it draws a band of nothing.</summary>
    public static class JournalArt
    {
        public const string Texture = "ui/uld/Journal_Detail.tex";

        /// <summary>The document-with-lines disc. Part list 6 part 6; the game puts it over
        /// Information, Requirements and Summary.</summary>
        public static readonly (float U, float V) GlyphDocument = (24f, 0f);

        /// <summary>The person silhouette. Part 17, and the obvious glyph for a line that names a
        /// quest giver — which is what the journal's own Information section holds.</summary>
        public static readonly (float U, float V) GlyphPerson = (144f, 0f);

        /// <summary>The treasure chest. Part 7, and the game's own marker for Reward.</summary>
        public static readonly (float U, float V) GlyphReward = (48f, 0f);

        /// <summary>The open book. Part 8, over Description.</summary>
        public static readonly (float U, float V) GlyphDescription = (72f, 0f);

        /// <summary>The one-row reward tray: a flat dark rounded panel with a fine noise texture and
        /// a 1px bevel, authored at 376x52.</summary>
        public static readonly (float U, float V) TrayOneRow = (0f, 28f);

        /// <summary>The level badge disc, 40x40.</summary>
        public static readonly (float U, float V) LevelBadge = (420f, 124f);
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
}
