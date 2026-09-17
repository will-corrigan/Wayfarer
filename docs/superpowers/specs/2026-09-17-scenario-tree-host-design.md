# Guidance inside the game's Main Scenario Guide

Date: 2026-09-17
Status: draft for review

## Why

Wayfarer draws its own guidance readout: a chromeless addon that borrows the art of the game's
Main Scenario Guide (the `ScenarioTree` addon) and lays a plate, a header pill, a crest, the quest
name, step text and route lines under it. It is a convincing imitation, and it is still a second
banner on screen next to the real one.

VanillaPlus showed the better shape. Its MSQ progress bar is not a window of its own: it attaches a
node into the game's `ScenarioTree` addon and lets the game own visibility, scale, fade and HUD
placement. Wayfarer should do the same. The game already draws the quest name; Wayfarer's job is
to put the route under it.

This pass is deliberately narrow. It tracks the main scenario quest only, retires the imitation
banner wholesale, and switches every other feature off until it has a home in the new host.

## Scope

In:

- A guidance node attached inside the game's `ScenarioTree` addon, drawing step text, route lines
  and the compass under the game's own quest rows.
- Main scenario quest only. No followed-quest override, no switcher.
- Retirement of the imitation readout: its addon host, overlay fallback, placement, menus, and
  every setting that only existed for them.
- Unlock checklist, hunting log and aether current modules switched off: not registered, no
  commands, no Settings entries, no hub tabs.
- KamiToolKit submodule brought to upstream master. Dalamud.NET.Sdk stays on 15.0.0, which is
  the current release.

Out, and named so nobody reaches for them:

- Following a quest other than the current MSQ. The design leaves the attachment point for a
  switcher on the game's banner (see "Room left for later"), and nothing more.
- Any progress bar or completion percentage. VanillaPlus does that; Wayfarer does not duplicate it.
- Porting hunting, unlocks or aether currents into the new host.
- Deleting the retired code from the tree. It is unconstructed after this pass; deletion is a
  separate, mechanical change once the new host has shipped to the testing channel and been
  used.

## The game's addon, read from its layout file

`ui/uld/ScenarioTree.uld` (game version 2026.09.01) was pulled through XIViewer's file API and
parsed with Lumina. The widget's node tree, in root coordinates, is:

```
#1  Res            0,0    340x86   root
 #5 Res            0,0    340x86
  #6  Res          44,54  294x58   job-quest row container
   #7  Comp 1002    0,0   294x32   job quest row 1  (icon 32x32 at 0,0; Axis 12 text at x=28 y=11 w=266)
   #8  Comp 1002    0,26  294x32   job quest row 2
   #9  Res         30,0   142x28   holds #10 Comp 1004 (nine-grid text tag, OperationGuide.tex)
  #11 Text        95,7    156x18   header pill words, Axis 12 emboss
  #12 NineGrid    58,3    230x24   header pill art
  #13 Comp 1001    0,20   340x48   headline row: crest at (0,-20) 76x80, quest name Text #6
                                   at (63,15) 250x18 Axis 14 ellipsis, plate Image at (39,0) 300x48,
                                   Collision at (18,4) 318x40
```

Facts the design relies on, all now verified from the file rather than cited from memory:

| Fact | Value |
| --- | --- |
| Root width and height | 340 x 86 |
| Job quest rows start (root y) | 54 |
| Job quest row pitch | 26 |
| Job quest icon column x, words x | 44, 72 |
| Wayfarer's own sub-line geometry is authored 15 to the left of the game's | headline words at 48 vs 63 |
| Quest name text node for a later switcher or compass | component 13, text node 6, root-absolute (63, 35) |
| AtkValue 6 is the current MSQ's ScenarioTree row while an MSQ is in progress | confirmed by VanillaPlus in game |

So the guidance node is attached to the root as its last child, at x=15 so its marker gutter
lands on the game's icon column and its words on the game's text column, and at
y = 54 + 26 * (visible job quest rows). With no job quest rows that is the pitch directly under
the plate, exactly where the game's first job row goes.

Two unknowns remain for the dev plugin, and the implementation plan schedules them before the
layout numbers are declared final:

1. Whether a child node placed below the root's declared 86 receives clicks. ATK collision
   nodes are usually independent of the parent's bounds, but this is not proven for this addon.
   If not, the node grows the root's height while it is shown and restores it on finalize.
2. What the addon does with no MSQ in progress and inside a duty (`DisplayMode` 1). Default:
   the node hides itself whenever the feed has nothing to say, which covers both.

## Architecture

```
game ScenarioTree addon (root)
  └─ ScenarioTreeGuidanceNode  (ours, attached on setup, disposed on finalize)
       ├─ line sections (measured text, medallion, rule)
       ├─ compass ring + needle
       └─ hit boxes / controller anchors for the pressable lines

ScenarioTreeHost ── owns an AddonController("ScenarioTree")
   │                 OnSetup    → build node, attach to root
   │                 OnUpdate   → frame = frames.Next(); node.Layout(frame)
   │                 OnFinalize → dispose node
   └── IGuidanceFrames  (what to draw this frame, or null)
          └── GuidanceFrameSource ── ReadoutFeed + config + player/camera → ReadoutFrame
```

### Components

**`ScenarioTreeHost`** (`Wayfarer/Windows/Native/ScenarioTreeHost.cs`). The only class that knows
the game's addon exists. Constructor takes `IGuidanceFrames`, `ScenarioTreeGuidanceNode.Factory`,
`IFramework`, `IPluginLog`. `Start()` enables the controller on the framework thread; `Dispose()`
disables it there too, with the same two-second bounded wait the current overlay uses, because
Dalamud unloads plugins off-thread. It never decides visibility: the node is a child of the
game's root, so it is shown, hidden, faded and scaled by the game.

Setup, update and finalize handlers each catch and log. A fault in our node must cost the frame
and nothing else; it must never propagate into the game's addon update.

**`ScenarioTreeGuidanceNode`** (`Wayfarer/Windows/Native/ScenarioTreeGuidanceNode.cs`). A
`ResNode` holding a `SectionStackNode` of line sections, the compass pair, and the pressable
lines' hit boxes and controller anchors. It has no banner, no pill, no crest, no heading, no
subject line, no cog, no switcher, no drag handle. It is built from the pieces that already
exist and are already tested: `MeasuredTextNode`, `LineSlot`, `SectionStackNode`, the medallion
part of `ScenarioTree.tex`, `CompassBitmap`, and the hit-box and nav-anchor builders lifted out
of `ReadoutBodyNode` into a small shared helper rather than copied.

`Layout(ReadoutFrame frame)` positions every child for this frame and returns the node's size.
Width is the addon root's width. The first line's gutter carries the compass, exactly as
`ReadoutBodyLayout.Arrow` places it now; that pure geometry is reused unchanged.

Constructed through a factory delegate so the host can rebuild it on every addon setup without
the host knowing the node's dependencies (`ITextureProvider`, `IPluginLog`, the diagnostics
flag, the press callbacks).

**`ScenarioTreeContent`** (`Wayfarer.Core/Ui/ScenarioTreeContent.cs`). Pure. Takes
`ReadoutInputs` and returns the lines the node draws: the step text when it differs from the
quest name, then the route lines from the existing `ReadoutComposer.AddRoute`. No heading, no
subject, no context lines. The composer's route-building methods become `internal` and are
shared; nothing is duplicated. There is deliberately no gate on the snapshot's source id: the
composer must stay ignorant of which features exist (see `ReadoutContent.StripLabel`), and
in this pass the quest source is the only source the arbiter is ever given, which is the gate.

**`ScenarioTreeLayout`** (`Wayfarer.Core/Ui/ScenarioTreeLayout.cs`). Pure geometry for a
lines-only stack: line sections, rules, medallions, words and the compass, with no banner and no
words section. Reuses `ReadoutBodyLayout`'s helpers and its line walk, and adds the two numbers
that place the node inside the game's root: `Left` (15) and `Top(visibleJobRows)`.

**`IGuidanceFrames` / `GuidanceFrameSource`** (`Wayfarer/Windows/GuidanceFrameSource.cs`). The
seam between the host and the rest of the plugin. `Next()` returns a `ReadoutFrame` or null. It
composes `ScenarioTreeContent` from `ReadoutFeed`, computes the compass bearing from the local
player and camera yaw (moved out of `GuidanceOverlay.Bearing` unchanged), and reads arrow icon,
arrow scale, text scale and the click-to-teleport flag from `QuestHelperConfig`. The host is
testable against a stub of this interface; the source is testable against a stub feed.

**`ReadoutFrame`** stays. It already carries no position, which is now literally true.

### Composition root

`Plugin` constructs, in order: config, input mode, guidance graph (arbiter, router, quest source,
service, navigator, map flag), `ReadoutFeed`, `GuidanceFrameSource`, `ScenarioTreeHost`, the
quest helper module, DTR entry, context menu actions, settings, hub. Disposal is the exact
reverse. No service locator, no static state beyond what KamiToolKit requires for its own
lifecycle hooks.

Dropped from the root: `ReadoutPlacement`, `GuidanceOverlay`, `NativeHubWindow`'s unlock and
hunting collaborators, `NamePlateMarkers` (it only marks hunting and unlock targets), the unlock
and hunting services and windows, the aether current service. `ModuleRegistry` registers the
quest helper module and nothing else.

`QuestHelperModule` keeps its ambient quest source registration and its `/way` toggle. The
`ArrowWindow` ImGui fallback goes: the new host has no fallback because the game's own banner
cannot fail to exist in the way a plugin window can. If the node cannot be built, the log says
so once and guidance is absent for the session, which is honest.

### Settings window

The hub window is 3,800 lines whose constructor takes the unlock and hunting services and whose
setup refuses to build without their data. Reducing it to one tab would be surgery on a file
that is being retired. Instead the Settings tab's ~250 lines become their own `SettingsWindow`
(a `NativeAddon`): a scrolling column of the catalogue's controls with the same
scroll-follows-focus wiring, cursor graph, controller hint line and corner presets. The hub, the
journal page and every `Hub*` node class stay in the tree unconstructed. `/wayfarer`, Dalamud's
cog and its main button all open the settings window; the ImGui `ConfigWindow` remains the
fallback when it cannot open.

`SettingsCatalog` sections become: Features (quest helper toggle), Guidance (map flag), Readout
(show, arrow colour, arrow size, text size, hide in combat, hide in duty, click to teleport,
diagnostics), Controls. The Readout Position section and every position, drag and native-toggle
setting are removed. `Configuration` keeps the fields so old configs still deserialise; the
migration bumps the version and clears the retired ones.

### KamiToolKit bump

The submodule moves from `2c6e52d` to upstream master (37 commits). One of them changes our
call sites: `KamiToolKitLibrary.Initialize` becomes `InitializeAsync` and `Cleanup` becomes
`Dispose` (main thread) or `DisposeAsync` (any thread). Dalamud constructs plugins on the
framework thread, and `InitializeAsync` awaits a framework-thread hop after file IO, so blocking
on it inside the constructor would deadlock. The composition root therefore starts the
initialisation task and chains every native start (the ScenarioTree host, the settings window)
onto its completion, marshalled back to the framework thread. Disposal uses the main-thread
variant when already there and blocks on the async one, bounded, otherwise. The bump is the
first task of the plan so every later task builds against the API the plugin will ship with.

## Room left for later

The switcher for following a different quest belongs on the game's banner, on the headline row:
component node 13, text node 6, the node VanillaPlus attaches its bar under. When that work
resumes, `ScenarioTreeHost` gains a second attachment in `OnSetup` next to the first, and
`ScenarioTreeContent` drops its quest-source gate. Nothing in this pass should make that harder,
and nothing in this pass builds any of it.

The compass can move from the gutter to beside the quest name by the same route; the choice is
deferred until both have been seen in game.

## Error handling

- Addon handlers: every `OnSetup`, `OnUpdate`, `OnFinalize` body is wrapped; exceptions are
  logged once per kind per session and the node is torn down, never left half-built inside the
  game's tree.
- Texture availability: the medallion and compass sheets can be late rather than missing. The
  existing "ask every frame, warn once under diagnostics" policy carries over.
- Off-thread disposal: bounded wait, warning on timeout, exactly as today.

## Testing

Pure, in `Wayfarer.Tests`:

- `ScenarioTreeContentTests`: no heading and no subject ever; step text only when it differs
  from the quest name; every route shape from the composer appears unchanged; empty for any
  source other than the quest source; empty for hidden mode.
- `GuidanceFrameSourceTests` against a stub feed: null when the widget is hidden or content is
  empty; bearing and hidden-reason mapping preserved from the current overlay tests.
- `ScenarioTreeLayoutTests`: a lines-only layout has no banner height, the compass gutter is
  reserved whether or not the arrow is drawn, and the containment proofs in
  `LayoutContainmentTests` hold for the new node's geometry.
- Existing readout composer, glyph, and compass bitmap tests stay green.

In game, on the dev plugin, before any release build:

1. Node appears under the job quest rows, hides with the Main Scenario Guide, follows HUD layout
   moves and scale.
2. Compass points at the objective and updates with camera yaw.
3. Teleport line is clickable with a mouse and reachable with a controller.
4. Entering a duty, completing the MSQ, and reloading the plugin each leave no stray node.

## Migration notes for players

The readout position settings disappear. The guidance now lives wherever the player's HUD layout
puts the Main Scenario Guide. Hunting log, unlock checklist and aether currents are switched off in
this build and will return once they have a place in the new host. Release notes say all of this.
