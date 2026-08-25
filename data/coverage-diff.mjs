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
  ALLOWANCE_RULES, CHANNELS, allowanceFor, classifyMissing, identityProjection, questRowsOf,
  reasonText,
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

/** Whether the game has a player-facing name for this row's identity.
 *
 * Three cases, and getting them muddled is how the recompute in CI would disagree with the
 * generator: the enumerator always sends a `name` field, possibly empty, so its emptiness is the
 * answer; a committed row carrying `unnamed` says so outright; and a committed row with neither is
 * one whose name was deliberately left off, which means it HAD one. `!row.name` alone would read
 * every excluded row in the artefact as nameless and reclassify half of it. */
function hasName(row) {
  if (row.unnamed === true) return false;
  if ('name' in row) return Boolean(row.name);
  return true;
}

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

  // ---- which rows are covered, and which of them speak for a group -------------------------
  //
  // The enumerator has already grouped the rows the game holds several of for one thing — the 22
  // "Ocean Fishing" duty rows — by pointing each later row's `duplicateOf` at the first. What it
  // cannot know is which member of a group the CATALOGUE happens to cover, because that is not a
  // fact about the game. "The Gilded Araya" is the case: the shipped entry covers row 944, and the
  // first row of that name is 69, so taking the enumerator's answer alone would list the same duty
  // twice. So a group whose covered member is not its first row re-points at the covered one.
  const hitsFor = enumerated.map((row) => {
    const identityHits =
      byIdentity.get(identityKey(row.identityKind, row.identityId, row.identitySubrowId)) ?? [];
    const gateHits = row.questRowId ? byQuest.get(row.questRowId) ?? [] : [];
    return {
      identityHits,
      gateHits,
      hits: [...new Set([...identityHits, ...gateHits])].sort((a, b) => a - b),
    };
  });

  const groupKey = (row) => `${row.channel}#${row.duplicateOf ?? row.identityId}`;
  const coveredInGroup = new Map();
  enumerated.forEach((row, i) => {
    const key = groupKey(row);
    if (hitsFor[i].hits.length > 0 && !coveredInGroup.has(key)) {
      coveredInGroup.set(key, row.identityId);
    }
  });

  /** The row this one defers to: the enumerator's answer, or the covered member of its group. */
  const deferTo = (row) => {
    if (row.duplicateOf !== null && row.duplicateOf !== undefined) return row.duplicateOf;
    const covered = coveredInGroup.get(groupKey(row));
    return covered !== undefined && covered !== row.identityId ? covered : null;
  };

  // ---- the game side ------------------------------------------------------------------------
  const channels = new Map();
  const coveringEntries = new Map();
  const rows = enumerated.map((row, index) => {
    const { identityHits, gateHits, hits } = hitsFor[index];

    const named = hasName(row);
    const verdict = hits.length
      ? null
      : classifyMissing({ ...row, unnamed: !named, duplicateOf: deferTo(row) });

    // Display names ride along ONLY on the rows somebody still has to decide about.
    //
    // The sheets are Square Enix material under the Materials Usage Licence, and the position this
    // repository works to is that shipping derived FACTS — a row id, a level, a relation — is a
    // materially different profile from shipping bulk display strings. Names and quest names on all
    // 3,091 rows would be the second thing. Names on the ~500 rows being proposed for inclusion or
    // left for a human to classify are the first: there is no way to review "should we ship
    // Title#858" without one. A covered row needs no name, and neither does one the policy has
    // already decided against.
    const nameable = verdict !== null && verdict.classification !== 'excluded';

    // Fields at their default are omitted rather than written out. There are 3,091 rows and the key
    // names alone cost more than the values; a row with no gate quest has nothing to say about its
    // gate, and saying it four times over is what turns a reviewable artefact into a megabyte
    // nobody opens.
    const out = {
      channel: row.channel,
      identityKind: row.identityKind,
      identityId: row.identityId,
      ...opt('identitySubrowId', row.identitySubrowId),
      // `unnamed` rather than an empty `name`. Whether the game has a player-facing name for a row
      // is a FACT the policy decides on, and it has to survive the name itself being left off — so
      // it is stated, not inferred from a string being absent.
      ...(named ? {} : { unnamed: true }),
      ...(nameable && row.name ? { name: row.name } : {}),
      ...opt('questRowId', row.questRowId),
      ...(nameable && row.questName ? { questName: row.questName } : {}),
      ...(row.gateLive ? { gateLive: true } : {}),
      // The row this one is a second copy of, when the game's tables hold several for one thing —
      // 22 "Ocean Fishing" duty rows, one per route. Decided by the enumerator, which is the only
      // side that has every name: display names are deliberately not written for excluded rows (see
      // below), so the grouping could not be re-derived here from the committed artefact.
      ...opt('duplicateOf', row.duplicateOf),
      ...opt('level', row.level),
      ...(row.festival ? { festival: row.festival } : {}),
      via: row.via,
      ...(row.contentType ? { contentType: row.contentType } : {}),
      ...opt('inDutyFinder', row.inDutyFinder),
      catalogue: hits.length ? 'covered' : 'missing',
    };

    if (verdict === null) {
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
      out.classification = verdict.classification;
      if (verdict.reason) out.reason = verdict.reason;
    }

    const c = channels.get(row.channel) ?? {
      gameTotal: 0, withGateQuest: 0, shipped: 0, byIdentity: 0, byGate: 0,
      recommended: 0, excluded: 0, undecided: 0, entries: new Set(),
    };
    c.gameTotal++;
    // `questRowId` is omitted rather than written as null, so this asks whether the key is there.
    // `!== null` would count every row, which is exactly the wrong answer for `duty`: the game
    // states the gate for 280 of its 857 rows and withholds it for the rest.
    if ('questRowId' in out) c.withGateQuest++;
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
      // The rule's NAME, not its text. The text lives once, in data/coverage-policy.mjs, for the
      // same reason the row reasons are keyed: 125 copies of a sentence is 125 lines of diff every
      // time somebody improves the wording.
      const allowance = allowanceFor(e);
      if (allowance) record.allowedBy = allowance.rule;
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

  // Only the reasons and rules this diff actually cites, so the artefact carries no dead text and
  // one that has stopped being used disappears from it.
  const reasons = {};
  for (const key of [...new Set(rows.map((r) => r.reason).filter(Boolean))].sort()) {
    reasons[key] = reasonText(key) ?? null;
  }
  for (const key of [...new Set(shipped.map((s) => s.allowedBy).filter(Boolean))].sort()) {
    reasons[`allowance:${key}`] = ALLOWANCE_RULES[key] ?? null;
  }

  return { channels: channelSummary, totals, rows, shipped, reasons };
}
