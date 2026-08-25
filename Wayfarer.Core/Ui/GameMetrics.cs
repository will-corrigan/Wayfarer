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

        /// <summary>The second line's own right-hand caption column — the rail the state sits in when
        /// its shape could not be drawn.
        ///
        /// <para>Wider than <see cref="TrailingWidth"/> because the two columns carry different
        /// things. The game has both widths in the same list: the entry row's caption
        /// (Journal <c>1023 #4</c>, ContentsFinder <c>1026 #19</c>) is 48 and holds a numeral, while
        /// the section row's (ContentsFinder <c>1024 #3</c>) is 64 and holds a word. A word is what
        /// goes here, so it is the wider of the two the game itself uses.</para></summary>
        public const float StatusWidth = Detail.KindWidth;

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

        /// <summary>Where a 20px status marker sits on a 48-tall row: centred over both lines.
        /// </summary>
        public const float EntryIconTop = (EntryHeight - IconSize) / 2f;

        /// <summary>A creature portrait, not a status marker. MonsterNoteBook <c>1017 #3</c> — the
        /// Hunting Log's own monster row — is 48x48, and that row (<c>1017 #1</c>) is 48 tall, which
        /// is already <see cref="EntryHeight"/>. The portrait therefore fills the row's full height
        /// rather than being centred in it.
        ///
        /// <para>Wayfarer drew these 63xxx portraits in the 20px status slot, which is what "the
        /// hunting log has no images" looked like once the ids themselves started resolving: a
        /// 48x48 piece of creature art sampled down into a marker's box.</para></summary>
        public const float PortraitSize = EntryHeight;

        /// <summary>Inset from the row's left edge to the portrait. MonsterNoteBook <c>1017 #3</c>
        /// is at x=6.</summary>
        public const float PortraitPadding = 6f;

        /// <summary>The left edge of a portrait row's text. MonsterNoteBook <c>1017 #4</c> — the
        /// creature's name — is at x=56, which is the portrait's own 6 plus its 48 plus two.</summary>
        public const float PortraitTextLeft = 56f;
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

        /// <summary>A canvas section's heading band.
        ///
        /// <para>The glyph's height, not the text's. JournalCanvas puts a 24x24 disc at the
        /// section's left edge (<c>#6</c>/<c>#10</c>/<c>#19</c>/<c>#28</c>) beside a 22-tall heading,
        /// and the game then leaves 44 pixels before the section's body. A band sized to the text
        /// instead — <see cref="Detail.HeadingHeight"/>, 20 — leaves four pixels of that disc hanging
        /// over whatever is drawn under it, which at the reward section is the tray.</para></summary>
        public const float SectionHeadingHeight = GlyphSize;

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

        /// <summary>The page's own parchment. JournalDetail <c>#54</c> is a <c>NineGrid</c> on part
        /// list 9 part 4 — <c>Journal_Detail.tex</c> (376,28) 96x96 — with corner offsets
        /// T20 B28 L24 R24, drawn at 496x618 under everything else on the page. This is the surface
        /// the gilt frame is nailed to, and the reason the page reads as paper.</summary>
        public const float ParchmentPartSize = 96f;

        /// <inheritdoc cref="ParchmentPartSize"/>
        public const float ParchmentTopOffset = 20f;

        /// <inheritdoc cref="ParchmentPartSize"/>
        public const float ParchmentBottomOffset = 28f;

        /// <inheritdoc cref="ParchmentPartSize"/>
        public const float ParchmentSideOffset = 24f;

        /// <summary>The journal's own section divider — the rule the page draws under its title and
        /// above its buttons. JournalDetail <c>#39</c> and <c>#48</c> both draw part list 9 part 0,
        /// <c>Journal_Detail.tex</c> (0,24) 392x4, at 382 wide. A plain image, not a nine-grid: the
        /// game never stretches it.</summary>
        public const float DividerWidth = 392f;

        /// <inheritdoc cref="DividerWidth"/>
        public const float DividerHeight = 4f;

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

        /// <inheritdoc cref="ParchmentPartSize"/>
        public static readonly (float U, float V) Parchment = (376f, 28f);

        /// <inheritdoc cref="DividerWidth"/>
        public static readonly (float U, float V) Divider = (0f, 24f);
    }

    /// <summary>The Journal's ornate gilt border, node for node.
    ///
    /// <para><b>Why it can be worn now.</b> The frame is assembled by JournalDetail's node
    /// <c>#11</c> — a <c>Res</c> at (0,20) sized 496x628 — out of sixteen children (<c>#13</c>–
    /// <c>#28</c>) at hard-coded positions on part list 10, which is
    /// <c>ui/uld/Journal_Frame.tex</c>. Fourteen of them are plain <c>Image</c> nodes drawn at the
    /// size their art is authored at; only two, the vertical rails <c>#13</c>/<c>#14</c>, are
    /// <c>NineGrid</c>s that stretch. So the assembly is authored for <b>one width and one width
    /// only — 496</b>, and the earlier survey was right to refuse to stretch it across a
    /// variable-width window. A window that simply <i>is</i> 496 wide has no such problem: the
    /// horizontal run is used exactly as authored, and the rails carry every height difference,
    /// which is precisely what the game does with them.</para>
    ///
    /// <para><b>The mirror.</b> Five pieces are drawn twice, the second time with the image node's
    /// own <c>FlipH</c> flag — the game sets it on <c>#23</c>/<c>#24</c>/<c>#25</c>/<c>#26</c>/
    /// <c>#27</c>. That is why a 240x192 sheet holding one corner, one edge run and one boss can
    /// close a border on all four sides.</para></summary>
    public static class JournalFrame
    {
        public const string Texture = "ui/uld/Journal_Frame.tex";

        /// <summary>The width the assembly is authored for. JournalDetail <c>#11</c> is 496 wide and
        /// its horizontal pieces tile that width exactly: 0..56 corner, 56..160 run, 160..208 bar,
        /// 208..288 centre boss, 288..336 bar, 336..440 run, 440..496 corner.</summary>
        public const float Width = 496f;

        /// <summary>The frame's authored height. JournalDetail <c>#11</c> is 628 tall.</summary>
        public const float AuthoredHeight = 628f;

        /// <summary>Everything above the stretching rails. <c>#14</c> (the left rail) starts at
        /// y=192.</summary>
        public const float TopFixed = 192f;

        /// <summary>Everything below them. <c>#14</c> is 340 tall and ends at y=532, which is 96
        /// short of the frame's 628.</summary>
        public const float BottomFixed = 96f;

        /// <summary>The shortest frame that can be assembled without the fixed top and bottom
        /// overlapping — the rails go to zero here and the border still closes.</summary>
        public const float MinHeight = TopFixed + BottomFixed;

        /// <summary>The rails' own width and the inner edge they leave. <c>#14</c> is 32 wide at x=0
        /// and <c>#13</c> is 32 wide at x=464, so the border eats 32 pixels on each side.</summary>
        public const float RailWidth = 32f;

        /// <summary>Where the parchment starts inside the frame. JournalDetail draws the frame group
        /// at y=20 and the parchment <c>#54</c> at y=30, so the paper begins ten pixels down the
        /// border and runs to its foot.</summary>
        public const float ParchmentTop = 10f;

        /// <summary>The top of the title band, in the frame's own space. JournalDetail's level badge
        /// group <c>#8</c> is at y=62 and its title <c>#38</c> at y=72 in a root whose frame starts
        /// at y=20 — so the band opens 42 pixels down the border.</summary>
        public const float TitleTop = 42f;

        /// <summary>The rule under the title. JournalDetail <c>#39</c> is at y=120, which is 100
        /// inside the frame — and that is exactly <see cref="TitleTop"/> plus the band's own
        /// <see cref="Journal.PageTitleHeight"/> plus a rule gap either side, so the two derivations
        /// agree.</summary>
        public const float TitleRuleTop = 100f;

        /// <summary>The top of the body. JournalDetail's body group <c>#40</c> is at y=128, which is
        /// 108 inside the frame.</summary>
        public const float BodyTop = 108f;

        /// <summary>How far the button row's foot sits above the frame's own foot. JournalDetail's
        /// button row <c>#49</c> is at y=584 and 28 tall, so it ends at 612 in a root whose frame
        /// ends at 648.</summary>
        public const float ButtonBottomInset = 36f;

        /// <summary>How far the footer rule sits above the frame's foot. JournalDetail <c>#48</c> is
        /// at y=568, which is 80 above the frame's 648.</summary>
        public const float FooterRuleBottomInset = 80f;

        /// <summary>How far the page overlaps the list window it opens beside.
        ///
        /// <para><c>Journal.uld</c>'s node <c>#9</c> — the empty <c>Res</c> that reserves the detail
        /// page's rectangle — is at (450,-40) in a root whose list panel (<c>#39</c>) is 462 wide. So
        /// the page starts twelve pixels <i>inside</i> the list's right edge and forty above its top:
        /// a deliberate overlap, which is what lets the border's ornament cross the seam instead of
        /// leaving a gap between two windows.</para></summary>
        public const float BesideOverlapX = 12f;

        /// <inheritdoc cref="BesideOverlapX"/>
        public const float BesideOverlapY = 40f;

        /// <summary>The gold rivet at the foot of the page, beside the button row.
        ///
        /// <para>Part list 10's part 7 — <c>Journal_Frame.tex</c> (200,152) 40x40, a small round
        /// boss. It is the one piece of the border sheet <c>JournalDetail</c> never places, and the
        /// extracted texture shows it plainly: a gold ring with a dark centre, authored to sit at the
        /// end of a run. It goes where the game puts its own 28x28 button (<c>#53</c>, at (414,582) —
        /// the chat-log icon in the player's screenshot), because that is the slot the layout leaves
        /// and because an ornament is the honest thing to put there: the button's job on the game's
        /// page has no counterpart on ours, and inventing one would be a control nobody asked
        /// for.</para></summary>
        public const float BossSize = 40f;

        /// <inheritdoc cref="BossSize"/>
        public static readonly (float U, float V) Boss = (200f, 152f);

        /// <summary>The page's content column, and where the frame's own numbers put it.
        ///
        /// <para>JournalDetail's canvas <c>#43</c> is 394 wide at x=42 — the authored
        /// <see cref="Journal.SectionWidth"/> — leaving 60 to the right edge rather than 42. The
        /// asymmetry is the scroll bar: <c>#2</c> is a 20-wide bar at x=458, and the page reserves
        /// its gutter. This window has no scroll bar, so the same authored column is centred
        /// instead, which is the one deliberate deviation on this surface and is stated rather than
        /// hidden.</para></summary>
        public static float ColumnLeft => (Width - Journal.SectionWidth) / 2f;
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

        /// <summary>The pitch of a subordinate line that carries a marker. The quest tracker's own
        /// icon-bearing line, <c>ToDoList 1008</c>, is h=22 around a 24x24 icon (<c>1008 #5</c>) —
        /// and this is the same construction with the "!" medallion in place of that icon.
        ///
        /// <para>It was 26, taken from ScenarioTree's two job-quest components at y=0 and y=26. Those
        /// are components in a <i>window</i> plate, and the readout is a heads-up element: the
        /// tracker's register is the right one, and it is tighter. See
        /// <see cref="AnnotationBlock"/> for the same mistake in its more expensive form.</para></summary>
        public const float SubLinePitch = Hud.MetaBlock;

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

        /// <summary>The transparent margin on the right of the medallion's art, proved by the game's
        /// own coordinates: the icon block is at x=44 and is 32 wide, so it ends at x=76 — four pixels
        /// past the sub-line text that starts at x=72. The game would not draw its own "!" over its
        /// own words, so those four pixels are authored emptiness inside the block.
        ///
        /// <para>It exists as a number because the geometry proofs need it. "The gutter never reaches
        /// the words" is only true of the ink, and the block is what has coordinates — so the proof
        /// compares the block less this margin, rather than being weakened to a vague
        /// tolerance.</para></summary>
        public const float MarkerArtMargin = MarkerSize - MarkerHang - SubLineIndent;

        /// <summary>Where a subordinate line's words start, measured from the banner's left
        /// edge.</summary>
        public const float SubLineLeft = HeadlineLeft + SubLineIndent;

        /// <summary>Where a marker's left edge sits.</summary>
        public const float MarkerLeft = HeadlineLeft - MarkerHang;

        /// <summary>The block an unmarked subordinate line gets — an annotation about the tracked
        /// thing rather than a second destination. Almost every line on the readout is one of these:
        /// the objective, the distance, the zone, the travel advice, the hunting summary.
        ///
        /// <para><b>This is the number that made the readout read as too spread out.</b> It was
        /// <see cref="Hud.MetaBlock"/>, 22, measured off <c>ToDoList 1008 #7</c>. That measurement is
        /// correct and was applied to the wrong thing: 22 is the height of a tracker <i>component</i>,
        /// a box that houses a 24x24 icon (<c>1008 #5</c>) and an 8-tall progress bar (<c>1008 #2</c>)
        /// as well as its words. Our line is a bare row of text, so 22 bought eight pixels of nothing
        /// under every line of the readout.</para>
        ///
        /// <para>The distance the tracker actually puts between two consecutive rows of Axis-12 text
        /// is its own line spacing, and it sets that to 14: <c>ToDoList 1008 #6</c> is
        /// <c>FontSize=12, LineSpacing=14</c> — see <see cref="Hud.MetaLeading"/>. That is a line's
        /// pitch. 22 was a component's height.</para></summary>
        public const float AnnotationBlock = Hud.MetaLeading;

        /// <summary>Every subordinate line's font, marked or not. Axis 12: the banner has exactly two
        /// type levels and this is the lower one.</summary>
        public const uint SubLineSize = Hud.MetaSize;

        /// <inheritdoc cref="SubLineSize"/>
        public const uint SubLineLeading = Hud.MetaLeading;

        /// <summary>The settings cog's side. Sized against the heading it sits beside rather than
        /// against the readout: it is a mark on the pill's line, not a button on a panel.
        ///
        /// <para><b>Chosen, not measured.</b> Alone in this file it cites no <c>.uld</c>, no node
        /// number and no component id, because there is nothing to cite: the game draws no cog at
        /// this size beside a heading of this size, so there was no measurement to take. 13 is one
        /// less than the <see cref="Type.BodySize"/> of the heading it sits on, which is what keeps
        /// it reading as a mark on that line rather than as a control of its own — a cog the same
        /// height as the words competes with them, and the next size down disappears. Anything that
        /// reads better on a screen is a better value than this one; that is what "chosen" means
        /// here.</para></summary>
        public const float CogSize = 13f;
    }
}
