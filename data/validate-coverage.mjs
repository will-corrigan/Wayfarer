// The completeness check, in CI, with no game installation.
//
// WHAT THIS EXISTS TO PREVENT
// The catalogue's existence set comes from one wiki guide. Anything the guide omits, the pipeline
// never learns about — and it has happened: a whole trophy-mount quest was missing until another
// plugin's data revealed it, and the game names 151 aether currents against the 30 the guide
// lists. data/coverage.json is the game's own answer to "what is unlockable", recorded next to the
// catalogue at generation time. This file is what stops the two drifting apart afterwards.
//
// It checks three things, and it FAILS rather than warns on all of them:
//
//   1. The artefact belongs to THIS catalogue. Its recorded entry count and identity fingerprint
//      must match the committed data/unlocks-by-level.json. Drop an entry, rebind one to another
//      quest, or change what one unlocks, and the fingerprint moves — so a regenerated catalogue
//      that silently loses entries cannot pass with a stale artefact beside it.
//
//   2. Every shipped entry is accounted for. Either it corresponds to a row the game enumerates,
//      or it is allowed by one of the two RULES in data/coverage-policy.mjs — the 223 `system`
//      entries have no game row anywhere, because the game has no general system-unlock table, and
//      that is a rule and not a list of exceptions. An entry that carries an identity gets no
//      allowance: it must appear in the enumeration.
//
//   3. The artefact's own arithmetic. Every classification, reason, count and total is recomputed
//      from the committed enumeration and the committed catalogue by the SAME code the generator
//      used, and must come out identical. A hand-edited coverage.json fails here.
//
//   4. Nothing is left recommended. This used to be the thing the check deliberately did NOT do:
//      378 recommended and 157 undecided rows were the point of the artefact, and a check that went
//      red for them would have been turned off within a week. The generator now imports every
//      recommended row, so the baseline is zero and a non-zero count means the game has shipped an
//      unlock of a kind the catalogue lists and the committed file does not have it. That is exactly
//      the alarm the artefact was written for, with the manual step taken out — and the fix is to
//      regenerate, not to edit a number.
//
// WHAT IT DOES NOT DO
// It does not read anything but this repository: no sqpack, no wiki, no tools/.
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';
import { buildCoverage, canonicaliseCoverage, catalogueFingerprint } from './coverage-diff.mjs';
import { ALLOWANCE_RULES, CHANNELS, reasonText } from './coverage-policy.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const rawCoverage = fs.readFileSync(path.join(HERE, 'coverage.json'), 'utf8');
const coverage = JSON.parse(rawCoverage);
const catalogue = JSON.parse(fs.readFileSync(path.join(HERE, 'unlocks-by-level.json'), 'utf8'));

let errors = 0;
const err = (m) => { console.error(m); errors++; };
const REGENERATE = 'Regenerate with: node scripts/build-unlock-catalogue.mjs --write';

// ---------------------------------------------------------------- 1. it belongs to this catalogue
if (coverage.catalogue?.entries !== catalogue.unlocks.length) {
  err(`coverage.json was generated beside a catalogue of ${coverage.catalogue?.entries} entries; `
    + `the committed catalogue has ${catalogue.unlocks.length}. ${REGENERATE}`);
}

const fingerprint = catalogueFingerprint(catalogue.unlocks);
if (coverage.catalogue?.identityFingerprint !== fingerprint) {
  err('coverage.json does not belong to the committed catalogue — an entry has been added, '
    + 'removed, rebound to a different quest, or had what it unlocks changed since the artefact '
    + `was written.\n  artefact:   ${coverage.catalogue?.identityFingerprint}`
    + `\n  catalogue:  ${fingerprint}\n  ${REGENERATE}`);
}

// ---------------------------------------------------------------- 2. every channel is classified
//
// Both directions. A channel the enumeration produces and the policy has never seen would classify
// its rows as undecided with no reasoning behind it; a channel the policy names and the artefact
// does not have is a join that has stopped matching, which is the schema-drift failure that
// produces a catalogue that only LOOKS complete.
for (const channel of Object.keys(CHANNELS)) {
  const summary = coverage.channels?.[channel];
  if (!summary) {
    err(`channel '${channel}' is in data/coverage-policy.mjs but absent from coverage.json — `
      + `either its join has stopped matching, or the artefact predates the policy. ${REGENERATE}`);
  } else if (summary.gameTotal === 0) {
    err(`channel '${channel}' enumerated 0 rows. A channel that has silently gone to zero is how `
      + 'a renamed sheet column makes the catalogue look complete. Check its join in '
      + 'tools/Wayfarer.CatalogueGen/UnlockEnumeration.cs.');
  }
}
for (const channel of Object.keys(coverage.channels ?? {})) {
  if (!(channel in CHANNELS)) {
    err(`channel '${channel}' is in coverage.json with no entry in data/coverage-policy.mjs, so `
      + 'nobody has said whether the catalogue lists that kind of thing. Classify it there.');
  }
}

// ---------------------------------------------------------------- 3. every entry is accounted for
const unaccounted = (coverage.shipped ?? []).filter((s) => !s.enumerated && !s.allowedBy);
for (const s of unaccounted) {
  err(`entry #${s.entry} ${JSON.stringify(s.unlock)}: the game enumerates nothing that matches it `
    + 'and no rule in data/coverage-policy.mjs allows it to be absent. Either it names something '
    + 'the game does not have, or a join is missing.');
}

// A rule cited by name has to be a rule this policy has, with the wording this policy gives it.
// Checked once per distinct rule rather than once per entry: 125 identical lines is not a better
// error message than one.
for (const rule of new Set((coverage.shipped ?? []).map((s) => s.allowedBy).filter(Boolean))) {
  const cited = (coverage.shipped ?? []).filter((s) => s.allowedBy === rule).length;
  if (!ALLOWANCE_RULES[rule]) {
    err(`${cited} entr${cited === 1 ? 'y cites' : 'ies cite'} allowance rule '${rule}', which `
      + 'data/coverage-policy.mjs does not have. An entry may only be excused by a rule that '
      + 'exists.');
  } else if (coverage.reasons?.[`allowance:${rule}`] !== ALLOWANCE_RULES[rule]) {
    err(`allowance rule '${rule}' (cited by ${cited} entries): its wording in coverage.json is not `
      + `what data/coverage-policy.mjs says it is. ${REGENERATE}`);
  }
}

// An identity is a row id. A row id that the enumeration does not contain is either a reward join
// that resolved something the enumeration cannot reach, or an invented identity — and both are
// bugs rather than facts about the game.
const enumeratedIdentities = new Set(
  (coverage.unlocks ?? []).map((r) => `${r.identityKind}#${r.identityId}`));
for (const s of coverage.shipped ?? []) {
  if (s.identity && !enumeratedIdentities.has(`${s.identity.kind}#${s.identity.id}`)) {
    err(`entry #${s.entry} ${JSON.stringify(s.unlock)}: claims to unlock `
      + `${s.identity.kind}#${s.identity.id}, which the enumeration does not contain. An entry `
      + 'that carries an identity gets no allowance — that row must be reachable.');
  }
}

// ---------------------------------------------------------------- 4. every missing row is judged
//
// Nothing is excluded or deferred without a stated reason, and a reason is a KEY that has to
// resolve — so an exclusion cannot be justified by a sentence written on one row, and a row cannot
// go on citing a reason that has been deleted from the policy.
for (const [i, row] of (coverage.unlocks ?? []).entries()) {
  const where = `row ${i} (${row.channel} ${row.identityKind}#${row.identityId})`;
  if (row.catalogue === 'covered') continue;
  if (!['recommended', 'excluded', 'undecided'].includes(row.classification)) {
    err(`${where}: classification ${JSON.stringify(row.classification)} is not one of `
      + 'recommended / excluded / undecided.');
    continue;
  }
  if (row.classification === 'recommended') continue;
  if (!row.reason) {
    err(`${where}: classified '${row.classification}' with no reason. Nothing is excluded or `
      + 'deferred without one.');
  } else if (!reasonText(row.reason)) {
    err(`${where}: cites reason '${row.reason}', which data/coverage-policy.mjs does not define.`);
  } else if (coverage.reasons?.[row.reason] !== reasonText(row.reason)) {
    err(`${where}: cites reason '${row.reason}', whose text in coverage.json is not what `
      + 'data/coverage-policy.mjs says it is.');
  }
}

// ---------------------------------------------------------------- 5. the arithmetic, recomputed
//
// The strongest check and the reason the whole enumeration is committed rather than just its
// summary: everything except the sheet walk is recomputed here, by the same functions the
// generator called, and required to come out identical. A hand-tuned count, a softened
// classification or an exclusion reason quietly reworded all fail.
const recomputed = buildCoverage(coverage.unlocks ?? [], catalogue.unlocks);
compare('totals', coverage.totals, recomputed.totals);
compare('channels', coverage.channels, recomputed.channels);
compare('reasons', coverage.reasons, recomputed.reasons);
compare('shipped', coverage.shipped, recomputed.shipped);
compare('unlocks', coverage.unlocks, recomputed.rows);

function compare(what, committed, expected) {
  const a = JSON.stringify(committed);
  const b = JSON.stringify(expected);
  if (a === b) return;
  err(`coverage.json's '${what}' is not what data/coverage-policy.mjs produces from the committed `
    + `enumeration and the committed catalogue.\n  ${firstDifference(a, b)}\n  ${REGENERATE}`);
}

/** Where two serialisations part company, with enough either side to read. */
function firstDifference(a, b) {
  let i = 0;
  while (i < a.length && i < b.length && a[i] === b[i]) i++;
  const from = Math.max(0, i - 60);
  return `at character ${i}:\n    committed: …${a.slice(from, i + 120)}`
    + `\n    expected:  …${b.slice(from, i + 120)}`;
}

// ---------------------------------------------------------------- 6. canonical form
//
// The artefact is written one row per line so that a regeneration after a patch reads as "these
// four rows appeared". Checking the form here is what stops a hand edit surviving until the next
// regeneration silently reverts it.
const canonical = canonicaliseCoverage({
  generated: coverage.generated,
  purpose: coverage.purpose,
  policy: coverage.policy,
  catalogue: coverage.catalogue,
  game: coverage.game,
  totals: coverage.totals,
  channels: coverage.channels,
  reasons: coverage.reasons,
  shipped: coverage.shipped,
  unlocks: coverage.unlocks,
});
if (canonical !== rawCoverage) {
  const a = rawCoverage.split('\n');
  const b = canonical.split('\n');
  const at = a.findIndex((l, i) => l !== b[i]);
  err(`coverage.json is not in canonical form (first difference at line ${at + 1}):`
    + `\n  committed:  ${JSON.stringify((a[at] ?? '').slice(0, 160))}`
    + `\n  canonical:  ${JSON.stringify((b[at] ?? '').slice(0, 160))}\n  ${REGENERATE}`);
}

// ---------------------------------------------------------------- 7. nothing left recommended
//
// The baseline the import established, and the one number in this file that is about the CONTENT of
// the catalogue rather than about the artefact's internal consistency.
//
// `recommended` means "a real, obtainable unlock of a kind the catalogue lists, that the catalogue
// does not have". The generator writes an entry for every one of them, so the steady state is zero;
// a non-zero count means the installed game data has moved since the committed file was generated
// and the file is now incomplete. `undecided` is zero for the same reason: every channel and every
// duty kind has a verdict, and a new one the policy has never seen lands here rather than being
// quietly swallowed.
//
// Both are stated as "must be zero" rather than as a number to update, so growth in the catalogue
// cannot turn this red on its own — only a gap can.
const missing = coverage.totals ?? {};
if ((missing.recommended ?? 0) !== 0) {
  err(`${missing.recommended} enumerated row(s) are recommended for inclusion and not in the `
    + 'catalogue. The generator imports every recommended row, so this means the installed game '
    + `data has unlocks the committed file does not. ${REGENERATE}`);
}
if ((missing.undecided ?? 0) !== 0) {
  err(`${missing.undecided} enumerated row(s) are undecided — a channel or duty kind the policy has `
    + 'never seen. Classify it in data/coverage-policy.mjs, then regenerate.');
}

// ---------------------------------------------------------------- report
const t = coverage.totals ?? {};
if (errors) {
  console.error(`\ndata/coverage.json: ${errors} problem(s).`);
  process.exit(1);
}

console.log(`data/coverage.json: the game proposes ${t.gameRows} rows across ${t.gameChannels} `
  + `channels; the catalogue's ${t.catalogueEntries} entries cover ${t.covered}.`);
console.log(`  not covered: ${t.recommended} recommended for inclusion, ${t.undecided} undecided, `
  + `${t.excluded} excluded by policy.`);
console.log(`  entries: ${t.entriesTiedToAnEnumeratedRow} tied to an enumerated row, `
  + `${t.entriesAllowedByRule} allowed by rule, ${t.entriesUnaccountedFor} unaccounted for.`);
