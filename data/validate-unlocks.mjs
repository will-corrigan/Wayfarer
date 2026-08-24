import { ALL as REWARD_KINDS } from './reward-kinds.mjs';

const d = (await import('./unlocks-by-level.json', { with: { type: 'json' } })).default;
const prios = new Set(['essential', 'nice', 'optional']);
const confidences = new Set(['verified', 'single-source', 'unverified']);
const types = new Set(['alliance-raid', 'dungeon', 'emote', 'minion', 'mount', 'raid', 'system', 'trial', 'zone']);
const questKinds = new Set(['classquest', 'msq', 'other', 'sidequest']);
const MAX_LEVEL = 110;
// Two fewer than the 588 the first regeneration produced: both belonged to the unreleased-
// expansion guide page, which is the previous expansion's page with the quest names blanked, and
// neither described content that exists. See data/README.md.
//
// +1 for "The Wing Spirit Cometh" (Quest#71005, Wings of Legacy): a real, live Dawntrail trophy-
// mount quest the wiki guide does not list, added by the generator's committed-overrides
// mechanism — see NEW_TROPHY_MOUNT_ENTRIES in scripts/build-unlock-catalogue.mjs.
const EXPECTED = 587;
let errors = 0;
const err = (m) => { console.error(m); errors++; };
if (!Array.isArray(d.unlocks) || d.unlocks.length !== EXPECTED) err(`unlocks length ${d.unlocks?.length} != ${EXPECTED}`);

// Anything this file does not name is a field the plugin does not read. System.Text.Json drops an
// unknown property without a word, so a misspelt 'requiers' or a requirement kind nobody
// implemented (`requires.achievements`) would ship looking like a real constraint and be enforced
// by nothing. A key that is not on a list here is a mistake, by definition.
const checkKeys = (where, obj, allowed) => {
  for (const k of Object.keys(obj)) if (!allowed.has(k)) err(`${where}: unknown field '${k}'`);
};

// The dataset is deserialised into fixed C# types, so a value of the wrong JSON kind is not a
// lenient coercion — it throws inside JsonSerializer and takes the whole unlocks feature down with
// it. Every scalar the plugin reads is type-checked here so that failure cannot leave the repo.
const checkScalar = (where, obj, key, kind, optional = false) => {
  if (!(key in obj) || obj[key] === null) {
    if (!optional) err(`${where}: missing '${key}'`);
    return false;
  }
  const v = obj[key];
  const ok = kind === 'uint' ? Number.isInteger(v) && v >= 0
    : kind === 'int' ? Number.isInteger(v)
    : typeof v === kind;
  if (!ok) err(`${where}: '${key}' must be ${kind}, got ${JSON.stringify(v)}`);
  return ok;
};

const collectibleKeys = {
  mounts: new Set(['id', 'name', 'from', 'level']),
  minions: new Set(['id', 'name', 'from', 'level']),
  items: new Set(['id', 'name', 'from', 'level', 'count', 'keyItem']),
  jobs: new Set(['id', 'name', 'level']),
  // 'id' here is an InstanceContent row id, which is what UIState.IsInstanceContentCompleted
  // takes — NOT the ContentFinderCondition row id the sources list cites for the same duty.
  duties: new Set(['id', 'name', 'from', 'level']),
};

const checkCollectible = (where, kind, c) => {
  if (typeof c !== 'object' || c === null || Array.isArray(c)) { err(`${where}: collectible must be an object`); return; }
  checkKeys(where, c, collectibleKeys[kind]);
  if (!Number.isInteger(c.id) || c.id <= 0) err(`${where}: collectible needs a positive id`);
  if (typeof c.name !== 'string' || c.name.length === 0) err(`${where}: collectible needs a name`);
  if ('from' in c && c.from !== null && typeof c.from !== 'string') err(`${where}: bad 'from'`);
  if ('keyItem' in c) checkScalar(where, c, 'keyItem', 'boolean', true);
  if ('level' in c && (!Number.isInteger(c.level) || c.level < 1 || c.level > MAX_LEVEL))
    err(`${where}: collectible level out of range`);
};

// The failure this whole schema exists to stop: an entry whose requirements the plugin cannot
// establish must say so, so the status calculator can refuse to call it available. "No gate
// found" is not the same fact as "no gate exists" — quest row 67086 has every gate column empty
// and still needs seven Extreme-trial mounts.
const lists = ['mounts', 'minions', 'items', 'jobs', 'duties'];
const requiresKeys = new Set([...lists, 'label', 'unverifiable', 'minLevel', 'requiresAnotherPlayer']);

const checkRequires = (where, r) => {
  if (typeof r !== 'object' || r === null || Array.isArray(r)) { err(`${where}: 'requires' must be an object`); return; }
  checkKeys(`${where} requires`, r, requiresKeys);
  for (const k of lists) {
    if (!(k in r)) continue;
    if (!Array.isArray(r[k])) { err(`${where}: requires.${k} must be an array`); continue; }
    for (const c of r[k]) checkCollectible(`${where} requires.${k}`, k, c);
  }
  for (const it of r.items ?? []) {
    if ('count' in it && (!Number.isInteger(it.count) || it.count < 1)) err(`${where}: requires.items count must be >= 1`);
  }
  for (const j of r.jobs ?? []) {
    if (!Number.isInteger(j.level) || j.level < 1 || j.level > MAX_LEVEL) err(`${where}: requires.jobs level out of range`);
  }
  if ('minLevel' in r && (!Number.isInteger(r.minLevel) || r.minLevel < 1 || r.minLevel > MAX_LEVEL))
    err(`${where}: requires.minLevel out of range`);
  if ('unverifiable' in r && typeof r.unverifiable !== 'boolean') err(`${where}: requires.unverifiable must be a boolean`);
  // A social requirement — a partner, a ceremony — is a different fact from 'unverifiable': it is
  // not that this plugin lacks a reader for it, it is that no reader could ever exist, because
  // the missing state lives on another player's client. The two are never both true of the same
  // requirement (one says "we don't know yet", the other says "this can never be known here"),
  // so a 'requires' block that sets both is a mistake, not a stronger claim.
  if ('requiresAnotherPlayer' in r && typeof r.requiresAnotherPlayer !== 'boolean')
    err(`${where}: requires.requiresAnotherPlayer must be a boolean`);
  if (r.requiresAnotherPlayer === true && r.unverifiable === true)
    err(`${where}: requires.requiresAnotherPlayer and requires.unverifiable are mutually exclusive`);
  if ('label' in r && (typeof r.label !== 'string' || r.label.length < 4)) err(`${where}: requires.label too short`);

  const hasConcrete = lists.some((k) => (r[k]?.length ?? 0) > 0) || 'minLevel' in r;
  const hasEnforcement = hasConcrete || r.unverifiable === true || r.requiresAnotherPlayer === true;
  if (!hasEnforcement) err(`${where}: 'requires' has neither a concrete requirement, unverifiable:true, nor requiresAnotherPlayer:true`);
  if (!hasConcrete && !r.label) err(`${where}: an unverifiable or partner-gated 'requires' must say what is missing, in 'label'`);
};

const entryKeys = new Set([
  'level', 'levelSource', 'category', 'unlock', 'type', 'reward', 'quest', 'questAnyOf',
  'questKind', 'notes', 'description', 'priority', 'cosmetic', 'requires', 'confidence', 'sources',
]);

// What the entry actually grants, as a sheet identity. The whole value of the field is that it is
// a ROW rather than prose, so all three parts have to be sound: a kind nothing can draw, an id of
// zero, or a name of nothing each turn the field back into the string it exists to replace.
//
// What is NOT checked here: whether the row exists, or whether it has an icon. Both need sqpack
// and CI has no game. The generator checks them at the moment it writes the field — see the
// icon-bearing rule in scripts/build-unlock-catalogue.mjs — which is the only place that can.
const rewardKeys = new Set(['kind', 'id', 'name']);

const checkReward = (where, r) => {
  if (typeof r !== 'object' || r === null || Array.isArray(r)) {
    err(`${where}: 'reward' must be an object`);
    return;
  }
  checkKeys(`${where} reward`, r, rewardKeys);
  if (!REWARD_KINDS.includes(r.kind))
    err(`${where}: reward kind '${r.kind}' is not one of data/reward-kinds.mjs' kinds`);
  if (!Number.isInteger(r.id) || r.id <= 0) err(`${where}: reward needs a positive row id`);

  // A reward with no name cannot be said out loud, and saying it is the point: KamiToolKit
  // registers tooltips on mouse events only, so an icon is not a substitute on a controller.
  if (typeof r.name !== 'string' || r.name.trim().length === 0)
    err(`${where}: reward needs a name — an icon on its own is unreadable on a pad`);
};

// A set of quest rows any ONE of which completes this unlock — the Grand Company, starting-city
// and relic-weapon variants. Written as row ids rather than a name because that is the whole
// point: the name is what was ambiguous. Each id has to be backed by its own source line, so an
// id can never appear here without the evidence that put it there.
const checkQuestAnyOf = (where, e) => {
  if (!('questAnyOf' in e)) return;
  if (!Array.isArray(e.questAnyOf) || e.questAnyOf.length < 2) {
    err(`${where}: 'questAnyOf' must be a list of at least two quest row ids`);
    return;
  }
  const seenIds = new Set();
  for (const id of e.questAnyOf) {
    if (!Number.isInteger(id) || id <= 0) err(`${where}: questAnyOf id ${JSON.stringify(id)} must be a positive integer`);
    else if (seenIds.has(id)) err(`${where}: questAnyOf lists ${id} twice`);
    else seenIds.add(id);
    if (!(e.sources ?? []).includes(`game-data:Quest#${id}`))
      err(`${where}: questAnyOf cites ${id} with no matching 'game-data:Quest#${id}' source`);
  }
  if (e.requires?.unverifiable === true)
    err(`${where}: an entry with questAnyOf has a checkable gate and must not also be unverifiable`);
};

// An entry duplicated verbatim is two rows of the same thing in the checklist, and — since the two
// share a name and a level — one group that can never disagree with itself, so nothing downstream
// would ever notice.
const seen = new Map();

for (const [i, e] of d.unlocks.entries()) {
  const where = `#${i} ${e.unlock}`;
  if (typeof e !== 'object' || e === null || Array.isArray(e)) { err(`${where}: entry must be an object`); continue; }
  checkKeys(where, e, entryKeys);
  if (typeof e.description !== 'string' || e.description.length < 20 || e.description.length > 400)
    err(`${where}: bad description`);
  if (!prios.has(e.priority)) err(`${where}: bad priority '${e.priority}'`);
  if (typeof e.cosmetic !== 'boolean') err(`${where}: bad cosmetic`);
  for (const k of ['unlock', 'type', 'quest', 'questKind', 'notes'])
    if (!(k in e)) err(`${where}: lost original field ${k}`);

  // The name and the level together identify an unlock, and the status calculator relies on that:
  // entries sharing both are treated as interchangeable quests for one unlock, so completing any
  // one marks them all done. Everything the pair is made of has to be sound.
  checkScalar(where, e, 'unlock', 'string');
  if (typeof e.unlock === 'string' && e.unlock.trim().length === 0) err(`${where}: 'unlock' is blank`);

  // No invented levels. Five sections of the source guide state no level at all, and the
  // original import silently filled them with the previous expansion's level cap — 13 entries
  // at a number no source had ever said. So a level may only be present when the entry records
  // where it came from, and an entry with no level must say what it is instead: it is not
  // level 0, it has no level requirement, and it belongs in its own section rather than sorted
  // among level-1 content.
  if ('level' in e && e.level !== null) {
    if (checkScalar(where, e, 'level', 'int') && (e.level < 1 || e.level > MAX_LEVEL))
      err(`${where}: level ${e.level} out of range 1..${MAX_LEVEL}`);
    if (typeof e.levelSource !== 'string' || e.levelSource.length === 0)
      err(`${where}: has a level but does not record what grounds it in 'levelSource'`);
    if ('category' in e)
      err(`${where}: has a level, so 'category' (which is for level-less entries) does not apply`);
  } else {
    if ('levelSource' in e) err(`${where}: has no level, so 'levelSource' says nothing`);
    if (typeof e.category !== 'string' || e.category.length < 4)
      err(`${where}: has no level, so it needs a 'category' naming what it is`);
  }
  if (!types.has(e.type)) err(`${where}: unknown type '${e.type}'`);
  if (e.questKind !== null && !questKinds.has(e.questKind)) err(`${where}: unknown questKind '${e.questKind}'`);
  checkScalar(where, e, 'quest', 'string', true);
  if (typeof e.quest === 'string' && e.quest.trim().length === 0)
    err(`${where}: 'quest' is whitespace — use null for "no quest recorded"`);
  checkScalar(where, e, 'notes', 'string', true);
  if (e.unlock.includes('???')) err(`${where}: wiki placeholder rows are not shippable`);

  const fingerprint = JSON.stringify(e, Object.keys(e).sort());
  if (seen.has(fingerprint)) err(`${where}: duplicate of #${seen.get(fingerprint)}`);
  else seen.set(fingerprint, i);

  if (!confidences.has(e.confidence)) err(`${where}: bad confidence '${e.confidence}'`);
  if (!Array.isArray(e.sources) || e.sources.length === 0 || e.sources.some((s) => typeof s !== 'string' || !s))
    err(`${where}: 'sources' must be a non-empty list of strings`);
  else if (e.confidence === 'verified' && e.sources.length < 2)
    err(`${where}: 'verified' needs at least two independent sources`);

  if ('requires' in e) checkRequires(where, e.requires);

  // Absent is a real answer: most `system` entries open a feature the game keeps no row for, and
  // presenting that as a gap would be a lie about the game rather than about the catalogue.
  if ('reward' in e) checkReward(where, e.reward);
  checkQuestAnyOf(where, e);

  // No entry may be silently identity-less. Every one has to say what it is gated on — a quest, a
  // set of quests, a curated requirement — or say out loud that it cannot be checked.
  const identified = e.quest !== null
    || (e.questAnyOf?.length ?? 0) > 0
    || e.requires?.unverifiable === true
    || e.requires?.requiresAnotherPlayer === true
    || lists.some((k) => (e.requires?.[k]?.length ?? 0) > 0)
    || 'minLevel' in (e.requires ?? {});
  if (!identified) err(`${where}: has no identity at all — no quest, no questAnyOf, no requires`);

  // An entry with no quest, or one nothing in the game data backs, has no discoverable gate at
  // all. Without an explicit unverifiable marker the calculator would fall through to Available
  // and tell the player to go and get something they cannot get.
  const unbacked = (e.quest === null && (e.questAnyOf?.length ?? 0) === 0) || e.confidence === 'unverified';
  if (unbacked && e.requires?.unverifiable !== true && e.requires?.requiresAnotherPlayer !== true)
    err(`${where}: nothing backs this entry, so it needs requires.unverifiable:true (or requiresAnotherPlayer:true)`);
  if (e.requires?.unverifiable === true && e.confidence !== 'unverified')
    err(`${where}: an unverifiable requirement cannot be better than 'unverified' confidence`);
}

// 'verified' claims two independent sources agree on what unlocks this. When two entries cite the
// same Quest row at levels well apart, they cannot both be right about what that quest unlocks —
// so neither of them is corroborated, whatever each one says on its own. A gap of one level is the
// wiki's own table rounding and is left alone; anything wider is a genuine conflict, and it has to
// be recorded rather than asserted away.
const LEVEL_AGREEMENT_SLACK = 1;
const byQuestRow = new Map();
for (const [i, e] of d.unlocks.entries()) {
  const row = (e.sources ?? []).find((s) => typeof s === 'string' && s.startsWith('game-data:Quest#'));
  if (row) byQuestRow.set(row, [...(byQuestRow.get(row) ?? []), i]);
}
for (const [row, idx] of byQuestRow) {
  if (idx.length < 2) continue;
  // A level-less entry states no level, so it cannot disagree with one.
  const levels = idx.map((i) => d.unlocks[i].level).filter((l) => typeof l === 'number');
  if (levels.length < 2) continue;
  if (Math.max(...levels) - Math.min(...levels) <= LEVEL_AGREEMENT_SLACK) continue;
  for (const i of idx) {
    const e = d.unlocks[i];
    if (e.confidence === 'verified')
      err(`#${i} ${e.unlock}: cites ${row} at level ${e.level}, which other entries place at ${levels.filter((l) => l !== e.level).join('/')} — sources disagree, so this is not 'verified'`);
    if (!/level disputed/i.test(e.notes ?? ''))
      err(`#${i} ${e.unlock}: shares ${row} with an entry at a different level and must say so in 'notes'`);
  }
}

const counts = d.unlocks.reduce((a, e) => ({ ...a, [e.confidence]: (a[e.confidence] ?? 0) + 1 }), {});
const rewarded = d.unlocks.filter((e) => e.reward).length;
console.log(errors
  ? `FAILED: ${errors} errors`
  : `OK: ${d.unlocks.length} entries valid (${JSON.stringify(counts)}); `
    + `${rewarded} carry a reward identity, ${d.unlocks.length - rewarded} have none the game states`);
process.exit(errors ? 1 : 0);
