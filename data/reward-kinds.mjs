// The closed set of values an entry's `reward.kind` may hold, split by what the game gives each
// one to draw.
//
// This is the JavaScript half of Wayfarer.Core/Unlocks/UnlockReward.cs's UnlockRewardKinds. The two
// have to agree exactly — the generator writes the field, this validates it, and the plugin draws
// it — so Wayfarer.Tests/UnlockRewardKindsTests.cs reads THIS file and asserts the C# lists match
// it. Add a kind in one place and the test fails until it is added in the other.

/** Kinds whose own sheet carries an icon column, so the reward draws as itself. */
export const WITH_ICON = [
  'Mount',
  'Companion',
  'Emote',
  'ContentFinderCondition',
  'Item',
  'Ornament',
  'BeastTribe',
  'GrandCompanyRank',
  'BuddyEquip',
  'Glasses',
  'CharaMakeCustomize',
  'ClassJob',
  'GeneralAction',
];

/** Kinds with no icon column of their own that reach one through the item that grants them.
 * Orchestrion is the whole list: the sheet has two columns, Name and Description. */
export const VIA_GRANTING_ITEM = ['Orchestrion'];

/** Kinds the game ships no usable icon for. Naming them is the point — an entry whose reward is a
 * title has nothing to draw, and that is a fact about the game rather than a hole in the
 * catalogue. QuestRewardOther and ContentsNote are here despite having an icon column because
 * those columns are not per-reward art: one is 0 for half its rows, the other returns the same
 * dungeon glyph for every row. SatisfactionNpc's is a 192x192 portrait, not a slot icon. */
export const WITHOUT_ICON = [
  'Title',
  'AetherCurrent',
  'GatheringSubCategory',
  'NotebookDivision',
  'MobHuntOrderType',
  'ContentsNote',
  'QuestRewardOther',
  'SatisfactionNpc',
  'TripleTriadCard',
];

export const ALL = [...WITH_ICON, ...VIA_GRANTING_ITEM, ...WITHOUT_ICON];

/** Whether the display can be expected to draw this kind as a picture. False is not a failure. */
export const drawsAnIcon = (kind) => WITH_ICON.includes(kind) || VIA_GRANTING_ITEM.includes(kind);
