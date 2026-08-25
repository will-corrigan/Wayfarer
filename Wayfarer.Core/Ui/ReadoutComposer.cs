using Wayfarer.Core.Navigation;

namespace Wayfarer.Core.Ui;

/// <summary>Turns the published guidance snapshot into the exact lines the readout draws.
///
/// This exists as pure, tested code because of a specific complaint: with a hunt running, the
/// readout also showed the followed quest, in the same weight, so there was no way to tell which
/// one the arrow was pointing at. The rules that fix it are invariants, not styling, and they are
/// enforced here rather than in whichever presentation happens to be drawing:
///
/// <list type="number">
/// <item><description><b>One heading, naming the active mode.</b> The heading is the mode
/// indicator; there is no separate badge and no second place to look.</description></item>
/// <item><description><b>One arrow.</b> It follows whatever the snapshot says is active. Nothing
/// else in the readout gets a direction indicator.</description></item>
/// <item><description><b>No competing peers.</b> While an explicit mode is engaged, the quest the
/// player happens to be on is not shown at all. Showing it — even demoted — was the "which one is
/// this pointing at?" confusion this exists to remove: the player asked for the arrow to follow a
/// hunt or a route, and everything above the rule belongs to that mode.</description></item>
/// <item><description><b>Nothing appears twice.</b> A hunting summary is emitted only when the
/// hunt is not already the primary objective.</description></item>
/// <item><description><b>A subordinate line has a glyph if and only if it has an action.</b> One of
/// the game's own bitmap-font icons inside a line's words is the readout's only way of saying "this
/// can be pressed", so the two are handed out together or not at all — see <see cref="Pressable"/>,
/// which is the only place either is set. A glyph on a line that does nothing is a promise the
/// readout does not keep; that is why an objective inside a duty the player has not unlocked keeps
/// its words and loses its mark, rather than showing a duty icon that refuses to queue. The subject
/// line is outside this rule because it is the banner's plate rather than a line of the block — see
/// <see cref="ReadoutLine.Glyph"/>.</description></item>
/// </list></summary>
public static class ReadoutComposer
{
    /// <summary>How many "available here" unlocks the readout will name at once. The service that
    /// supplies them already keeps to the nearest few, but the budget is enforced here as well
    /// because it is a property of the readout — legibility at TV distance — rather than of the
    /// unlock scan, and this is where it can be tested.</summary>
    public const int MaxNearbyUnlockLines = 3;

    /// <summary>What the banner's header pill says when nothing is being followed, and the fallback
    /// whenever the active source has not named itself. The plugin's own name is the honest answer to
    /// "what is this element?" when the answer to "what is it tracking?" is nothing.</summary>
    public const string PluginName = "Wayfarer";

    /// <summary>The word the header pill puts in front of the module's own name. Kept here rather
    /// than at either end of the sentence it makes: the module supplies "Quest", the pill says
    /// "Current Quest", and neither half is complete on its own.</summary>
    private const string CurrentlyTracking = "Current";

    /// <summary>What pressing the hunting summary does, and for now it is nothing.
    ///
    /// <para><b>It is a named seam rather than a literal because of the glyph rule.</b> The line
    /// deserves the game's own monster mark — the player asked why it had an aetheryte crystal beside
    /// it, and the honest answer was that the crystal was the only glyph the readout had. But a glyph
    /// is the readout's way of saying "press this" (see <see cref="Pressable"/>), so the mark cannot
    /// go on until the press does. Give this a real action and the mark appears with it; leave it
    /// alone and the line stays correctly bare. One edit, both halves.</para></summary>
    private const ReadoutLineAction HuntingLineAction = ReadoutLineAction.None;

    public static ReadoutContent Compose(ReadoutInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var state = inputs.State;
        if (string.Equals(state.Mode, NavigationState.Modes.Hidden, StringComparison.Ordinal))
        {
            return ReadoutContent.Empty;
        }

        var lines = new List<ReadoutLine>();
        AddHeading(lines, state);
        AddObjective(lines, state);
        var (showArrow, x, y, z) = AddRoute(lines, inputs);
        AddContext(lines, inputs);

        return lines.Count == 0
            ? ReadoutContent.Empty
            : new ReadoutContent(lines, showArrow, x, y, z, inputs.Elevation, StripLabel(state));
    }

    /// <summary>The header pill's words: "Current" plus whatever the active module calls itself, or
    /// the plugin's own name when no module owns the arrow.
    ///
    /// <para><b>Why the module's name and not its mode label.</b> The mode label already exists
    /// (<see cref="NavigationState.SourceLabel"/>) and names the objective's context — "Main
    /// Scenario", "Hunting Log - Warrior". The pill is a different statement: it names the element
    /// itself, the way the game's own pill says "Current Main Scenario Quest" above whichever quest
    /// happens to be in the plate. So it takes the module's name, which the module supplies, and
    /// nothing here or downstream ever maps a source id to a word.</para>
    ///
    /// <para>The route position rides along, because that is where it already was: it used to be
    /// parenthesised onto the mode heading, and the pill is what that heading became.</para></summary>
    private static string StripLabel(NavigationState state)
    {
        if (state.SourceName is not { Length: > 0 } module)
        {
            return PluginName;
        }

        var label = $"{CurrentlyTracking} {DisplayNames.TitleCase(module)}";
        return state.RouteStop is { } stop && state.RouteTotal is { } total
            ? $"{label} ({stop} of {total})"
            : label;
    }

    private static void AddHeading(List<ReadoutLine> lines, NavigationState state)
    {
        var label = state.SourceLabel is { Length: > 0 } source
            ? source
            : string.Equals(state.Mode, NavigationState.Modes.Idle, StringComparison.Ordinal) ? "Wayfarer" : "Guidance";

        // "Stop 3 of 11" belongs on the mode line, not on the objective: it describes the plan the
        // mode is executing, and putting it here is what makes an ordered chain legible at a glance.
        // Parenthesised rather than dash-separated because the heading may already carry a dash
        // ("Hunting Log - Warrior"), and because the em dash that used to be here is not a character
        // the heading font can be relied on to draw — see HeadingText.
        if (state.RouteStop is { } stop && state.RouteTotal is { } total)
        {
            label = $"{label} ({stop} of {total})";
        }

        // The last thing the heading passes through, so nothing downstream can reintroduce a glyph
        // Trump Gothic cannot draw.
        lines.Add(new ReadoutLine(HeadingText.Plain(label), ReadoutEmphasis.Heading));
    }

    private static void AddObjective(List<ReadoutLine> lines, NavigationState state)
    {
        // Idle is still a subject: "No quest followed" is precisely the line a player wants the
        // switcher beside, and it is the only line the idle readout has.
        if (string.Equals(state.Mode, NavigationState.Modes.Idle, StringComparison.Ordinal))
        {
            lines.Add(new ReadoutLine(
                "No quest followed", ReadoutEmphasis.Muted, Separated: false, ReadoutLineAction.None, Subject: true));
            return;
        }

        var headline = state.QuestName is { Length: > 0 } name ? name : "Current objective";
        if (state.ProgressText is { Length: > 0 } progress)
        {
            headline = $"{headline}  {progress}";
        }

        // Marked as the door to the journal only when there is a quest row behind the name. A hunt
        // and a bare "Current objective" have no journal entry, and marking them would put a hand
        // cursor over words that then did nothing.
        var action = state.QuestId is > 0 ? ReadoutLineAction.OpenJournal : ReadoutLineAction.None;
        lines.Add(new ReadoutLine(headline, ReadoutEmphasis.Primary, Separated: false, action, Subject: true));

        if (state.StepLabel is { Length: > 0 } step
            && !string.Equals(step, state.QuestName, StringComparison.OrdinalIgnoreCase))
        {
            lines.Add(new ReadoutLine(step, ReadoutEmphasis.Secondary));
        }
    }

    /// <summary>The only place a readout line is given a glyph or an action, and it gives both or
    /// neither.
    ///
    /// <para><b>This is the readout's whole affordance vocabulary in one expression.</b> A line's
    /// glyph is the only thing on it that says it can be pressed, so the two cannot be handed out
    /// separately: an unpressable line with a mark invites a press that does nothing, and a pressable
    /// line without one hides the press entirely. Callers therefore say what the line WOULD do and
    /// which mark WOULD suit it, and a caller whose action came back
    /// <see cref="ReadoutLineAction.None"/> gets a plain line rather than having to remember to strip
    /// the glyph itself.</para>
    ///
    /// <para>That asymmetry is deliberate: forgetting to pass an action is a line that reads as
    /// ordinary prose, which is safe. Forgetting to strip a glyph is a lie about what the line does,
    /// which is the defect this exists to make unrepresentable.</para></summary>
    private static ReadoutLine Pressable(
        string text,
        ReadoutEmphasis emphasis,
        ReadoutLineAction action,
        DtrGlyph glyph,
        int glyphAt = 0,
        bool separated = false) =>
        action == ReadoutLineAction.None
            ? new ReadoutLine(text, emphasis, separated)
            : new ReadoutLine(text, emphasis, separated, action, Glyph: glyph, GlyphAt: glyphAt);

    private static (bool ShowArrow, float? X, float? Y, float? Z) AddRoute(List<ReadoutLine> lines, ReadoutInputs inputs)
    {
        var state = inputs.State;
        return state.Mode switch
        {
            NavigationState.Modes.SameZone => AddSameZone(lines, inputs),
            NavigationState.Modes.OtherZone => AddOtherZone(lines, inputs),
            NavigationState.Modes.DutyObjective => AddDuty(lines, state),
            _ => AddReasonOnly(lines, state),
        };
    }

    /// <summary>The objective is inside instanced content, so the line names the duty and — when the
    /// player has unlocked it — queues for it.
    ///
    /// <para><b>Unlocked: the name alone, with the duty mark in front of it.</b> The prose it replaces
    /// was "Complete the duty: Halatali", which spent two thirds of a narrow line telling the player
    /// something the mark says at a glance and the readout's whole existence already implies. The
    /// aetheryte line has the same shape — a mark and a place name — and this is that shape applied to
    /// the one other thing on the readout that is a destination you enter rather than walk to.</para>
    ///
    /// <para><b>Not unlocked: the words, whole, and no mark.</b> There is nothing to queue for, and
    /// the sentence has something to say that no icon can — that the content has to be unlocked
    /// first. Keyed on <see cref="NavigationState.DutyContentFinderConditionId"/>, which is the
    /// game's own answer to whether this duty can be entered, rather than on anything about the
    /// wording.</para>
    ///
    /// <para>No arrow either way: an instanced territory has no aetheryte and no entrance to point
    /// at, which is the reason <see cref="DutyObjectiveGuidance"/> exists at all.</para></summary>
    private static (bool ShowArrow, float? X, float? Y, float? Z) AddDuty(
        List<ReadoutLine> lines, NavigationState state)
    {
        if (DutyObjectiveGuidance.DutyName(state.Reason) is not { Length: > 0 } duty)
        {
            return AddReasonOnly(lines, state);
        }

        if (state.DutyContentFinderConditionId is null)
        {
            lines.Add(new ReadoutLine(state.Reason!, ReadoutEmphasis.Secondary));
            return (false, null, null, null);
        }

        lines.Add(Pressable(
            duty, ReadoutEmphasis.Secondary, ReadoutLineAction.OpenDutyFinder, DtrGlyph.Duty));
        return (false, null, null, null);
    }

    private static (bool ShowArrow, float? X, float? Y, float? Z) AddSameZone(List<ReadoutLine> lines, ReadoutInputs inputs)
    {
        var state = inputs.State;
        if (state.TargetX is not { } tx || state.TargetZ is not { } tz)
        {
            return AddReasonOnly(lines, state);
        }

        // Inside a search-area objective's circle, the centre coordinate is no longer a place to
        // walk to — it never was the objective, only the middle of where the game says to look. An
        // arrow still pointing at it a few yalms away would be the same confident-but-wrong
        // precision this feature exists to remove, so the arrow is suppressed entirely rather than
        // dimmed: there is no more useful direction to give than "look around you", and the readout
        // already has precedent for no arrow when there is nothing precise to point at (see
        // AddOtherZone's teleport-only case).
        if (inputs.AreaHint == SearchAreaHint.Inside)
        {
            lines.Add(new ReadoutLine("You're in the search area — look around", ReadoutEmphasis.Primary));
            return (false, null, null, null);
        }

        AddDistance(lines, inputs);

        if (state.AethernetExitName is { Length: > 0 } exit)
        {
            // The arrow already points at the entry shard in this case, so say so rather than
            // leaving the player wondering why it points away from the objective.
            lines.Add(new ReadoutLine(EntryShard(state.AethernetEntryName), ReadoutEmphasis.Secondary));
            lines.Add(new ReadoutLine($"Aethernet to {exit}", ReadoutEmphasis.Secondary));
        }

        return (true, tx, state.TargetY, tz);
    }

    private static (bool ShowArrow, float? X, float? Y, float? Z) AddOtherZone(List<ReadoutLine> lines, ReadoutInputs inputs)
    {
        var state = inputs.State;
        var hasEntrance = state.EntranceX is { } ex && state.EntranceZ is { } ez;

        if (hasEntrance)
        {
            AddDistance(lines, inputs);
            if (state.AethernetExitName is { Length: > 0 } exit)
            {
                lines.Add(new ReadoutLine(EntryShard(state.AethernetEntryName), ReadoutEmphasis.Secondary));
                lines.Add(new ReadoutLine($"Aethernet to {exit}{Remaining(state)}", ReadoutEmphasis.Secondary));
            }
            else
            {
                lines.Add(new ReadoutLine($"Through {state.EntranceName}{Remaining(state)}", ReadoutEmphasis.Secondary));
            }
        }

        AddTeleportAdvice(lines, inputs, hasEntrance);

        // One place name, said once — see AlreadySaid. The bare zone line's whole content IS the
        // place name, so once any line above has said it there is nothing left for this line to
        // add; every other line carries something the name alone does not.
        if (state.ZoneName is { Length: > 0 } zone && !AlreadySaid(lines, zone))
        {
            lines.Add(new ReadoutLine(zone, ReadoutEmphasis.Muted));
        }

        return hasEntrance
            ? (true, state.EntranceX, null, state.EntranceZ)
            : (false, null, null, null);
    }

    private static void AddTeleportAdvice(List<ReadoutLine> lines, ReadoutInputs inputs, bool hasEntrance)
    {
        var state = inputs.State;
        if (state.AetheryteName is not { Length: > 0 } aetheryte)
        {
            if (!hasEntrance && state.Reason is { Length: > 0 } reason)
            {
                // "In Fortemps Manor — find the entrance" sitting directly under a step that reads
                // "Enter Fortemps Manor." says the place name twice. What gets dropped is the clause
                // that names it, never the instruction — see AlreadySaid.
                if (state.ZoneName is { Length: > 0 } zone && AlreadySaid(lines, zone))
                {
                    reason = OtherZoneResolution.WithoutZoneName(reason, zone);
                }

                lines.Add(new ReadoutLine(reason, ReadoutEmphasis.Secondary));
            }

            return;
        }

        // The line is marked as a teleport whether or not this surface can be clicked: the mark says
        // what the line means, and each surface decides for itself whether it can offer the action.
        //
        // The words are the same on every surface too. They used to gain a "(click)" suffix wherever
        // the action was actually offered, which is a caption standing in for an affordance — and it
        // told a player reading the click-through overlay to click something that could not be
        // clicked. The clickable host lights the line under the pointer instead, which is what the
        // cog beside it already does, and the words are left to say the one thing they are for.
        //
        // Attuned or not is the same distinction the duty line makes: attuned is somewhere the player
        // can go from here, so it takes the crystal and the press; not attuned is a statement of fact,
        // so it takes neither. Both go through Pressable, which is what makes that pairing a rule
        // rather than two independent decisions that happen to agree.
        const string Verb = "Teleport to ";
        if (!state.AetheryteUnlocked)
        {
            lines.Add(new ReadoutLine($"Not attuned to {aetheryte}", ReadoutEmphasis.Secondary));
            return;
        }

        // The mark needs the id as well as the attunement, because the id is what the action needs: a
        // line marked Teleport with no id behind it becomes a hit box and a controller anchor whose
        // press reaches a handler that reads the id, finds none and returns — a control that looks
        // live and is not. The words are unchanged in that case; only the mark is withheld, and
        // withholding it is spelled as "no action", because Pressable takes the glyph away with it.
        lines.Add(Pressable(
            Verb + aetheryte,
            ReadoutEmphasis.Secondary,
            state.AetheryteId is null ? ReadoutLineAction.None : ReadoutLineAction.Teleport,
            DtrGlyph.Aetheryte,
            glyphAt: Verb.Length));
    }

    private static (bool ShowArrow, float? X, float? Y, float? Z) AddReasonOnly(List<ReadoutLine> lines, NavigationState state)
    {
        if (state.Reason is { Length: > 0 } reason)
        {
            lines.Add(new ReadoutLine(reason, ReadoutEmphasis.Secondary));
        }

        return (false, null, null, null);
    }

    /// <summary>The distance line, and — when the target is on a different level of the world — the
    /// fact that it is, in words.
    ///
    /// <para>"56 yalms · above you". The drawn readout also hangs the game's own up/down chevron off
    /// the arrow, mirroring what the minimap does for a marker on another floor, but the words are
    /// what make it work: a chevron is a convention the player has to already know, and the flat
    /// distance is what made "the arrow points straight at a wall" confusing in the first
    /// place.</para>
    ///
    /// <para>Deliberately not said on "You have arrived": within five yalms horizontally, being six
    /// yalms up is the top of the stairs you are standing at the bottom of, and the readout has
    /// already told the player they are there. Saying both at once contradicts itself.</para></summary>
    private static void AddDistance(List<ReadoutLine> lines, ReadoutInputs inputs)
    {
        if (inputs.DistanceYalms is not { } distance)
        {
            return;
        }

        // A search-area objective (AreaHint.Outside here — Inside returns before this is ever
        // called) is never "arrived at" and never gets the plain distance line: the arrow points at
        // a circle's centre, not the thing itself, so the readout says so plainly rather than
        // implying the precision a point objective actually has. Absent radius — the entire rest of
        // this method — is completely unchanged, which is what keeps a point objective's output
        // byte-identical to before this feature existed. TargetRadiusYalms is already gated to
        // SearchAreaRadius.IsArea by GuidanceProjection, so this re-check is defensive, not load
        // bearing — but it keeps this method correct on its own terms rather than trusting a caller.
        if (inputs.State.TargetRadiusYalms is { } radius && SearchAreaRadius.IsArea(radius))
        {
            var areaText = $"Search the area · {NavMath.FormatDistance(distance)}";
            if (Elevation.Words(inputs.Elevation) is { Length: > 0 } areaElevation)
            {
                areaText = $"{areaText} · {areaElevation}";
            }

            lines.Add(new ReadoutLine(areaText, ReadoutEmphasis.Primary));
            return;
        }

        // Arrival is a claim about the OBJECTIVE, and this distance is only the objective's in
        // same-zone mode. In other-zone mode it is the distance to the way in — ReadoutFeed measures
        // it to (EntranceX, EntranceZ) — so a player standing on the entry shard is at the start of
        // the route, not the end of it. The Forgotten Knight is reached through shard 80 in
        // Foundation, and standing on that shard the readout said "You have arrived" in Primary above
        // "Aethernet to The Last Vigil, then 42 yalms": the loudest line on screen contradicting the
        // two beneath it. Zero yalms to the door you have not walked through yet is a distance, and
        // the plain distance line says it without claiming anything.
        if (distance < 5f && string.Equals(inputs.State.Mode, NavigationState.Modes.SameZone, StringComparison.Ordinal))
        {
            lines.Add(new ReadoutLine("You have arrived", ReadoutEmphasis.Primary));
            return;
        }

        var text = NavMath.FormatDistance(distance);
        if (Elevation.Words(inputs.Elevation) is { Length: > 0 } elevation)
        {
            text = $"{text} · {elevation}";
        }

        lines.Add(new ReadoutLine(text, ReadoutEmphasis.Primary));
    }

    private static void AddContext(List<ReadoutLine> lines, ReadoutInputs inputs)
    {
        var state = inputs.State;
        var separated = true;

        // A hunt that is not the current objective still deserves a line — but only one, and only
        // when it is not already the primary objective two lines above.
        if (!inputs.HuntingIsPrimary && inputs.HuntingSummary is { Length: > 0 } hunting)
        {
            // The game's own monster mark is the right glyph for this line, and it goes on the moment
            // the line does something — see HuntingLineAction, which is the one place that decides.
            lines.Add(Pressable(
                hunting, ReadoutEmphasis.Muted, HuntingLineAction, DtrGlyph.Monster, separated: separated));
            separated = false;
        }

        AddNearbyUnlocks(lines, inputs, separated);
    }

    private static void AddNearbyUnlocks(List<ReadoutLine> lines, ReadoutInputs inputs, bool separated)
    {
        if (inputs.NearbyUnlocks.Count == 0)
        {
            return;
        }

        // Engaged: one count, because the player asked to be guided somewhere and a list of other
        // things to do is exactly the clutter that made the old widget hard to read. Ambient:
        // the names and their distances, because that is the moment they are useful.
        if (inputs.State.Engaged)
        {
            // Phrased the way the game phrases a count — "3 unlocks nearby", not "Unlocks nearby:
            // 3" — and kept in sentence case because it is content, not a label.
            var count = inputs.NearbyUnlocks.Count;
            lines.Add(new ReadoutLine(
                count == 1 ? "1 unlock nearby" : $"{count} unlocks nearby",
                ReadoutEmphasis.Muted,
                separated));
            return;
        }

        var first = separated;
        var shown = 0;
        foreach (var unlock in inputs.NearbyUnlocks)
        {
            if (shown == MaxNearbyUnlockLines)
            {
                return;
            }

            // The only lines the readout marks. Each one names a place with a quest at the end of
            // it, which is exactly what the banner's medallion means — see ReadoutLine.Marked. The
            // engaged case above is deliberately NOT marked: "3 unlocks nearby" is a count, and a
            // count is not somewhere you can walk to.
            lines.Add(new ReadoutLine(unlock, ReadoutEmphasis.Muted, first, Marked: true));
            first = false;
            shown++;
        }
    }

    /// <summary>"To the Brume aetheryte", but "To The Forgotten Knight aetheryte" — the game's shard
    /// names carry their own article where they have one, so adding a second produces "To the The
    /// Forgotten Knight aetheryte". Two of Ishgard's five Pillars shards and two of Foundation's
    /// three are named this way, so the reported route hits it immediately.</summary>
    private static string EntryShard(string? name) =>
        name is not null && name.StartsWith("The ", StringComparison.OrdinalIgnoreCase)
            ? $"To {name} aetheryte"
            : $"To the {name} aetheryte";

    /// <summary>Whether a place name has already appeared in the lines composed so far.
    ///
    /// <para>This enforces the readout's fourth invariant — <b>nothing appears twice</b> — for place
    /// names, which is where it was being broken: an interior objective produced the zone's name in
    /// the step line, again in the interior-entrance message, and a third time as the bare zone
    /// line. The rule is that a place is named ONCE, at its first and most informative occurrence,
    /// and every later line that would only repeat it either drops the clause that names it or drops
    /// entirely. Matching is case-insensitive and by containment, because the repetitions are
    /// inflected ("Enter Fortemps Manor." / "In Fortemps Manor — …") rather than
    /// identical.</para></summary>
    private static bool AlreadySaid(List<ReadoutLine> lines, string place)
    {
        foreach (var line in lines)
        {
            if (line.Text.Contains(place, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // "Aethernet to X, then 40 yalms" — the walk after the shard hop or door crossing.
    private static string Remaining(NavigationState state) =>
        state.RemainingYalms is { } r ? $", then {NavMath.FormatDistance(r)}" : string.Empty;
}
