# Changelog

## [0.8.0](https://github.com/will-corrigan/Wayfarer/compare/v0.7.0...v0.8.0) (2026-08-25)


### New

* a window that reads like the game's, and a catalogue that tells the truth ([#27](https://github.com/will-corrigan/Wayfarer/issues/27)) ([df20176](https://github.com/will-corrigan/Wayfarer/commit/df2017628da719824b530ecfa258884f2a3a76b8))

## [0.7.0](https://github.com/will-corrigan/Wayfarer/compare/v0.6.0...v0.7.0) (2026-08-23)


### New

* **readout:** open settings from a cog on the readout ([70c4a0c](https://github.com/will-corrigan/Wayfarer/commit/70c4a0cd721f2ae6d37d45054f2ea00f70bb7dbd))
* **readout:** say when the target is above or below you ([c15a336](https://github.com/will-corrigan/Wayfarer/commit/c15a3365f50939a7994334e2c1a99cba820eb4f3))


### Fixed

* correct the trophy-mount quest requirements ([c72ec0a](https://github.com/will-corrigan/Wayfarer/commit/c72ec0a902998f1f059b42d6de310f0dc07f047e))
* place the readout, and say when a target is above you ([fe87d06](https://github.com/will-corrigan/Wayfarer/commit/fe87d067cabb4d7575b862ee039fd70fbf95f95d))
* **readout:** rebuild the collision list when the set of hit boxes changes ([ac2f1c6](https://github.com/will-corrigan/Wayfarer/commit/ac2f1c6c50dd79baa01841946660ee10aa4ee206))
* **settings:** show the readout's real position on the sliders ([fa912de](https://github.com/will-corrigan/Wayfarer/commit/fa912de71aaf6b72f788afd3b36d46e72e3711a7))
* **settings:** stop the tab stretching its controls back over the scroll bar ([17ac047](https://github.com/will-corrigan/Wayfarer/commit/17ac0473f144a6e635026c62c391f381c7053055))
* **ui:** call the unlocks tab "Unlocks" ([ccf7e73](https://github.com/will-corrigan/Wayfarer/commit/ccf7e73ffb790e5eaa2e5da8807c73bb5720d42f))
* **unlocks:** correct trophy-mount quest requirements ([4eff92c](https://github.com/will-corrigan/Wayfarer/commit/4eff92c150e978b3a8409cfb973a3abf493cb848))

## [0.6.0](https://github.com/will-corrigan/Wayfarer/compare/v0.5.0...v0.6.0) (2026-08-23)


### New

* regenerate the unlock catalogue from its sources ([5d34edb](https://github.com/will-corrigan/Wayfarer/commit/5d34edbc8260973929a250b33f91f56eb044fec3))
* **tools:** resolve catalogue names against the game's own sheets ([5854f9f](https://github.com/will-corrigan/Wayfarer/commit/5854f9f742016a8a13b61fe1f0233a3484565e88))
* **unlocks:** regenerate the catalogue from link targets; stop inventing levels ([9e2da17](https://github.com/will-corrigan/Wayfarer/commit/9e2da1714595ef6f04fa1f319ccaec00f35676bb))
* **unlocks:** teach the generator the gates the hand pass found ([666778b](https://github.com/will-corrigan/Wayfarer/commit/666778b83eb0eaeb6e24957e0d017a8c202eb198))


### Fixed

* a real arrow, and put the readout where you want it ([d535d40](https://github.com/will-corrigan/Wayfarer/commit/d535d403309c00f19f8b9f8be6c498f8b3dfdc99))
* **data:** ground the catalogue in the guide's links, not its display text ([3057281](https://github.com/will-corrigan/Wayfarer/commit/3057281c086d00b45f34ebef3aca4c0579a73fc4))
* **dtr:** make the info bar describe the next step, not the mode ([11e7c3e](https://github.com/will-corrigan/Wayfarer/commit/11e7c3e37797ae8f71b652f869c34b9e3fcf0528))
* **hub:** scroll the focused setting into view, and title-case the window ([1acc27c](https://github.com/will-corrigan/Wayfarer/commit/1acc27c8b04bcf841d7434d188e3e49ecccf5d32))
* **hud:** keep readout diagnostics out of the log unless asked for ([1929c89](https://github.com/will-corrigan/Wayfarer/commit/1929c89c2200d040fc097e25c7636b7f918fa742))
* **hunting:** repair the mangled "Hunting Log tt warrior" heading ([a0e7540](https://github.com/will-corrigan/Wayfarer/commit/a0e7540654f391ddd1cd3a37c2f3eb0caf363f11))
* **logging:** make the whole plugin's log worth reading ([bcb48e1](https://github.com/will-corrigan/Wayfarer/commit/bcb48e138907042f839d780466e6e02104ad7959))
* quieter logs, honest catalogue, less dead code ([b0fd73c](https://github.com/will-corrigan/Wayfarer/commit/b0fd73c8d67c3f1e05b24bb7fb087bb67c3bde13))
* **readout:** draw a real direction arrow and let the player place the readout ([61ff952](https://github.com/will-corrigan/Wayfarer/commit/61ff95256289fc3b698ac2451d7f2c88f12caaf9))
* **settings:** title-case the readout-diagnostics label ([c37d128](https://github.com/will-corrigan/Wayfarer/commit/c37d128eea52554e093d8250f04b1020da83c700))

## [0.5.0](https://github.com/will-corrigan/Wayfarer/compare/v0.4.0...v0.5.0) (2026-08-22)


### Features

* guidance architecture and rank-wide hunting ([ab03536](https://github.com/will-corrigan/Wayfarer/commit/ab03536db57f7fca384e04a78ded7e4c62ef4330))
* **guidance:** add core guidance types and payload-blind arbiter ([421c2eb](https://github.com/will-corrigan/Wayfarer/commit/421c2eb0b95b29dbbf3c6d759f10737a2eaf7ce5))
* **guidance:** flag each objective through a declared affordance ([13a0424](https://github.com/will-corrigan/Wayfarer/commit/13a042444efd02b963f1dee852d094212161d2b9))
* **guidance:** own completion per source and fix the vanishing hunting target ([2737b56](https://github.com/will-corrigan/Wayfarer/commit/2737b5617181449bf717786e0e54171d384cbb45))
* **hunting:** chain the whole rank, grouped by zone ([800c979](https://github.com/will-corrigan/Wayfarer/commit/800c9798373346371412ae34e882f7b0a8de85e9))
* open the checklist and hunting log by command ([b321041](https://github.com/will-corrigan/Wayfarer/commit/b321041939167db4e92aded01655edc42a7b6c31))
* reach the checklist and hunting log without typing ([e02f04c](https://github.com/will-corrigan/Wayfarer/commit/e02f04ca513a07921eeca549b52b6319ed2102ca))


### Bug Fixes

* **hunting:** drop tracked page state when no log is active ([fc32ae2](https://github.com/will-corrigan/Wayfarer/commit/fc32ae217cc8830ce8195b7e337c1438515d75f9))

## [0.4.0](https://github.com/will-corrigan/Wayfarer/compare/v0.3.0...v0.4.0) (2026-08-22)


### Features

* controller hub window with checklist, hunting log and settings tabs ([b24ced1](https://github.com/will-corrigan/Wayfarer/commit/b24ced1114c55d94258b6988625790e6670e5fd4))


### Bug Fixes

* rework the controller experience ([cd2e4dc](https://github.com/will-corrigan/Wayfarer/commit/cd2e4dce9e66fffe5e9e03f32e143ca2d9eced1f))

## [0.3.0](https://github.com/will-corrigan/Wayfarer/compare/v0.2.1...v0.3.0) (2026-08-22)


### Features

* adaptive input mode with controller glyphs and scaling ([#7](https://github.com/will-corrigan/Wayfarer/issues/7)) ([7f29153](https://github.com/will-corrigan/Wayfarer/commit/7f29153d810ee2be52f2cac398ca84c516f909ef))
* duty finder link and resizable widget ([#6](https://github.com/will-corrigan/Wayfarer/issues/6)) ([b13db5b](https://github.com/will-corrigan/Wayfarer/commit/b13db5bac9eafb1164baae4bf3ded16fe912341b))
* glanceable unlock lines on the widget ([8bcafd1](https://github.com/will-corrigan/Wayfarer/commit/8bcafd191b26855e23821fef7fe951114cc7fed4))
* hunting log data and progress logic ([ee170d2](https://github.com/will-corrigan/Wayfarer/commit/ee170d2027020add49e8dbae51bc8f9249a1fcc6))
* hunting log module with live tracking ([059678c](https://github.com/will-corrigan/Wayfarer/commit/059678cf5df9928039c869f459c31b1ba516a979))
* native context menu actions ([#9](https://github.com/will-corrigan/Wayfarer/issues/9)) ([9094eac](https://github.com/will-corrigan/Wayfarer/commit/9094eac071f4e9809424600b39b8a31453d3f698))
* native controller checklist window ([c9ee781](https://github.com/will-corrigan/Wayfarer/commit/c9ee781c3e9407464e5e1bc69ae380d4a9d30254))
* vendor native ui toolkit with prototype window ([125eb9b](https://github.com/will-corrigan/Wayfarer/commit/125eb9ba29fe9cad96d53e40f8ab0fe8593368a3))


### Bug Fixes

* catch zero-group hub aetherytes and park the context-menu everywhere design ([07dace1](https://github.com/will-corrigan/Wayfarer/commit/07dace115e963950d54eba66d8104665662a1617))
* correct hunting log live-tracking id space and page indexing ([2ea72e0](https://github.com/will-corrigan/Wayfarer/commit/2ea72e00b27962ea26f08d77fb027f7be0e5c89a))
* duty finder guidance for objectives inside instanced content ([#5](https://github.com/will-corrigan/Wayfarer/issues/5)) ([9583df4](https://github.com/will-corrigan/Wayfarer/commit/9583df451355ef44c3eba1ee1ee1ada3f768e387))
* follow accepted quests from the unlock list and explain statuses ([#3](https://github.com/will-corrigan/Wayfarer/issues/3)) ([ddc922d](https://github.com/will-corrigan/Wayfarer/commit/ddc922d3532e7775ec507f1cd99935ee138a194b))
* guide correctly to interior objectives with entrance markers in the current zone ([#10](https://github.com/will-corrigan/Wayfarer/issues/10)) ([29745a7](https://github.com/will-corrigan/Wayfarer/commit/29745a7482da045d81e48a9fcd65f342609cee61))
* harden marker precedence so multi-floor objectives still route via entrances ([#11](https://github.com/will-corrigan/Wayfarer/issues/11)) ([c292d9c](https://github.com/will-corrigan/Wayfarer/commit/c292d9c590d50783ac02c75ae0bf503833744451))
* resolve shard positions in cities without direct map refs ([83b49b8](https://github.com/will-corrigan/Wayfarer/commit/83b49b805a2c66d6272e65bc910e0934070fe2f7))
* review findings from A1 (glyph naming, resize fight, tie-break) ([#8](https://github.com/will-corrigan/Wayfarer/issues/8)) ([e2cf1a8](https://github.com/will-corrigan/Wayfarer/commit/e2cf1a807678c43f54dc0381168ec8687ad2870e))
* suppress teleports within the current city network ([f257774](https://github.com/will-corrigan/Wayfarer/commit/f257774fdbeceff0b21d6938ff4993d8686b2315))
* test the OtherZone fallback decision and open the context menu everywhere ([7fa8215](https://github.com/will-corrigan/Wayfarer/commit/7fa821504791cbbf800508108fe4f95a86127298))
* test the OtherZone fallback decision and open the context menu everywhere ([f8aaa39](https://github.com/will-corrigan/Wayfarer/commit/f8aaa395126b3b08716967ea8ed857601a94b8ad))

## [0.2.1](https://github.com/will-corrigan/Wayfarer/compare/v0.2.0...v0.2.1) (2026-08-21)


### Bug Fixes

* keep testing channel in sync and tidy review findings ([#9](https://github.com/will-corrigan/Wayfarer/issues/9)) ([c2d8a0e](https://github.com/will-corrigan/Wayfarer/commit/c2d8a0e13e440c5d357b59d66b1a6e04a6413471))

## [0.2.0](https://github.com/will-corrigan/Wayfarer/compare/v0.1.1...v0.2.0) (2026-08-21)


### Features

* route progress display and cancellation ([10e0c3e](https://github.com/will-corrigan/Wayfarer/commit/10e0c3eaacfd7a6032a658e8a0f7e8e6e6c3a8c6))
* show quest giver names in unlock list and pickup guidance ([3efb270](https://github.com/will-corrigan/Wayfarer/commit/3efb2701946f837f97ff6e5f0ef15a77501e8e52))


### Bug Fixes

* correct camera rotation sign and add arrival state ([#7](https://github.com/will-corrigan/Wayfarer/issues/7)) ([806819d](https://github.com/will-corrigan/Wayfarer/commit/806819d821f9eedd3dba113966f7360da023d092))
* enforce full quest acceptance gates in unlock checklist ([afe2afa](https://github.com/will-corrigan/Wayfarer/commit/afe2afac4bb3486ff79f222c448fa5a6503b7842))
* ignore sentinel second job category unless its level gate is real ([55128f5](https://github.com/will-corrigan/Wayfarer/commit/55128f555717d62471b3f7c968258e073222892f))
* route intra-city travel via aethernet groups, entrances and honest costs ([0da1ede](https://github.com/will-corrigan/Wayfarer/commit/0da1ede10ff95f7a7ad56d58dfc6e48270f107e7))

## [0.1.1](https://github.com/will-corrigan/Wayfarer/compare/v0.1.0...v0.1.1) (2026-08-21)


### Bug Fixes

* document automated release flow in readme ([d3f77c8](https://github.com/will-corrigan/Wayfarer/commit/d3f77c8fbe2a15f169dbe87e7ffd4cfb5cdb99cb))
* plugin master must be a json array; keep array shape in release patcher ([ade4d84](https://github.com/will-corrigan/Wayfarer/commit/ade4d847fb2771da10cc5c8ef8771b88f6eab4d7))
* repo version must match four-part built manifest version ([e4be8b3](https://github.com/will-corrigan/Wayfarer/commit/e4be8b307defd314a21ca6dba513e8966b43cb02))
