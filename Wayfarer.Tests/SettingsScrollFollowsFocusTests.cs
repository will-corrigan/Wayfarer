using System.Text.RegularExpressions;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The Settings tab must never put the cursor on a control the player cannot see.
///
/// <para><b>Why this exists, and why the test that shipped before it was not enough.</b> The
/// reported symptom, photographed off the owner's television, was a controller cursor walking down
/// past the bottom edge of the Settings tab while the scrollbar stayed exactly where it was: "the
/// scroll doesn't loop but the page doesn't follow the scroll still and it goes off screen". The
/// first half of that defect — the cursor looping back to the tab bar at the top — was fixed by
/// pointing the region's downward exit at nothing, and the test written for it asserted the nav
/// exit. That assertion passed while the second half of the defect was live on the player's screen,
/// because where the cursor is <i>allowed</i> to go and what is <i>visible</i> when it gets there are
/// two different claims and only the first was being made.</para>
///
/// <para>So the claim here is the visibility one, stated over the whole catalogue: focus any control
/// on the tab, at any window height the plugin can produce, and that control is wholly inside the
/// rectangle the container clips at. The stack is derived from the catalogue's own source, so a
/// setting added later is proved without anyone remembering to come back here.</para>
///
/// <para><b>And the wiring, separately.</b> Arithmetic that is never called is not a fix. The
/// version of this that shipped broken had correct arithmetic hung off <c>AtkEventType.FocusStart</c>,
/// an event the game never delivers to a pad-navigated component — so the guards at the bottom of
/// this file read the window's own source and insist it is driven by the one arrival signal in this
/// codebase that is known to work on a controller, cross-checked against the vendored toolkit node
/// that established it.</para></summary>
public class SettingsScrollFollowsFocusTests
{
    /// <summary>The tab body heights the plugin can actually produce. The window clamps itself to the
    /// viewport and then divides by the HUD scale, so a television at 200% is arithmetically a very
    /// short window — which is the case the defect was reported from. The last entry is taller than
    /// the whole catalogue, i.e. nothing scrolls at all.</summary>
    public static TheoryData<float> Viewports =>
    [
        140f, 200f, 260f, 340f, 480f, 620f, 4000f,
    ];

    /// <summary>How many modules the Features section can list. The catalogue builds one toggle per
    /// registered module, so the count is not a constant and the proof sweeps it.</summary>
    private static int[] ModuleCounts => [1, 3, 6, 10];

    [Theory]
    [MemberData(nameof(Viewports))]
    public void Walking_down_the_whole_catalogue_keeps_every_focused_control_visible(float viewport)
    {
        foreach (var modules in ModuleCounts)
        {
            var stack = SettingsStackModel.Build(modules);
            var scroll = 0f;

            foreach (var control in stack.Controls)
            {
                scroll = stack.Focus(control, viewport, scroll);
                AssertVisible(control, viewport, scroll, stack, $"walking down, {modules} modules");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Viewports))]
    public void Walking_back_up_the_whole_catalogue_keeps_every_focused_control_visible(float viewport)
    {
        foreach (var modules in ModuleCounts)
        {
            var stack = SettingsStackModel.Build(modules);

            // Start where walking down leaves the cursor: at the bottom. Walking up from the top
            // would prove nothing, because everything is already in view.
            var scroll = stack.MaxScroll(viewport);

            for (var index = stack.Controls.Count - 1; index >= 0; index--)
            {
                var control = stack.Controls[index];
                scroll = stack.Focus(control, viewport, scroll);
                AssertVisible(control, viewport, scroll, stack, $"walking up, {modules} modules");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Viewports))]
    public void The_first_control_sits_flush_at_the_top(float viewport)
    {
        foreach (var modules in ModuleCounts)
        {
            var stack = SettingsStackModel.Build(modules);
            var first = stack.Controls[0];

            var scroll = stack.Focus(first, viewport, stack.MaxScroll(viewport));

            Assert.Equal(0f, scroll, 0.01f);
        }
    }

    [Theory]
    [MemberData(nameof(Viewports))]
    public void The_last_control_sits_flush_at_the_bottom(float viewport)
    {
        foreach (var modules in ModuleCounts)
        {
            var stack = SettingsStackModel.Build(modules);
            var last = stack.Controls[^1];

            var scroll = stack.Focus(last, viewport, 0f);

            // Flush, not merely visible: any dead region below the last control is a strip of the
            // tab the player can see but never scroll to.
            Assert.Equal(stack.MaxScroll(viewport), scroll, 0.01f);
            Assert.Equal(stack.ContentHeight, Math.Min(scroll + viewport, stack.ContentHeight), 0.01f);
        }
    }

    [Theory]
    [MemberData(nameof(Viewports))]
    public void A_control_already_in_view_never_moves_the_page(float viewport)
    {
        foreach (var modules in ModuleCounts)
        {
            var stack = SettingsStackModel.Build(modules);
            var scroll = 0f;

            foreach (var control in stack.Controls)
            {
                var settled = stack.Focus(control, viewport, scroll);

                // Focusing the same control again must be a no-op, or the page jitters under the
                // cursor as it is walked down the tab.
                Assert.Equal(settled, stack.Focus(control, viewport, settled), 0.01f);
                scroll = settled;
            }
        }
    }

    [Fact]
    public void The_catalogue_the_proof_reads_is_the_catalogue_the_window_builds()
    {
        var stack = SettingsStackModel.Build(1);

        // A parser that quietly stopped finding settings would make every proof above vacuously
        // true. These are the shapes the catalogue has today: five sections, and enough settings
        // that the tab scrolls at all.
        Assert.Equal(5, stack.Sections);
        Assert.True(stack.Controls.Count >= 20, $"only {stack.Controls.Count} settings were found in the catalogue.");
        Assert.Contains(stack.Controls, control => control.Kind == SettingKind.Toggle);
        Assert.Contains(stack.Controls, control => control.Kind == SettingKind.Choice);
        Assert.Contains(stack.Controls, control => control.Kind == SettingKind.Scale);

        // And the tab really does overflow every scrolling viewport in the sweep, or the proofs
        // above would all be satisfied by a page that never has to move.
        Assert.True(
            stack.ContentHeight > 620f,
            $"the modelled settings stack is only {stack.ContentHeight} tall and would never scroll.");
    }

    [Fact]
    public void Every_control_the_settings_tab_adds_is_wired_for_scroll_follows_focus()
    {
        var window = SourceGuard.SourceOf("Wayfarer/Windows/NativeHubWindow.cs");
        var body = SourceGuard.Body(window, "private void RebuildSettings()");

        Assert.Contains("settingsArea.ContentNode.AddNode(control)", body, StringComparison.Ordinal);
        Assert.Equal(
            SourceGuard.Occurrences(body, "AddNode(control)"),
            SourceGuard.Occurrences(body, "WireScrollFollowsFocus(control"));
    }

    [Fact]
    public void Scroll_follows_focus_is_driven_by_the_arrival_signal_that_works_on_a_controller()
    {
        var window = SourceGuard.SourceOf("Wayfarer/Windows/NativeHubWindow.cs");
        var wiring = SourceGuard.Body(window, "private unsafe void WireScrollFollowsFocus(");

        // FocusStart is what shipped, and it never fired once for a pad: the arithmetic was right,
        // the call site was there, and the cursor still walked off the bottom of the window.
        Assert.DoesNotContain("FocusStart", wiring, StringComparison.Ordinal);

        Assert.Contains("AtkEventType.InputReceived", wiring, StringComparison.Ordinal);
        Assert.Contains("InputState.Up", wiring, StringComparison.Ordinal);
        Assert.Contains("InputId.UP", wiring, StringComparison.Ordinal);
        Assert.Contains("InputId.DOWN", wiring, StringComparison.Ordinal);
    }

    [Fact]
    public void The_arrival_signal_is_still_the_one_the_toolkits_own_focus_nav_node_uses()
    {
        // The precedent, read rather than remembered: NavFocusNode is what steers every row on the
        // list-backed tabs, and if the toolkit ever changes how it hears "the cursor is on me now",
        // the Settings tab has to change with it.
        var toolkit = SourceGuard.SourceOf("external/KamiToolKit/Nodes/ComponentNode/NavFocusNode.cs");

        Assert.Contains("AtkEventType.InputReceived", toolkit, StringComparison.Ordinal);
        Assert.Contains("InputId.DOWN or InputId.UP", toolkit, StringComparison.Ordinal);
        Assert.Contains("InputState.Up", toolkit, StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_is_scrolled_through_the_games_own_scrollbar_so_the_thumb_tracks_it()
    {
        var window = SourceGuard.SourceOf("Wayfarer/Windows/NativeHubWindow.cs");
        var body = SourceGuard.Body(window, "private void ScrollSettingIntoView(");

        // The reported fault was a page that did not follow the cursor while the scrollbar stayed
        // put. Moving the content node directly would fix the first half and entrench the second:
        // the thumb only tracks when the scroll goes through the component that owns both.
        Assert.DoesNotContain("ContentNode.Y", body, StringComparison.Ordinal);
        Assert.Contains("bar.ScrollPosition = target", body, StringComparison.Ordinal);
        Assert.Contains("ScrollIntoView.Adjust", body, StringComparison.Ordinal);
        Assert.Contains("settingsArea.Height", body, StringComparison.Ordinal);
        Assert.Contains("bar.ScrollMaxPosition", body, StringComparison.Ordinal);
    }

    [Fact]
    public void The_modelled_heading_height_is_the_one_the_window_draws()
    {
        // The stack model gets its heading height from SettingsLayout; the window gets it from its
        // own constant. Both are the game's section-header row, and this is what says so.
        var window = SourceGuard.SourceOf("Wayfarer/Windows/NativeHubWindow.cs");

        Assert.Contains(
            "HuntingHeaderHeight = GameMetrics.Row.SectionHeight",
            window,
            StringComparison.Ordinal);
        Assert.Equal(GameMetrics.Row.SectionHeight, SettingsLayout.HeadingHeight);
    }

    private static void AssertVisible(
        SettingsStackModel.Item control, float viewport, float scroll, SettingsStackModel stack, string what)
    {
        Assert.True(
            control.Height <= viewport,
            $"{what}: a {control.Kind} control is taller than the whole tab, which this proof cannot speak for.");

        Assert.True(
            control.Top >= scroll - 0.01f,
            $"{what}: {control.Kind} at {control.Top} is above the visible rect [{scroll}, {scroll + viewport}].");

        var below = $"{what}: {control.Kind} ending at {control.Bottom} is below the visible rect "
            + $"[{scroll}, {scroll + viewport}] of a {stack.ContentHeight}-tall stack.";

        Assert.True(control.Bottom <= scroll + viewport + 0.01f, below);
    }

    /// <summary>The Settings tab's content column, modelled from the catalogue's own source.
    ///
    /// <para><b>Why the source and not the type.</b> <c>SettingsCatalog</c> lives in the plugin
    /// assembly, which this project cannot reference — it links against the game. Reading the
    /// declarations instead keeps the one property that matters: adding a setting to the catalogue
    /// adds it to this model, and therefore to every proof above, with no second edit.</para>
    ///
    /// <para>The geometry is KamiToolKit's <c>VerticalListNode</c> under a <c>ScrollingNode</c>:
    /// items abut with <see cref="SettingsLayout.ItemSpacing"/> between them, the first sits at zero,
    /// and the content is exactly as tall as its items — so the last item's bottom edge is the
    /// content's bottom edge, which is what makes flush-at-the-bottom reachable.</para></summary>
    private sealed class SettingsStackModel
    {
        /// <summary>How long a pattern is given against a file this size before it is called a
        /// runaway. Generous: the answer either arrives in microseconds or never.</summary>
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

        private SettingsStackModel(List<Item> controls, float contentHeight, int sections)
        {
            Controls = controls;
            ContentHeight = contentHeight;
            Sections = sections;
        }

        public List<Item> Controls { get; }

        public float ContentHeight { get; }

        public int Sections { get; }

        public static SettingsStackModel Build(int moduleCount)
        {
            var code = SourceGuard.SourceOf("Wayfarer/Settings/SettingsCatalog.cs");
            var sections = SectionBuilders(code);

            var controls = new List<Item>();
            var y = 0f;
            void Place(float height, SettingKind? kind, float? headingTop)
            {
                if (kind is { } settingKind)
                {
                    controls.Add(new Item(settingKind, y, height, headingTop));
                }

                y += height + SettingsLayout.ItemSpacing;
            }

            foreach (var builder in sections)
            {
                var headingTop = y;
                Place(SettingsLayout.HeadingHeight, null, null);

                var first = true;
                foreach (var kind in Kinds(code, builder, moduleCount))
                {
                    Place(SettingsLayout.ControlHeight(kind), kind, first ? headingTop : null);
                    first = false;
                }
            }

            // VerticalListNode leaves no trailing gap: the spacing goes between items only.
            var contentHeight = Math.Max(y - SettingsLayout.ItemSpacing, 0f);
            return new SettingsStackModel(controls, contentHeight, sections.Count);
        }

        public float MaxScroll(float viewport) => Math.Max(ContentHeight - viewport, 0f);

        /// <summary>Where the container ends up when <paramref name="control"/> takes the cursor —
        /// the same call the window makes, against the same numbers.</summary>
        public float Focus(Item control, float viewport, float scroll)
        {
            var ceiling = MaxScroll(viewport);
            var target = ScrollIntoView.Adjust(control.Top, control.Height, viewport, scroll, ceiling);

            if (control.HeadingTop is { } headingTop
                && target < scroll
                && control.Bottom - headingTop <= viewport)
            {
                target = ScrollIntoView.Adjust(
                    headingTop, control.Bottom - headingTop, viewport, scroll, ceiling);
            }

            return target;
        }

        /// <summary>The section builders <c>Build()</c> composes, in order.</summary>
        private static List<string> SectionBuilders(string code)
        {
            var declaration = Region(code, "Build");
            var builders = Regex
                .Matches(
                    declaration,
                    @"new SettingSection\(""[^""]+"",\s*(?<builder>\w+)\(\)\)",
                    RegexOptions.ExplicitCapture,
                    Patience)
                .Select(match => match.Groups["builder"].Value)
                .ToList();

            Assert.NotEmpty(builders);
            return builders;
        }

        /// <summary>The setting kinds one builder yields, in order, following the builders it
        /// composes and repeating anything it generates in a loop.</summary>
        private static List<SettingKind> Kinds(string code, string builder, int moduleCount)
        {
            var region = Region(code, builder);
            var kinds = new List<SettingKind>();

            // Left to right, taking whichever comes first: a declared kind, or a call out to another
            // builder whose kinds belong at this point in the order.
            foreach (var match in Regex
                         .Matches(
                             region,
                             @"Kind = SettingKind\.(?<kind>\w+)|(?<call>Build\w+)\(\)",
                             RegexOptions.ExplicitCapture,
                             Patience)
                         .Cast<Match>())
            {
                if (match.Groups["kind"].Success)
                {
                    var kind = Enum.Parse<SettingKind>(match.Groups["kind"].Value);

                    // A builder that loops over something emits its kinds once per item. Modules are
                    // the only such list, and how many there are is not a constant.
                    var repeat = region.Contains("foreach", StringComparison.Ordinal) ? moduleCount : 1;
                    for (var i = 0; i < repeat; i++)
                    {
                        kinds.Add(kind);
                    }
                }
                else
                {
                    kinds.AddRange(Kinds(code, match.Groups["call"].Value, moduleCount));
                }
            }

            Assert.NotEmpty(kinds);
            return kinds;
        }

        /// <summary>A method's body or collection expression, bracket-matched from whichever of
        /// <c>{</c> or <c>[</c> opens it. <see cref="SourceGuard"/> can do one or the other; the
        /// catalogue has both, and the expression-bodied ones contain statement lambdas that a
        /// scan-to-the-semicolon would cut in half.
        ///
        /// <para>The method is found by its <i>declaration</i> and not by its name, because every one
        /// of these is also called by name from the builder above it — matching the first mention
        /// would read a call site and find whatever happened to follow it.</para></summary>
        private static string Region(string code, string method)
        {
            var found = Regex.Match(
                code,
                $@"(?:private|public|internal)[^\n(]*\b{Regex.Escape(method)}\(\)",
                RegexOptions.None,
                Patience);
            Assert.True(found.Success, $"'{method}' is no longer declared in the catalogue this proof reads.");

            var at = found.Index;
            var brace = code.IndexOf('{', at);
            var bracket = code.IndexOf('[', at);
            var open = brace >= 0 && (bracket < 0 || brace < bracket) ? brace : bracket;
            Assert.True(open >= 0, $"'{method}' has no body.");

            var (opener, closer) = code[open] == '{' ? ('{', '}') : ('[', ']');
            var depth = 0;
            for (var i = open; i < code.Length; i++)
            {
                depth += code[i] == opener ? 1 : code[i] == closer ? -1 : 0;
                if (depth == 0)
                {
                    return code[open..(i + 1)];
                }
            }

            Assert.Fail($"'{method}' has an unterminated body.");
            return string.Empty;
        }

        /// <summary>One control in the modelled column. <see cref="HeadingTop"/> is set only on the
        /// first control of a section — the one whose section heading is scrolled in with it.
        /// </summary>
        internal sealed record Item(SettingKind Kind, float Top, float Height, float? HeadingTop)
        {
            public float Bottom => Top + Height;
        }
    }
}
