# Wayfarer

[![CI](https://github.com/will-corrigan/Wayfarer/actions/workflows/ci.yml/badge.svg)](https://github.com/will-corrigan/Wayfarer/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/will-corrigan/Wayfarer)](https://github.com/will-corrigan/Wayfarer/releases/latest)
[![License: AGPL-3.0](https://img.shields.io/badge/license-AGPL--3.0-blue.svg)](LICENSE)

A quest arrow that knows the way: teleports, doors and aethernet included — plus an unlock checklist
and a hunting-log mode, in one window that a mouse and a controller drive equally well.

## Install

1. Open `/xlsettings` in-game, go to **Experimental**, and add this URL under **Custom Plugin Repositories**:

   ```
   https://raw.githubusercontent.com/will-corrigan/Wayfarer/main/repo.json
   ```

2. Open `/xlplugins` and install **Wayfarer** from the plugin list.

## What it does

Wayfarer is a [Dalamud](https://github.com/goatcorp/Dalamud) plugin for FINAL FANTASY XIV. It is
built around three loops.

### Follow the main scenario

Follow a quest as normal and Wayfarer puts a readout on screen, drawn with the game's own fonts,
colours and direction chevron so it reads as part of the interface rather than as an overlay bolted
on top. It does not merely aim at map coordinates — it plans the trip:

- **Teleports.** When flying to an aetheryte beats the run, the readout says which one, and
  clicking that line casts the teleport. It is the only server-affecting action the plugin ever
  takes on your behalf; everything else it does is read-only or client-side UI navigation.
- **Building entrances.** Objectives inside instanced housing, inns or other interior maps are
  routed through the correct entrance rather than pointed at through a wall.
- **City aethernet.** Inside the big cities the arrow uses aethernet shards for the same kind of
  detour it uses aetherytes for out in the field.
- **Duty Finder.** When the objective is inside a dungeon, trial or raid you can already queue for,
  the Duty Finder is one click or one menu entry away.

The readout sits under the game's own quest tracker by default and follows it wherever you move it,
including the way the tracker mirrors itself on the left half of the screen. Corner presets, text
size, arrow size and arrow colour are all in Settings.

### Pick things up on the way

A running list of every feature, mount, dungeon and system you can unlock at your current level and
quest progress — hunting logs, chocobo, jobs, dungeons, glamour plates — cross-referenced against
your actual quest log, so it shows only what is realistically available now.

It is designed to be noticed without opening anything:

- The game's own quest marker appears over the heads of quest givers you can pick up from.
- The entry in the server info bar carries an exclamation marker whenever this zone has something
  available, and keeps carrying it while a route or a hunt is running.
- The readout names the nearest few, with live distances, when nothing else is engaged.

Open the window for the detail: filter by zone, level range or type, chip filters for category and
priority, and **Route me**, which chains the arrow through every available pickup currently shown,
nearest first, so you can clear a run of unlock quests back to back. Locked entries say why —
level, quest, duty clear, Grand Company rank, beast tribe reputation, a mount, or already done.

### Go hunting

Switch into hunting mode and Wayfarer walks you through the current rank's hunting log one mob at a
time, advancing itself on your kill count, with exactly the same guidance as the main scenario gets:
the same arrow, the same teleport advice, the same aethernet legs, and the game's map marker on the
current target. Start it from the window's Hunting Log tab or from the game's own right-click menu;
Stop is beside it wherever it appears.

## The window

There is one Wayfarer window, native rather than plugin-drawn, with four tabs — **Checklist**,
**Hunting Log**, **Quests** and **Settings**. It works the same with a mouse or a controller: the
game's own cursor navigation is wired through it, so a d-pad reaches every control, and the button
hints along the bottom render as Ⓐ/Ⓑ or ✕/○ to match your pad setting.

The **Quests** tab is where you choose which accepted quest the arrow follows, and where guidance
gets the buttons an on-screen readout cannot carry: Teleport, Duty Finder and Stop.

Nothing requires typing. The window opens from the server info bar entry, from the plugin installer,
from the settings cog, and from the game's right-click menu. `/wayfarer` and its shortcuts
(`hunt`, `checklist`, `quests`, `settings`, `stop`) are conveniences, not the way in.

## Mouse and controller

Both are first class. The readout is clickable for a mouse — the teleport line is one click — and
click-through for a controller, where a focusable surface floating over the world would trap the
cursor; there the same actions live on the game's own context menu and on the window's tabs. Which
one you get follows whichever device you used last, and can be pinned in Settings.

## A note on the arrow

The arrow points in a straight line to its next waypoint — it does not path around terrain, walls or
collision geometry. In open zones and along the routes above (aetherytes, entrances, aethernet)
that's almost always the right answer; you may still need to eyeball your way around an obstacle
here and there.

## Data

Unlock data (levels, quest names, prerequisites) is compiled from the
[Gamer Escape](https://ffxiv.gamerescape.com/wiki/Guide:Progression_and_Level_Locked_Content) community
wiki. Thanks to the Gamer Escape contributors for maintaining it.

Hunting log target coordinates are curated from [Hunty](https://github.com/Infiziert90/Hunty) by
Infi (MIT). Thanks to Infi for maintaining that data.

## Third-party

Native (non-ImGui) windows are built on [KamiToolKit](https://github.com/MidoriKami/KamiToolKit) by
MidoriKami (MIT), vendored as a git submodule under `external/KamiToolKit`. Full license text and
other third-party notices live in [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).

## Building from source

Requires the [.NET SDK](https://dotnet.microsoft.com/) version pinned in `global.json` and a local
Dalamud dev environment. This repo has a git submodule (`external/KamiToolKit`), so clone with
`--recurse-submodules`, or run `git submodule update --init` after a plain clone. Point the
`DALAMUD_HOME` environment variable at your Dalamud dev hooks directory before building:

```bash
git submodule update --init
export DALAMUD_HOME=/path/to/XIVLauncher/addon/Hooks/dev
dotnet build -c Release
```

### Testing

```bash
dotnet test
```

### Contributing

Issues and pull requests are welcome. Please run `dotnet format` and the hygiene check before submitting:

```bash
dotnet format --exclude external/KamiToolKit
pwsh -NoProfile -File scripts/check-hygiene.ps1
```

The `--exclude external/KamiToolKit` flag is not optional: the vendored submodule is listed in
`Wayfarer.slnx`, so a plain `dotnet format` will reformat ~185 upstream files against its own
`.editorconfig` (BOM stripping, whitespace rules) that upstream never applies. If that happens,
restore the submodule's working tree with `git -C external/KamiToolKit checkout -- .` — never
commit submodule content.

## Releases

Releases are automated with [release-please](https://github.com/googleapis/release-please). Commits to
`main` are expected to follow [Conventional Commits](https://www.conventionalcommits.org/); the commit
type drives the version bump:

- `fix:` bumps the patch version
- `feat:` bumps the minor version
- a `!` after the type (or a `BREAKING CHANGE:` footer) bumps the major version

Reserve `feat:` for something a player can point at and use — a new module, a new surface, a
capability that did not exist. Repairing, tightening or completing something that was already
advertised is `fix:`, even when the diff is large; a release that only repairs things should be a
patch. Work with no user-visible effect (`refactor:`, `chore:`, `test:`, `ci:`, `docs:`) is hidden
from the changelog entirely, and `data:` is used for changes to the unlock catalogue.

The subject line of every one of these becomes a line in the release notes, so write it for the
person installing the plugin rather than for the person reviewing the diff: say what changed for
them, in the words they would use. "fix: stop the arrow vanishing when a hunting target is chosen"
belongs in a changelog; "fix: correct completion signal ownership in the arbiter" does not — that
belongs in the commit body, which readers of the repository will find and players never need.

release-please keeps an up-to-date pull request open with the next version bump and a generated
`CHANGELOG.md`. Before merging it, check the pull request's checks: because it's authored by
`github-actions[bot]`, GitHub usually holds its CI run for manual approval (a banner reading
**"Approve and run"** on the pull request) — click that first so the checks actually run. Once it's
green, merge the pull request. Merging tags the release and, in the same workflow run, chains straight
into a packaging job: it builds the plugin and attaches `Wayfarer.zip` to the GitHub release. There is
no manual tagging step.

Where that build goes is the next section.

### Two channels

Every release is published to Dalamud's **testing** channel first, and reaches everyone else only
when it is promoted by hand. `repo.json` carries both channels at once: `AssemblyVersion` /
`DownloadLinkInstall` / `DownloadLinkUpdate` are stable, `TestingAssemblyVersion` /
`DownloadLinkTesting` are testing. There is one version stream — release-please's — and the two
channels are two pointers into it.

**Merging the release PR** tags `vX.Y.Z`, builds it, attaches the zip as a **prerelease**, and points
the *testing* channel at it. Opted-in testers get it on their next update check; nobody else sees
anything. `AssemblyVersion` is not touched, so stable installs are unaffected.

**Actions → Promote to Stable → Run workflow** points the stable channel at whatever the testing
channel currently holds, and clears the prerelease flag on that release. It **does not build**:
`AssemblyVersion` becomes the tested build's version, `DownloadLinkInstall`/`DownloadLinkUpdate`
become its URL, and everyone installs the byte-identical zip the tester played. Optionally type the
tag you expect (`v0.8.2`) into the input as a confirmation; leave it blank to promote whatever is
there. The workflow refuses to run if testing isn't newer than stable, if the zip isn't actually
attached to that release, or if `repo.json`'s testing version and testing URL name different
releases.

So a normal cycle reads: land fixes → merge the release PR (testing gets `0.8.1`, stable stays
`0.8.0`) → iterate, merging again as needed (testing `0.8.2`) → promote (both `0.8.2`) →
land more work → merge (testing `0.9.0`, stable stays `0.8.2`). `AssemblyVersion` differing from
`TestingAssemblyVersion` in `repo.json` is not a bug: it is the readable state "there is a build
waiting to be promoted". Equal means there is nothing left to test.

Two things are load-bearing, both enforced by `scripts/validate-repo-manifest.mjs` in CI and by
guards in the workflows:

- Dalamud shows a testing build only while `TestingAssemblyVersion` is **strictly greater** than
  `AssemblyVersion`, compared as a plain four-integer `System.Version`. Equal or lower and the
  channel is inert with no error reported anywhere — that has already happened here once.
- A channel's version and its download URL must name the same release. Dalamud rejects a zip whose
  own baked-in version differs from the repo manifest's version for the channel it came from, so
  the promoted version number is never a choice: it is whatever the tested build baked in.

That second point is why release-please must keep its **default versioning strategy**. Do not set
`prerelease-type` or `versioning-strategy: prerelease` in `release-please-config.json`, however
tempting `0.8.1-beta.2` looks: it is not a valid `System.Version`, so it would fail to parse and take
the whole repo entry offline for every user, and MSBuild strips the suffix anyway, so every
`-beta.N` would bake the identical `0.8.1.0` and no tester could ever receive the second one.
Plain `X.Y.Z` → `X.Y.Z.0` is the only representable shape.

### Rolling back

Dalamud only ever offers a strictly greater version — there is no downgrade path, and nothing can
recall a build from someone who already updated. In order of usefulness:

- **Roll forward.** Revert the commit, merge the new release PR, let the tester confirm, promote.
  This is the answer in almost every case.
- **Stop the spread.** Set `"IsHide": "True"` in `repo.json` and push. Dalamud filters hidden
  plugins out of the installer list *and* out of the update scan, so nobody who hasn't taken the bad
  build yet will get it.
- **Repoint stable at the previous release.** Set `AssemblyVersion` and both stable download links
  back to the previous tag by hand. This stops fresh installs pulling the bad zip, but it puts the
  version stream in reverse and helps nobody who already updated. Last resort.

### For a tester

Two one-time settings in their own Dalamud install, no new repo URL and no reinstall:

1. `/xlsettings` → **Experimental** tab → check **"Get plugin testing builds"**.
2. `/xlplugins` → **Installed Plugins** tab → right-click **Wayfarer** → **"Receive plugin testing
   versions"**.

Both are local to that Dalamud install and survive plugin updates. From then on the normal update
check picks up each new release as it is cut, and the plugin's header carries yellow caution tape to
make it obvious it is a testing build.

## License

Wayfarer is licensed under the [GNU Affero General Public License v3.0](LICENSE).
