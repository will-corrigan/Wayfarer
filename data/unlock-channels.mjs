// The taxonomy: which KIND of thing a catalogue entry is.
//
// WHY THIS IS A FIELD AND NOT A STRING MATCH
// The catalogue used to answer "what kind of thing is this" from `type`, a nine-value editorial
// field that predates the game-data enumeration and mixes two questions: `dungeon`/`trial`/`raid`
// answer "which filter chip", and `system` answers "we had nowhere else to put it". That was
// survivable at 587 entries, all of them duties, systems and a handful of cosmetics. It is not
// survivable now that the catalogue also lists titles, orchestrion rolls, jobs, folklore books,
// challenge-log entries, framer's kits and Masked Carnivale acts: a display that wants to show
// "Titles" as its own page needs one field to group by, and 158 entries typed `system` is not it.
//
// So every entry carries `channel`, and it is the same vocabulary
// tools/Wayfarer.CatalogueGen/UnlockEnumeration.cs walks — one channel per join, named after the
// thing rather than after the sheet. `type` stays exactly as it was, because it drives the filter
// chips that already exist (see Wayfarer.Core/Unlocks/UnlockFilters.cs) and rewriting those is
// presentation work for another day.
//
// WHAT DERIVES WHAT
//   imported entry   channel comes from the enumeration row; `type` is derived from the channel by
//                    TYPE_FOR_CHANNEL below, so it lands in a chip that exists.
//   curated entry    channel is derived from the entry's own strongest identity statement: the
//                    sheet its `reward` names, falling back to `type` when it has none. Nothing
//                    here reads a name — that is the defect the reward field exists to have ended.
import { CHANNELS } from './coverage-policy.mjs';

/** Every value an entry's `channel` may hold: the channels the policy lists, plus `zone`.
 *
 * `zone` is not an enumeration channel and cannot be — the housing districts, the Gold Saucer and
 * White Wolf Gate open a place rather than a row, and no sheet holds a "you may now enter" bit for
 * them. The twenty catalogue entries that say so are wiki-only by construction (see
 * `allowanceFor`), so the taxonomy needs a word for them and the game does not supply one. */
export const ENTRY_CHANNELS = [
  ...Object.keys(CHANNELS).filter((c) => CHANNELS[c].ship),
  'zone',
].sort();

/** The `reward.kind` an entry carries → the channel that kind belongs to.
 *
 * One line per sheet the reward join can produce, so a new reward kind cannot be added without
 * deciding which channel it is. The pairing is the enumeration's own: `UnlockEnumeration` walks
 * `Mount` under `mount` and `NotebookDivision` under `crafting-log-division`, and this is the same
 * statement read backwards. */
export const CHANNEL_FOR_REWARD_KIND = {
  AetherCurrent: 'aether-current',
  BeastTribe: 'allied-society',
  BuddyEquip: 'barding',
  CharaMakeCustomize: 'hairstyle',
  ClassJob: 'job',
  Companion: 'minion',
  ContentFinderCondition: 'duty',
  ContentsNote: 'challenge-log',
  Emote: 'emote',
  GatheringSubCategory: 'gathering-folklore',
  GeneralAction: 'general-action',
  Glasses: 'facewear',
  GrandCompanyRank: 'grand-company-rank',
  // The only Item-identity reward the catalogue holds is a framer's kit, which is what the
  // enumeration's `framers-kit` channel is: an unlock whose identity IS the item, because nothing
  // else in the game holds a row for it.
  Item: 'framers-kit',
  MobHuntOrderType: 'hunt-board',
  Mount: 'mount',
  NotebookDivision: 'crafting-log-division',
  Orchestrion: 'orchestrion',
  Ornament: 'fashion-accessory',
  QuestRewardOther: 'system',
  SatisfactionNpc: 'custom-delivery',
  Title: 'title',
  TripleTriadCard: 'triple-triad-card',
};

/** The `type` an entry carries → the channel to fall back to when it names no reward.
 *
 * 275 of the original 587 entries name no reward, and for most of them that is a fact about the
 * game rather than a gap: there is no general system-unlock table, so the Aesthetician and retainer
 * ventures have nothing to point at. `type` is then the only statement the entry makes about what
 * it is, and it is the one the catalogue has always made. */
export const CHANNEL_FOR_TYPE = {
  'alliance-raid': 'duty',
  dungeon: 'duty',
  emote: 'emote',
  minion: 'minion',
  mount: 'mount',
  raid: 'duty',
  system: 'system',
  trial: 'duty',
  zone: 'zone',
};

/** An imported entry's channel → the `type` it takes, so it lands in a filter chip that exists.
 *
 * Everything that is not a duty, a mount, a minion or an emote takes `system`, which is what `type`
 * has always meant for the entries the nine-value set has no word for. That is deliberately coarse:
 * the fine-grained answer is `channel`, and duplicating it here would give two fields that can
 * disagree. Duties are the exception because `type` distinguishes them and the chips read it. */
export const TYPE_FOR_CHANNEL = {
  duty: 'dungeon',
  emote: 'emote',
  minion: 'minion',
  mount: 'mount',
};

/** Duty `ContentType` → the `type` that duty takes. Trials and raids are told apart because the
 * filter chips do; every other listed kind is content you clear, and `dungeon` is the word the
 * catalogue has always used for that. */
export const TYPE_FOR_DUTY_CONTENT_TYPE = {
  Trials: 'trial',
  Raids: 'raid',
  'Ultimate Raids': 'raid',
  'Chaotic Alliance Raid': 'alliance-raid',
};

/** Channels whose unlocks are ornament rather than progress — the `cosmetic` flag, decided per
 * channel rather than per entry because it is a property of the kind of thing. A title, a roll and
 * a card are all things you can complete the game without; a duty, a job and a folklore book are
 * not.
 *
 * It matters beyond taste: `UnlockFilters.Category` sends a `system`-typed entry to the cosmetic
 * chip when this is true, which is the only thing that keeps 158 titles out of the systems list. */
export const COSMETIC_CHANNELS = new Set([
  'barding',
  'facewear',
  'fashion-accessory',
  'framers-kit',
  'hairstyle',
  'minion',
  'mount',
  'orchestrion',
  'title',
  'triple-triad-card',
  'emote',
]);

/** A human-readable name for the channel, for the `category` an entry with no level needs.
 *
 * Naming our own taxonomy rather than inventing a fact about the game: `category` exists because an
 * entry with no level must say what it is instead of being sorted among level-1 content, and for a
 * row the game states no level for, the kind of thing it is is the honest answer. Duties do better
 * than this — they use the game's own `ContentType` name — so no duty ever reads it. */
export const CATEGORY_FOR_CHANNEL = {
  'allied-society': 'Allied Societies',
  barding: 'Chocobo Barding',
  'challenge-log': 'Challenge Log',
  'chocobo-companion': 'Chocobo Companion',
  'crafting-log-division': 'Crafting and Gathering Log',
  'custom-delivery': 'Custom Deliveries',
  duty: 'Duties',
  emote: 'Emotes',
  facewear: 'Facewear',
  'fashion-accessory': 'Fashion Accessories',
  'framers-kit': "Framer's Kits",
  'gathering-folklore': 'Folklore Books',
  'general-action': 'General Actions',
  'grand-company-rank': 'Grand Company Ranks',
  hairstyle: 'Hairstyles',
  'hunt-board': 'Hunt Boards',
  job: 'Jobs and Classes',
  minion: 'Minions',
  mount: 'Mounts',
  orchestrion: 'Orchestrion Rolls',
  'stone-sky-sea': 'Stone, Sky, Sea',
  system: 'Systems and Features',
  title: 'Titles',
  'triple-triad-card': 'Triple Triad Cards',
  'variant-dungeon': 'Variant and Criterion Dungeons',
  'aether-current': 'Aether Currents',
  zone: 'Zones',
};

/** The channel a CURATED entry belongs to — the reward's sheet, or failing that its `type`.
 *
 * @returns {string} always a member of {@link ENTRY_CHANNELS}; throws rather than guessing, because
 *   an entry with no channel is exactly the "typed `system` because we had nowhere to put it" state
 *   this field exists to end.
 */
export function channelForCuratedEntry(entry) {
  const fromReward = entry.reward ? CHANNEL_FOR_REWARD_KIND[entry.reward.kind] : null;
  if (fromReward) return fromReward;

  const fromType = CHANNEL_FOR_TYPE[entry.type];
  if (fromType) return fromType;

  throw new Error(
    `${entry.unlock}: no channel for reward kind '${entry.reward?.kind ?? '(none)'}' / type `
      + `'${entry.type}'. Add it to CHANNEL_FOR_REWARD_KIND or CHANNEL_FOR_TYPE in `
      + 'data/unlock-channels.mjs — a channel cannot be guessed at runtime.',
  );
}

/** The `type` an imported enumeration row takes. */
export function typeForImportedRow(row) {
  if (row.channel === 'duty') {
    return TYPE_FOR_DUTY_CONTENT_TYPE[row.contentType ?? ''] ?? 'dungeon';
  }
  return TYPE_FOR_CHANNEL[row.channel] ?? 'system';
}
