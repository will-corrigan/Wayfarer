const d = (await import('./unlocks-by-level.json', { with: { type: 'json' } })).default;
const prios = new Set(['essential', 'nice', 'optional']);
const confidences = new Set(['verified', 'single-source', 'unverified']);
const EXPECTED = 588;
let errors = 0;
const err = (m) => { console.error(m); errors++; };
if (!Array.isArray(d.unlocks) || d.unlocks.length !== EXPECTED) err(`unlocks length ${d.unlocks?.length} != ${EXPECTED}`);

const checkCollectible = (where, c) => {
  if (!Number.isInteger(c?.id) || c.id <= 0) err(`${where}: collectible needs a positive id`);
  if (typeof c?.name !== 'string' || c.name.length === 0) err(`${where}: collectible needs a name`);
  if ('from' in c && c.from !== null && typeof c.from !== 'string') err(`${where}: bad 'from'`);
};

// The failure this whole schema exists to stop: an entry whose requirements the plugin cannot
// establish must say so, so the status calculator can refuse to call it available. "No gate
// found" is not the same fact as "no gate exists" — quest row 67086 has every gate column empty
// and still needs seven Extreme-trial mounts.
const checkRequires = (where, r) => {
  if (typeof r !== 'object' || r === null || Array.isArray(r)) { err(`${where}: 'requires' must be an object`); return; }
  const lists = ['mounts', 'minions', 'items', 'jobs'];
  for (const k of lists) {
    if (!(k in r)) continue;
    if (!Array.isArray(r[k])) { err(`${where}: requires.${k} must be an array`); continue; }
    for (const c of r[k]) checkCollectible(`${where} requires.${k}`, c);
  }
  for (const it of r.items ?? []) {
    if ('count' in it && (!Number.isInteger(it.count) || it.count < 1)) err(`${where}: requires.items count must be >= 1`);
  }
  for (const j of r.jobs ?? []) {
    if (!Number.isInteger(j.level) || j.level < 1 || j.level > 110) err(`${where}: requires.jobs level out of range`);
  }
  if ('minLevel' in r && (!Number.isInteger(r.minLevel) || r.minLevel < 1 || r.minLevel > 110))
    err(`${where}: requires.minLevel out of range`);
  if ('unverifiable' in r && typeof r.unverifiable !== 'boolean') err(`${where}: requires.unverifiable must be a boolean`);
  if ('label' in r && (typeof r.label !== 'string' || r.label.length < 4)) err(`${where}: requires.label too short`);

  const hasConcrete = lists.some((k) => (r[k]?.length ?? 0) > 0) || 'minLevel' in r;
  if (!hasConcrete && !r.unverifiable) err(`${where}: 'requires' has neither a concrete requirement nor unverifiable:true`);
  if (!hasConcrete && !r.label) err(`${where}: an unverifiable 'requires' must say what is missing, in 'label'`);
};

for (const [i, e] of d.unlocks.entries()) {
  const where = `#${i} ${e.unlock}`;
  if (typeof e.description !== 'string' || e.description.length < 20 || e.description.length > 400)
    err(`${where}: bad description`);
  if (!prios.has(e.priority)) err(`${where}: bad priority '${e.priority}'`);
  if (typeof e.cosmetic !== 'boolean') err(`${where}: bad cosmetic`);
  for (const k of ['level', 'unlock', 'type', 'quest', 'questKind', 'notes'])
    if (!(k in e)) err(`${where}: lost original field ${k}`);
  if (e.unlock.includes('???')) err(`${where}: wiki placeholder rows are not shippable`);

  if (!confidences.has(e.confidence)) err(`${where}: bad confidence '${e.confidence}'`);
  if (!Array.isArray(e.sources) || e.sources.length === 0 || e.sources.some((s) => typeof s !== 'string' || !s))
    err(`${where}: 'sources' must be a non-empty list of strings`);
  else if (e.confidence === 'verified' && e.sources.length < 2)
    err(`${where}: 'verified' needs at least two independent sources`);

  if ('requires' in e) checkRequires(where, e.requires);

  // An entry with no quest, or one nothing in the game data backs, has no discoverable gate at
  // all. Without an explicit unverifiable marker the calculator would fall through to Available
  // and tell the player to go and get something they cannot get.
  const unbacked = e.quest === null || e.confidence === 'unverified';
  if (unbacked && e.requires?.unverifiable !== true)
    err(`${where}: nothing backs this entry, so it needs requires.unverifiable:true`);
  if (e.requires?.unverifiable === true && e.confidence !== 'unverified')
    err(`${where}: an unverifiable requirement cannot be better than 'unverified' confidence`);
}

const counts = d.unlocks.reduce((a, e) => ({ ...a, [e.confidence]: (a[e.confidence] ?? 0) + 1 }), {});
console.log(errors
  ? `FAILED: ${errors} errors`
  : `OK: ${d.unlocks.length} entries valid (${JSON.stringify(counts)})`);
process.exit(errors ? 1 : 0);
