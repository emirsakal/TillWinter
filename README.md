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
| Hold / drag (mouse or finger) | The ring follows the pointer, offset **0.8 plots toward the top of the screen** so your finger doesn't hide it. Plots under the ring grow; ripe ones are harvested instantly. |
| Tap a plot with a crow (< 0.2 s, < 20 px) | Scares the crow. A tap is also a one-frame ring, so tapping a ripe plot harvests it. |
| `DBG` button (top-right) | Debug panel: time scale 0.5×–8×, ring offset, ring radius override, +1000 coins, skip to Winter, spawn crow, live sim readout. |
| Winter shop | Buy upgrades, then **Next Year ▶**. Pointer input is ignored while the shop is open. |

Season colours, light angle, frost vignette and snow are the only "season" presentation.
The last 10 s of Autumn is the frost warning: cold vignette, blue light shift, heartbeat on
the timer bar and a tick each second.

## How to test

```bat
run-tests.bat
```

Runs the `TillWinter.Tests` EditMode suite (44 NUnit tests against `TillWinter.Core` only) in
Unity batchmode and writes `TestResults\EditMode.xml` + `EditMode.log`. Exit code 0 = pass,
2 = failures, 3 = Unity could not run (compile error, or the project is already open in an
editor). Override the editor path with `set UNITY_PATH=...`.

```bat
smoke-test.bat
```

Play-mode smoke test. Opens the real editor (not batchmode), enters Play, injects a virtual
Input System mouse that holds the ring over the field, fast-forwards to frost and Winter,
buys Apprentice/Irrigation/Expand Field, presses Next Year, runs year 2 with the apprentice,
spawns a crow, taps it, and exits. Screenshots and `report.txt` land in `TestResults\smoke\`.
Exit code 0 = all checks passed and no console errors. Handy after any presentation change.

Both scripts need the project to be closed in the editor.

## Project layout

```
Assets/TillWinter/Core/    TillWinter.Core   pure C#, no UnityEngine (asmdef: noEngineReferences)
Assets/TillWinter/Unity/   TillWinter.Unity  presentation + input, reads FarmState, forwards input
Assets/TillWinter/Tests/   TillWinter.Tests  EditMode NUnit tests, references Core only
Assets/TillWinter/Editor/  Game view presets, play-mode smoke test (Assembly-CSharp-Editor)
Assets/TillWinter/Scenes/  Farm.unity (one Bootstrap object)
Assets/Audio/Kenney/       CC0 clips from kenney.nl + licenses (loaded via Resources/Kenney)
```

All tunables live in `FarmConfig` (Core). `FarmConfigAsset` is an optional ScriptableObject
wrapper you can assign on the Bootstrap object to tweak numbers in the Inspector.

## What is intentionally missing

Out of scope for this demo: rebirth/prestige, saving, offline income, localization, real art
or models (everything is Unity primitives + flat URP materials), additional crops or helpers,
sound design beyond placeholders (Kenney CC0 clips, generated blips as fallback), performance
work, store/build settings, monetization. Balance is a first guess and deliberately untuned.

See `DECISIONS.md` for choices the design brief left open, and `CLAUDE.md` for the
architecture rules future sessions must keep.
