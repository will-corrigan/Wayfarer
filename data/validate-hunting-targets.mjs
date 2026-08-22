const d = (await import('./hunting-targets.json', { with: { type: 'json' } })).default;

let errors = 0;
const err = (m) => { console.error(m); errors++; };

const expectedLogKeys = ['1', '2', '3', '4', '5', '6', '7', '26', '29', '10001', '10002', '10003'];
const classJobKeys = new Set(['1', '2', '3', '4', '5', '6', '7', '26', '29']);
const eliteKeys = new Set(['10001', '10002', '10003']);

const logKeys = Object.keys(d.logs ?? {});
if (logKeys.length !== 12 || !expectedLogKeys.every((k) => logKeys.includes(k)))
  err(`logs keys ${JSON.stringify(logKeys)} != expected ${JSON.stringify(expectedLogKeys)}`);

let totalMonsterRecords = 0;
let totalTasks = 0;
const bNpcNameIds = new Set();
let routableFalseCount = 0;

for (const [key, log] of Object.entries(d.logs ?? {})) {
  if (classJobKeys.has(key)) {
    if (log.kind !== 'classJob') err(`log ${key}: expected kind 'classJob', got '${log.kind}'`);
    if (log.classJobId !== Number(key)) err(`log ${key}: classJobId ${log.classJobId} != ${key}`);
    if (!Array.isArray(log.ranks) || log.ranks.length !== 5)
      err(`log ${key}: expected 5 ranks, got ${log.ranks?.length}`);
  } else if (eliteKeys.has(key)) {
    if (log.kind !== 'grandCompanyElite') err(`log ${key}: expected kind 'grandCompanyElite', got '${log.kind}'`);
    if (!Array.isArray(log.ranks) || log.ranks.length !== 3)
      err(`log ${key}: expected 3 ranks, got ${log.ranks?.length}`);
  } else {
    err(`log ${key}: unexpected jobKey`);
    continue;
  }

  for (const [rankIdx, rank] of (log.ranks ?? []).entries()) {
    if (rank.rank !== rankIdx + 1) err(`log ${key}: rank[${rankIdx}].rank ${rank.rank} != ${rankIdx + 1}`);
    if (!Array.isArray(rank.tasks) || rank.tasks.length !== 10)
      err(`log ${key} rank ${rank.rank}: expected 10 tasks, got ${rank.tasks?.length}`);

    for (const [taskIdx, task] of (rank.tasks ?? []).entries()) {
      totalTasks++;
      if (task.taskIndex !== taskIdx) err(`log ${key} rank ${rank.rank}: task[${taskIdx}].taskIndex ${task.taskIndex} != ${taskIdx}`);
      if (!Array.isArray(task.monsters) || task.monsters.length === 0)
        err(`log ${key} rank ${rank.rank} task ${taskIdx}: no monsters`);

      for (const [monIdx, mon] of (task.monsters ?? []).entries()) {
        totalMonsterRecords++;
        if (mon.monsterIndex !== monIdx)
          err(`log ${key} rank ${rank.rank} task ${taskIdx}: monster[${monIdx}].monsterIndex ${mon.monsterIndex} != ${monIdx}`);
        if (typeof mon.bNpcNameId !== 'number') err(`log ${key} rank ${rank.rank} task ${taskIdx} monster ${monIdx}: bad bNpcNameId`);
        bNpcNameIds.add(mon.bNpcNameId);
        if (typeof mon.requiredKills !== 'number' || mon.requiredKills <= 0)
          err(`log ${key} rank ${rank.rank} task ${taskIdx} monster ${monIdx}: bad requiredKills`);

        if (!Array.isArray(mon.locations) || mon.locations.length === 0)
          err(`log ${key} rank ${rank.rank} task ${taskIdx} monster ${monIdx}: empty locations`);

        let primaryCount = 0;
        for (const [locIdx, loc] of (mon.locations ?? []).entries()) {
          if (loc.isPrimary) {
            primaryCount++;
            if (locIdx !== 0) err(`log ${key} rank ${rank.rank} task ${taskIdx} monster ${monIdx}: isPrimary at index ${locIdx} != 0`);
          }

          if (loc.routable === false) {
            routableFalseCount++;
            if (typeof loc.dutyTerritoryTypeId !== 'number')
              err(`log ${key} rank ${rank.rank} task ${taskIdx} monster ${monIdx} loc ${locIdx}: routable:false without dutyTerritoryTypeId`);
          } else if (loc.dutyTerritoryTypeId !== undefined && typeof loc.dutyTerritoryTypeId !== 'number') {
            err(`log ${key} rank ${rank.rank} task ${taskIdx} monster ${monIdx} loc ${locIdx}: bad dutyTerritoryTypeId`);
          }

          if (typeof loc.territoryTypeId !== 'number' || typeof loc.mapId !== 'number'
            || typeof loc.x !== 'number' || typeof loc.y !== 'number')
            err(`log ${key} rank ${rank.rank} task ${taskIdx} monster ${monIdx} loc ${locIdx}: bad coordinate shape`);
        }

        if (primaryCount !== 1)
          err(`log ${key} rank ${rank.rank} task ${taskIdx} monster ${monIdx}: expected exactly 1 isPrimary, got ${primaryCount}`);
      }
    }
  }
}

if (totalTasks !== 540) err(`total tasks ${totalTasks} != 540`);
if (totalMonsterRecords !== 666) err(`total monster records ${totalMonsterRecords} != 666`);
// 362, not the prep report's 361: 6 records had a curation slip (upstream Hunty data used a
// same-display-name duplicate BNpcName row id) fixed against live MonsterNote/MonsterNoteTarget
// sheet data 2026-08-22 (see THIRD_PARTY_NOTICES / commit history) — the fix nets +1 distinct id.
if (bNpcNameIds.size !== 362) err(`distinct bNpcNameId count ${bNpcNameIds.size} != 362`);
if (routableFalseCount !== 25) err(`routable:false count ${routableFalseCount} != 25`);

console.log(errors
  ? `FAILED: ${errors} errors`
  : `OK: 12 logs, 540 tasks, 666 monster records, ${bNpcNameIds.size} distinct bNpcNameId, ${routableFalseCount} non-routable`);
process.exit(errors ? 1 : 0);
