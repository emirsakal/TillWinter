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

## Controls

| Input | Effect |
|---|---|
| Hold / drag (mouse or finger) | The ring follows the pointer, offset **0.8 plots toward the top of the screen** so your finger doesn't hide it. Every plot under the ring advances its own phase: **Dry -> Wet** (watering), **Wet -> Ripe** (growing), **Ripe -> coins** (harvest, takes the crop's harvest time). The starting ring (radius 0.7) covers one plot. |
| Tap a plot with a crow (< 0.2 s, < 20 px) | Scares the crow and drops 2x the crop's value. A tap is also a one-frame ring, so it no longer harvests by itself. |
| `DBG` button (top-right) | Debug panel: time scale 0.5x-8x, ring offset, ring radius override, +1000 coins, skip to Winter, spawn crow, Ripe all, per-node Almanac level editor (- / +), resolved stats. |
| Winter Almanac | Scrolling list of every node grouped by branch (LOCKED / BUY / MAX, "not implemented yet" for table-only effects), then **Next Year**. Pointer input is ignored while it is open. |

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

Runs the `TillWinter.Tests` EditMode suite (61 NUnit tests against `TillWinter.Core` only) in
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

All four scripts need the project to be closed in the editor.

## Project layout

```
Assets/TillWinter/Core/    TillWinter.Core   pure C#, no UnityEngine (asmdef: noEngineReferences)
Assets/TillWinter/Unity/   TillWinter.Unity  presentation + input, reads FarmState, forwards input
Assets/TillWinter/Tests/   TillWinter.Tests  EditMode NUnit tests, references Core only
Assets/TillWinter/Editor/  Game view presets, play-mode smoke test (Assembly-CSharp-Editor)
Assets/TillWinter/Scenes/  Farm.unity (one Bootstrap object)
Assets/Audio/Kenney/       CC0 clips from kenney.nl + licenses (loaded via Resources/Kenney)
```

All tunables live in `FarmConfig` (Core); Almanac nodes and their per-level values live in
`AlmanacData` (Core) and resolve to numbers through `StatResolver`. `FarmConfigAsset` is an
optional ScriptableObject wrapper you can assign on the Bootstrap object to tweak numbers in the
Inspector. Design source of truth: `docs/GDD.md`.

## What is intentionally missing

Not yet built (see the GDD roadmap): Heritage/rebirth, save/offline, rain cloud, golden crop,
tractor/greenhouse/combo behaviour (table entries only), the Almanac tree canvas, real art
(everything is Unity primitives + flat URP materials), localization (EN placeholder strings only),
sound design beyond placeholders (Kenney CC0 clips, generated blips as fallback), performance
work, store/build settings. No monetization, ever. Balance is a first guess and deliberately untuned.

See `DECISIONS.md` for choices the design brief left open, and `CLAUDE.md` for the
architecture rules future sessions must keep.
