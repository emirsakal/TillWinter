# Till Winter — playable demo

A single-session demo of a mobile incremental farming game, built to answer one question:
**is the 90-second core loop fun?** Drag a ring over the field to grow and auto-harvest crops,
race the frost, spend coins in the Winter shop, start the next year.

Unity **6000.3.22f1**, Universal RP, Input System, portrait 1080×2340.

## How to run

1. Open the project in Unity 6000.3.22f1 (`open-project.bat`, or Unity Hub).
2. Open `Assets/TillWinter/Scenes/Farm.unity` (it is the only scene in Build Settings).
3. Set the Game view to `1080x2340 (Portrait)` — the editor script adds this preset (plus
   `1080x1920` and `1080x2400`) on load and selects it the first time.
4. Press Play. You boot straight into Spring of year 1 with 0 coins. No title screen.

Everything in the scene is built in code from one `Bootstrap` object, so there are no prefabs
or materials to keep in sync.

### Save file

The game saves to `%USERPROFILE%\AppData\LocalLow\DefaultCompany\TillWinter\tillwinter.json`
(Unity's `persistentDataPath`) with a `.bak` next to it; it saves at Winter, every purchase,
retire, new generation, next year, on pause/focus loss/quit and every 30 s during a year.
Loading resumes mid-year and shows a "While you were away" card when passive systems earned
coins (max 8 h). To reset: press `DBG` -> "Delete save" and restart Play, or delete the file.

## Controls

| Input | Effect |
|---|---|
| Hold / drag (mouse or finger) | The ring follows the pointer, offset **0.8 plots toward the top of the screen** so your finger doesn't hide it. Every plot under the ring advances its own phase: **Dry -> Wet** (watering), **Wet -> Ripe** (growing), **Ripe -> coins** (harvest, takes the crop's harvest time). The starting ring (radius 0.7) covers one plot. |
| Tap a plot with a crow (< 0.2 s, < 20 px) | Scares the crow and drops 2x the crop's value. A tap is also a one-frame ring, so it no longer harvests by itself. |
| `DBG` button (top-right) | Debug panel: time scale 0.5x-8x, ring offset, ring radius override, +1000 coins, skip to Winter, spawn crow, Ripe all, per-node Almanac level editor (- / +), resolved stats, +50 seeds, Force CanRetire, Offline 1 h, Delete save, Spawn cloud, Next golden, Tractor sweep, +30 s greenhouse, Balance table. |
| Winter Almanac | Pannable/zoomable skill tree (drag, wheel or pinch). Tap a node to open its card; Buy keeps the card open. Heritage toggle in the top bar, Next Year and Pass on the farm below. |
| Winter panel: Pass on the farm | Unlocks at 5 000 lifetime coins this generation; shows the seed preview; confirm dialog lists what you keep (Heritage tree, seeds, stats) and lose (coins, Almanac, field, helpers). Heritage tab: spend seeds on permanent nodes; Start new generation begins year 1 with the bonuses. |

Plot states read at a glance: Dry = light cracked soil, Wet = dark soil + sprout, growing = the
plant scales with progress, Ripe = wobble + warm glow. Passive systems (Irrigation, Sun, apprentices)
are Almanac nodes; at level 0 nothing happens without the ring.

Season colours, light angle, frost vignette and snow are the only "season" presentation.
The last 10 s of Autumn is the frost warning: cold vignette, blue light shift, heartbeat on
the timer bar and a tick each second.

## How to test

```bat
run-tests.bat
```

Runs the `TillWinter.Tests` EditMode suite (115 NUnit tests against `TillWinter.Core` only) in
Unity batchmode and writes `TestResults\EditMode.xml` + `EditMode.log`. Exit code 0 = pass,
2 = failures, 3 = Unity could not run (compile error, or the project is already open in an
editor). Override the editor path with `set UNITY_PATH=...`.

```bat
smoke-test.bat
```

Play-mode smoke test. Opens the real editor (not batchmode), enters Play, injects a virtual
Input System mouse that holds the ring over the field, fast-forwards to frost and Winter,
buys apprentices/irrigation/expand field/ring radius, presses Next Year, runs year 2 with two
apprentices, spawns a crow, taps it, and exits. Screenshots and `report.txt` land in `TestResults\smoke\`.
Exit code 0 = all checks passed and no console errors. Handy after any presentation change.

```bat
run-tests-summary.bat
smoke-test-summary.bat
```

Token-cheap wrappers for Claude Code sessions: they run the raw script, then print only compile
errors, pass/fail counts and the first lines of each failure (tests), or the failed checks and the
console-error count (smoke). A Claude Code hook (`.claude/settings.json`) rewrites any call to the raw
scripts into these, so a session never floods its context with a Unity log. Use the raw scripts
yourself when you want the full log in `TestResults\`.

```bat
balance-sim.bat [seed] [generations]
```

Headless balance run: a scripted player (`AutoPlayer`) plays N generations (default 3, seed 7) and
writes `TestResults\balance.csv` and `balance.txt`, then prints the year table (coins per year,
seeds, nodes bought, field, ring, top crop, first ripe time, harvest share by source). Paste the
table into the design chat when tuning.

All four scripts need the project to be closed in the editor.

One-off setup: `ui-setup.bat` imports TMP essentials, builds the font asset, creates `TreeTheme`,
`HeritageTheme`, `HudTheme` and `FarmDecor` (all Resources ScriptableObjects) and wires `en.json`
into the scene (idempotent).

`art-setup.bat` builds everything under "Asset credits" below into prefabs and materials:
primitive meshes (house, well, windmill, tractor, etc.), per-`PaletteSlot` materials from
`Palette`, node-icon sprite atlas, and the `VisualCatalog` Resources asset every view spawns
from. Idempotent; re-run after cloning or after changing a prefab default in code.

## Project layout

```
Assets/TillWinter/Core/    TillWinter.Core   pure C#, no UnityEngine (asmdef: noEngineReferences)
Assets/TillWinter/Unity/   TillWinter.Unity  presentation + input, reads FarmState, forwards input
Assets/TillWinter/Tests/   TillWinter.Tests  EditMode NUnit tests, references Core only
Assets/TillWinter/Editor/  Game view presets, play-mode smoke test (Assembly-CSharp-Editor)
Assets/TillWinter/Scenes/  Farm.unity (one Bootstrap object)
Assets/Audio/Kenney/       CC0 clips from kenney.nl + licenses (loaded via Resources/Kenney)
Assets/Fonts/              Nunito (OFL, `OFL.txt`) + the generated `NunitoSDF` TMP font asset
Assets/TillWinter/Unity/Localization/en.json   the EN string table
```

Fonts and licences: UI text uses Nunito (SIL Open Font License 1.1, see `Assets/Fonts/OFL.txt`)
rendered with TextMeshPro; the TMP essential resources (LiberationSans SDF, also OFL) are in
`Assets/TextMesh Pro/`. Re-run `ui-setup.bat` after cloning if the font asset, `TreeTheme`,
`HeritageTheme`, `HudTheme` or `FarmDecor` is missing (and after changing any of those assets'
code defaults — the ScriptableObject in `Resources/` must be deleted and regenerated).

Screenshots of the current build (Winter, Heritage, HUD, generation card, away card) live in
`docs/screenshots/` (`s5-*.png`), taken by `smoke-test.bat`. Session 6's art pass adds `s6-*.png`
(spring 3x3, spring 6x6 generation 3, golden crop, tractor sweep, summer, autumn frost, winter
tree, heritage icons, winter at 1080x1920, ring harvest).

All tunables live in `FarmConfig` (Core); Almanac nodes and their per-level values live in
`AlmanacData` (Core) and resolve to numbers through `StatResolver`. `FarmConfigAsset` is an
optional ScriptableObject wrapper you can assign on the Bootstrap object to tweak numbers in the
Inspector. Design source of truth: `docs/GDD.md`.

## Asset credits

Low-poly art is Kenney CC0 kits (public domain, no attribution required — credited here anyway),
under `Assets/Art/Kenney/` with each kit's original `License.txt` kept alongside it
(index: `Assets/Art/LICENSES.md`):

- [Nature Kit](https://kenney.nl/assets/nature-kit) — crops, trees, bush, rock, fences, flowers, log stack, mushroom, stump
- [Food Kit](https://kenney.nl/assets/food-kit) — tomato, grapes, barrel
- [Mini Characters](https://kenney.nl/assets/mini-characters-1) — the six apprentices
- [Game Icons](https://kenney.nl/assets/game-icons) — Almanac / Heritage node icons

Everything the kits don't cover (house, well, windmill, greenhouse, tractor, crow, cloud, plot,
path tile, signpost, flowerbed, trellis) is built from primitives by `ArtSetup`/`art-setup.bat`.
UI font is Nunito (SIL OFL 1.1, see `Assets/Fonts/OFL.txt`), covered under Fonts and licences above.

## What is intentionally missing

Not yet built (see the GDD roadmap): localization (EN placeholder strings only), sound design
beyond placeholders (Kenney CC0 clips, generated blips as fallback), performance work beyond the
Session 6 draw-call budget, store/build settings. No monetization, ever. Balance is a first guess
and deliberately untuned.

See `DECISIONS.md` for choices the design brief left open, and `CLAUDE.md` for the
architecture rules future sessions must keep.
