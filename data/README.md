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
2. **The game's own sheets** — `Quest`, `ContentFinderCondition`, `Item`, `Mount`, `Companion`,
   `Emote`, `Orchestrion`, `TripleTriadCard`, `Action` — via `tools/Wayfarer.CatalogueGen`.

Every quest binding is then cross-checked against a **third**, independently maintained statement:
the linked page's own `{{ARR Infobox Quest}}` `Quest Number`, which the wiki's import bot writes.
Pages marked `DontBot = yes` are hand-written and are **not** used — several of those carry a
copy-pasted quest number that is simply wrong.

Disagreements between sources are **recorded, never silently resolved**. An entry whose sources
disagree loses its `verified` standing and says so in `notes`.

## Generated fields vs curated fields

The generator owns the entry's **identity and provenance**. It preserves everything editorial.

| Generated every run | Carried forward from the committed file |
|---|---|
| `quest` (display name of the resolved row) | `unlock`, `type`, `questKind` |
| `sources` | `description`, `notes`, `priority`, `cosmetic` |
| `confidence` | `requires` (curated script-only requirements) |
| `level`, `levelSource`, `category` | |

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
confidences and requirement kinds, and that an unknown field is an error rather than a value the
plugin silently ignores.

`validate-catalogue-identity.mjs` — the properties that make the file trustworthy and its diffs
readable:

- **canonical form** — the file must be exactly what the generator would write (fixed key order,
  2-space indent, trailing newline), so a hand edit shows up instead of surviving until the next
  regeneration reverts it;
- **stable ordering** — non-decreasing level, level-less entries last;
- **a recorded identity for every entry** — either the game rows it rests on, or an explicit
  `requires.unverifiable`, so nothing can be silently identity-less;
- **no entry claiming Available on an absent gate** — an entry with no game row must be marked
  unverifiable, and one that cites a row must not be;
- **grounded levels** — a level needs a `levelSource`, and a `levelSource` naming a quest row must
  name a row the entry itself cites;
- **no duplicate identities** — same unlock, same level, same rows.

`Wayfarer.Tests/UnlockDatasetShapeTests.cs` asserts the same invariants through the C# parser, so
they hold for the code that actually reads the file and not only for the Node validator.
