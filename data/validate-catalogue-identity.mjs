// Enforces, in CI, the guarantees scripts/build-unlock-catalogue.mjs makes — using ONLY what is
// in this repository.
//
// The split this file exists to hold up: GENERATION needs a local game installation, so it runs
// on a developer's machine and its output, data/unlocks-by-level.json, is committed. VALIDATION
// runs on GitHub's runners, which have no game and never will. Nothing here reads sqpack, calls
// the wiki, or shells out to tools/ — it reads the committed file and checks that it is
// internally sound and canonically formatted.
//
// data/validate-unlocks.mjs already checks the schema field by field. This is the other half:
// the properties that make a REGENERATION reviewable rather than a wall of churn, and the one
// property the original defect turned on — that no entry claims to be checkable when nothing in
// the file says what would check it.
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const DATASET = path.join(path.dirname(fileURLToPath(import.meta.url)), 'unlocks-by-level.json');
const raw = fs.readFileSync(DATASET, 'utf8');
const d = JSON.parse(raw);

let errors = 0;
const err = (m) => { console.error(m); errors++; };

// Kept in step with ENTRY_KEYS in scripts/build-unlock-catalogue.mjs. Key ORDER is part of the
// canonical form, not decoration: without it a regeneration diff can show every entry as changed
// because a serialiser emitted the same facts in a different order.
const ENTRY_KEYS = [
  'level', 'levelSource', 'category', 'unlock', 'type', 'reward', 'quest', 'questAnyOf',
  'questKind', 'notes', 'description', 'priority', 'cosmetic', 'requires', 'confidence', 'sources',
];

// ---------------------------------------------------------------- 1. canonical form
//
// The file must be exactly what the generator would write: 2-space indent, fixed key order,
// trailing newline. This is the stable-ordering check. It also makes a hand edit visible as a
// formatting failure instead of silently surviving until the next regeneration reverts it.
const canonical = `${JSON.stringify({
  source: d.source,
  fetched: d.fetched,
  notes: d.notes,
  unlocks: d.unlocks.map((e) => {
    const o = {};
    for (const k of ENTRY_KEYS) if (k in e) o[k] = e[k];
    for (const k of Object.keys(e)) if (!ENTRY_KEYS.includes(k)) o[k] = e[k];
    return o;
  }),
}, null, 2)}\n`;

if (canonical !== raw) {
  // Point at the first differing line rather than saying "it differs".
  const a = raw.split('\n');
  const b = canonical.split('\n');
  const at = a.findIndex((l, i) => l !== b[i]);
  err(`not in canonical form (first difference at line ${at + 1}):\n  committed:  ${JSON.stringify(a[at])}\n  canonical:  ${JSON.stringify(b[at])}\n  Regenerate with: node scripts/build-unlock-catalogue.mjs --write`);
}

// ---------------------------------------------------------------- 2. stable ordering
//
// Entries are emitted in the source guide's own order, which runs level by level. A level that
// goes backwards means two entries were reordered by hand or an edit was applied to the wrong
// row; either way the next regeneration would produce a diff that is impossible to read.
let previous = 0;
let levellessSeen = false;
for (const [i, e] of d.unlocks.entries()) {
  if (typeof e.level !== 'number') {
    // Level-less entries are their own sections and sort after everything with a level.
    levellessSeen = true;
    continue;
  }
  if (levellessSeen) err(`#${i} ${e.unlock}: has a level but follows an entry that has none — level-less entries sort last`);
  if (e.level < previous) err(`#${i} ${e.unlock}: level ${e.level} follows level ${previous} — entries must be in non-decreasing level order`);
  previous = Math.max(previous, e.level);
}

// ---------------------------------------------------------------- 3. one identity each
//
// The failure this whole exercise came from: an entry whose identity is a STRING. A name that
// matches nothing is not an identity, and 180 entries shipped that way. Every entry must record
// either the game rows it rests on, or an explicit admission that it rests on nothing.
//
// Identity and gradeability are two different things, and the file distinguishes them. A Quest
// row is a GATE: the client records whether the player completed it, so an entry citing one can
// be graded and must not also claim to be unverifiable. A ContentFinderCondition or Item row is
// an IDENTITY without being a gate: the guide says the Ultimate opens after clearing Sigmascape
// and that the Aquapolis is entered with a treasure map, and both facts are checkable — but
// whether the player then took the unlock itself is written nowhere a plugin can read. Those
// entries carry real rows AND requires.unverifiable, and that combination is correct.
const questRows = (e) => (e.sources ?? [])
  .filter((s) => typeof s === 'string' && s.startsWith('game-data:Quest#'))
  .map((s) => Number(s.slice('game-data:Quest#'.length)));

for (const [i, e] of d.unlocks.entries()) {
  const where = `#${i} ${e.unlock}`;
  const rows = questRows(e);
  const ids = (e.sources ?? []).filter((s) => typeof s === 'string' && /^game-data:[A-Za-z]+#\d+$/.test(s));

  if (ids.length === 0) {
    // No game row at all. That is allowed, and honest — but only when the entry says so, so the
    // status calculator can refuse to grade it instead of falling through to Available.
    if (e.requires?.unverifiable !== true)
      err(`${where}: records no game row, so it must carry requires.unverifiable:true — an entry may not be silently identity-less`);
    if (e.confidence !== 'unverified')
      err(`${where}: records no game row, so its confidence cannot be '${e.confidence}'`);
  } else if (rows.length > 0 && e.requires?.unverifiable === true) {
    err(`${where}: is marked unverifiable but cites ${rows.map((n) => `Quest#${n}`).join(', ')}, whose completion the client records — one of the two is wrong`);
  } else if (rows.length === 0 && e.requires?.unverifiable !== true) {
    err(`${where}: cites ${ids.join(', ')} but no Quest row, so nothing says whether the unlock was taken — it must carry requires.unverifiable:true`);
  }

  // A duty gate names an InstanceContent row, and the entry has to cite the
  // ContentFinderCondition row it came from, so the two ids can be traced back to one duty.
  if ((e.requires?.duties?.length ?? 0) > 0
    && !ids.some((s) => s.startsWith('game-data:ContentFinderCondition#')))
    err(`${where}: gates on a duty but cites no 'game-data:ContentFinderCondition#' source for it`);

  for (const s of ids) {
    const n = Number(s.split('#')[1]);
    if (!Number.isInteger(n) || n <= 0) err(`${where}: '${s}' is not a usable row id`);
  }

  // questAnyOf is a claim about which rows are interchangeable. Every id in it has to be one the
  // entry itself cites, or the set says something the sources do not.
  for (const n of e.questAnyOf ?? []) {
    if (!rows.includes(n)) err(`${where}: questAnyOf cites Quest#${n}, which this entry does not otherwise cite`);
  }

  // A level has to point at something in the same file's vocabulary: the guide section it came
  // from, or one of the quest rows this very entry cites. A levelSource naming a row the entry
  // does not otherwise reference is a level borrowed from somewhere unexamined.
  if (typeof e.levelSource === 'string' && e.levelSource.startsWith('game-data:Quest#')) {
    const n = Number(e.levelSource.slice('game-data:Quest#'.length));
    if (!rows.includes(n))
      err(`${where}: levelSource cites Quest#${n}, which this entry does not otherwise cite`);
  }
}

// ---------------------------------------------------------------- 4. no duplicate identities
//
// Several entries MAY share a name and a level: that is how the catalogue models one unlock with
// alternative quests, one per starting city or Grand Company ("Levequests" at level 10 is three
// entries, Leves of Bentbranch / Horizon / Swiftperch). UnlockStatusCalculator groups on
// (unlock, level) precisely so that completing any one of them marks the group done.
//
// What must not happen is two entries that are the same unlock at the same level bound to the
// SAME quest rows. That is not an alternative, it is one thing listed twice — two checklist rows
// that can never disagree with each other, so nothing downstream would ever notice.
const seen = new Map();
for (const [i, e] of d.unlocks.entries()) {
  const rows = questRows(e).sort((a, b) => a - b);
  const identity = [
    e.unlock,
    typeof e.level === 'number' ? e.level : `cat:${e.category}`,
    rows.length ? rows.join('+') : `no-rows:${e.quest ?? ''}`,
  ].join('|');
  if (seen.has(identity)) err(`#${i} ${e.unlock}: duplicate of #${seen.get(identity)} — same unlock, same level, same quest rows`);
  else seen.set(identity, i);
}

// ---------------------------------------------------------------- 5. summary
const withRows = d.unlocks.filter((e) => questRows(e).length > 0).length;
const anyOf = d.unlocks.filter((e) => (e.questAnyOf?.length ?? 0) > 0).length;
const dutyGated = d.unlocks.filter((e) => (e.requires?.duties?.length ?? 0) > 0).length;
const itemGated = d.unlocks.filter((e) => (e.requires?.items?.length ?? 0) > 0).length;
const levelless = d.unlocks.filter((e) => typeof e.level !== 'number');
console.log(errors
  ? `FAILED: ${errors} errors`
  : `OK: ${d.unlocks.length} entries, ${withRows} bound to a quest row (${anyOf} to a set of them), `
    + `${dutyGated} gated on a duty clear, ${itemGated} on an item, `
    + `${d.unlocks.length - withRows} not gradeable, ${levelless.length} with no level `
    + `(${[...new Set(levelless.map((e) => e.category))].join('; ') || 'none'}), canonical form verified`);
process.exit(errors ? 1 : 0);
