namespace Wayfarer.Core.Navigation;

/// <summary>The single threshold that decides whether a quest marker's radius means a genuine
/// "search this area" objective (the client draws a circle and the true target could be anywhere
/// inside it) or is incidental proximity-trigger noise on an otherwise precise point objective.
///
/// <para><b>This constant must never again look arbitrary — here is the evidence.</b> A prior
/// attempt at this feature (commit <c>4d22b40</c>) used "radius greater than zero" as the area
/// test and was reverted (<c>24c96bb</c>) after it labelled an ordinary "talk to Aymeric to
/// complete the quest" step (radius 1-3 yalms, same as any other NPC-talk step) as a search area.
/// The revert's own postmortem also flagged a step that measured 203 yalms and assumed, wrongly,
/// that it must be a point objective because its wording was an ordinary "speak with" sentence.
/// That assumption was the actual bug, not the radius: a follow-up quest-step-text census (dated
/// 2026-08-24) settled it — the client draws a circle for that step too, and step wording does not
/// partition point objectives from area objectives at all (verified: "Search" steps exist that
/// are points, "Speak" steps exist that are areas — do not use text as a signal, it would be
/// unlocalisable besides).</para>
///
/// <para><b>The measured signal</b> — <c>Level.Radius</c> across every quest step location in the
/// game (the <c>Quest.TodoParams[n].ToDoLocation</c> join, the same join
/// <see cref="Guidance.Sources.QuestObjectiveSource"/>'s static-sheet fallback already performs):
/// the histogram has two clearly separated modes with a near-empty valley between them, and 20
/// yalms sits in that valley. Independently re-verified against the currently-installed game data
/// (not just cited from the spec above) via Lumina over the live <c>sqpack</c>, scoped to the
/// 15,925 distinct <c>Level</c> rows actually referenced by a <c>Quest.TodoParams</c> ToDo
/// location: 4,031 of those rows have radius &gt;= 20 (area, by this rule); 632 more are
/// <c>Level.Type == 51</c> ("pop/map range") with radius &lt; 20 — small ranges like doorways and
/// "Enter …" steps, correctly kept as points despite carrying the "area" type. Exactly ONE row in
/// the entire quest-step census disagrees the other way (radius &gt;= 20 but <c>Type != 51</c>):
/// Level row 10589881, radius 50, <c>Type 49</c> ("Battle NPC / target range"), the "Land at the
/// designated location" step of Quest 70602 ("Leap into the Unknown"). <b>Resolved in favour of
/// radius</b>: radius is the numeric field that actually varies per location and is what the
/// client uses to draw the circle; <c>Type</c> is corroboration, not an override, and a 50-yalm
/// landing zone reads as a genuine area regardless of which type code it was filed under.</para>
///
/// <para>The two fixtures this was built against, also reconfirmed live:
/// "Heroes of the Hour" (Quest 67782) — the very step that fooled the original revert — resolves
/// to <c>Type 51</c>, radius 203 (genuinely an area); "The Full Report, Warts and All"
/// (Quest 69901)'s frog-transfiguration step resolves to two <c>Type 51</c> locations, radius 406
/// and 102 (also genuinely areas, on the SAME step as three radius-1 <c>Type 8</c> companion-escort
/// points — classification is per location, not per step).</para>
///
/// <para><b>Runtime vs. static source.</b> <c>Level.Radius</c> (the static Excel sheet, used for
/// the bulk analysis above) and <c>MapMarkerData.Radius</c> (the live marker struct
/// <see cref="Guidance.Sources.QuestObjectiveSource"/> actually reads every poll) are the same
/// field under two different names — confirmed via reflection against the installed dev DLLs:
/// both are a <c>float</c> named "Radius", in yalms. <c>MapMarkerData.Radius</c> is authoritative
/// for shipped behaviour, since it is what the game is live-drawing at the moment the arrow
/// points; <c>Level.Radius</c> is only a static proxy used because analysing 15,925+ rows at once
/// requires the sheet, not a per-step live sample. The two are identical by construction on the
/// static-sheet fallback path (no live marker at all — the fallback reads <c>level.X/Y/Z</c>
/// directly, so it is reading the exact same row this threshold was measured against); for the
/// live-marker path this constant applies to the identically-named, identically-typed live field
/// without any conversion. A live in-client walk-the-fixture comparison is a manual dev-first
/// check, not something this environment can drive interactively — see the deploy step.</para></summary>
public static class SearchAreaRadius
{
    /// <summary>Yalms. See the type doc comment for the histogram this sits in the middle of.</summary>
    public const float ThresholdYalms = 20f;

    /// <summary>Whether a marker/location radius represents a genuine search-area objective.
    /// <see cref="ThresholdYalms"/> is the whole rule; see the type doc comment for why.</summary>
    public static bool IsArea(float radiusYalms) => radiusYalms >= ThresholdYalms;
}
