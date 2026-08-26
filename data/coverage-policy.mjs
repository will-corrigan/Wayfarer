// What the catalogue ships and what it deliberately does NOT, and why — the reviewed half of the
// completeness check, and now also the thing that decides what the generator imports.
//
// WHY THIS FILE EXISTS
// The game's own sheets propose 3,091 unlockable things across 36 channels (see
// tools/Wayfarer.CatalogueGen/UnlockEnumeration.cs). Some of that difference is not a gap:
// per-item dye conditions, individual PvP arenas and 475 Triple Triad rows that name an NPC-match
// prerequisite rather than a reward are things a checklist should never list. The rest of it IS a
// gap — every title, every orchestrion roll, the ARR job unlocks, three live dungeons — and the
// whole failure this check exists to prevent is a gap nobody can see.
//
// So the difference is classified, once, here:
//
//   recommended  a real, obtainable unlock the catalogue should list. The generator IMPORTS these,
//                so a healthy artefact has none left: see the note below.
//   excluded     deliberately not listed, with a stated reason.
//   undecided    a real decision nobody has made yet. Left visible on purpose, and today empty.
//
// WHAT "RECOMMENDED" MEANS NOW
// It used to mean "somebody should write this entry by hand". It means "the generator will write
// this entry from the game's own row" — scripts/build-unlock-catalogue.mjs reads exactly this
// classification and emits one catalogue entry per recommended row. So the steady state is
// `recommended: 0`, and data/validate-coverage.mjs enforces that: a patch that adds an unlock of a
// listed kind turns the count non-zero and CI says so, which is the same alarm as before with the
// manual step taken out.
//
// THE TEST A CHANNEL HAS TO PASS
// Does a player have to go and DO something to get it? A quest, a duty, a purchase, a collection,
// an achievement, a discovery: that is an unlock. Something that arrives by gaining a level in a
// class is not — the game teaches it, announces it, and there is nothing to track. That is the
// whole of why `action`, `trait` and `craft-action` are out and everything else obtainable is in.
//
// THE RULE ABOUT RULES
// Two layers, and mixing them up is the bug to watch for.
//
//   Layer 1 (CHANNELS, DUTY_CONTENT_TYPES) is editorial taste. It cannot be derived from anything
//   and has to be written down: whether a channel belongs in the catalogue at all is a judgement
//   about the product. It is also where most of the uncovered rows are resolved, in about forty
//   lines.
//
//   Layer 2 (the per-row predicates in classifyMissing, plus the duplicate rule in
//   data/coverage-diff.mjs which needs to see the other rows) is facts, and must NEVER be written
//   down as a list, because the facts change every patch: does the gate quest still exist, does the
//   identity have a name, is this row the same thing as one already listed. A fact that gets pasted
//   into layer 1 is how an exclusion list rots.
//
// There is no layer 3. There is deliberately no per-identity exception list — an exclusion that
// has to name a row id is a sign the rule is wrong, and a list of row ids is precisely the thing
// that goes stale silently and cannot be reviewed.
//
// WHAT THIS FILE IS NOT ALLOWED TO DO
// It does not import anything. It is read by the generator (which has the game data) and by
// data/validate-coverage.mjs (which has nothing but this repository), and both must reach the same
// verdict from the same inputs or the check is worthless.

/** The 36 channels the enumeration walks, and whether a level checklist lists that kind of thing.
 *
 * `ship: false` needs a `reason`: that string is what a reviewer reads, and it is what lands in
 * data/coverage.json against every row the channel drops.
 *
 * `granularity` is for the one channel the catalogue lists at a DELIBERATELY coarser grain than
 * the game's rows. Its missing rows are excluded, with that reason, rather than proposed.
 */
export const CHANNELS = {
  // ---- listed ----------------------------------------------------------------------------
  duty: { ship: true },
  title: { ship: true },
  orchestrion: { ship: true },
  job: { ship: true },
  mount: { ship: true },
  minion: { ship: true },
  emote: { ship: true },
  barding: { ship: true },
  'fashion-accessory': { ship: true },
  facewear: { ship: true },
  hairstyle: { ship: true },
  'framers-kit': { ship: true },
  'triple-triad-card': { ship: true },
  'gathering-folklore': { ship: true },
  'crafting-log-division': { ship: true },
  'challenge-log': { ship: true },
  'allied-society': { ship: true },
  'custom-delivery': { ship: true },
  'grand-company-rank': { ship: true },
  'hunt-board': { ship: true },
  system: { ship: true },
  'stone-sky-sea': { ship: true },
  'variant-dungeon': { ship: true },
  'chocobo-companion': { ship: true },
  // Desynthesis, materia melding, Dye, Decipher/Dig, Glamour Plate, Aetherial Reduction. Every one
  // of the twelve is granted by a named quest and none of them arrives on a level-up, so they pass
  // the test at the top of this file — unlike the combat actions and traits below.
  'general-action': { ship: true },

  // ---- listed, at a coarser grain than the game's rows ------------------------------------
  'aether-current': {
    ship: true,
    granularity:
      'the catalogue lists one entry per ZONE (30 of them) and the game has 151 individual '
      + 'currents. Collecting a zone\'s currents is one thing a player sets out to do; 151 pickup '
      + 'rows is not a checklist anybody wants. Per-current granularity is deliberately not '
      + 'proposed — but the ratio is exactly why this artefact exists, so it is reported rather '
      + 'than hidden.',
  },

  // ---- not listed, each with the reason ---------------------------------------------------
  action: {
    ship: false,
    reason:
      'a combat action: it arrives with the level, the game announces it itself, and there is '
      + 'nothing for a player to go and do. Not an unlock by the test at the top of this file',
  },
  trait: { ship: false, reason: 'a passive job trait, granted on level-up; same reason as action' },
  'craft-action': {
    ship: false,
    reason: 'a crafting action, granted on level-up in the class; same reason as action',
  },
  'dye-slot': {
    ship: false,
    reason:
      'not a dye and not a slot a player earns: ItemStainCondition keys on an ITEM row and records '
      + 'that that one item accepts a second dye channel. 242 of the 243 rows name the same single '
      + 'quest, so the player-facing unlock is that one quest — which the catalogue lists — and the '
      + 'rows themselves carry no name a checklist could show',
  },
  fate: {
    ship: false,
    reason:
      'a FATE is repeatable world content that becomes available, not something you obtain and '
      + 'keep; there is nothing to mark done',
  },
  'triple-triad-npc': {
    ship: false,
    reason: 'an opponent to play against rather than an unlock, and the rows carry no name',
  },
  'occult-note': {
    ship: false,
    reason: 'internal Occult Crescent note rows, no player-facing name',
  },
  'emj-costume': { ship: false, reason: 'internal costume rows, no player-facing name' },
  'aether-current-set': {
    ship: false,
    reason:
      'the per-zone aggregate of the aether-current rows; listing it as well would count the '
      + 'same unlock twice',
  },
  flight: {
    ship: false,
    reason:
      'per-zone flight is the same unlock as that zone\'s aether current set, which the '
      + 'catalogue already lists',
  },
};

/** Duty kinds, by `ContentFinderCondition.ContentType`'s own name.
 *
 * The duty channel is the one place a channel-wide verdict is too coarse: 857 named rows span
 * everything from Sastasha to a chocobo-race course. The type name is the game's own word for the
 * distinction and is what the decision rests on. */
export const DUTY_CONTENT_TYPES = {
  /** Kinds the catalogue lists. Anything here that is missing is a real gap.
   *
   * Raids, Ultimate Raids and Deep Dungeons used to sit in `undecided` because the catalogue listed
   * the TIER ("Alexander (Gordias) Access", "Palace of the Dead") and the game lists the FLOOR: 77
   * Coil turns, 39 Alexander/Deltascape/Sigmascape/Alphascape floors, 36 Deep Dungeon floor bands.
   * That was a presentation question dressed up as a correctness one. The per-floor rows are real
   * duties a player clears one at a time, so they are listed; grouping them back under their tier is
   * something a display can do and a missing row is not.
   *
   * Treasure Hunt and The Masked Carnivale joined for the same reason. A map dungeon is entered from
   * an item rather than opened by a quest, and a Carnivale act is one of thirty-two the game tracks
   * individually — neither of those makes it not a thing you go and do. */
  listed: [
    'Dungeons',
    'Trials',
    'Guildhests',
    'Disciples of the Land',
    'V&C Dungeon Finder',
    'Eureka',
    'Save The Queen',
    'Occult Crescent',
    'Chaotic Alliance Raid',
    'Raids',
    'Ultimate Raids',
    'Deep Dungeons',
    'Treasure Hunt',
    'The Masked Carnivale',
  ],

  /** Kinds the catalogue does not list, with the reason. Each of these fails the "did the player
   * have to go and do something to get it" test in a different way, so each says how. */
  excluded: {
    'Gold Saucer':
      'a Duty Finder queue row for an attraction rather than an unlock: 61 of the 65 distinct names '
      + 'have no gate of any kind, and the chocobo-race courses and Mahjong ranked/quick variants '
      + 'are matchmaking modes for one activity. Reaching the Gold Saucer is the unlock, and the '
      + 'catalogue lists it',
    PvP:
      'an individual arena in a rotation, not something a player unlocks and keeps; the catalogue '
      + 'lists the PvP modes themselves as system entries',
    'Quest Battles':
      'a solo instance played once inside an MSQ quest. It is never entered from the finder and '
      + 'never unlocked separately from the quest the catalogue already lists',
    '':
      'tutorial and retired rows with no ContentType of their own — the Hall of the Novice '
      + 'exercises ("Avoid Area of Effect Attacks") and superseded Diadem variants',
  },

  /** Kinds nobody has decided about. Empty, and that is the finding rather than an oversight: the
   * granularity question that used to live here was settled by listing the game's rows and leaving
   * the grouping to the display. A NEW ContentType lands in `unclassified-duty-kind` instead, which
   * is undecided by construction and says so. */
  undecided: {},
};

/** Reasons layer 2 produces, keyed. Reasons are cited by KEY on each row of data/coverage.json
 * rather than written out 1,637 times: a reader gets the twenty distinct reasons once, at the top
 * of the artefact, and rewording one is a one-line diff instead of a thousand-line one. */
export const ROW_REASONS = {
  'row:dead-gate':
    'the gate quest names a row that is absent from the live sheet — unreleased or removed content',
  'row:unnameable':
    'the identity has no player-facing name in any sheet, so there is nothing a checklist could '
    + 'display',
  'row:duplicate-identity':
    'a second sheet row for a thing the catalogue already lists — 22 ContentFinderCondition rows '
    + 'say "Ocean Fishing", one per route, and superseded rows keep the name of the row that '
    + 'replaced them. The multiplicity is a routing detail of the game\'s own tables, not 22 '
    + 'unlocks, so one row of each name carries the entry — the one the catalogue already covers, '
    + 'or failing that the first — and the rest are accounted for here',
  'triple-triad-card:npc-match-prerequisite':
    'TripleTriadCardResident.Quest names the NPC-match prerequisite, not a quest that awards the '
    + 'card; only the cards that arrive through an ItemAction are genuine quest rewards',
};

/** Every reason key any classification can cite, with its text. The artefact carries the subset it
 * uses, and data/validate-coverage.mjs checks that every key a row cites is defined here — so a
 * reason cannot be invented on a row, and a row cannot cite one that no longer exists. */
export function reasonText(key) {
  if (key in ROW_REASONS) return ROW_REASONS[key];

  const [kind, ...rest] = key.split(':');
  const name = rest.join(':');
  if (kind === 'channel') return CHANNELS[name]?.reason ?? null;
  if (kind === 'granularity') return CHANNELS[name]?.granularity ?? null;
  if (kind === 'duty-kind') {
    const why = DUTY_CONTENT_TYPES.excluded[name];
    return why ? `duty kind the catalogue does not list (ContentType '${name || '(none)'}'): ${why}` : null;
  }
  if (kind === 'duty-granularity') return DUTY_CONTENT_TYPES.undecided[name] ?? null;
  if (kind === 'unclassified-channel') {
    return `channel '${name}' is not in data/coverage-policy.mjs — new content, or a join that has `
      + 'moved. Classify it there.';
  }
  if (kind === 'unclassified-duty-kind') {
    return `duty ContentType '${name}' is in neither the listed, excluded nor undecided set in `
      + 'data/coverage-policy.mjs — new content. Classify it there.';
  }
  return null;
}

/** Why one enumerated row the catalogue does not cover is not covered.
 *
 * Order is the layering: taste, then facts, then the two per-channel refinements. A row that falls
 * through everything is a real, live, named, non-seasonal unlock of a kind the catalogue lists —
 * which is the definition of a gap, so it is recommended.
 *
 * @param {object} row one entry of coverage.json's `unlocks`
 * @returns {{ classification: 'recommended'|'excluded'|'undecided', reason: string }} `reason` is a
 *   key into `reasonText`, empty for a recommendation (which needs no excuse).
 */
export function classifyMissing(row) {
  const channel = CHANNELS[row.channel];

  // A channel the enumeration produced and the policy has never seen. Not swallowed: it is new
  // content or a renamed sheet, and somebody has to look at it.
  if (!channel) {
    return { classification: 'undecided', reason: `unclassified-channel:${row.channel}` };
  }

  // ---- layer 1: does the catalogue list this kind of thing at all, and at what grain
  if (!channel.ship) return { classification: 'excluded', reason: `channel:${row.channel}` };
  if (channel.granularity) {
    return { classification: 'excluded', reason: `granularity:${row.channel}` };
  }

  // ---- layer 2: facts about this row, none of them written down anywhere
  if (row.questRowId !== null && row.questRowId !== undefined && !row.gateLive) {
    return { classification: 'excluded', reason: 'row:dead-gate' };
  }
  if (row.unnamed) return { classification: 'excluded', reason: 'row:unnameable' };

  // Seasonal rows (Quest.Festival set) used to be excluded here. They are real, permanent once
  // earned, and exactly the kind of thing a player wants a list of — what they are not is
  // obtainable today, and that is a fact about STATUS rather than about existence. So they are
  // listed, and the generator gives them requires.unverifiable with a label saying so, which is the
  // one thing that stops the checklist reporting a Starlight emote as available in August.
  if (row.duplicateOf !== null && row.duplicateOf !== undefined) {
    return { classification: 'excluded', reason: 'row:duplicate-identity' };
  }

  // ---- per-channel refinements
  if (row.channel === 'duty') return classifyDuty(row);
  if (row.channel === 'triple-triad-card' && row.via === 'TripleTriadCardResident.Quest') {
    return { classification: 'excluded', reason: 'triple-triad-card:npc-match-prerequisite' };
  }

  return { classification: 'recommended', reason: '' };
}

function classifyDuty(row) {
  const type = row.contentType ?? '';
  if (type in DUTY_CONTENT_TYPES.excluded) {
    return { classification: 'excluded', reason: `duty-kind:${type}` };
  }
  if (type in DUTY_CONTENT_TYPES.undecided) {
    return { classification: 'undecided', reason: `duty-granularity:${type}` };
  }

  // There used to be a `duty:retired` rule here — no gate quest and not in the duty finder — and it
  // was wrong in both directions. It caught two superseded "the Diadem" rows, which the duplicate
  // rule now catches properly, and it also called "the Unmaking (Extreme)" and "Shinryu's Domain
  // (Unreal)" retired: both are live content that the finder flag simply does not cover, because
  // whole kinds of duty (Ultimates, Deep Dungeons, Treasure Hunt, the Carnivale) have that flag
  // false on every row. A flag that is false for a whole kind cannot be evidence about one row.

  if (!DUTY_CONTENT_TYPES.listed.includes(type)) {
    return { classification: 'undecided', reason: `unclassified-duty-kind:${type}` };
  }

  return { classification: 'recommended', reason: '' };
}

/** The rules under which a SHIPPED catalogue entry is allowed not to appear in the enumeration.
 *
 * This is the direction that matters for CI, and it is where an exception list would have been the
 * easy wrong answer. 272 of the 587 entries carry no `reward` identity, and there is no join to
 * make without one — but the reason differs, and saying which is the point:
 *
 *   223 are `system`. The Aesthetician, retainer ventures, the gemstone traders, the Unreal
 *   rotation: there is NO general system-unlock table in the game. `Quest.OtherReward` has 18 rows
 *   and covers the Aether Compass, Wondrous Tails, Spearfishing and the job soul stones; that is
 *   the entire vocabulary. Systems must stay curated, and this rule is that fact written as code.
 *
 *   49 are entries whose LABEL SHAPE names something no sheet holds — "Deltascape (Savage) Access"
 *   against per-floor duty rows, "Kobold Quests" against a minion, the three housing districts.
 *   The generator declines to guess an identity for them, which is correct, and leaves nothing to
 *   look up.
 *
 * Both are derived from the entry itself. Neither names a row id. An entry that DOES carry an
 * identity gets no allowance at all: it must appear in the enumeration, or the check fails.
 *
 * @returns {{ rule: string } | null} null when the entry needs no allowance.
 */
export function allowanceFor(entry) {
  if (entry.reward) return null;
  return entry.type === 'system'
    ? { rule: 'system-features-have-no-game-row' }
    : { rule: 'label-names-no-game-row' };
}

/** The two allowance rules, so the artefact carries their wording once and a reader does not have
 * to come here for it. Same reasoning as `ROW_REASONS`: a rule cited 125 times is written once. */
export const ALLOWANCE_RULES = {
  'system-features-have-no-game-row':
    'the game keeps no row for this feature anywhere; there is no general system-unlock table to '
    + 'enumerate against, so systems stay curated',
  'label-names-no-game-row':
    'the entry\'s label shape names something no sheet holds, so the generator declined to guess an '
    + 'identity and there is nothing to look up',
};

/** Every Quest row a catalogue entry is bound to. The entry's own `sources` are the record — the
 * generator writes one `game-data:Quest#N` line per row — so this reads them rather than the
 * `quest` display name, which is prose. */
export function questRowsOf(entry) {
  const rows = new Set();
  for (const s of entry.sources ?? []) {
    const m = /^game-data:Quest#(\d+)$/.exec(s);
    if (m) rows.add(Number(m[1]));
  }
  return rows;
}

/** The catalogue's identity-bearing shape, and nothing else.
 *
 * coverage.json is generated beside a particular catalogue and CI has to be able to say whether it
 * still belongs to it. Hashing the whole file would make every prose edit a CI failure that only a
 * developer with the game installed could clear, which is a check nobody would keep. This is the
 * projection the coverage artefact actually depends on: drop an entry, change what it unlocks, or
 * rebind it to another quest and the fingerprint moves; fix a typo in a description and it does
 * not. */
export function identityProjection(unlocks) {
  return unlocks.map((e) => ({
    unlock: e.unlock,
    level: e.level ?? null,
    type: e.type,
    reward: e.reward ? { kind: e.reward.kind, id: e.reward.id } : null,
    quests: [...questRowsOf(e)].sort((a, b) => a - b),
  }));
}
