// The diff itself: what the game proposes, against what the catalogue ships.
//
// WHY IT IS A MODULE AND NOT PART OF EITHER CALLER
// The generator runs this on a machine with the game installed and commits the answer as
// data/coverage.json. CI runs the SAME function over the committed enumeration and the committed
// catalogue and requires the same answer. If the two sides computed the diff differently the check
// would be measuring the difference between two implementations rather than the difference between
// the game and the catalogue — so there is one implementation, and both call it.
//
// Nothing here decides policy. Which rows matter is data/coverage-policy.mjs's job; this decides
// only whether a row is covered, and by what.
//
// WHAT "COVERED" MEANS, AND WHY IT IS TWO THINGS
// Two independent joins, and a row counts as covered if either hits.
//
//   identity  a shipped entry's own `reward` names this exact row. The strong form.
//   gate      a shipped entry cites the Quest row the game says opens this. Weaker, and it is the
//             one that keeps the measure honest in the other direction: the catalogue's 30 aether
//             current entries genuinely tell a player about all 151 currents, and its job entries
//             name the job in prose while carrying no ClassJob identity.
//
// Both are reported per channel, because the difference between them IS a finding. A channel with
// a high `shippedByGate` and a zero `shippedByIdentity` is one where the catalogue talks about the
// right quests without ever naming the thing they unlock — which is the shape of every bug this
// pipeline has had.
//
// Neither join uses a NAME. That is the whole lesson of the original defect: 180 entries shipped
// keyed on prose that matched no row, and 23 duties looked like orphans only because
// "The Minstrel's Ballad: Zodiark's Fall Extreme Trial Access" is not what any sheet calls that
// duty.
import { CHANNELS, allowanceFor, classifyMissing, questRowsOf } from './coverage-policy.mjs';

const identityKey = (kind, id, subrow) =>
  subrow === null || subrow === undefined ? `${kind}#${id}` : `${kind}#${id}.${subrow}`;

/**
 * @param {Array} enumerated rows from tools/Wayfarer.CatalogueGen's `enumerate` verb, or the
 *   `game.unlocks` array of a committed coverage.json — the same shape either way.
 * @param {Array} unlocks the catalogue's `unlocks` array.
 */
export function buildCoverage(enumerated, unlocks) {
  // ---- the two indexes a match can come from ------------------------------------------------
  const byIdentity = new Map();
  const byQuest = new Map();
  unlocks.forEach((e, i) => {
    if (e.reward) {
      const k = identityKey(e.reward.kind, e.reward.id);
      if (!byIdentity.has(k)) byIdentity.set(k, []);
      byIdentity.get(k).push(i);
    }
    for (const q of questRowsOf(e)) {
      if (!byQuest.has(q)) byQuest.set(q, []);
      byQuest.get(q).push(i);
    }
  });

  // ---- the game side ------------------------------------------------------------------------
  const channels = new Map();
  const coveringEntries = new Map();
  const rows = enumerated.map((row) => {
    const identityHits =
      byIdentity.get(identityKey(row.identityKind, row.identityId, row.identitySubrowId)) ?? [];
    const gateHits = row.questRowId ? byQuest.get(row.questRowId) ?? [] : [];
    const hits = [...new Set([...identityHits, ...gateHits])].sort((a, b) => a - b);

    const out = {
      channel: row.channel,
      identityKind: row.identityKind,
      identityId: row.identityId,
      ...(row.identitySubrowId === null || row.identitySubrowId === undefined
        ? {} : { identitySubrowId: row.identitySubrowId }),
      name: row.name,
      questRowId: row.questRowId ?? null,
      questName: row.questName ?? '',
      gateLive: row.gateLive ?? false,
      level: row.level ?? null,
      festival: row.festival ?? 0,
      via: row.via,
      ...(row.contentType ? { contentType: row.contentType } : {}),
      ...(row.inDutyFinder === null || row.inDutyFinder === undefined
        ? {} : { inDutyFinder: row.inDutyFinder }),
      catalogue: hits.length ? 'covered' : 'missing',
    };

    if (hits.length) {
      out.coveredBy = identityHits.length ? 'identity' : 'gate';
      out.coveredByEntries = hits;
      for (const i of hits) {
        if (!coveringEntries.has(i)) coveringEntries.set(i, []);
        coveringEntries.get(i).push({
          channel: row.channel,
          basis: identityHits.includes(i) ? 'identity' : 'gate',
        });
      }
    } else {
      const { classification, reason } = classifyMissing(row);
      out.classification = classification;
      if (reason) out.reason = reason;
    }

    const c = channels.get(row.channel) ?? {
      gameTotal: 0, withGateQuest: 0, shipped: 0, byIdentity: 0, byGate: 0,
      recommended: 0, excluded: 0, undecided: 0, entries: new Set(),
    };
    c.gameTotal++;
    if (out.questRowId !== null) c.withGateQuest++;
    if (hits.length) {
      c.shipped++;
      if (identityHits.length) c.byIdentity++;
      if (gateHits.length) c.byGate++;
      for (const i of hits) c.entries.add(i);
    } else {
      c[out.classification]++;
    }
    channels.set(row.channel, c);
    return out;
  });

  // ---- the catalogue side ------------------------------------------------------------------
  //
  // The direction that stops a regeneration quietly losing entries: every shipped entry has to be
  // accounted for here, either by an enumerated row it corresponds to or by a stated rule.
  const shipped = unlocks.map((e, i) => {
    const covers = coveringEntries.get(i) ?? [];
    const record = {
      entry: i,
      unlock: e.unlock,
      level: e.level ?? null,
      type: e.type,
      identity: e.reward ? { kind: e.reward.kind, id: e.reward.id } : null,
      enumerated: covers.length > 0,
    };

    if (covers.length) {
      record.basis = covers.some((c) => c.basis === 'identity') ? 'identity' : 'gate';
      record.channels = [...new Set(covers.map((c) => c.channel))].sort();
      record.rows = covers.length;
    } else {
      const allowance = allowanceFor(e);
      if (allowance) {
        record.allowedBy = allowance.rule;
        record.why = allowance.why;
      }
    }
    return record;
  });

  const channelSummary = {};
  for (const key of [...channels.keys()].sort()) {
    const c = channels.get(key);
    const policy = CHANNELS[key];
    channelSummary[key] = {
      gameTotal: c.gameTotal,
      withGateQuest: c.withGateQuest,
      shipped: c.shipped,
      shippedByIdentity: c.byIdentity,
      shippedByGate: c.byGate,
      shippedEntries: c.entries.size,
      recommended: c.recommended,
      excluded: c.excluded,
      undecided: c.undecided,
      listed: policy?.ship ?? null,
    };
  }

  const totals = {
    gameRows: rows.length,
    gameChannels: Object.keys(channelSummary).length,
    covered: rows.filter((r) => r.catalogue === 'covered').length,
    missing: rows.filter((r) => r.catalogue === 'missing').length,
    recommended: rows.filter((r) => r.classification === 'recommended').length,
    excluded: rows.filter((r) => r.classification === 'excluded').length,
    undecided: rows.filter((r) => r.classification === 'undecided').length,
    catalogueEntries: unlocks.length,
    entriesTiedToAnEnumeratedRow: shipped.filter((s) => s.enumerated).length,
    entriesAllowedByRule: shipped.filter((s) => s.allowedBy).length,
    entriesUnaccountedFor: shipped.filter((s) => !s.enumerated && !s.allowedBy).length,
  };

  return { channels: channelSummary, totals, rows, shipped };
}
