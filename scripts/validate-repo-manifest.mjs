#!/usr/bin/env node
// Validates repo.json - the Dalamud custom-repo manifest the plugin installer reads directly -
// against the shape Dalamud's PluginManifest actually parses, plus the two invariants that keep
// the testing channel (see release.yml, promote.yml and the README's Releases section) working.
//
// WHY THIS EXISTS
// ----------------
// Dalamud compares TestingAssemblyVersion to AssemblyVersion as a plain four-integer
// System.Version, purely numerically - no semver, no pre-release syntax. If either field is
// malformed (wrong number of parts, non-numeric, missing) or if TestingAssemblyVersion is not
// AT LEAST AssemblyVersion, the testing channel is unavailable to every opted-in tester and
// nothing anywhere reports an error - that is exactly the bug this repository shipped once
// already (both fields quietly pinned equal, forever). This validator exists so a malformed or
// regressed manifest fails CI instead of shipping silently.
//
// Equal is fine and expected: it is what a promotion leaves behind (promote.yml copies the
// testing version into stable and touches nothing else), and it means "nothing new to test".
// Only STRICTLY LESS is rejected here - stable can never overtake testing through either
// workflow, so that can only come from a hand edit or a bug.

const repo = (await import('../repo.json', { with: { type: 'json' } })).default;

let errors = 0;
const err = (m) => { console.error(m); errors++; };

const FOUR_PART_VERSION = /^\d+\.\d+\.\d+\.\d+$/;
const isNonEmptyString = (v) => typeof v === 'string' && v.length > 0;

const parseVersion = (v) => v.split('.').map(Number);
const compareVersions = (a, b) => {
  for (let i = 0; i < 4; i++) {
    if (a[i] !== b[i]) return a[i] - b[i];
  }
  return 0;
};

// Each component becomes part of the built assembly's AssemblyVersionAttribute, and the .NET
// compiler requires every component to fit in a ushort (0-65535) - confirmed by actually building
// with a component above that and watching it fail with CS7034/CS7035. A version string that
// passes FOUR_PART_VERSION but carries an out-of-range component would build nowhere; catch it
// here rather than at the next release.
const USHORT_MAX = 65535;
const hasInRangeComponents = (v) => parseVersion(v).every((n) => n >= 0 && n <= USHORT_MAX);

if (!Array.isArray(repo)) err(`repo.json must be an array, got ${typeof repo}`);
if (Array.isArray(repo) && repo.length !== 1) err(`repo.json must contain exactly 1 plugin entry, got ${repo.length}`);

const p = Array.isArray(repo) ? repo[0] : undefined;

if (p) {
  if (!isNonEmptyString(p.InternalName)) err('InternalName must be a non-empty string');

  if (!isNonEmptyString(p.AssemblyVersion)) err('AssemblyVersion must be a non-empty string');
  else if (!FOUR_PART_VERSION.test(p.AssemblyVersion))
    err(`AssemblyVersion "${p.AssemblyVersion}" is not four dot-separated integers (Dalamud parses it as System.Version)`);
  else if (!hasInRangeComponents(p.AssemblyVersion))
    err(`AssemblyVersion "${p.AssemblyVersion}" has a component above ${USHORT_MAX} - that cannot come from a build of this repo (AssemblyVersionAttribute would fail to compile), so it did not come from a release`);

  if (!isNonEmptyString(p.DownloadLinkInstall)) err('DownloadLinkInstall must be a non-empty string');
  else if (!/^https:\/\//.test(p.DownloadLinkInstall)) err(`DownloadLinkInstall "${p.DownloadLinkInstall}" is not an https URL`);

  if (!isNonEmptyString(p.DownloadLinkUpdate)) err('DownloadLinkUpdate must be a non-empty string');
  else if (!/^https:\/\//.test(p.DownloadLinkUpdate)) err(`DownloadLinkUpdate "${p.DownloadLinkUpdate}" is not an https URL`);

  if (typeof p.DalamudApiLevel !== 'number') err('DalamudApiLevel must be a number');

  // Testing fields: every release lands here first (see release.yml), so they are held to the
  // same shape as their stable counterparts, plus the never-regress invariant.
  if (!isNonEmptyString(p.TestingAssemblyVersion)) err('TestingAssemblyVersion must be a non-empty string');
  else if (!FOUR_PART_VERSION.test(p.TestingAssemblyVersion))
    err(`TestingAssemblyVersion "${p.TestingAssemblyVersion}" is not four dot-separated integers (Dalamud parses it as System.Version)`);
  else if (!hasInRangeComponents(p.TestingAssemblyVersion))
    err(`TestingAssemblyVersion "${p.TestingAssemblyVersion}" has a component above ${USHORT_MAX} - that cannot come from a build of this repo (AssemblyVersionAttribute would fail to compile), so it did not come from a release`);

  if (!isNonEmptyString(p.DownloadLinkTesting)) err('DownloadLinkTesting must be a non-empty string');
  else if (!/^https:\/\//.test(p.DownloadLinkTesting)) err(`DownloadLinkTesting "${p.DownloadLinkTesting}" is not an https URL`);

  if (p.TestingDalamudApiLevel !== undefined && typeof p.TestingDalamudApiLevel !== 'number')
    err('TestingDalamudApiLevel, when present, must be a number');

  if (typeof p.TestingDalamudApiLevel === 'number' && typeof p.DalamudApiLevel === 'number'
    && p.TestingDalamudApiLevel !== p.DalamudApiLevel)
    err(`TestingDalamudApiLevel (${p.TestingDalamudApiLevel}) must equal DalamudApiLevel (${p.DalamudApiLevel}) - the testing build targets the same Dalamud API level as stable, it never ships against a different one`);

  // The never-regress invariant. Equal is the correct resting state after a promotion; only
  // strictly LESS is a bug (a hand edit, or a workflow change that let stable overtake testing).
  if (FOUR_PART_VERSION.test(p.AssemblyVersion) && FOUR_PART_VERSION.test(p.TestingAssemblyVersion)) {
    const cmp = compareVersions(parseVersion(p.TestingAssemblyVersion), parseVersion(p.AssemblyVersion));
    if (cmp < 0)
      err(`TestingAssemblyVersion (${p.TestingAssemblyVersion}) is LESS than AssemblyVersion (${p.AssemblyVersion}) - Dalamud requires TestingAssemblyVersion > AssemblyVersion for the testing channel to be available at all, so a testing version below stable can only make the channel inert. It must never regress below stable.`);
  }

  // Each channel's version and its download URL must describe the SAME release. Publishing a
  // release moves TestingAssemblyVersion and DownloadLinkTesting together; promotion moves
  // AssemblyVersion and DownloadLinkInstall/Update together. Either one moving without the other
  // hands Dalamud a zip whose baked-in manifest version disagrees with the repo manifest's
  // version for that channel, and Dalamud throws on it - "Distributed plugin version does not
  // match repo version" - so every install and update on that channel fails.
  //
  // Both producers emit X.Y.Z.0 for tag vX.Y.Z, so the tag the version implies is the version
  // with its trailing .0 removed. A version that cannot be expressed that way is itself the bug:
  // it could not have come from a tagged, changelogged release, and so could never be promoted.
  const tagOfVersion = (v) => `v${v.replace(/\.0$/, '')}`;
  const tagOfUrl = (url) => (typeof url === 'string' ? url.match(/\/download\/([^/]+)\//)?.[1] : undefined);

  for (const [versionField, urlField] of [
    ['AssemblyVersion', 'DownloadLinkInstall'],
    ['AssemblyVersion', 'DownloadLinkUpdate'],
    ['TestingAssemblyVersion', 'DownloadLinkTesting'],
  ]) {
    if (!FOUR_PART_VERSION.test(p[versionField] ?? '')) continue;
    const urlTag = tagOfUrl(p[urlField]);
    if (!urlTag)
      err(`${urlField} ("${p[urlField]}") is not a release asset URL, so nothing confirms it serves ${versionField} (${p[versionField]})`);
    else if (urlTag !== tagOfVersion(p[versionField]))
      err(`${urlField} points at release ${urlTag}, but ${versionField} (${p[versionField]}) is the build tagged ${tagOfVersion(p[versionField])} - a version moved without its URL, or the reverse. Dalamud rejects a zip whose own version differs from the repo manifest's version for that channel.`);
  }
}

console.log(errors ? `FAILED: ${errors} errors` : 'OK: repo.json manifest shape valid');
process.exit(errors ? 1 : 0);
