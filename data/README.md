# The unlock catalogue

`unlocks-by-level.json` is the checklist of everything the plugin can tell you that you have not
unlocked yet. It is **generated**, not hand-maintained, and the generated file is **committed**.

## The split, and why it exists

| | Generation | Validation |
|---|---|---|
| runs | locally, by a developer | in CI, on every push and pull request |
| needs | a FINAL FANTASY XIV installation | nothing but this repository |
| command | `node scripts/build-unlock-catalogue.mjs` | `node data/validate-unlocks.mjs`<br>`node data/validate-catalogue-identity.mjs` |
| output | `data/unlocks-by-level.json` | pass / fail |

The generator resolves every name against the game's own Excel sheets, read out of `sqpack` with
Lumina. GitHub's runners have no game installation, so **generation can never be a CI step**. That
is the whole reason the output is committed: CI checks the committed file, and a regeneration is
something a person does deliberately and reviews as a diff.

## What it is generated from

Two sources, and neither one alone is trusted.

1. **The Gamer Escape progression guide**, read as **wikitext** through the MediaWiki API — never
   as rendered text. The guide's tables link every quest, duty and reward to its own page. The
   original hand-scrape kept the visible label and threw the link away, and that single decision
   produced 180 catalogue entries that matched no quest in the game, 7 bound to the wrong quest,
   and entries shown as available with nothing gating them. Parsing `[[link targets]]` is the fix
   this pipeline exists to make permanent.
2. **The game's own sheets** — `Quest`, `ContentFinderCondition`, `InstanceContent`, `Item`,
   `Mount`, `Companion`, `Emote`, `Orchestrion`, `TripleTriadCard`, `Action` — via
   `tools/Wayfarer.CatalogueGen`.

Every quest binding is then cross-checked against a **third**, independently maintained statement:
the linked page's own `{{ARR Infobox Quest}}` `Quest Number`, which the wiki's import bot writes.
Pages marked `DontBot = yes` are hand-written and are **not** used — several of those carry a
copy-pasted quest number that is simply wrong. A treasure-map page's `{{ARR Infobox Map Chest}}`
`Type` is read the same way, and for the same reason: it is the page's own statement of the item
row behind a name the guide only shows in its display form.

Disagreements between sources are **recorded, never silently resolved**. An entry whose sources
disagree loses its `verified` standing and says so in `notes`.

## What a row can be gated on

Not every unlock is opened by a quest, and the catalogue used to pretend otherwise: a row whose
requirement named a duty or a treasure map had that name written into `quest`, where it matched no
Quest row and left the entry with no identity at all. Four gate kinds are now derived, each from
the guide's own link target.

| The guide's requirement links | What the entry gets | Graded? |
|---|---|---|
| one Quest row | `quest` + `game-data:Quest#N` | yes |
| several Quest rows that are variants of one quest | `questAnyOf` + one source line each | yes — any one counts |
| a ContentFinderCondition row | `requires.duties` (InstanceContent id) + the CFC source | no — see below |
| an Item row, directly or through the linked page's `{{ARR Infobox Map Chest}}` `Type` | `requires.items` + the Item source | no |

`questAnyOf` exists because the game ships one quest per starting city and per Grand Company, and
binding the lowest row id told two thirds of characters they had not done something they had. The
set is taken only when a source says it is a choice: the rows differ solely by the parenthetical
the sheet disambiguates them with and share a level, or the guide's sentence says "one of", "the
applicable", or "or" without describing a sequence. **Picking one of several is the error this
field exists to end; enumerating them is the fix.** Note what is *not* done — the parenthetical is
never folded away to match a name against a row. That collapses the ten "A Relic Reborn" weapon
quests onto one key, and the name-reconciliation audit measured and rejected it.

A duty or item gate is real but **does not make the entry gradeable**. Clearing Sigmascape opens
the Ultimate; whether the player then spoke to the Wandering Minstrel is written nowhere a plugin
can read. Those entries keep `requires.unverifiable` *and* cite their rows — that combination is
correct, and it is the difference between "status unknown" and "requires clearing Sigmascape V4.0
(Savage)".

## What an entry grants — `reward`

`unlock` is prose. It is what a guide calls the thing, and the guide calls the same mount "Firebird
(Mount)" and the same duty "The Aery Dungeon Access". Neither string is a row in any sheet, so
nothing could be drawn from it: the picture of a mount lives on `Mount.Icon`. `reward` is the pair a
lookup can start from —

```json
"reward": { "kind": "ContentFinderCondition", "id": 155, "name": "the Aery" }
```

`kind` names the sheet that owns the identity and is one of the closed set in
`data/reward-kinds.mjs`; `id` is the row; `name` is that row's own player-facing name, kept so the
reward can always be said in words. The name is not decoration — KamiToolKit registers tooltips on
mouse events only, so an icon with no text beside it is unreadable on a controller.

**The field is generated**, by `tools/Wayfarer.CatalogueGen`'s `rewards` verb, from the game's own
reward channels: `Quest.Reward` → `Item.ItemAction` for mounts, minions, orchestrion rolls, bardings
and the rest, `Quest.EmoteReward` / `ClassJobUnlock` / `OtherReward` / `InstanceContentUnlock`
directly, and the feature tables that name their own unlock quest. Three rules pick which of a
quest's several rewards an entry is about, strongest first: the entry's own name matching one the
game says that quest grants; the ContentFinderCondition its label link already resolved to; or a
single reward of the kind the entry's `type` names. **There is no fourth rule** — anything weaker is
a guess of the kind that once bound seven entries to the wrong quest.

**Absent is a real answer, not a gap.** 316 of the 587 entries carry one. The 271 that do not are
overwhelmingly `system` (223) and `zone` (14): the Aesthetician, retainer ventures, the gemstone
traders and the housing districts open features the game keeps no row for anywhere. The rest are
entries whose *label shape* names something the sheets do not — "Deltascape (Savage) Access" against
per-floor duty rows, "Kobold Quests" against a minion, "Mount speed increased in Mor Dhona" against
a mount sheet it has nothing to do with. Guessing at those is the error; leaving the field off is
the fact.

**No icon id is stored.** Icon ids are renumbered between patches and a committed number that has
moved draws a band of nothing with no way to notice, so the plugin resolves them live from the
identity. What generation does check is that one *exists*: a kind listed in `WITH_ICON` whose row has
no icon fails the generator on the spot, where the sheet walk that produced it can be corrected.

## Where the generator reaches into curated fields

It owns identity and provenance and leaves prose alone, with three narrow exceptions. The first
two are reported by name in `generation-report.json` rather than merely counted.

**`type`, from the guide's row icon.** Only for an entry the catalogue typed `mount` whose label
resolves to a ContentFinderCondition row and to no Mount row. The catalogue typed entries by the
first word of their own name, so every duty called "Mount Ordeals" or "Mount Rokkon" was filed as
a mount; the row icon says trial or dungeon and the game agrees.

**Deletion, for unreleased content.** A guide page for an expansion that has not shipped is the
previous expansion's page with the quest names blanked to `???`. Thirty-three of the Evercold
page's thirty-four rows are blanked; the one that is not was imported as a real level-105 entry
duplicating a level-92 one. The rule is the measurement, not the page name — a page that is mostly
placeholders describes content that does not exist, and an entry whose only guide row is on such a
page is dropped. This is the **only** deletion the generator performs.

**The "Level disputed" sentence in `notes`.** That sentence is *written* by the generator, so it is
stripped before the disputes pass and rewritten from the current set. Carrying it forward the way
the rest of `notes` is carried forward made it permanent: five entries kept a note about a dispute
that a later regeneration had already resolved, and the note then held them at `single-source`
because confidence was reading it. Everything else in `notes` is yours and is left alone.

## Generated fields vs curated fields

The generator owns the entry's **identity and provenance**. It preserves everything editorial.

| Generated every run | Carried forward from the committed file |
|---|---|
| `quest` (display name of the resolved row) | `unlock`, `questKind` |
| `questAnyOf` | `description`, `notes`, `priority`, `cosmetic` |
| `sources` | `requires` (curated script-only requirements) |
| `confidence` | `type`, except the one correction above |
| `level`, `levelSource`, `category` | |
| `reward` | |
| `requires.duties`, `requires.items` where the guide states one | |

The committed dataset is therefore also the **curation store**. Editing prose in it is expected;
the next regeneration keeps that prose and rewrites only the identity fields around it.

## Levels

A level is only present when a source states one — the guide section the row sits under, or the
accept level of the quest the entry is bound to (`ClassJobLevel[0] + QuestLevelOffset`).
`levelSource` records which.

Where neither states one, the entry has **no level** and carries a `category` instead. It is not
level 0 and not level 1: the trophy mounts (Firebird, Kamuy of the Nine Tails, Landerwaffe,
Apocryphal Bahamut) are granted by hidden level-1 reward rows and are really gated on owning a set
of Extreme-trial mounts, so any number printed against them would be invented. They belong in
their own section, after the levelled entries, and a level filter must not hide them.

## Regenerating

```sh
# One-off: the resolver needs Lumina, which ships with Dalamud.
export DALAMUD_HOME="$APPDATA/XIVLauncher/addon/Hooks/dev"   # or wherever Dalamud lives
export WAYFARER_SQPACK="/path/to/FINAL FANTASY XIV Online/game/sqpack"

node scripts/build-unlock-catalogue.mjs            # writes a CANDIDATE, changes nothing
node scripts/build-unlock-catalogue.mjs --write    # accepts it
```

Useful flags: `--offline` (use only the cache, contact nothing), `--no-cross-check` (skip the
per-quest infobox second source — much faster, weaker evidence), `--help`.

Fetched pages are cached under `.catalogue-cache/` (gitignored) with their revision id and fetch
date, so a re-run costs no requests. The generator identifies itself, rate-limits, and asks for
pages 50 at a time.

It is **deterministic**: the same cache produces a byte-identical file. Running it twice in a row
must report `REPRODUCES the committed dataset byte for byte` — if it does not, that is a bug in
the generator, not a data change.

## Reviewing a regeneration diff

Read `.catalogue-cache/out/generation-report.json` alongside the diff. Work down this list.

1. **Losses first.** Did any entry lose its game row — `sources` going from
   `game-data:Quest#N` to nothing? That is a regression unless the row genuinely no longer
   exists. The generator never deletes an entry, so a vanished entry is always a bug.
2. **Rebindings.** Did an entry move to a *different* quest row? Each one needs a reason. The
   report's `disagreements` should already name it.
3. **Confidence drops.** `verified` → `single-source` means the guide stopped corroborating the
   binding — usually a wiki edit that broke a link. Worth a look, not usually a blocker.
4. **Level changes.** Every one should be accompanied by a `levelSource`. A level appearing or
   disappearing without one is a bug in the generator.
5. **Unassigned guide rows.** `unassignedGuideRows` lists guide rows with no catalogue entry.
   Placeholders (`???`, unreleased content) are expected; anything else is new content that needs
   curating in — the generator will not invent a description for it.
6. **Gains.** Entries moving from `unverified` to a real quest row are the point of the exercise.
   Spot-check a few against the wiki.

Then run the validators and the tests before committing:

```sh
node data/validate-unlocks.mjs && node data/validate-catalogue-identity.mjs
dotnet test -c Debug
```

Never build `-c Release` while doing this: `local.props` deploys a Release build straight into the
dev-plugin folder, on top of whatever is being tested in-game.

## What CI enforces

`validate-unlocks.mjs` — the schema: every field's type, the closed set of types, priorities,
confidences, requirement kinds and reward kinds, and that an unknown field is an error rather than a
value the plugin silently ignores. It cannot check that a `reward.id` exists or has an icon — those
need sqpack and CI has no game — so the generator checks both at the moment it writes the field.

`validate-catalogue-identity.mjs` — the properties that make the file trustworthy and its diffs
readable:

- **canonical form** — the file must be exactly what the generator would write (fixed key order,
  2-space indent, trailing newline), so a hand edit shows up instead of surviving until the next
  regeneration reverts it;
- **stable ordering** — non-decreasing level, level-less entries last;
- **a recorded identity for every entry** — either the game rows it rests on, or an explicit
  `requires.unverifiable`, so nothing can be silently identity-less;
- **no entry claiming Available on an absent gate** — an entry with no game row must be marked
  unverifiable; one citing a **Quest** row must not be, because the client records that
  completion; one citing only non-quest rows must be, because nothing records the unlock itself;
- **every `questAnyOf` id cited by the entry's own sources**, and every duty gate backed by the
  `ContentFinderCondition` row it came from;
- **grounded levels** — a level needs a `levelSource`, and a `levelSource` naming a quest row must
  name a row the entry itself cites;
- **no duplicate identities** — same unlock, same level, same rows.

`Wayfarer.Tests/UnlockDatasetShapeTests.cs` asserts the same invariants through the C# parser, so
they hold for the code that actually reads the file and not only for the Node validator.
