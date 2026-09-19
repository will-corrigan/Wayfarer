# Wayfarer

[![CI](https://github.com/will-corrigan/Wayfarer/actions/workflows/ci.yml/badge.svg)](https://github.com/will-corrigan/Wayfarer/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/will-corrigan/Wayfarer)](https://github.com/will-corrigan/Wayfarer/releases/latest)
[![License: AGPL-3.0](https://img.shields.io/badge/license-AGPL--3.0-blue.svg)](LICENSE)

Your quest step and the way there, written under the game's own Main Scenario Guide in its own
fonts and colours, and reachable with a mouse or a controller.

## Install

1. Open `/xlsettings` in-game, go to **Experimental**, and add this URL under **Custom Plugin Repositories**:

   ```
   https://raw.githubusercontent.com/will-corrigan/Wayfarer/main/repo.json
   ```

2. Open `/xlplugins` and install **Wayfarer** from the plugin list.

## What it does

Wayfarer is a [Dalamud](https://github.com/goatcorp/Dalamud) plugin for FINAL FANTASY XIV. It adds
two lines under the game's Main Scenario Guide: the step you are on, and how to get to it. Beside
them sits a compass needle and the distance in yalms.

### The route

It does not simply aim at map coordinates. It plans the trip and says so in the second line:

- **Teleports.** When flying to an aetheryte beats the run, the line names it, and pressing it casts
  the teleport. That is the only server-affecting action the plugin ever takes for you.
- **Building entrances.** An objective inside an inn, a housing ward or any other interior map is
  routed through the right door rather than pointed at through a wall.
- **City aethernet.** Inside the big cities the route uses aethernet shards for the same kind of
  detour it uses aetherytes for in the field.
- **Duty Finder.** When the step is inside a dungeon, trial or raid you can queue for, the line
  opens the Duty Finder at it.

### Pressing the words

The words that name what to do are printed in the colour the game gives a link, with the thing's own
icon in front of them. Those words are the control:

- A key item the step wants is used.
- An emote the step wants is performed.
- A phrase the step wants said goes into your chat box, ready for you to send.
- A teleport is cast, or the Duty Finder opens.

A controller reaches all of it. The pad's cursor stops on the words themselves, not on the whole
line, so what is highlighted is what a press will do.

### Following a quest

By default Wayfarer follows the main scenario, the same quest the guide's own plate names. A button
in your quest journal follows any other accepted quest instead: the plate then carries that quest's
name, and pressing it opens the journal at it.

### Settings

A cog sits at the guide's top right, beside the plate. The settings window is the game's own, laid
out like the journal, and holds the entry and route text sizes, the line spacing, the block's left
edge, the compass size and where the compass sits. A live preview of the real block sits above them.
`/wayfarer` opens the same window.

## A note on the compass

The needle points in a straight line at the next waypoint. It does not path around terrain, walls or
collision geometry. In open zones and along the routes above that is almost always the right answer,
but you may still need to eyeball your way around an obstacle.

## Data

Routing data (aetherytes, aethernet shards and building entrances) is generated from the game's own
files by the generator in `tools/Wayfarer.RoutingGen`.

## Third-party

Native (non-ImGui) windows are built on [KamiToolKit](https://github.com/MidoriKami/KamiToolKit) by
MidoriKami (MIT), used as the package its author publishes. Full license text and
other third-party notices live in [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).

## Building from source

Requires the [.NET SDK](https://dotnet.microsoft.com/) version pinned in `global.json` and a local
Dalamud dev environment. Point the `DALAMUD_HOME` environment variable at your Dalamud dev hooks
directory before building:

```bash
export DALAMUD_HOME=/path/to/XIVLauncher/addon/Hooks/dev
dotnet build -c Release
```

### Testing

The tests target the plugin's own framework, so build them and run the assembly. `dotnet test`
reports no tests on a non-Windows host and is not the way in.

```bash
dotnet build Wayfarer.Tests/Wayfarer.Tests.csproj
dotnet ./Wayfarer.Tests/bin/Debug/net10.0-windows/Wayfarer.Tests.dll
```

### Contributing

Issues and pull requests are welcome. Please run `dotnet format` and the hygiene check before submitting:

```bash
dotnet format
pwsh -NoProfile -File scripts/check-hygiene.ps1
```

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
from the changelog entirely.

The subject line of every one of these becomes a line in the release notes, so write it for the
person installing the plugin rather than for the person reviewing the diff: say what changed for
them, in the words they would use. "fix: stop the compass vanishing when a step has no route"
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
