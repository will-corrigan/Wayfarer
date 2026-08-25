using Wayfarer.Core.Navigation;

namespace Wayfarer.Core.Guidance;

/// <summary>The aether-current source's own semantics, kept pure and testable: when a stop counts as
/// done, where it sends the player, what it puts on the readout, and — the careful part — whether a
/// per-zone total may be shown at all.</summary>
public static class AetherCurrentPlan
{
    /// <summary>What this module calls itself on the readout's banner, which prints "Current" in
    /// front of it — so this one has to be chosen with that collision in mind.
    ///
    /// <para><b>Plural, breaking the singular rule the other source names follow, on purpose.</b>
    /// Singular would make the banner read "Current Aether Current", which is a stutter rather than a
    /// label. Plural it reads "Current Aether Currents", and it is verbatim the title of the game's
    /// own panel for this — so the player parses it as the name of a thing they already know instead
    /// of as a sentence that went wrong. Inventing a synonym to dodge the collision was the other
    /// option and it is the worse one: the banner would then be carrying a word the game never uses.
    /// See <see cref="ObjectiveCopy.SourceName"/>.</para></summary>
    public const string SourceName = "Aether Currents";

    /// <summary>"Aether Currents - Il Mheg" — the mode indicator, naming the zone whose set is being
    /// worked through. Folded to ASCII because the heading is drawn in the game's display face,
    /// which does not carry the typographic apostrophe in "The Rak'tika Greatwood" — see
    /// <see cref="Ui.HeadingText"/>, where that lesson was learned the first time.</summary>
    public static string SourceLabel(string? zoneName) =>
        zoneName is { Length: > 0 } zone
            ? Ui.HeadingText.Plain($"Aether Currents - {zone}")
            : "Aether Currents";

    /// <summary>THE COMPLETION SIGNAL, and it lives only here.
    ///
    /// <para>For a placed current, attunement is the whole story: the player flies to it, touches it,
    /// the bit flips.</para>
    ///
    /// <para>For a quest current the route walks to the GIVER, so the stop is done the moment the
    /// quest is in hand — exactly the rule <see cref="UnlockRoutePlan.IsPickedUp"/> uses, and for the
    /// same reason: once accepted there is nothing left at the giver's feet, and holding the arrow
    /// there until the quest is finished would point at someone the player has already spoken to.
    /// Attunement still counts, for the case where the quest was done long ago.</para></summary>
    public static bool IsReached(
        AetherCurrentKind kind, bool attuned, bool questAccepted, bool questComplete) =>
        kind == AetherCurrentKind.Quest
            ? attuned || questAccepted || questComplete
            : attuned;

    /// <summary>Where a stop sends the player. A current with no resolvable position becomes
    /// <see cref="ObjectiveDestination.Unresolved"/> rather than being dropped from the plan, so the
    /// readout can say it does not know where this one is instead of the plan quietly being shorter
    /// than the zone.</summary>
    public static ObjectiveDestination Destination(AetherCurrentPoint point) =>
        point.HasLocation
            ? new ObjectiveDestination.WorldPoint(point.Territory, point.MapId, point.X, point.Y, point.Z)
            : new ObjectiveDestination.Unresolved(
                point.Kind == AetherCurrentKind.Quest
                    ? "the game's data does not say where this quest is given"
                    : "the game's data does not say where this current is");

    /// <summary>The name on the plate: for a quest current the QUEST's name, for a placed one the
    /// game's own noun for the object. Never a label of ours — the plate is the game's Main Scenario
    /// Guide plate, and the rule is <see cref="UnlockRoutePlan.Headline"/>'s.</summary>
    public static string Headline(AetherCurrentPoint point) =>
        point.QuestName is { Length: > 0 } name ? name : "Aether Current";

    /// <summary>Our own words underneath: who to see, or that this one is flown to. The placed case
    /// says "attune" because that is the verb the game's own tooltip uses, and it is the difference
    /// between a stop the player finishes by arriving and one they finish by talking.</summary>
    public static string Detail(AetherCurrentPoint point)
    {
        if (point.Kind != AetherCurrentKind.Quest)
        {
            return "Fly here and attune to the aether current";
        }

        return point.GiverName is { Length: > 0 } giver
            ? $"Speak with {giver} to earn this aether current"
            : "Speak with the quest giver to earn this aether current";
    }

    /// <summary>WHETHER THE DENOMINATOR MAY BE PRINTED, and the reasoning is the whole point of this
    /// method.
    ///
    /// <para>The obvious total — the length of a set row's <c>AetherCurrents</c> array — is wrong.
    /// That column is a fixed 15 wide on every row including the two empty ones, and it is SPARSE:
    /// Coerthas Western Highlands fills indices 0-5, 7, 9 and 10 and leaves the rest blank. So the
    /// array length is 15 for a zone that wants nine, and using it would overstate every zone
    /// outside Dawntrail. What the game requires is the count of NON-EMPTY entries, because a set
    /// row is the only place a zone's requirement is written down and the client's own
    /// zone-complete predicate can only be reading it — a predicate that demanded all fifteen slots
    /// would make those zones impossible to finish.</para>
    ///
    /// <para>What makes the non-empty count safe to print is that the sheets close over themselves.
    /// A current is OBTAINABLE if it is granted by a quest or placed in the world as an object;
    /// measured across the whole sheet, the obtainable rows and the rows referenced by some set are
    /// the SAME 303 rows, with nothing on either side of the difference and no row claimed by two
    /// sets. The other 145 rows in the sheet are linked to no quest and have no object anywhere in
    /// any zone, so no player can reach one and none can be a hidden requirement. There is therefore
    /// no candidate for a current a zone needs and this count misses.</para>
    ///
    /// <para>That was measured against one game version, though, so it is also checked every time
    /// rather than assumed to hold forever. The client will answer "is this zone finished?" for free,
    /// and our count implies its own answer to the same question; when the two disagree, the sheet
    /// list and the client's requirement have stopped being the same set and the honest response is
    /// to keep the count of what is left — every bit of which was read individually and is still
    /// true — and say nothing about the total. The check is a backstop against a future patch, not
    /// the evidence: it only bites at the boundary, since two counts that differ in the middle can
    /// still agree that the zone is unfinished.</para></summary>
    /// <param name="known">Distinct non-empty currents found in the zone's set row.</param>
    /// <param name="attuned">How many of those the character has.</param>
    /// <param name="gameSaysZoneComplete">The client's own verdict, or null when it could not be
    /// read — in which case nothing was readable and there is no cross-check to pass.</param>
    public static AetherCurrentTally Tally(int known, int attuned, bool? gameSaysZoneComplete)
    {
        var remaining = Math.Max(0, known - attuned);
        var trustworthy = known > 0
            && gameSaysZoneComplete is { } complete
            && complete == (attuned >= known);

        return new AetherCurrentTally(attuned, remaining, trustworthy ? known : null);
    }

    /// <summary>The progress line, which says as much as it can prove. With a trusted total it reads
    /// "4 of 9 attuned"; without one it drops to the half that is still certain, because "5 left to
    /// attune" is true whether or not we know what the zone adds up to.</summary>
    public static string ProgressText(AetherCurrentTally tally) =>
        tally.Total is { } total
            ? $"{tally.Attuned} of {total} attuned"
            : $"{tally.Remaining} left to attune";
}
