# Changelog

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
