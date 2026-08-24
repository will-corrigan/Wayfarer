// What the catalogue deliberately does NOT ship, and why — the reviewed half of the completeness
// check.
//
// WHY THIS FILE EXISTS
// The game's own sheets propose 3,091 unlockable things across 36 channels (see
// tools/Wayfarer.CatalogueGen/UnlockEnumeration.cs). The catalogue ships 587 entries. Most of that
// difference is not a gap: per-item dye conditions, individual PvP arenas and 475 Triple Triad
// rows that name an NPC-match prerequisite rather than a reward are things a level checklist
// should never list. But some of it IS a gap — every title, every orchestrion roll, the ARR job
// unlocks, three live dungeons — and the whole failure this check exists to prevent is a gap
// nobody can see.
//
// So the difference is classified, once, here:
//
//   recommended  a real, live, obtainable unlock the catalogue should list and does not.
//   excluded     deliberately not shipped, with a stated reason.
//   undecided    a real decision nobody has made yet. Left visible on purpose.
//
// THE RULE ABOUT RULES
// Two layers, and mixing them up is the bug to watch for.
//
//   Layer 1 (CHANNELS below) is editorial taste. It cannot be derived from anything and has to be
//   written down: whether a channel belongs in a level checklist at all is a judgement about the
//   product. It is also where 1,463 of the 2,064 missing rows are resolved, in about twenty lines.
//
//   Layer 2 (classifyMissing below) is facts, and must NEVER be written down as a list, because
//   the facts change every patch: does the gate quest still exist, does the identity have a name,
//   is the gate a seasonal event. A fact that gets pasted into layer 1 is how an exclusion list
//   rots.
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
    reason: 'combat action granted by a job quest; the game announces it itself on level-up',
  },
  trait: { ship: false, reason: 'passive job trait; same reason as action' },
  'craft-action': { ship: false, reason: 'crafting action from a class quest; same reason as action' },
  'general-action': {
    ship: false,
    reason: 'general action already covered by the catalogue\'s own system entries',
  },
  'dye-slot': { ship: false, reason: 'per-item dye condition, not a player-facing unlock' },
  fate: { ship: false, reason: 'a FATE is world content, not something you unlock and keep' },
  'triple-triad-npc': { ship: false, reason: 'an opponent, not an unlock' },
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
  /** Kinds the catalogue lists. Anything here that is missing is a real gap. */
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
  ],

  /** Kinds the catalogue does not list, with the reason. */
  excluded: {
    'Gold Saucer': 'chocobo-race courses and GATEs, not level-gated unlocks',
    PvP: 'individual arenas; the catalogue lists PvP as one system entry',
    'The Masked Carnivale': 'individual acts of one system the catalogue lists once',
    'Quest Battles': 'MSQ solo instances, not separate unlocks',
    'Treasure Hunt': 'map dungeons reached from an item, not a level-gated unlock',
    '': 'tutorial and unnamed rows such as "Avoid Area of Effect Attacks"',
  },

  /** Kinds where the catalogue and the game disagree about GRANULARITY rather than about
   * existence, and nobody has decided which is right. The catalogue lists the tier — "Alexander
   * (Gordias) Access", "Palace of the Dead" — and the game lists the floor: 77 Coil turns, 39
   * Alexander/Deltascape/Sigmascape/Alphascape floors, 36 Deep Dungeon floor bands. Shipping
   * these is a UI decision, not a correctness fix, so they are left visible and undecided rather
   * than quietly recommended or quietly dropped. */
  undecided: {
    Raids:
      'the catalogue lists the raid TIER and the game lists the per-floor duty row; which '
      + 'granularity the checklist wants is an open UI decision',
    'Ultimate Raids': 'same open question as Raids',
    'Deep Dungeons':
      'the catalogue lists the dungeon and the game lists its floor bands; same open question '
      + 'as Raids',
  },
};

/** Reasons layer 2 produces. Named so the artefact, the validator and the report all say the same
 * words, and so a reader can count them. */
export const ROW_REASONS = {
  deadGate:
    'the gate quest names a row that is absent from the live sheet — unreleased or removed content',
  unnameable:
    'the identity has no player-facing name in any sheet, so there is nothing a checklist could '
    + 'display',
  seasonal:
    'the gate quest belongs to a seasonal event (Quest.Festival is set), so it is real but not '
    + 'obtainable today; showing it as available in a level checklist would be a lie. Ships only '
    + 'behind a seasonal status the gate calculator can map to "not right now"',
  retiredDuty: 'retired: not in the duty finder and the game names no unlock quest',
  npcMatchPrerequisite:
    'TripleTriadCardResident.Quest names the NPC-match prerequisite, not a quest that awards the '
    + 'card; only the cards that arrive through an ItemAction are genuine quest rewards',
};

/** Why one enumerated row the catalogue does not cover is not covered.
 *
 * Order is the layering: taste, then facts, then the two per-channel refinements. A row that falls
 * through everything is a real, live, named, non-seasonal unlock of a kind the catalogue lists —
 * which is the definition of a gap, so it is recommended.
 *
 * @param {object} row one entry of coverage.json's `game.unlocks`
 * @returns {{ classification: 'recommended'|'excluded'|'undecided', reason: string }}
 */
export function classifyMissing(row) {
  const channel = CHANNELS[row.channel];
  if (!channel) {
    // A channel the enumeration produced and the policy has never seen. Not an error to be
    // swallowed: it is new content or a renamed sheet, and somebody has to look at it.
    return {
      classification: 'undecided',
      reason: `channel '${row.channel}' is not in data/coverage-policy.mjs — new content, or a `
        + 'join that has moved. Classify it there.',
    };
  }

  // ---- layer 1: does the catalogue list this kind of thing at all, and at what grain
  if (!channel.ship) return { classification: 'excluded', reason: channel.reason };
  if (channel.granularity) return { classification: 'excluded', reason: channel.granularity };

  // ---- layer 2: facts about this row, none of them written down anywhere
  if (row.questRowId !== null && row.questRowId !== undefined && !row.gateLive) {
    return { classification: 'excluded', reason: ROW_REASONS.deadGate };
  }
  if (!row.name) return { classification: 'excluded', reason: ROW_REASONS.unnameable };
  if (row.festival) return { classification: 'excluded', reason: ROW_REASONS.seasonal };

  // ---- per-channel refinements
  if (row.channel === 'duty') return classifyDuty(row);
  if (row.channel === 'triple-triad-card' && row.via === 'TripleTriadCardResident.Quest') {
    return { classification: 'excluded', reason: ROW_REASONS.npcMatchPrerequisite };
  }

  return { classification: 'recommended', reason: '' };
}

function classifyDuty(row) {
  const type = row.contentType ?? '';
  if (type in DUTY_CONTENT_TYPES.excluded) {
    return {
      classification: 'excluded',
      reason: `duty kind the catalogue does not list (ContentType '${type || '(none)'}'): `
        + DUTY_CONTENT_TYPES.excluded[type],
    };
  }
  if (type in DUTY_CONTENT_TYPES.undecided) {
    return { classification: 'undecided', reason: DUTY_CONTENT_TYPES.undecided[type] };
  }

  // A duty that is neither in the finder nor gated on a quest the game names is not reachable and
  // is not a gap. Note the ORDER against the ContentType rules above: a retired Gold Saucer row is
  // reported under its kind, because that is the more useful of the two true statements.
  const gated = row.questRowId !== null && row.questRowId !== undefined;
  if (!gated && row.inDutyFinder === false) {
    return { classification: 'excluded', reason: ROW_REASONS.retiredDuty };
  }

  if (!DUTY_CONTENT_TYPES.listed.includes(type)) {
    return {
      classification: 'undecided',
      reason: `duty ContentType '${type}' is in neither the listed, excluded nor undecided set in `
        + 'data/coverage-policy.mjs — new content. Classify it there.',
    };
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
 * @returns {{ rule: string, why: string } | null} null when the entry needs no allowance.
 */
export function allowanceFor(entry) {
  if (entry.reward) return null;
  return entry.type === 'system'
    ? {
      rule: 'system-features-have-no-game-row',
      why: 'the game keeps no row for this feature anywhere; there is no general system-unlock '
        + 'table to enumerate against',
    }
    : {
      rule: 'label-names-no-game-row',
      why: 'the entry\'s label shape names something no sheet holds, so the generator declined to '
        + 'guess an identity and there is nothing to look up',
    };
}

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
