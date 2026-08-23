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
/// </list></summary>
public static class ReadoutComposer
{
    /// <summary>How many "available here" unlocks the readout will name at once. The service that
    /// supplies them already keeps to the nearest few, but the budget is enforced here as well
    /// because it is a property of the readout — legibility at TV distance — rather than of the
    /// unlock scan, and this is where it can be tested.</summary>
    public const int MaxNearbyUnlockLines = 3;

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
            : new ReadoutContent(lines, showArrow, x, y, z, inputs.Elevation);
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

    private static (bool ShowArrow, float? X, float? Y, float? Z) AddRoute(List<ReadoutLine> lines, ReadoutInputs inputs)
    {
        var state = inputs.State;
        return state.Mode switch
        {
            NavigationState.Modes.SameZone => AddSameZone(lines, inputs),
            NavigationState.Modes.OtherZone => AddOtherZone(lines, inputs),
            _ => AddReasonOnly(lines, state),
        };
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
            lines.Add(new ReadoutLine($"To the {state.AethernetEntryName} aetheryte", ReadoutEmphasis.Secondary));
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
                lines.Add(new ReadoutLine($"To the {state.AethernetEntryName} aetheryte", ReadoutEmphasis.Secondary));
                lines.Add(new ReadoutLine($"Aethernet to {exit}{Remaining(state)}", ReadoutEmphasis.Secondary));
            }
            else
            {
                lines.Add(new ReadoutLine($"Through {state.EntranceName}{Remaining(state)}", ReadoutEmphasis.Secondary));
            }
        }

        AddTeleportAdvice(lines, inputs, hasEntrance);

        if (state.ZoneName is { Length: > 0 } zone)
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
                lines.Add(new ReadoutLine(reason, ReadoutEmphasis.Secondary));
            }

            return;
        }

        // The line is marked as a teleport whether or not this surface can be clicked: the mark says
        // what the line means, and each surface decides for itself whether it can offer the action.
        lines.Add(state.AetheryteUnlocked
            ? new ReadoutLine(
                inputs.TeleportOnClick ? $"Teleport to {aetheryte} first (click)" : $"Teleport to {aetheryte} first",
                ReadoutEmphasis.Secondary,
                Separated: false,
                ReadoutLineAction.Teleport)
            : new ReadoutLine(
                $"Not attuned to {aetheryte}",
                ReadoutEmphasis.Secondary));
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
        // implying the precision a point objective actually has. Zero/absent radius — the entire
        // rest of this method — is completely unchanged, which is what keeps a point objective's
        // output byte-identical to before this feature existed.
        if (inputs.State.TargetRadiusYalms is { } radius && radius > 0f)
        {
            var areaText = $"Search the area · {NavMath.FormatDistance(distance)}";
            if (Elevation.Words(inputs.Elevation) is { Length: > 0 } areaElevation)
            {
                areaText = $"{areaText} · {areaElevation}";
            }

            lines.Add(new ReadoutLine(areaText, ReadoutEmphasis.Primary));
            return;
        }

        if (distance < 5f)
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
            lines.Add(new ReadoutLine(hunting, ReadoutEmphasis.Muted, separated));
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

            lines.Add(new ReadoutLine(unlock, ReadoutEmphasis.Muted, first));
            first = false;
            shown++;
        }
    }

    // "Aethernet to X, then 40 yalms" — the walk after the shard hop or door crossing.
    private static string Remaining(NavigationState state) =>
        state.RemainingYalms is { } r ? $", then {NavMath.FormatDistance(r)}" : string.Empty;
}
