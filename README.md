# Wayfarer

[![CI](https://github.com/will-corrigan/Wayfarer/actions/workflows/ci.yml/badge.svg)](https://github.com/will-corrigan/Wayfarer/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/will-corrigan/Wayfarer)](https://github.com/will-corrigan/Wayfarer/releases/latest)
[![License: AGPL-3.0](https://img.shields.io/badge/license-AGPL--3.0-blue.svg)](LICENSE)

A quest arrow that knows the way: teleports, doors and aethernet included — plus an unlock checklist.

Wayfarer is a [Dalamud](https://github.com/goatcorp/Dalamud) plugin for FINAL FANTASY XIV. It draws an
on-screen arrow that points at your current quest objective, and it plans the trip for you — aetheryte
teleports, building entrances, city aethernet — instead of just pointing at a dot on the map. Alongside
that, it keeps a running checklist of every quest-unlockable feature, mount and dungeon you're eligible
to pick up right now, and can route the arrow straight to the quest giver.

## Install

1. Open `/xlsettings` in-game, go to **Experimental**, and add this URL under **Custom Plugin Repositories**:

   ```
   https://raw.githubusercontent.com/will-corrigan/Wayfarer/main/repo.json
   ```

2. Open `/xlplugins` and install **Wayfarer** from the plugin list.

## Modules

### Quest Helper — the arrow

Follow a quest as normal and Wayfarer draws an arrow that points at the objective. It doesn't just
aim at map coordinates — it understands how you actually get there:

- **Teleports.** If the objective is far enough away that flying an aetheryte beats the run, the arrow
  points you to the nearest attuned aetheryte first.
- **Building entrances.** Objectives inside instanced housing, inns or other interior maps get routed
  through the correct entrance rather than pointing through a wall.
- **City aethernet.** Inside the big cities, the arrow uses aethernet shards for the same kind of
  detour it uses aetherytes for out in the field.
- **One click, one teleport.** Click the arrow when it's pointing at an aetheryte and Wayfarer casts
  that teleport for you — it's the only server-affecting action the plugin ever takes on your behalf.
  Client UI navigation, like opening the Duty Finder for a duty objective, is fine too; everything
  else it does is read-only.
- **Duty Finder link.** When your objective is inside a dungeon, trial or raid you can already queue
  for, the duty's name is a clickable link that opens it straight in the Duty Finder.

`/way` toggles the arrow widget. Lock its position, resize it, and hide it in combat or duties from
its settings panel. Turn off auto-sizing to resize the widget by hand — its size is remembered the
same way its position is.

> Screenshot coming soon: `docs/screenshots/quest-helper.png`

### Unlock Checklist

A living list of every feature, mount, dungeon and system you can unlock at your current level and
quest progress — hunting logs, chocobo, jobs, dungeons, glamour plates, all of it — cross-referenced
against your actual quest log so it only shows what's realistically available to you right now.

- **Filter by zone, level range or type**, or search by name.
- **Chip filters** for category (content, systems, cosmetics, zones) and priority (essential, nice to
  have, optional), so you can focus on what matters to you.
- **Route me** chains the Quest Helper arrow through every available pickup currently shown, ordered
  by distance from your position, so you can clear a run of unlock quests back to back.
- Locked entries show why — level-gated, quest-gated, gated behind a duty clear, Grand Company rank,
  beast tribe reputation, a mount, or already done — right in the tooltip.

> Screenshot coming soon: `docs/screenshots/unlock-checklist.png`

## A note on the arrow

The arrow points in a straight line to its next waypoint — it does not path around terrain, walls or
collision geometry. In open zones and along the routes above (aetherytes, entrances, aethernet) that's
almost always the right answer; you may still need to eyeball your way around an obstacle here and there.

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

## Releases

Releases are automated with [release-please](https://github.com/googleapis/release-please). Commits to
`main` are expected to follow [Conventional Commits](https://www.conventionalcommits.org/); the commit
type drives the version bump:

- `fix:` bumps the patch version
- `feat:` bumps the minor version
- a `!` after the type (or a `BREAKING CHANGE:` footer) bumps the major version

release-please keeps an up-to-date pull request open with the next version bump and a generated
`CHANGELOG.md`. Before merging it, check the pull request's checks: because it's authored by
`github-actions[bot]`, GitHub usually holds its CI run for manual approval (a banner reading
**"Approve and run"** on the pull request) — click that first so the checks actually run. Once it's
green, merge the pull request. Merging tags the release and, in the same workflow run, chains straight
into a packaging job: it builds the plugin, attaches `Wayfarer.zip` to the GitHub release, and publishes
the updated `repo.json` so the in-game plugin installer picks up the new version. There is no manual
tagging step.

## License

Wayfarer is licensed under the [GNU Affero General Public License v3.0](LICENSE).
