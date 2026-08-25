# The unlock catalogue

`unlocks-by-level.json` is the checklist of everything the plugin can tell you that you have not
unlocked yet. It is **generated**, not hand-maintained, and the generated file is **committed**.

## The split, and why it exists

| | Generation | Validation |
|---|---|---|
| runs | locally, by a developer | in CI, on every push and pull request |
| needs | a FINAL FANTASY XIV installation | nothing but this repository |
| command | `node scripts/build-unlock-catalogue.mjs` | `node data/validate-unlocks.mjs`<br>`node data/validate-catalogue-identity.mjs`<br>`node data/validate-coverage.mjs` |
| output | `data/unlocks-by-level.json`<br>`data/coverage.json` | pass / fail |

The generator resolves every name against the game's own Excel sheets, read out of `sqpack` with
Lumina. GitHub's runners have no game installation, so **generation can never be a CI step**. That
is the whole reason the output is committed: CI checks the committed file, and a regeneration is
something a person does deliberately and reviews as a diff.

## The two halves

The file has 1,208 entries and they come from two different places. Almost everything below is
about the first half; the second is short because it has to be.

| | Curated | Imported |
|---|---:|---:|
| entries | 587 | 621 |
| built from | the wiki guide, corroborated against the game | the game's own enumeration, alone |
| carries a description | yes, always, and CI requires one | no — nothing states one |
| `channel` | derived from what it unlocks | the channel it was enumerated under |
| marked by | `gamerescape:progression-guide` in `sources` | `game-enumeration:<channel>` in `sources` |

**Why the second half exists.** The guide decides what exists, so anything the guide omits this
pipeline could never learn about — and the completeness check below had been measuring that gap
without closing it: every quest-completion title, every orchestrion roll, all twenty ARR job
unlocks, three live dungeons. The generator now writes an entry for each of them from the game's own
row.

**Why imported entries are rebuilt every run rather than carried forward.** The committed file is
also the input to the next generation. An import that landed once would be indistinguishable from
curation immediately afterwards, and the next patch's new duty would need the whole exercise
repeating by hand. So `main` drops every entry marked `game-enumeration:` on the way in and rebuilds
it from the enumeration on the way out. A patch that adds a title adds an entry; one that removes a
duty removes one.

**What an imported entry will not do: invent.** No description, because the sheets state a name and
a gate and no prose, and manufacturing a sentence that reads like curation is the same error as
inventing a level. No `questKind`, because the guide is the only source that has ever said whether a
quest is main scenario or a sidequest. No level unless the Quest row or the duty row states one, and
never a level of 1 — in both those places 1 means "no level requirement". `priority` is the neutral
value for the channel (`optional` for cosmetics, `nice` for the rest) rather than a judgement nobody
made.

## What kind of thing an entry is — `channel`

Every entry carries one, and it is the field a per-category display groups by.

`type` cannot do that job. Its nine values were chosen when the catalogue was 587 duties, systems
and a handful of cosmetics; it has no word for a title, an orchestrion roll, a folklore book or a
Masked Carnivale act, so asked to describe 1,208 entries it answers `system` for a third of them.
`channel` is the vocabulary the game-data enumeration already walks — `duty`, `title`,
`orchestrion`, `job`, `minion`, `challenge-log`, and 20 more — which means the taxonomy and the
completeness check cannot drift apart. `type` is left exactly as it was, because it drives filter
chips that already exist and rewriting those is presentation work.

It is generated, never curated. An imported entry carries the channel it was enumerated under; a
curated one has it derived from the sheet its `reward` names, falling back to its `type` when it has
none — no string matching, which is the failure class `reward` exists to have ended. The closed set
and both derivations live in `data/unlock-channels.mjs`, and `validate-unlocks.mjs` re-derives every
curated entry's channel in CI, so a hand-edited one does not survive.

The one channel the game cannot supply is `zone`: the housing districts, the Gold Saucer and White
Wolf Gate open a place rather than a row, and no sheet holds a "you may now enter" bit for them.

## What the curated half is generated from

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

## The wiki link

When Wayfarer cannot explain a requirement — "the game does not say" — the player has nowhere to
go. `wikiUrl` is the backup: a link to the entry's own page on Consolegameswiki (chosen over Gamer
Escape, the site this catalogue is otherwise built from, because it is the genuinely independent
source and its Prerequisites sections carry exactly the conditions this pipeline cannot derive).

It links the entry's own **quest** page, never the catalogue's label — the quest is what the
player is actually sent to do, and `unlock` is this repo's own name for it, not a wiki title. Where
an entry has no bound quest at all, it links whatever the entry genuinely is instead (a duty, most
often). Where an entry's quest identity is ambiguous (`questAnyOf`, or no single duty identity), no
name is even attempted: picking one of several pages to link would be the same kind of guess the
rest of this generator exists to refuse.

Checked, never assembled. The generator hits Consolegameswiki's own API for every candidate name —
politely: a contactable user agent, a floor on the request interval, and a cache so a re-run asks
again for nothing already checked — and `wikiUrl` is written only when the wiki confirms the page
exists. A name that resolves to nothing, or resolves ambiguously, leaves the field absent rather
than guessed; the journal window's wiki button simply does not render for that entry.
`data/validate-unlocks.mjs` checks that any URL present is well-formed, but cannot re-verify it
against the wiki with no network — the field's presence is the verification, by construction of
the one thing that is allowed to write it.

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
| `wikiUrl`, checked every run, absent when unverified | |
| `sources` | `requires` (curated script-only requirements) |
| `confidence` | `type`, except the one correction above |
| `level`, `levelSource`, `category` | |
| `reward`, `channel` | |
| `requires.duties`, `requires.items` where the guide states one | |

The committed dataset is therefore also the **curation store**. Editing prose in it is expected;
the next regeneration keeps that prose and rewrites only the identity fields around it.

**This table describes the curated half only.** An imported entry has no curated side at all: every
field on it is generated, and the whole entry is discarded and rebuilt from the game data on the next
run. Writing prose into one is not curation, it is an edit that the next regeneration will silently
revert — and the way to keep it is to say so somewhere the generator reads, which today means
promoting the entry into curation.

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

7. **Coverage.** Read `data/coverage.json`'s diff alongside. Rows moving from `missing` to `covered`
   are the point. A channel whose `gameTotal` has *fallen* is schema drift, not a game change —
   check its join before accepting. New rows in `recommended` are new content the guide has not
   caught up with; new rows in `undecided` are a classification somebody needs to make in
   `data/coverage-policy.mjs`.

Then run the validators and the tests before committing:

```sh
node data/validate-unlocks.mjs && node data/validate-catalogue-identity.mjs \
  && node data/validate-coverage.mjs
dotnet test -c Debug
```

Never build `-c Release` while doing this: `local.props` deploys a Release build straight into the
dev-plugin folder, on top of whatever is being tested in-game.

## What CI enforces

`validate-unlocks.mjs` — the schema: every field's type, the closed set of types, channels,
priorities, confidences, requirement kinds and reward kinds, and that an unknown field is an error
rather than a value the plugin silently ignores. It cannot check that a `reward.id` exists or has an
icon — those need sqpack and CI has no game — so the generator checks both at the moment it writes
the field.

It also checks the two halves separately (587 curated, 621 imported), because they fail differently:
a change in the first is somebody editing curation and a change in the second is the game shipping a
patch, and a single total would let one move inside the other. And it requires a **description on
every curated entry** while allowing an imported one to have none. That rule used to be "every entry
carries 20 to 400 characters of description", which was right while every entry had been written by a
person and is a trap now that most have not — satisfying it for 621 game-proposed rows would mean
generating a sentence each, and a manufactured sentence that reads like curation is worse than an
honest blank. The row and the journal fall through to `notes`, then the requirement label, then the
entry's own name; see `UnlockRowText.Description`.

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
- **no duplicate identities** — same unlock, same level, same rows, same reward. The reward is part
  of that key because two different things can share a name: the quest behind "The Promise of
  Tomorrow" grants both a title and an orchestrion roll of that name, and "Tiisol Ja" is both a
  custom-delivery client and that client's crafting-log division.

`Wayfarer.Tests/UnlockDatasetShapeTests.cs` asserts the same invariants through the C# parser, so
they hold for the code that actually reads the file and not only for the Node validator.

`validate-coverage.mjs` — the completeness check, below.

## The completeness check — `coverage.json`

Everything above validates that the catalogue is *internally* sound. None of it can notice that the
catalogue is **missing something**, because the guide decides what exists: anything the guide omits
this pipeline never learns about. That is not hypothetical. A whole trophy-mount quest — quest
71005, *The Wing Spirit Cometh* — was absent until another plugin's data revealed it, and the game
names **151** aether currents against the **30** rows the guide lists.

So generation also asks the game. `tools/Wayfarer.CatalogueGen`'s `enumerate` verb walks every
place in the schema where the game states "this is gated on a quest" — found mechanically, by
reflecting over every `RowRef<Quest>` in `Lumina.Excel.Sheets` — and produces **3,091 rows across
36 channels**. The diff against the catalogue about to be written is `data/coverage.json`, which is
committed. CI checks it with no game installation.

It used to change nothing about what the catalogue contains, and only make the gap measurable. It now
decides part of it: the generator imports every row this classifies as **recommended**, so the gap
and the import cannot disagree about what a gap is.

### The numbers

| channel | the game has | we cover | of those, by identity | curated entries | imported entries | excluded |
|---|---:|---:|---:|---:|---:|---:|
| `duty` | 857 | 540 | 486 | 290 | **220** | 317 |
| `triple-triad-card` | 481 | 381 | 10 | — | **5** | 100 |
| `dye-slot` | 243 | — | — | — | — | 243 |
| `title` | 201 | 201 | 158 | — | **158** | — |
| `action` | 160 | 41 | — | — | — | 119 |
| `aether-current` | 151 | 45 | — | — | — | 106 |
| `gathering-folklore` | 134 | 54 | 12 | — | **12** | 80 |
| `triple-triad-npc` | 108 | 46 | — | — | — | 62 |
| `orchestrion` | 79 | 79 | 54 | — | **53** | — |
| `minion` | 64 | 64 | 59 | 20 | **41** | — |
| `flight` | 63 | 29 | — | — | — | 34 |
| `fate` | 58 | 18 | — | — | — | 40 |
| `emote` | 56 | 56 | 53 | 15 | **40** | — |
| `occult-note` | 56 | 13 | — | — | — | 43 |
| `trait` | 54 | 15 | — | — | — | 39 |
| `job` | 48 | 47 | 36 | — | **20** | 1 |
| `crafting-log-division` | 44 | 32 | 13 | 3 | **10** | 12 |
| `mount` | 37 | 37 | 31 | 18 | **19** | — |
| `aether-current-set` | 31 | — | — | — | — | 31 |
| `craft-action` | 24 | 24 | — | — | — | — |
| `emj-costume` | 21 | 21 | — | — | — | — |
| `hunt-board` | 20 | 20 | — | — | — | — |
| `challenge-log` | 19 | 17 | 13 | — | **13** | 2 |
| `system` | 13 | 13 | 12 | 225 | **10** | — |
| `allied-society` | 12 | 12 | 3 | — | **3** | — |
| `custom-delivery` | 12 | 12 | 1 | — | **1** | — |
| `general-action` | 12 | 12 | 1 | — | **1** | — |
| `barding` | 6 | 6 | 6 | 1 | **5** | — |
| `grand-company-rank` | 6 | 6 | 3 | — | **1** | — |
| `stone-sky-sea` | 5 | 5 | — | — | — | — |
| `fashion-accessory` | 4 | 4 | 4 | — | **4** | — |
| `framers-kit` | 4 | 4 | 4 | 1 | **3** | — |
| `variant-dungeon` | 4 | 4 | — | — | — | — |
| `chocobo-companion` | 2 | — | — | — | — | 2 |
| `facewear` | 1 | 1 | 1 | — | **1** | — |
| `hairstyle` | 1 | 1 | 1 | — | **1** | — |
| `zone` | — | — | — | 14 | — | — |
| **total** | **3,091** | **1,860** | **961** | **587** | **621** | **1,231** |

The `curated entries` and `imported entries` columns count catalogue entries, not enumerated rows,
and only the imported column totals cleanly: a curated entry can cover rows in more than one channel
and would be counted twice. `zone` has no enumerated rows at all, which is the point of it.

**Nothing is recommended and nothing is undecided**, and that is the baseline CI now holds. Every
row of a kind the catalogue lists is either covered or excluded with a stated reason, so a non-zero
`recommended` means the installed game data has an unlock the committed file does not — which is
the alarm this artefact was written for, with the manual step taken out.

The 1,231 excluded rows, by reason:

| reason | rows |
|---|---:|
| `channel:dye-slot` | 243 |
| `row:duplicate-identity` | 140 |
| `channel:action` | 119 |
| `granularity:aether-current` | 106 |
| `triple-triad-card:npc-match-prerequisite` | 100 |
| `row:dead-gate` | 91 |
| `duty-kind:Quest Battles` | 82 |
| `duty-kind:Gold Saucer` | 63 |
| `channel:triple-triad-npc` | 62 |
| `channel:occult-note` | 43 |
| `channel:fate` | 40 |
| `channel:trait` | 39 |
| `channel:flight` | 34 |
| `channel:aether-current-set` | 31 |
| `duty-kind:` (tutorial and retired rows) | 22 |
| `duty-kind:PvP` | 16 |

Two of those are worth reading twice. `row:duplicate-identity` is the game holding several rows for
one thing — 22 `ContentFinderCondition` rows are called "Ocean Fishing", one per route — and one row
of each name carries the entry. `granularity:aether-current` is the one place the catalogue is
deliberately coarser than the game: it lists 30 zones against the game's 151 individual currents,
because collecting a zone's currents is one thing a player sets out to do and 151 pickup rows is not
a checklist anybody wants.

`shipped, of those by identity` is worth reading as its own column. A channel with coverage and a
zero there is one where the catalogue names the right *quests* without ever naming what they
unlock — which is the shape of every bug this pipeline has had. The import moved that figure from
319 to 961.

### Where "we cover it" comes from

Two joins, and a row counts as covered if either hits. Neither uses a name.

- **identity** — a shipped entry's `reward` names that exact row. The strong form.
- **gate** — a shipped entry cites the Quest row the game says opens it. Weaker, and necessary: the
  30 aether-current entries genuinely tell a player about all 151 currents, and the job entries name
  the job in prose while carrying no `ClassJob` identity.

### The policy — `coverage-policy.mjs`

Every row the catalogue does not cover is classified **recommended** (which now means "the
generator will write an entry for it"), **excluded with a stated reason**, or **undecided**. Two
layers, and mixing them up is the bug to watch for.

**Layer 1 is editorial taste** and has to be written down: whether a channel belongs in the
catalogue at all is a judgement about the product, not something derivable. The test each channel
has to pass is *does a player have to go and do something to get it* — a quest, a duty, a purchase, a
collection, an achievement, a discovery. Something that arrives by gaining a level in a class is not
an unlock, which is the whole of why `action` (160 rows), `trait` (54) and `craft-action` (24) are
out. So is `dye-slot`, for a different reason: `ItemStainCondition` keys on an *item* and records
that that one item accepts a second dye channel, 242 of its 243 rows name the same single quest, and
the rows carry no name a checklist could show — the player-facing unlock is that one quest, which the
catalogue lists.

**Layer 2 is facts**, and must never be written down as a list, because the facts change every patch:
does the gate quest still exist, does the identity have a name, is this row a second copy of
something already listed.

**Seasonal rows are listed**, and they used to be excluded. They are real, permanent once earned, and
not obtainable today — but that is a fact about *status* rather than about existence, and the plugin
already reads `Quest.Festival` off the bound row and refuses to call such an entry Available (it
reports "needs a festival or a house" instead). Nothing has to be written into the entry to say so,
and writing `unverifiable` into it would have been the opposite of true: the gate is perfectly
readable.

**There is no layer 3.** There is deliberately no per-identity exception list: an exclusion that has
to name a row id is a sign the rule is wrong, and a list of row ids is exactly the thing that goes
stale silently and cannot be reviewed. Reasons are cited by *key* against a dictionary at the head
of `coverage.json`, so a reviewer reads the 16 distinct reasons once and rewording one is a one-line
diff rather than a 1,231-line one.

Two rules were removed rather than reworded, and both had been quietly wrong:

- **`duty:retired`** — "not in the duty finder and the game names no unlock quest". It called *the
  Unmaking (Extreme)* and *Shinryu's Domain (Unreal)* retired, both of which are live, because whole
  KINDS of duty have that flag false on every row (Ultimates, Deep Dungeons, Treasure Hunt, the
  Carnivale). A flag that is false for a whole kind cannot be evidence about one row. The two
  superseded rows it did catch are now caught properly, as duplicates.
- **the raid/deep-dungeon granularity question** — 157 rows sat `undecided` because the catalogue
  listed the *tier* and the game lists the *floor*. That was a presentation question dressed up as a
  correctness one. The floors are real duties a player clears one at a time, so they are listed;
  grouping them back under their tier is something a display can do and a missing row is not.

### What CI fails on

**On rows being missing — now.** That used to be the one thing it did not do, because 378
recommended and 157 undecided rows were the point of the artefact and a check that went red for them
would have been switched off within a week. The generator imports every recommended row, so the
baseline is **zero recommended and zero undecided**, and a non-zero count means the installed game
data has unlocks the committed file does not. Stated as "must be zero" rather than as a number to
keep updating, so growth in the catalogue cannot turn it red on its own — only a gap can.

It also fails when the artefact and the catalogue stop describing the same thing:

- **the identity fingerprint moves** — a sha256 over each entry's `unlock`, `level`, `type`,
  `reward` and quest rows. Drop an entry, rebind one to another quest, or change what one unlocks
  and this fails, so a regenerated catalogue that silently loses entries cannot pass with a stale
  artefact beside it. Deliberately *not* a hash of the whole file: making every typo fix a CI
  failure that only a developer with the game installed could clear is how a check gets deleted.
- **an entry is unaccounted for** — every shipped entry must correspond to an enumerated row, or be
  allowed by one of two **rules**. 223 entries are `system` with no identity: the Aesthetician,
  retainer ventures, the gemstone traders. There is *no* general system-unlock table in the game —
  `Quest.OtherReward` has 18 rows and that is the whole vocabulary — so systems stay curated, and
  that is a rule rather than an exception list. The other rule covers 49 entries whose label shape
  names something no sheet holds. An entry that *does* carry an identity gets no allowance at all.
- **a channel has silently gone to zero** — the `ItemAction` type numbers and the sheet column names
  are community-reverse-engineered and do move between Lumina releases. A join that stops matching
  raises no error of its own; it produces an artefact saying the game has nothing in that channel,
  and a catalogue that therefore looks complete. Checked in CI, and again in the generator against
  the counts the previous run recorded.
- **the arithmetic disagrees** — every classification, reason, count and total is recomputed in CI
  from the committed enumeration and the committed catalogue, by the same functions the generator
  called, and required to come out identical. A hand-tuned count or a softened classification fails
  here.
- **a reason does not resolve** — a row may only cite a reason key the policy defines, with the text
  the policy gives it.
- **the artefact is not canonical** — one row per line, fixed key order, so a hand edit shows up
  instead of surviving until the next regeneration reverts it.
