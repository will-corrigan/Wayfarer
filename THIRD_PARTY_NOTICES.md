# Third-party notices

Wayfarer is licensed under the AGPL-3.0 (see `LICENSE`). It also vendors or otherwise
incorporates the following third-party works, retained here per their license terms.

## KamiToolKit

Referenced from the `Wayfarer` project as the package its author publishes on NuGet — used to build native game-styled addon windows (controller-navigable,
matching the game's own UI chrome) as an alternative to the plugin's ImGui windows.

- Source: <https://github.com/MidoriKami/KamiToolKit>
- Copyright (c) 2024 MidoriKami
- License: MIT

```
MIT License

Copyright (c) 2024 MidoriKami

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## FFXIV Teamcraft (monster data)

`data/monster-positions.json` is built in part from Teamcraft's monster data
(`libs/data/src/lib/json/monsters.json`): the spots players have reported monsters standing, used
to guide hunts to where their targets live. Only the monsters a mark bill or hunting log asks for
are kept, converted to world positions.

- Source: <https://github.com/ffxiv-teamcraft/ffxiv-teamcraft>
- Copyright (c) 2017 Flavien Normand
- License: MIT

## Hunty (hunting log data)

`data/monster-positions.json` is also built from Hunty's hunting log data (`Hunty/monsters.json`):
the spots each hunting log monster is found at.

- Source: <https://github.com/Infiziert90/Hunty>
- Copyright (c) 2024 Infi
- License: MIT

Both are distributed under the MIT License:

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
