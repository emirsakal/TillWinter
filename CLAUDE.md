# Till Winter — rules for working in this repo

Mobile incremental farming demo. Unity 6000.3.22f1, URP, Input System only (legacy Input
Manager is disabled), portrait 1080×2340. Namespace and assembly prefix: `TillWinter`.
Working title in UI: "Till Winter".

## Architecture (non-negotiable)

| Assembly | Folder | Rule |
|---|---|---|
| `TillWinter.Core` | `Assets/TillWinter/Core/` | **Pure C#. Zero `UnityEngine` references** (asmdef has `noEngineReferences: true`; keep it). All rules, state, economy, timers. Deterministic for a fixed `dt` and seed. |
| `TillWinter.Unity` | `Assets/TillWinter/Unity/` | Thin presentation + input. Reads `FarmState`, plays VFX/SFX, forwards input. **No game rules here.** If a number affects gameplay it belongs in `FarmConfig`. |
| `TillWinter.Tests` | `Assets/TillWinter/Tests/` | EditMode NUnit tests against Core only. |

- Core façade is `FarmSim` (`Tick(dt, ring)`, `TapAt`, `TryBuy(nodeId)`, `StartNextYear`, events).
  The Unity layer never mutates state except through that API (plus the `Debug*` hooks).
- **Plots are a three-phase state machine** (`PlotState.Dry -> Wet -> Ripe`, each with its own
  0..1 `Progress`, GDD 2.2). Ring rate replaces the passive rate while under the ring; Irrigation
  waters Dry plots, Sun grows Wet plots, Soil multiplies growing only. Do not add a fourth state or
  a single "growth" number.
- **The Almanac is data.** `AlmanacData.Nodes` (Core) is the only place nodes exist; adding a node
  is a table row with an `EffectType`, not code. `StatResolver.Resolve` is the only place levels turn
  into numbers; `FarmSim` reads `State.Stats` and never recomputes a stat itself. Effects the sim does
  not apply yet stay in the table and are listed in `AlmanacData.Implemented` (false => UI shows
  "not implemented"). `AlmanacData.Validate` must stay green (unique ids, prerequisites exist, no cycles).
  The fixed `UpgradeId` enum is gone; never reintroduce a per-node switch outside `StatResolver`/`ApplyPurchase`.
- Every tunable that is not a node value lives in `FarmConfig` with defaults (crop table, caps,
  crow/apprentice bases). `FarmConfigAsset` (ScriptableObject) is an optional wrapper; Core must never depend on it.
- Core holds no user-facing strings: crops and nodes carry keys; `TillWinter.Unity.Localize` maps them.
- `[System.Serializable]` on Core classes is fine; `UnityEngine.*` is not.
- Editor-only tooling goes in `Assets/TillWinter/Editor/` (plain Assembly-CSharp-Editor).

## Presentation conventions

- The scene `Farm.unity` contains one `Bootstrap` object. `GameBootstrap` builds everything
  (camera, light, volume, field, UI, audio) in code. Prefer extending that over adding scene
  content or prefabs.
- Programmer art only: primitives + flat URP Lit/Unlit materials via `Prims`. No downloaded models.
- uGUI built with `UiKit` using the built-in `LegacyRuntime.ttf` (TextMeshPro resources are not imported).
- Input goes through `PointerInput` (Input System `Pointer.current`) so mouse and touch share one path.
- Presentation reads `FarmState` only: plot state/progress, `Apprentices` (up to six), `Stats`, `AlmanacLevels`.
- Materials/shaders are created at runtime; URP Lit and Unlit are in Always Included Shaders.

## Workflow

- `run-tests.bat` = EditMode tests in batchmode (results in `TestResults/`). Keep it green. Every rule with a number in `docs/GDD.md` has a test; change the rule => change the test and the GDD.
- `smoke-test.bat` = play-mode smoke run with screenshots. Run it after touching the Unity layer.
- Both need the project closed in the editor.
- Branch per feature, PR into `main`. Commit in small logical steps (`type: summary`). Record uncovered design choices in `DECISIONS.md` (one section per session); `docs/GDD.md` is the source of truth and is updated only when implementation forces a rule change.
- Out of scope unless asked: rebirth, saving, offline income, localization, monetization,
  real assets, new crops/helpers, performance work, store builds.
