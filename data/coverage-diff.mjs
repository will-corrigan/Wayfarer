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
import crypto from 'node:crypto';
import {
  CHANNELS, allowanceFor, classifyMissing, identityProjection, questRowsOf, reasonText,
} from './coverage-policy.mjs';

/** The order data/coverage.json's own keys are written in. Part of the canonical form. */
const DOC_KEYS = [
  'generated', 'purpose', 'policy', 'catalogue', 'game', 'totals', 'channels', 'reasons',
  'shipped', 'unlocks',
];

/** What the coverage artefact says the catalogue it was generated beside looked like.
 *
 * Over the identity projection, not the file: hashing the file would make every prose edit a CI
 * failure that only a developer with the game installed could clear, and a check nobody can keep
 * green is a check that gets deleted. Drop an entry, change what it unlocks, or rebind it to
 * another quest and this moves. Fix a typo in a description and it does not. */
export function catalogueFingerprint(unlocks) {
  const projection = JSON.stringify(identityProjection(unlocks));
  return `sha256:${crypto.createHash('sha256').update(projection).digest('hex')}`;
}

/** The artefact's canonical text form.
 *
 * The two long arrays get ONE LINE PER ROW. That is not a formatting preference: a regeneration
 * after a patch should read as "these four rows appeared", and 3,091 rows pretty-printed nine
 * lines deep is a diff nobody reviews. Everything else is 2-space indented, as the catalogue is.
 * The generator writes this and data/validate-coverage.mjs re-derives it, so a hand edit shows up
 * as a formatting failure instead of surviving until the next regeneration reverts it. */
export function canonicaliseCoverage(doc) {
  const head = {};
  for (const k of DOC_KEYS) if (k in doc && k !== 'shipped' && k !== 'unlocks') head[k] = doc[k];

  // Everything but the closing brace, with a comma added to the last property so the two arrays
  // can be appended after it.
  const lines = JSON.stringify(head, null, 2).split('\n').slice(0, -1);
  lines[lines.length - 1] += ',';

  const body = [
    ...lines,
    ...oneLineArray('shipped', doc.shipped, true),
    ...oneLineArray('unlocks', doc.unlocks, false),
    '}',
  ];
  return `${body.join('\n')}\n`;

  function oneLineArray(name, rows, comma) {
    const tail = comma ? ',' : '';
    if (!rows.length) return [`  "${name}": []${tail}`];
    const out = [`  "${name}": [`];
    rows.forEach((r, i) => out.push(`    ${JSON.stringify(r)}${i === rows.length - 1 ? '' : ','}`));
    out.push(`  ]${tail}`);
    return out;
  }
}

const identityKey = (kind, id, subrow) =>
  subrow === null || subrow === undefined ? `${kind}#${id}` : `${kind}#${id}.${subrow}`;

/** A field, or nothing at all when it has no value. */
const opt = (key, value) => (value === null || value === undefined ? {} : { [key]: value });

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

    // Fields at their default are omitted rather than written out. There are 3,091 rows and the
    // key names alone cost more than the values; a row with no gate quest has nothing to say about
    // its gate, and saying it four times over is what turns a reviewable artefact into a megabyte
    // nobody opens.
    const out = {
      channel: row.channel,
      identityKind: row.identityKind,
      identityId: row.identityId,
      ...opt('identitySubrowId', row.identitySubrowId),
      ...(row.name ? { name: row.name } : {}),
      ...opt('questRowId', row.questRowId),
      ...(row.questName ? { questName: row.questName } : {}),
      ...(row.gateLive ? { gateLive: true } : {}),
      ...opt('level', row.level),
      ...(row.festival ? { festival: row.festival } : {}),
      via: row.via,
      ...(row.contentType ? { contentType: row.contentType } : {}),
      ...opt('inDutyFinder', row.inDutyFinder),
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

  // Only the reasons this diff actually cites, so the artefact carries no dead text and a reason
  // that has stopped being used disappears from it.
  const reasons = {};
  for (const key of [...new Set(rows.map((r) => r.reason).filter(Boolean))].sort()) {
    reasons[key] = reasonText(key) ?? null;
  }

  return { channels: channelSummary, totals, rows, shipped, reasons };
}
