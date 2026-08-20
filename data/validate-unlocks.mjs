const d = (await import('./unlocks-by-level.json', { with: { type: 'json' } })).default;
const prios = new Set(['essential', 'nice', 'optional']);
let errors = 0;
const err = (m) => { console.error(m); errors++; };
if (!Array.isArray(d.unlocks) || d.unlocks.length !== 621) err(`unlocks length ${d.unlocks?.length} != 621`);
for (const [i, e] of d.unlocks.entries()) {
  if (typeof e.description !== 'string' || e.description.length < 20 || e.description.length > 400)
    err(`#${i} ${e.unlock}: bad description`);
  if (!prios.has(e.priority)) err(`#${i} ${e.unlock}: bad priority '${e.priority}'`);
  if (typeof e.cosmetic !== 'boolean') err(`#${i} ${e.unlock}: bad cosmetic`);
  for (const k of ['level', 'unlock', 'type', 'quest', 'questKind', 'notes'])
    if (!(k in e)) err(`#${i} ${e.unlock}: lost original field ${k}`);
}
console.log(errors ? `FAILED: ${errors} errors` : 'OK: 621 entries valid');
process.exit(errors ? 1 : 0);
