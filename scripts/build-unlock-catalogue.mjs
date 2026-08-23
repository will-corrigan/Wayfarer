#!/usr/bin/env node
// Regenerates data/unlocks-by-level.json from the Gamer Escape progression guide's WIKITEXT and
// the game's own Excel sheets.
//
// WHY THIS EXISTS
// ---------------
// The catalogue was originally hand-scraped from the guide's RENDERED TEXT. Those tables link
// every quest, duty and reward to its own page, and the scrape kept the visible label and threw
// the link away. That single decision produced 180 entries that never matched a Quest row, 7
// entries bound to the wrong row, and entries reported as Available with no discoverable gate.
// Parsing [[link targets]] instead of display text is the fix this script exists to make
// permanent, so the defect cannot come back the next time the catalogue is refreshed.
//
// WHERE IT RUNS
// -------------
// Locally, on a machine with the game installed. It resolves names against sqpack through
// tools/Wayfarer.CatalogueGen. GitHub's runners have no game installation, so generation can
// never be a CI step: the generated file is COMMITTED, and CI validates the committed file with
// data/validate-unlocks.mjs and data/validate-catalogue-identity.mjs using only what is in the
// repo. See data/README.md.
//
// WHAT IT GENERATES AND WHAT IT CARRIES FORWARD
// ---------------------------------------------
// Generated from source, every run:
//   quest        the display name of the Quest row the guide's link target resolves to
//   sources      the provenance list, including every game row id the identity rests on
//   confidence   verified / single-source / unverified, decided by how the identity resolved
// Carried forward from the committed dataset, which is the curation store:
//   unlock, level, type, questKind, notes, description, priority, cosmetic, requires
// Those are editorial judgements (player-facing prose, the deliberate choice between the level
// the guide states and the level the Quest sheet states, curated script-only requirements). They
// are not derivable from either source, so the generator preserves them and reports any entry it
// cannot find curation for rather than inventing it.
//
// The guide's own value for level and type IS read every run and compared against the curated
// one. Disagreements are recorded in the report, never silently resolved.
//
// DETERMINISM
// -----------
// Same cache in, byte-identical file out: sheet resolution is ordered by row id, entries keep the
// guide's row order within a level, and the file is serialised canonically (2-space indent, fixed
// key order, trailing newline) so a regeneration diff shows only what actually changed.
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const DATASET = path.join(REPO, 'data', 'unlocks-by-level.json');
const TOOL = path.join(REPO, 'tools', 'Wayfarer.CatalogueGen', 'Wayfarer.CatalogueGen.csproj');

const API = 'https://ffxiv.gamerescape.com/w/api.php';
// The wiki namespace the progression guide lives in. Enumerating it is what makes a new
// expansion's page appear on its own instead of being missed until someone notices.
const GUIDE_NAMESPACE = 800;
const GUIDE_TITLE = /Progression and Level Locked Content$/;

// Politeness. A contactable identity and a floor on the request interval are the two things a
// wiki asks of an automated reader; the cache is what keeps a re-run from asking again at all.
const USER_AGENT =
  'WayfarerCatalogueGenerator/1.0 (https://github.com/will-corrigan/Wayfarer; unlock catalogue regeneration)';
const MIN_REQUEST_INTERVAL_MS = 250;

const DEFAULT_SQPACK = '/mnt/d/SteamLibrary/steamapps/common/FINAL FANTASY XIV Online/game/sqpack';

const args = parseArgs(process.argv.slice(2));
const CACHE = path.resolve(args.cache ?? path.join(REPO, '.catalogue-cache'));
const OUT = path.resolve(args.out ?? path.join(CACHE, 'out'));

function parseArgs(argv) {
  const out = { write: false, offline: false, noCrossCheck: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--write') out.write = true;
    else if (a === '--offline') out.offline = true;
    else if (a === '--no-cross-check') out.noCrossCheck = true;
    else if (a === '--out') out.out = argv[++i];
    else if (a === '--cache') out.cache = argv[++i];
    else if (a === '--sqpack') out.sqpack = argv[++i];
    else if (a === '--help' || a === '-h') out.help = true;
    else throw new Error(`unknown argument '${a}' (try --help)`);
  }
  return out;
}

if (args.help) {
  process.stdout.write(`Usage: node scripts/build-unlock-catalogue.mjs [options]

  --write            overwrite data/unlocks-by-level.json with the candidate (default: don't)
  --offline          use only what is already cached; never contact the wiki
  --no-cross-check   skip the per-quest infobox second source (much faster, weaker evidence)
  --out DIR          where to write the candidate and the report (default <cache>/out)
  --cache DIR        wikitext cache directory (default .catalogue-cache)
  --sqpack PATH      the game's sqpack directory (default the Steam install, or WAYFARER_SQPACK)

Generation needs a local game installation. CI validates the committed dataset instead; see
data/README.md.
`);
  process.exit(0);
}

// --------------------------------------------------------------------------- wiki access

let lastRequestAt = 0;

/** Requests go through curl, not fetch(). The wiki sits behind a WAF that answers Node's HTTP
 * client with 403 whatever headers it is given, and answers curl normally — so this is not a
 * style preference, it is the difference between the generator working and not. */
function api(params) {
  const wait = MIN_REQUEST_INTERVAL_MS - (Date.now() - lastRequestAt);
  if (wait > 0) {
    const until = Date.now() + wait;
    while (Date.now() < until) { /* spin briefly; the interval is a floor, not a schedule */ }
  }
  lastRequestAt = Date.now();

  const argv = ['-sS', '--fail', '--compressed', '--max-time', '90', '-A', USER_AGENT, '--get'];
  for (const [k, v] of Object.entries(params)) argv.push('--data-urlencode', `${k}=${v}`);
  argv.push(API);

  const run = spawnSync('curl', argv, { encoding: 'utf8', maxBuffer: 128 * 1024 * 1024 });
  if (run.status !== 0) throw new Error(`curl failed (${run.status}) for ${JSON.stringify(params)}: ${run.stderr?.trim()}`);
  return JSON.parse(run.stdout);
}

/** Cache slot for a page title. Titles contain characters a filesystem will not take, so the
 * slot name is a sanitised prefix plus a hash — collisions are impossible and the directory
 * stays browsable when a fetch needs to be inspected by hand. */
function cacheSlot(kind, title) {
  const safe = title.replace(/[^A-Za-z0-9]+/g, '_').slice(0, 60);
  let h = 0x811c9dc5;
  for (let i = 0; i < title.length; i++) {
    h ^= title.charCodeAt(i);
    h = Math.imul(h, 0x01000193) >>> 0;
  }
  return path.join(CACHE, kind, `${safe}.${h.toString(16).padStart(8, '0')}.json`);
}

/** How many titles go in one API call. The wiki allows 50 and asking for 50 pages in one request
 * is markedly kinder than 50 requests; the cache means a re-run asks for none at all. */
const TITLES_PER_REQUEST = 50;

const readCached = (title) => {
  const slot = cacheSlot('pages', title);
  return fs.existsSync(slot) ? JSON.parse(fs.readFileSync(slot, 'utf8')) : null;
};

/** Fetches every title not already cached, in batches. Each cached record keeps the revision id
 * and the fetch date: that is the provenance for everything derived from the page, and it is
 * what lets a later run reproduce the same output with --offline. */
function fetchTitles(titles) {
  const todo = [...new Set(titles)].filter((t) => !readCached(t)).sort();
  if (todo.length && args.offline) throw new Error(`--offline but ${todo.length} page(s) are not cached, e.g. '${todo[0]}'`);

  for (let i = 0; i < todo.length; i += TITLES_PER_REQUEST) {
    const batch = todo.slice(i, i + TITLES_PER_REQUEST);
    const j = api({
      action: 'query', prop: 'revisions', rvprop: 'content|ids', rvslots: 'main',
      titles: batch.join('|'), format: 'json', formatversion: '2', redirects: '1',
    });

    // A title can be normalised (underscores, capitalisation) and then redirected before it
    // reaches a page, so walk both maps back to the title that was actually asked for.
    const origin = {};
    for (const n of j.query?.normalized ?? []) origin[n.to] = n.from;
    for (const r of j.query?.redirects ?? []) origin[r.to] = origin[r.from] ?? r.from;

    const seen = new Set();
    for (const p of j.query?.pages ?? []) {
      const asked = origin[p.title] ?? p.title;
      seen.add(asked);
      writeCached(asked, p.missing
        ? { title: asked, resolvedTitle: p.title, missing: true, fetched: today(), wikitext: '' }
        : {
            title: asked, resolvedTitle: p.title, missing: false,
            revid: p.revisions?.[0]?.revid ?? null, fetched: today(),
            wikitext: p.revisions?.[0]?.slots?.main?.content ?? '',
          });
    }
    // A title the API answered about under no name at all still has to be recorded, or every
    // future run will ask for it again.
    for (const t of batch) if (!seen.has(t)) writeCached(t, { title: t, missing: true, fetched: today(), wikitext: '' });
    process.stderr.write(`  fetched ${Math.min(i + batch.length, todo.length)}/${todo.length}\r`);
  }
  if (todo.length) process.stderr.write('\n');
  return new Map(titles.map((t) => [t, readCached(t)]));
}

function writeCached(title, record) {
  const slot = cacheSlot('pages', title);
  fs.mkdirSync(path.dirname(slot), { recursive: true });
  fs.writeFileSync(slot, JSON.stringify(record, null, 1));
}

/** Discovers the guide pages instead of hard-coding them. A hard-coded list is how the next
 * expansion's page goes missing for a year; enumerating the namespace means a new page shows up
 * in the report as unassigned rows the first time the generator is run after it appears. */
function discoverGuidePages() {
  const slot = path.join(CACHE, 'guide-pages.json');
  if (fs.existsSync(slot)) return JSON.parse(fs.readFileSync(slot, 'utf8'));
  if (args.offline) throw new Error(`--offline but the page list is not cached (${slot})`);

  const titles = [];
  let cont;
  do {
    const j = api({
      action: 'query', list: 'allpages', apnamespace: String(GUIDE_NAMESPACE),
      aplimit: 'max', format: 'json', formatversion: '2', ...(cont ? { apcontinue: cont } : {}),
    });
    for (const p of j.query.allpages) if (GUIDE_TITLE.test(p.title)) titles.push(p.title);
    cont = j.continue?.apcontinue;
  } while (cont);

  titles.sort((a, b) => (a < b ? -1 : a > b ? 1 : 0));
  fs.mkdirSync(CACHE, { recursive: true });
  fs.writeFileSync(slot, `${JSON.stringify(titles, null, 1)}\n`);
  return titles;
}

const today = () => new Date().toISOString().slice(0, 10);

// --------------------------------------------------------------------------- wikitext parsing

const stripComments = (s) => s.replace(/<!--[\s\S]*?-->/g, '');

/** Every [[target]] in a fragment, with the display text kept so the link can be attributed to
 * the label side or the requirement side of the row. File/Image links are chrome, not content.
 *
 * The target is returned RAW. Folding it is the resolver's job and only the resolver's, because
 * the fold has to be the one the shipping plugin uses — and at least one guide link target
 * carries an invisible U+200E that only that fold removes. */
function extractLinks(fragment) {
  const out = [];
  const re = /\[\[([^\]|]+)(?:\|([^\]]*))?\]\]/g;
  let m;
  while ((m = re.exec(fragment))) {
    let target = m[1].trim();
    if (/^(File|Image):/i.test(target)) continue;
    const isCategory = /^:?Category:/i.test(target);
    target = target.replace(/^:/, '');
    out.push({ target, display: (m[2] ?? m[1]).trim(), isCategory });
  }
  return out;
}

function stripTemplates(s) {
  let prev;
  let cur = s;
  do {
    prev = cur;
    cur = cur.replace(/\{\{([^{}]*)\}\}/g, (_, inner) => {
      const parts = inner.split('|');
      return parts.length > 1 ? parts[parts.length - 1] : '';
    });
  } while (cur !== prev);
  return cur;
}

/** The row as a player reads it: templates expanded, links reduced to their display text. Used
 * only to locate the colon that separates the thing being unlocked from the requirement, and to
 * decide which side of it each link fell on. Never used as an identity. */
function plain(s) {
  return stripTemplates(s)
    .replace(/\[\[(?:File|Image):[^\]]*\]\]/gi, '')
    .replace(/\[\[([^\]|]+)\|([^\]]*)\]\]/g, '$2')
    .replace(/\[\[:?([^\]|]+)\]\]/g, '$1')
    .replace(/'''?/g, '')
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

// The verbs a requirement clause starts with. The split colon is the LAST colon whose tail
// begins with one of these, because row labels contain colons of their own
// ("Duty Roulette: Guildhests: Unlocked after ...").
const REQUIREMENT_CLAUSE =
  /^\s*(Unlocked|Unlocks|Complete[sd]?\b|Received|Receive\b|Obtained|Obtainable|Begins|Available|Access(?:ed)?\b|Talk\b|Purchas|Requires|Rank \d|Dyeable|Class change|The Quests? start|The emote|Automatic)/i;

function splitStatement(text) {
  let at = -1;
  for (let i = text.indexOf(':'); i >= 0; i = text.indexOf(':', i + 1)) {
    if (REQUIREMENT_CLAUSE.test(text.slice(i + 1))) at = i;
  }
  if (at < 0) {
    const first = text.indexOf(':');
    at = first > 0 && first < 140 ? first : -1;
  }
  return at > 0
    ? { label: text.slice(0, at).trim(), requirement: text.slice(at + 1).trim(), colon: at }
    : { label: text, requirement: '', colon: -1 };
}

/** One content row of a guide table. `{{PGH|...}}` opens a section (and therefore a level), an
 * icon-only cell types the row, and the content cell holds one statement per `<br>`. */
function parseGuidePage(page) {
  const rows = [];
  const lines = stripComments(page.wikitext).split('\n');
  let section = null;
  let cell = null;
  let icon = null;

  const flush = () => {
    if (!cell) return;
    const raw = cell.join('\n');
    const segments = raw.split(/<br\s*\/?>/i).map((s) => s.trim()).filter(Boolean);

    // A segment with no colon is a continuation of the previous statement, not a new one.
    const statements = [];
    for (const seg of segments) {
      const p = plain(seg);
      if (!p) continue;
      const prev = statements[statements.length - 1];
      const continues = prev && (plain(prev).endsWith(':') || !p.includes(':'));
      if (continues) statements[statements.length - 1] = `${prev} <br> ${seg}`;
      else statements.push(seg);
    }

    for (const statement of statements) {
      const text = plain(statement);
      const { label, requirement, colon } = splitStatement(text);
      const links = extractLinks(statement);

      // Attribute each link to a side by walking the plain text in link order — plain() keeps
      // both the order and the display text, so a link's position in it is its position in the
      // sentence.
      let cursor = 0;
      for (const link of links) {
        const display = stripTemplates(link.display).replace(/\[\[|\]\]/g, '').trim();
        const at = display ? text.indexOf(display, cursor) : -1;
        if (at >= 0) cursor = at + display.length;
        link.side = colon > 0 && at >= 0 ? (at < colon ? 'label' : 'requirement') : 'unknown';
      }

      const level = section ? Number(/Level (\d+)/.exec(section)?.[1] ?? NaN) : NaN;
      rows.push({
        page: page.title,
        revid: page.revid ?? null,
        section,
        // A section like "Heavensward Unique Quest Rewards" states no level at all. Recording
        // that as a fact stops a regeneration from inventing one — the original scrape quietly
        // used the previous expansion's cap and put 13 entries at the wrong level.
        level: Number.isFinite(level) ? level : null,
        levelFromSection: Number.isFinite(level),
        icon,
        label,
        requirement,
        text,
        links,
        placeholder: text.includes('???'),
      });
    }
    cell = null;
    icon = null;
  };

  for (const line of lines) {
    const pgh = /\{\{PGH\|([^}]*)\}\}/.exec(line);
    if (pgh) {
      flush();
      section = pgh[1].trim();
      continue;
    }
    if (/^\|-|^\{\||^\|\}/.test(line)) {
      flush();
      continue;
    }
    if (/^[|!]/.test(line)) {
      let body = line.replace(/^\|/, '');
      const attrs = /^\s*((?:[a-zA-Z-]+\s*=\s*"[^"]*"\s*|colspan\s*=\s*\d+\s*)+)\|/.exec(body);
      if (attrs) body = body.slice(attrs[0].length);
      const iconCell = /^\s*\[\[(?:File|Image):([^|\]]+)/i.exec(body);
      if (iconCell) {
        icon = iconCell[1].trim();
        continue;
      }
      if (!body.trim()) continue;
      if (cell) cell.push(body);
      else cell = [body];
      continue;
    }
    if (cell && line.trim()) cell.push(line);
  }
  flush();
  return rows;
}

// --------------------------------------------------------------------------- resolution

/** Hands every raw link target to tools/Wayfarer.CatalogueGen, which folds it with
 * Wayfarer.Core's QuestNameKey and picks between duplicate rows with QuestNameMatch — the exact
 * algorithms the shipping plugin matches with at runtime. This script deliberately contains no
 * normalisation of its own: a second implementation is a second set of answers. */
function resolveNames(names, sqpack, questRowIds = []) {
  fs.mkdirSync(OUT, { recursive: true });
  const reqPath = path.join(OUT, 'resolve-request.json');
  const resPath = path.join(OUT, 'resolve-response.json');
  fs.writeFileSync(reqPath, JSON.stringify({ sqpack, names: [...names].sort(), questRowIds: [...questRowIds].sort((a, b) => a - b) }, null, 1));

  const run = spawnSync(
    'dotnet',
    ['run', '--project', TOOL, '-c', 'Debug', '--', 'resolve', reqPath, resPath],
    { stdio: ['ignore', 'inherit', 'inherit'], encoding: 'utf8' },
  );
  if (run.status !== 0) {
    throw new Error(
      `tools/Wayfarer.CatalogueGen exited ${run.status}. Generation needs a local game ` +
        'installation; pass --sqpack or set WAYFARER_SQPACK. CI validates the committed dataset ' +
        'instead — see data/README.md.',
    );
  }
  return JSON.parse(fs.readFileSync(resPath, 'utf8'));
}

/** Every link on the requirement side of a row, plus the ones the colon split could not place.
 * Links on the LABEL side name the thing being unlocked, not the thing that unlocks it, so they
 * can never establish a gate. */
function requirementLinks(row) {
  return row.links.filter((l) => !l.isCategory && (l.side === 'requirement' || l.side === 'unknown'));
}

/** The quest rows a row's requirement clause points at.
 *
 * Two ways to get there, in order. First the link target's own name, folded and looked up in the
 * Quest sheet. Second — and this is what recovers the links the first way cannot reach — the
 * linked page's own {{ARR Infobox Quest}} "Quest Number".
 *
 * The second route matters because the wiki disambiguates page titles with suffixes the game
 * does not use: [[Heavensward (Quest)]], [[Syrcus Tower (Quest)]], [[Simply the Hest (Gridania)]].
 * Stripping those by rule is exactly the parenthetical strip the name-reconciliation audit
 * measured and rejected — it collapses the ten "A Relic Reborn (weapon)" rows and every Grand
 * Company triple into one key. Reading the row id the page itself states costs nothing, needs no
 * such rule, and is a statement by a source rather than a guess by us. */
function questRowsFromRequirement(row, resolved, infoboxes) {
  const targets = [];
  for (const link of requirementLinks(row)) {
    const r = resolved.names[link.target];
    if (r?.questRowId != null) {
      targets.push({
        target: link.target,
        rows: r.questAnyOf.length ? [...r.questAnyOf] : [r.questRowId],
        via: 'link-target-name',
      });
      continue;
    }
    const stated = infoboxes.get(link.target);
    // A stated row id is only usable if the Quest sheet actually has that row: the wiki is
    // maintained by hand and an id that resolves to nothing is a typo, not a fact.
    if (stated != null && resolved.quests[stated]) {
      targets.push({ target: link.target, rows: [stated], via: 'linked-page-infobox' });
      continue;
    }
    // Third route, and the narrowest. No row carries this name, several carry it with a
    // parenthetical, and the guide's own sentence says a choice is being described — "one of the
    // Squadron and Commander Grand Company quests", "the applicable Let the Hunt Begin
    // sidequest", "one of the A Relic Reborn Sidequests". The whole set goes forward as
    // alternatives. Without the sentence saying so this is not taken: the set would be a guess.
    if ((r?.questVariants?.length ?? 0) > 1 && CHOICE_CLAUSE.test(row.requirement || row.text)) {
      targets.push({ target: link.target, rows: [...r.questVariants], via: 'quest-name-variants' });
    }
  }
  return targets;
}

/** Links on the LABEL side — the thing being unlocked. These never establish a gate, but when the
 * thing being unlocked is a duty the label link names it, and that row id is the entry's identity:
 * "[[The Aquapolis]] Access" is ContentFinderCondition 179 whatever opens it. */
function labelLinks(row) {
  return row.links.filter((l) => !l.isCategory && l.side === 'label');
}

/** The duty rows a set of links names, in link order. A ContentFinderCondition row carries the
 * name the wiki and the guide use; the InstanceContent row it points at is what
 * `UIState.IsInstanceContentCompleted` takes, and the resolver returns both. */
function dutyRows(links, resolved) {
  const out = [];
  for (const link of links) {
    for (const hit of resolved.names[link.target]?.sheets?.duty ?? []) {
      if (!out.some((d) => d.cfcId === hit.rowId)) {
        out.push({ cfcId: hit.rowId, contentId: hit.contentId ?? null, name: hit.name, target: link.target });
      }
    }
  }
  return out;
}

// A treasure-map page states the item you actually carry in its own {{ARR Infobox Map Chest}}
// "Type" field. The guide links the map by its DISPLAY name ("Dragonskin Treasure Map"), which is
// the key item you receive after deciphering — it is not an Item row, so the link resolves to
// nothing and the six treasure-map dungeons were left with no identity at all. The Type field is
// the linked page's own statement of the row that exists, in the same shape as the quest-page
// infobox route, and it is read the same way.
const MAP_CHEST_TYPE = /\{\{\s*ARR Infobox Map Chest[\s\S]*?\|\s*Type\s*=\s*([^\n|}]+)/i;

function infoboxMapChestType(record) {
  if (!record || record.missing) return null;
  const m = MAP_CHEST_TYPE.exec(record.wikitext);
  const name = m?.[1]?.trim();
  return name || null;
}

/** The item rows a row's requirement clause points at: directly, when the link target is an Item
 * name, or through the linked page's own map-chest "Type". */
function itemRowsFromRequirement(row, resolved, mapChestTypes) {
  const out = [];
  const take = (rowId, name, target, via) => {
    if (!out.some((i) => i.id === rowId)) out.push({ id: rowId, name, target, via });
  };
  for (const link of requirementLinks(row)) {
    const direct = resolved.names[link.target]?.sheets?.item ?? [];
    if (direct.length === 1) {
      take(direct[0].rowId, direct[0].name, link.target, 'link-target-name');
      continue;
    }
    const stated = mapChestTypes.get(link.target);
    const viaPage = stated ? resolved.names[stated]?.sheets?.item ?? [] : [];
    if (viaPage.length === 1) take(viaPage[0].rowId, viaPage[0].name, link.target, 'linked-page-map-chest');
  }
  return out;
}

// A requirement clause that offers a choice rather than a sequence. Both halves matter: "or"
// alone would take "Complete X after Y or Z", where the "after" is a chain the schema cannot
// express and picking from it would be a guess.
const ALTERNATIVE_CLAUSE = /\bor\b/i;
const CHAIN_CLAUSE = /\b(and|after|then|followed by)\b/i;

// The guide saying, in its own words, that the row describes a choice between quests rather than
// a list of them: "one of the Squadron and Commander Grand Company quests", "the applicable Let
// the Hunt Begin sidequest".
const CHOICE_CLAUSE = /\b(one of|any of|the applicable|whichever)\b/i;

/** A quest name with one trailing parenthetical removed: the base name the game's own sheet
 * disambiguates with "(Maelstrom)", "(Gridania)", "(Bravura)".
 *
 * This is NOT a matching rule and must never become one — folding the parenthetical away to bind
 * a name to a row is what the name-reconciliation audit rejected, because it makes ten different
 * relic quests indistinguishable. It is only ever asked of rows that are ALREADY identified, to
 * decide whether they are variants of one quest. */
const baseQuestName = (name) => (name ?? '').replace(/\s*\([^()]*\)$/, '').trim();

/** Whether a row's several quest rows are ALTERNATIVES — any one of which counts — rather than a
 * chain of quests all of which must be done.
 *
 * Two ways to know, and both are statements by a source rather than inferences about one. The
 * game's own sheet is the stronger: rows whose names differ only by the parenthetical the sheet
 * disambiguates them with, all at the same level, are one quest that ships once per starting city
 * or Grand Company, and a character holds exactly one — that is what the three "The Company You
 * Keep" rows are. Failing that, the guide's own sentence offers a choice and does not describe a
 * sequence. */
function statesAlternatives(row, union, resolved) {
  if (union.length < 2) return false;

  const facts = union.map((id) => resolved.quests[id]).filter(Boolean);
  if (facts.length === union.length) {
    const bases = new Set(facts.map((f) => joinKey(baseQuestName(f.name))));
    const levels = new Set(facts.map((f) => f.level));
    if (bases.size === 1 && levels.size === 1) return true;
  }

  const clause = row?.requirement || row?.text || '';
  if (CHOICE_CLAUSE.test(clause)) return true;
  return ALTERNATIVE_CLAUSE.test(clause) && !CHAIN_CLAUSE.test(clause);
}

/** The requirement link whose target is the entry's own name. Only meaningful where the colon
 * split failed and every link came back 'unknown': in a statement with no colon, the link that
 * names the unlock itself is naming the quest that IS the unlock. */
function namesItself(entry, fromGuide) {
  return fromGuide.find((t) => joinKey(t.target) === joinKey(entry.unlock)) ?? null;
}

// --------------------------------------------------------------------------- assignment

/** Folds a catalogue name or a guide label for the purpose of MATCHING A ROW TO AN ENTRY. This
 * is deliberately not QuestNameKey: it is not a game identity, it is a join between two strings
 * this repo owns on both sides, and it never decides which quest anything is. */
const joinKey = (s) =>
  (s ?? '')
    .normalize('NFKC')
    .replace(/[‘’ʼ´`]/g, "'")
    .replace(/[‐-―−]/g, '-')
    .replace(/[​-‏﻿­]/g, '')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase();

/** Ties each guide row to the catalogue entry it produced. Exact label match first; then the
 * prefix case, which exists because the original scrape sometimes cut the sentence at a
 * different point than this parser does ("Mount speed increased in eastern La Noscea after" for
 * a row labelled "... after completion of In the Company of Heroes Main Story Quest").
 *
 * Rows that match nothing and entries that are matched by nothing are both reported. A guide row
 * with no entry is new content or a placeholder; an entry with no guide row has lost its source. */
function assign(rows, entries) {
  const group = (items, keyOf) => {
    const m = new Map();
    items.forEach((it, i) => {
      const k = keyOf(it);
      if (!m.has(k)) m.set(k, []);
      m.get(k).push(i);
    });
    return m;
  };
  const rowsByLabel = group(rows, (r) => joinKey(r.label));
  const entriesByLabel = group(entries, (e) => joinKey(e.unlock));

  const rowForEntry = new Map();
  const entriesForRow = new Map();
  const claim = (rowIndex, entryIndex) => {
    if (rowForEntry.has(entryIndex)) return;
    rowForEntry.set(entryIndex, rowIndex);
    if (!entriesForRow.has(rowIndex)) entriesForRow.set(rowIndex, []);
    entriesForRow.get(rowIndex).push(entryIndex);
  };

  for (const [label, entryIdx] of entriesByLabel) {
    const rowIdx = rowsByLabel.get(label);
    if (!rowIdx) continue;

    // One label, several rows AND several entries: the guide repeats a label once per
    // expansion ("Stone, Sky, Sea Access" at 60/70/80/90/100), and so does the catalogue. They
    // have to be paired, not collapsed — collapsing them binds every expansion's entry to the
    // last expansion's quest, which is precisely the class of wrong-quest binding this
    // generator exists to prevent. Pair on the level the guide section states, which is an
    // independent fact on both sides, and fall back to document order for the rest.
    const freeRows = new Set(rowIdx);
    const unpaired = [];
    for (const ei of entryIdx) {
      const match = rowIdx.find((ri) => freeRows.has(ri) && rows[ri].levelFromSection && rows[ri].level === entries[ei].level);
      if (match === undefined) unpaired.push(ei);
      else {
        freeRows.delete(match);
        claim(match, ei);
      }
    }
    const remaining = rowIdx.filter((ri) => freeRows.has(ri));
    unpaired.forEach((ei, n) => claim(remaining[Math.min(n, remaining.length - 1)] ?? rowIdx[0], ei));
  }

  // Prefix fallback, longest label first so the most specific row wins a shared prefix. This
  // catches the rows where the original scrape cut the sentence at a different point than this
  // parser does ("Mount speed increased in eastern La Noscea after ...").
  const order = rows.map((_, i) => i).sort((a, b) => rows[b].label.length - rows[a].label.length);
  entries.forEach((entry, ei) => {
    if (rowForEntry.has(ei)) return;
    const k = joinKey(entry.unlock);
    for (const ri of order) {
      const rk = joinKey(rows[ri].label);
      if (rk.startsWith(k) || k.startsWith(rk)) {
        claim(ri, ei);
        break;
      }
    }
  });
  return { rowForEntry, entriesForRow };
}

// --------------------------------------------------------------------------- second source

/** The linked quest page's own {{ARR Infobox Quest}} "Quest Number". That field is maintained
 * separately from the guide table, so it is a genuinely independent statement of the same fact —
 * which is what the project's multi-source rule asks for. A disagreement here is recorded and
 * costs the entry its 'verified' standing; it is never resolved by picking a side. */
function infoboxQuestNumber(record) {
  if (!record || record.missing) return null;

  // `DontBot = yes` is the wiki's own marker for "this page is maintained by hand, keep the
  // import bot off it". Its Quest Number is therefore NOT the independently generated field
  // this check depends on — and on these pages it is demonstrably wrong: the hand-written
  // "Talk about the Endsinger", "Talk about Barbariccia" and "Talk about the Dragonsong War"
  // stubs all carry `Quest Number = 70541`, which is "Trial by Spire", an unrelated Dawntrail
  // sidequest. Trusting them bound three Endwalker trials to it.
  //
  // This is the whole point of the marker: a source that says it is not maintained is not a
  // second source. Reading these would be one hand-written page corroborating another.
  if (/\|\s*DontBot\s*=\s*yes/i.test(record.wikitext)) return null;

  const m = /\|\s*Quest Number\s*=\s*(\d+)/i.exec(record.wikitext);
  return m ? Number(m[1]) : null;
}

// --------------------------------------------------------------------------- emit

const ENTRY_KEYS = [
  'level', 'levelSource', 'category', 'unlock', 'type', 'quest', 'questAnyOf', 'questKind',
  'notes', 'description', 'priority', 'cosmetic', 'requires', 'confidence', 'sources',
];

// The guide types each row with an icon, and that icon is a statement by the source about what
// the row IS. The original scrape had no access to it — it read rendered text — and typed entries
// by string-matching their own names, which filed every duty whose name begins with the word
// "Mount" as a mount. Only the icons that map onto a catalogue type are listed; a row whose icon
// is not here keeps the curated type.
const TYPE_FROM_ICON = new Map([
  ['Trialicon.png', 'trial'],
  ['Raidicon.png', 'raid'],
  ['Allianceraidicon.png', 'alliance-raid'],
  ['Dungeonicon.png', 'dungeon'],
  ['Variant_Dungeonicon.png', 'dungeon'],
]);

// How an entry's level was grounded. The catalogue may not invent one: the original scrape used
// the previous expansion's level cap for five guide sections that state no level at all, which
// put 13 entries at a number no source had ever said — Golden Dhyata at 80 for a level-90 quest,
// Haurchefant (Emote) at 50 for a level-60 one.
const LEVEL_FROM_GUIDE = 'gamerescape:progression-guide-section';

// The sentence the level-disputes pass appends to `notes`. It is generated, so it is stripped
// before that pass and rewritten from the current set — otherwise a dispute that has been
// resolved leaves behind a note nothing supports and a confidence nothing justifies.
const LEVEL_DISPUTE_SENTENCE = /\s*Level disputed: [^]*?is resolved\./g;
const withoutLevelDispute = (notes) =>
  (typeof notes === 'string' ? notes.replace(LEVEL_DISPUTE_SENTENCE, '').trim() || null : notes ?? null);

/** The level, and the record of where it came from — or neither.
 *
 * An entry has a level when a source states one: the guide section it sits under, or failing
 * that the accept level of the quest it is bound to. When neither does, it has NO level. It is
 * not level 1 and it is not the expansion cap; it is a reward with no level requirement, and it
 * is presented under its own category instead of being sorted among low-level content.
 * (USER RULING 2026-08-23: "Things with no level requirement should just have their own
 * category.")
 *
 * The bound quest's level is only meaningful above 1: the trophy-mount rewards — Firebird, Kirin,
 * Kamuy of the Nine Tails, Landerwaffe, Apocryphal Bahamut — are hidden level-1 rows whose real
 * requirement is owning a set of Extreme-trial mounts, so their 1 is an absence, not a level. */
function groundLevel(entry, row, questRows, quests) {
  // Level 0 is not a level. It is what the import wrote for a row the guide records as "???" —
  // an unannounced job with no level, no quest and no release date — and it would sort that row
  // above everything a player can actually do.
  if (row?.levelFromSection && Number.isInteger(entry.level) && entry.level > 0) {
    // The guide states a level for this section. Where the catalogue deliberately records a
    // different one (the guide states when content becomes relevant, the sheet when the quest
    // can be accepted) that curated choice stands and the difference is reported.
    return { level: entry.level, levelSource: LEVEL_FROM_GUIDE };
  }

  const levels = [...new Set(questRows.map((id) => quests[id]?.level).filter((l) => Number.isFinite(l)))];
  if (levels.length === 1 && levels[0] > 1) {
    return { level: levels[0], levelSource: `game-data:Quest#${questRows[0]}` };
  }

  return { level: null, levelSource: null, category: row?.section ?? null };
}

/** The canonical serialisation. Fixed key order, 2-space indent, trailing newline — so the file
 * is a function of its content and a regeneration diff shows changed facts, not churn.
 * data/validate-catalogue-identity.mjs enforces exactly this form in CI. */
export function canonicalise(dataset) {
  const out = {
    source: dataset.source,
    fetched: dataset.fetched,
    notes: dataset.notes,
    unlocks: dataset.unlocks.map((e) => {
      const o = {};
      for (const k of ENTRY_KEYS) if (k in e) o[k] = e[k];
      for (const k of Object.keys(e)) if (!ENTRY_KEYS.includes(k)) o[k] = e[k];
      return o;
    }),
  };
  return `${JSON.stringify(out, null, 2)}\n`;
}

/** The type the sources state, or null to keep the curated one.
 *
 * Deliberately narrow. `type` is an editorial field and the generator does not own it; this
 * corrects exactly one defect, and only where two sources agree the curated value is impossible.
 * The catalogue types an entry `mount` by matching the word at the start of its own name, so the
 * five duties called "Mount Ordeals", "Mount Rokkon" and friends were filed as mounts. The guide
 * row's icon says trial or dungeon, and the label resolves to a ContentFinderCondition row and to
 * no Mount row at all — you cannot add "Another Mount Rokkon (Savage)" to your mount list. */
function typeFromSources(entry, row, resolved) {
  if (entry.type !== 'mount' || !row) return null;
  const stated = TYPE_FROM_ICON.get(row.icon ?? '');
  if (!stated) return null;

  const labelled = labelLinks(row).map((l) => resolved.names[l.target]).filter(Boolean);
  const isDuty = labelled.some((r) => (r.sheets?.duty?.length ?? 0) > 0);
  const isMount = labelled.some((r) => (r.sheets?.mount?.length ?? 0) > 0);
  return isDuty && !isMount ? stated : null;
}

const GUIDE_SOURCE = 'gamerescape:progression-guide';

/** Provenance, in a fixed order so two runs produce the same list: the guide row that names the
 * unlock, the duty rows the entry's identity and gate rest on, every quest row it rests on, the
 * items it is entered with, then curated extras (a Mount row, a script-gated marker) that no
 * source states mechanically.
 *
 * Deduplicated, first occurrence winning, because the committed file is also the input: a source
 * this function derives is carried forward in `curatedExtras` on the next run and would otherwise
 * be emitted twice. */
function buildSources(guideAssigned, questRows, curatedExtras, derived = {}) {
  const cfc = (id) => `game-data:ContentFinderCondition#${id}`;
  const before = [
    ...(derived.dutyRows ?? []).map((d) => cfc(d.cfcId)),
    ...curatedExtras.filter((s) => s.startsWith('game-data:ContentFinderCondition#')),
  ];
  const after = [
    ...(derived.itemRows ?? []).map((i) => `game-data:Item#${i.id}`),
    ...curatedExtras.filter((s) => !s.startsWith('game-data:ContentFinderCondition#')),
  ];
  return [...new Set([
    ...(guideAssigned ? [GUIDE_SOURCE] : []),
    ...before,
    ...questRows.map((id) => `game-data:Quest#${id}`),
    ...after,
  ])];
}

// --------------------------------------------------------------------------- main

async function main() {
  const sqpack = args.sqpack ?? process.env.WAYFARER_SQPACK ?? DEFAULT_SQPACK;
  const committed = JSON.parse(fs.readFileSync(DATASET, 'utf8'));
  const curated = committed.unlocks;

  const titles = discoverGuidePages();
  console.log(`guide pages discovered in namespace ${GUIDE_NAMESPACE}: ${titles.length}`);

  const fetched = fetchTitles(titles);
  const pages = titles.map((t) => fetched.get(t));
  const missing = pages.filter((p) => p.missing);
  if (missing.length) throw new Error(`could not fetch: ${missing.map((p) => p.title).join(', ')}`);

  const rows = pages.flatMap(parseGuidePage);
  console.log(`guide content rows parsed: ${rows.length} (${rows.filter((r) => r.placeholder).length} "???" placeholders)`);

  // A guide page for an expansion that has not shipped is the previous expansion's page with the
  // quest names blanked to "???". Thirty-three of the Evercold page's thirty-four rows are
  // blanked; the one that is not is an editing oversight, and the scrape imported it as a real
  // level-105 entry duplicating a level-92 one and bound to the same quest. Rather than name the
  // page — next year there will be another — the rule is the measurement: a page that is mostly
  // placeholders is describing content that does not exist yet, and nothing on it is shippable.
  const UNRELEASED_PLACEHOLDER_SHARE = 0.5;
  const unreleasedPages = new Set(
    pages
      .map((p) => rows.filter((r) => r.page === p.title))
      .filter((rs) => rs.length > 0 && rs.filter((r) => r.placeholder).length / rs.length > UNRELEASED_PLACEHOLDER_SHARE)
      .map((rs) => rs[0].page),
  );
  for (const row of rows) row.unreleased = unreleasedPages.has(row.page);
  if (unreleasedPages.size) console.log(`unreleased-expansion guide pages (mostly "???"): ${[...unreleasedPages].join(', ')}`);

  const targets = new Set();
  for (const row of rows) for (const l of row.links) if (!l.isCategory) targets.add(l.target);
  console.log(`distinct link targets to resolve: ${targets.size}`);
  let resolved = resolveNames(targets, sqpack);

  const { rowForEntry, entriesForRow } = assign(rows, curated);
  console.log(`catalogue entries tied to a guide row: ${rowForEntry.size} of ${curated.length}`);

  // Every requirement-side link on a row that produced an entry gets its page read: for a target
  // whose name did not resolve the page's infobox is the resolver, and for one that did it is
  // the independent second source the project's multi-source rule requires.
  const infoboxes = new Map();
  const mapChestTypes = new Map();
  if (!args.noCrossCheck) {
    const linked = new Set();
    for (const ri of entriesForRow.keys()) for (const l of requirementLinks(rows[ri])) linked.add(l.target);
    console.log(`reading ${linked.size} linked pages for their own {{ARR Infobox Quest}}...`);
    for (const [t, record] of fetchTitles([...linked].sort())) {
      infoboxes.set(t, infoboxQuestNumber(record));
      const chest = infoboxMapChestType(record);
      if (chest) mapChestTypes.set(t, chest);
    }

    // Row ids and item names that came from a linked page rather than from a link target still
    // need their facts, so the resolver is asked a second time with both added.
    const stated = [...new Set([...infoboxes.values()].filter((v) => v != null && !resolved.quests[v]))];
    const extraNames = [...new Set(mapChestTypes.values())].filter((n) => !(n in resolved.names));
    if (stated.length || extraNames.length) {
      for (const n of extraNames) targets.add(n);
      resolved = resolveNames(targets, sqpack, stated);
    }
  }

  // ------------------------------------------------------------------ per-entry identity
  const report = {
    generated: today(),
    sqpack,
    guidePages: pages.map((p) => ({ title: p.title, revid: p.revid, fetched: p.fetched, rows: rows.filter((r) => r.page === p.title).length })),
    counts: {},
    disagreements: [],
    unassignedGuideRows: [],
    entriesWithoutAGuideRow: [],
    droppedEntries: [],
    retypedEntries: [],
    alternativeSets: [],
    gates: [],
    levelless: [],
    crossCheck: { checked: 0, agree: 0, disagree: 0, unanswerable: 0 },
  };

  const assignments = [];
  const proposals = curated.map((entry, i) => {
    const row = rowForEntry.has(i) ? rows[rowForEntry.get(i)] : null;
    const fromGuide = row ? questRowsFromRequirement(row, resolved, infoboxes) : [];
    const committedRows = entry.sources
      .filter((s) => s.startsWith('game-data:Quest#'))
      .map((s) => Number(s.slice('game-data:Quest#'.length)));

    // The guide's link is the source of truth for identity.
    let questRows = [];
    let basis = 'none';
    if (fromGuide.length === 1) {
      // One requirement link, one answer. This is the case the whole exercise is about: the
      // identity comes from the link target, never from the label the old scrape kept.
      questRows = fromGuide[0].rows;
      basis = 'guide-link';
    } else if (fromGuide.length > 1) {
      // Several requirement links each naming a quest: the row states alternatives ("or") or a
      // chain ("and"). If the curated binding is one of the rows the guide itself links, the
      // guide still corroborates it and it stands; if it is not, the guide contradicts it and
      // only the game data is left backing it.
      const union = [...new Set(fromGuide.flatMap((t) => t.rows))].sort((a, b) => a - b);
      const self = namesItself(entry, fromGuide);
      if (committedRows.length && committedRows.every((r) => union.includes(r))) {
        questRows = committedRows;
        basis = 'guide-link-subset';
      } else if (statesAlternatives(row, union, resolved)) {
        // The row names a SET any one of which counts, and the catalogue can now say so. Picking
        // one of them is what bound the Grand Company entries to a single company's quest and
        // told two thirds of characters they had not done something they had.
        questRows = union;
        basis = 'guide-link-any-of';
      } else if (self) {
        // The row's own subject is a quest and it links to it: "A Gentleman Falls, Rather than
        // Flies unlocked after completing the Heavensward Main Storyline Quest" links both the
        // quest being unlocked and its prerequisite, and the statement has no colon for the
        // split to work from. The link that IS the unlock is the identity; the other is context.
        questRows = self.rows;
        basis = 'guide-link-self';
      } else if (committedRows.length) {
        questRows = committedRows;
        basis = 'curated (the guide row names several quests, none of them this one)';
      } else {
        // Nothing curated to fall back on, and picking one of several would be a guess of
        // exactly the kind that put seven entries on the wrong quest.
        basis = 'unresolved (the guide row names several quests)';
      }
    } else if (committedRows.length) {
      questRows = committedRows;
      basis = 'curated (the guide link resolves to no quest row)';
    }

    if (basis === 'guide-link' && committedRows.length) {
      const same = JSON.stringify([...questRows].sort((a, b) => a - b)) === JSON.stringify([...committedRows].sort((a, b) => a - b));
      if (!same) {
        report.disagreements.push({
          kind: 'quest identity: guide link vs committed binding',
          unlock: entry.unlock, level: entry.level,
          guideLink: fromGuide[0].target, guideResolvesTo: questRows,
          committedBinding: committedRows,
          note: 'recorded, not resolved',
        });
      }
    }

    if (row && entry.level !== row.level && row.levelFromSection) {
      report.disagreements.push({
        kind: 'level: guide section vs catalogue',
        unlock: entry.unlock, catalogueLevel: entry.level,
        guideLevel: row.level, guideSection: `${row.page} / ${row.section}`,
        note: 'the guide states the level at which the content becomes relevant, the sheet the level at which the quest can be accepted; recorded, not resolved',
      });
    }

    assignments.push({
      unlock: entry.unlock, level: entry.level,
      guidePage: row?.page ?? null, guideSection: row?.section ?? null,
      guideLabel: row?.label ?? null, guideText: row?.text ?? null,
      basis, questRows,
      requirementLinks: row ? requirementLinks(row).map((l) => l.target) : [],
    });
    return { entry, index: i, row, questRows, basis, fromGuide };
  });

  // ------------------------------------------------------------------ drop unreleased content
  //
  // The one edit the generator makes that is a DELETION, and the only one it is allowed: an entry
  // whose sole source is a guide page describing an expansion that has not shipped. Everything
  // else it can do is rewrite an identity; this removes a row from the checklist, so it is
  // reported by name and not merely counted.
  const dropped = proposals.filter((p) => p.row?.unreleased);
  for (const p of dropped) {
    report.droppedEntries.push({
      unlock: p.entry.unlock, level: p.entry.level,
      page: p.row.page, section: p.row.section, text: p.row.text,
      why: 'the only guide row for this entry is on a page that is mostly "???" placeholders — an expansion that has not shipped',
    });
  }
  const shipping = proposals.filter((p) => !p.row?.unreleased);

  // ------------------------------------------------------------------ build the entries
  const unlocks = shipping.map(({ entry, row, questRows, basis, fromGuide }) => {
    const curatedExtras = entry.sources.filter((s) => s !== GUIDE_SOURCE && !s.startsWith('game-data:Quest#'));

    // Gates that are not quests. Only reached when nothing bound a Quest row: a quest completion
    // is the strongest gate the client records, and where one exists it is the answer. Where none
    // does, the guide still often says something checkable — clear this duty, carry this map —
    // and the difference between "status unknown" and "requires clearing Sigmascape V4.0
    // (Savage)" is the whole value of reading it.
    const labelDuties = row && !questRows.length ? dutyRows(labelLinks(row), resolved) : [];
    const gateDuties = row && !questRows.length ? dutyRows(requirementLinks(row), resolved) : [];
    const gateItems = row && !questRows.length ? itemRowsFromRequirement(row, resolved, mapChestTypes) : [];
    // A duty the entry is ABOUT is its identity, not its gate: "[[The Aquapolis]] Access" names
    // the Aquapolis on the label side, and clearing the Aquapolis is obviously not how you unlock
    // it. Only requirement-side duties become a requirement, and only those with an
    // InstanceContent row, which is the id the client can actually be asked about.
    const checkableDuties = gateDuties
      .filter((d) => !labelDuties.some((l) => l.cfcId === d.cfcId))
      .filter((d) => d.contentId != null);
    const derived = {
      dutyRows: [...labelDuties, ...gateDuties.filter((d) => !labelDuties.some((l) => l.cfcId === d.cfcId))],
      itemRows: gateItems,
    };

    // The infobox is only a SECOND source for a row the link target's own name already produced.
    // Where the infobox was itself the resolver there is nothing independent to compare against,
    // and counting it would be the same source agreeing with itself.
    let agreed = null;
    for (const t of fromGuide) {
      if (t.via !== 'link-target-name') continue;
      const stated = infoboxes.get(t.target);
      if (stated == null) continue;
      report.crossCheck.checked++;
      const ok = t.rows.includes(stated);
      if (ok) report.crossCheck.agree++;
      else {
        report.crossCheck.disagree++;
        report.disagreements.push({
          kind: 'quest row id: game data vs the linked page\'s own infobox',
          unlock: entry.unlock, level: entry.level, page: t.target,
          infoboxQuestNumber: stated, gameDataRows: t.rows, note: 'recorded, not resolved',
        });
      }
      agreed = agreed === false ? false : ok;
    }

    // The quest NAME is display text, and it comes from the sheet whenever an identity was
    // established — that is what stops a hand-typed string drifting from the row it claims to
    // be. Where no identity was established the curated string is kept as-is: it is the only
    // human-readable hint the entry has left, and deleting curated content is not this
    // generator's job. Such an entry is 'unverified' and carries requires.unverifiable, so the
    // status calculator can never report it as Available on the strength of that string.
    const names = [...new Set(questRows.map((id) => resolved.quests[id]?.name).filter(Boolean))];
    const quest = names.length === 1 ? names[0] : (entry.quest ?? null);

    // 'confidence' is a statement about evidence. 'verified' means two independent sources agree
    // on the identity — the guide's own link target and the game's Quest sheet — and nothing
    // contradicts them. Everything weaker is named as such rather than rounded up.
    const corroboratedByGuide = basis.startsWith('guide-link');
    let confidence;
    if (!questRows.length) confidence = 'unverified';
    else if (questRows.length > 1) confidence = 'single-source';
    else if (curatedExtras.includes('script-gated:curated')) confidence = 'single-source';
    else if (agreed === false) confidence = 'single-source';
    else if (!corroboratedByGuide) confidence = 'single-source';
    else confidence = 'verified';

    const grounded = groundLevel(entry, row, questRows, resolved.quests);
    if (grounded.level === null) {
      report.levelless.push({
        unlock: entry.unlock, catalogueLevel: entry.level, category: grounded.category,
        boundQuestLevels: questRows.map((id) => resolved.quests[id]?.level ?? null),
        why: row?.section
          ? `the guide section "${row.section}" states no level and the bound quest is a hidden level-1 reward row`
          : 'no source states a level',
      });
    } else if (grounded.level !== entry.level) {
      report.disagreements.push({
        kind: 'level: the catalogue level was not stated by any source',
        unlock: entry.unlock, catalogueLevel: entry.level,
        groundedLevel: grounded.level, groundedBy: grounded.levelSource,
        note: 'the guide section states no level; the catalogue used the previous expansion cap. Replaced with the level the bound quest actually states.',
      });
    }

    // A curated `quest` on an entry the guide gates on a duty or an item is a string that has now
    // been shown NOT to be a quest: the link the catalogue took it from resolves to a
    // ContentFinderCondition or an Item row. Keeping it would leave the plugin matching a name
    // against the Quest sheet forever, which is the defect this pipeline exists to end.
    const out = {
      ...entry, ...grounded, confidence,
      quest: !questRows.length && (checkableDuties.length || gateItems.length) ? null : quest,
      // The level-dispute sentence is written BY the level-disputes pass below, so it is
      // generated content living in a curated field and has to be cleared before that pass runs.
      // Carrying it forward makes it permanent: the entry that shared Quest#70353 with a
      // level-105 duplicate kept the note, and the note kept the entry at 'single-source', long
      // after the duplicate had gone.
      notes: withoutLevelDispute(entry.notes),
      sources: buildSources(!!row, questRows, curatedExtras, derived),
    };
    if (out.level === null) delete out.level;
    if (!out.levelSource) delete out.levelSource;
    if (!out.category) delete out.category;

    // Several rows for one unlock is a fact about the game, not an ambiguity to be picked from,
    // and the file has to say so or the plugin will match on the name again.
    if (questRows.length > 1) {
      out.questAnyOf = [...questRows].sort((a, b) => a - b);
      report.alternativeSets.push({
        unlock: entry.unlock, level: entry.level, basis,
        rows: out.questAnyOf.map((id) => ({ id, name: resolved.quests[id]?.name ?? null, level: resolved.quests[id]?.level ?? null })),
        guideText: row?.text ?? null,
      });
    } else {
      delete out.questAnyOf;
    }

    const stated = typeFromSources(entry, row, resolved);
    if (stated && stated !== entry.type) {
      report.retypedEntries.push({
        unlock: entry.unlock, level: entry.level, from: entry.type, to: stated,
        icon: row.icon,
        why: 'the catalogue typed this from the first word of its own name; the guide row\'s icon and the game data both say it is a duty',
      });
      out.type = stated;
    }

    // `requires.unverifiable` is the marker that stops the status calculator grading an entry.
    // It has to track the identity both ways: set when nothing backs the entry, and CLEARED the
    // moment something does. Leaving it set on an entry that now resolves to a live quest row
    // would keep 40 recovered entries in the "can't be checked" bucket they just left.
    //
    // A duty or item gate does NOT clear it. Clearing Sigmascape opens the Ultimate; whether the
    // player then spoke to the minstrel is written nowhere a plugin can read. The gate is real
    // and worth showing — it just cannot promise the unlock was taken.
    if (questRows.length) {
      if (out.requires) {
        const { unverifiable, label, ...concrete } = out.requires;
        out.requires = Object.keys(concrete).length ? { label, ...concrete } : undefined;
        if (!out.requires) delete out.requires;
      }
    } else {
      // The derived gate lists are rewritten from source every run, so the committed ones are
      // dropped rather than merged — otherwise a duty the guide stopped naming would live on
      // forever. Everything else in `requires` is curated and is kept.
      const { label: curatedLabel, duties: _duties, items: _items, ...keep } = entry.requires ?? {};
      const gated = checkableDuties.length || gateItems.length;
      // A gate the generator derived also replaces whatever prose the catalogue had, because that
      // prose was written about the absence this gate has just filled.
      const label = checkableDuties.length
        ? `unlocked by clearing ${checkableDuties.map((d) => d.name).join(', ')}; whether you have taken the unlock itself is not something the game lets a plugin read`
        : gateItems.length
          ? `entered with ${gateItems.map((i) => i.name).join(', ')}, never from a quest`
          : curatedLabel ?? 'no unlocking quest is recorded for this entry';
      out.requires = {
        label,
        ...keep,
        ...(checkableDuties.length ? { duties: checkableDuties.map((d) => ({ id: d.contentId, name: d.name })) } : {}),
        ...(gateItems.length ? { items: gateItems.map((i) => ({ id: i.id, name: i.name })) } : {}),
        unverifiable: true,
      };
      if (gated) {
        report.gates.push({
          unlock: entry.unlock, level: entry.level,
          duties: checkableDuties.map((d) => ({ contentId: d.contentId, contentFinderCondition: d.cfcId, name: d.name, from: d.target })),
          items: gateItems.map((i) => ({ id: i.id, name: i.name, from: i.target, via: i.via })),
          identityDuties: labelDuties.map((d) => ({ contentFinderCondition: d.cfcId, name: d.name })),
        });
      }
    }
    return out;
  });

  // ------------------------------------------------------------------ order
  //
  // Level order, then the guide's own row order within a level (Array.prototype.sort is stable,
  // so equal levels keep the order they were parsed in). Entries with no level sort last, as
  // their own sections — they are not level 0 and must not lead the list.
  //
  // Sorting here rather than trusting the source order matters because grounding a level can
  // MOVE an entry: an entry the import filed at 50 whose quest is level 60 belongs with the
  // level-60 rows. Without this the file would come out in an order no rule describes, and CI
  // could not check it.
  unlocks.sort((a, b) => (a.level ?? Number.MAX_SAFE_INTEGER) - (b.level ?? Number.MAX_SAFE_INTEGER));

  // ------------------------------------------------------------------ level disputes
  //
  // Two entries citing the same Quest row at levels well apart cannot both be right about what
  // that quest unlocks, so neither is corroborated — whatever each says on its own. This has to
  // run after every entry is built, because it is a property of the set, not of an entry. A gap
  // of one level is the guide's own table rounding and is left alone.
  const LEVEL_AGREEMENT_SLACK = 1;
  const byQuestRow = new Map();
  unlocks.forEach((e, i) => {
    for (const s of e.sources) {
      if (!s.startsWith('game-data:Quest#')) continue;
      if (!byQuestRow.has(s)) byQuestRow.set(s, []);
      byQuestRow.get(s).push(i);
    }
  });

  for (const [source, idx] of byQuestRow) {
    const levelled = idx.filter((i) => typeof unlocks[i].level === 'number');
    if (levelled.length < 2) continue;
    const levels = levelled.map((i) => unlocks[i].level);
    if (Math.max(...levels) - Math.min(...levels) <= LEVEL_AGREEMENT_SLACK) continue;

    const row = source.slice('game-data:'.length);
    for (const i of levelled) {
      const e = unlocks[i];
      const others = levelled.filter((j) => j !== i && unlocks[j].level !== e.level);
      if (!others.length) continue;
      if (e.confidence === 'verified') e.confidence = 'single-source';

      report.disagreements.push({
        kind: 'level: two entries cite one quest row at different levels',
        unlock: e.unlock, level: e.level, questRow: row,
        alsoCitedBy: others.map((j) => ({ unlock: unlocks[j].unlock, level: unlocks[j].level })),
        note: 'recorded, not resolved: neither entry is corroborated while they disagree',
      });

      if (/level disputed/i.test(e.notes ?? '')) continue;
      const otherLevels = [...new Set(others.map((j) => unlocks[j].level))].sort((a, b) => a - b);
      const sentence = others.length === 1
        ? `Level disputed: ${row} is also cited by ${unlocks[others[0]].unlock} at level ${unlocks[others[0]].level}, so at most one of these levels can be what that quest unlocks — treated as single-source until the pair is resolved.`
        : `Level disputed: ${row} is also cited by ${others.length} other entries at level ${otherLevels.join('/')}, so at most one of these levels can be what that quest unlocks — treated as single-source until the group is resolved.`;
      e.notes = e.notes ? `${e.notes} ${sentence}` : sentence;
    }
  }

  // ------------------------------------------------------------------ completeness
  rows.forEach((row, ri) => {
    if (entriesForRow.has(ri)) return;
    report.unassignedGuideRows.push({
      page: row.page, section: row.section, label: row.label,
      placeholder: row.placeholder,
      note: row.placeholder ? 'wiki "???" placeholder for unreleased content' : 'no catalogue entry — new content, or a row the guide lists twice',
    });
  });
  curated.forEach((e, i) => {
    if (!rowForEntry.has(i)) report.entriesWithoutAGuideRow.push({ unlock: e.unlock, level: e.level });
  });

  report.counts = {
    guidePages: pages.length,
    guideContentRows: rows.length,
    guidePlaceholderRows: rows.filter((r) => r.placeholder).length,
    linkTargetsResolved: targets.size,
    catalogueEntries: unlocks.length,
    entriesTiedToAGuideRow: rowForEntry.size,
    entriesWhoseIdentityCameFromAGuideLink: proposals.filter((p) => p.basis.startsWith('guide-link')).length,
    entriesWithAQuestAnyOfSet: unlocks.filter((e) => (e.questAnyOf?.length ?? 0) > 0).length,
    entriesGatedOnADutyClear: unlocks.filter((e) => (e.requires?.duties?.length ?? 0) > 0).length,
    entriesGatedOnAnItem: unlocks.filter((e) => (e.requires?.items?.length ?? 0) > 0).length,
    entriesDroppedAsUnreleased: report.droppedEntries.length,
    entriesRetypedByTheGuideIcon: report.retypedEntries.length,
    confidence: unlocks.reduce((a, e) => ({ ...a, [e.confidence]: (a[e.confidence] ?? 0) + 1 }), {}),
    disagreementsRecorded: report.disagreements.length,
    entriesWithoutALevel: report.levelless.length,
    levellessCategories: report.levelless.reduce(
      (a, e) => ({ ...a, [e.category ?? 'uncategorised']: (a[e.category ?? 'uncategorised'] ?? 0) + 1 }), {}),
  };

  // ------------------------------------------------------------------ write
  fs.mkdirSync(OUT, { recursive: true });
  const candidate = canonicalise({ ...committed, unlocks });
  const candidatePath = path.join(OUT, 'unlocks-by-level.json');
  fs.writeFileSync(candidatePath, candidate);
  fs.writeFileSync(path.join(OUT, 'generation-report.json'), `${JSON.stringify(report, null, 2)}\n`);
  fs.writeFileSync(path.join(OUT, 'assignments.json'), `${JSON.stringify(assignments, null, 1)}\n`);

  const before = fs.readFileSync(DATASET, 'utf8');
  const identical = before === candidate;
  console.log('');
  console.log(`counts: ${JSON.stringify(report.counts)}`);
  console.log(`cross-check: ${JSON.stringify(report.crossCheck)}`);
  console.log(`disagreements recorded: ${report.disagreements.length}`);
  console.log(`unassigned guide rows: ${report.unassignedGuideRows.length} (${report.unassignedGuideRows.filter((r) => r.placeholder).length} placeholders)`);
  console.log(`catalogue entries with no guide row: ${report.entriesWithoutAGuideRow.length}`);
  for (const d of report.droppedEntries) console.log(`dropped as unreleased: ${d.unlock} (${d.page})`);
  for (const t of report.retypedEntries) console.log(`retyped by the guide's row icon: ${t.unlock} ${t.from} -> ${t.to} (${t.icon})`);
  console.log(`entries with no grounded level, categorised instead: ${report.levelless.length} ${JSON.stringify(report.counts.levellessCategories)}`);
  console.log('');
  console.log(identical
    ? 'REPRODUCES the committed dataset byte for byte.'
    : `DIFFERS from the committed dataset. Candidate: ${candidatePath}`);

  if (args.write) {
    fs.writeFileSync(DATASET, candidate);
    console.log(`written to ${DATASET}`);
  } else if (!identical) {
    console.log('Re-run with --write to accept, after reviewing the diff (see data/README.md).');
  }
  return identical ? 0 : 1;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  main().then((code) => process.exit(code)).catch((e) => {
    console.error(e.message);
    process.exit(2);
  });
}
