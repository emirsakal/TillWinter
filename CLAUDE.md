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

- Core façade is `FarmSim` (`Tick(dt, ring)`, `TapAt`, `TryBuy`, `StartNextYear`, events).
  The Unity layer never mutates state except through that API (plus the `Debug*` hooks).
- Every tunable lives in `FarmConfig` with defaults. `FarmConfigAsset` (ScriptableObject) is an
  optional wrapper; Core must never depend on it.
- `[System.Serializable]` on Core classes is fine; `UnityEngine.*` is not.
- Editor-only tooling goes in `Assets/TillWinter/Editor/` (plain Assembly-CSharp-Editor).

## Presentation conventions

- The scene `Farm.unity` contains one `Bootstrap` object. `GameBootstrap` builds everything
  (camera, light, volume, field, UI, audio) in code. Prefer extending that over adding scene
  content or prefabs.
- Programmer art only: primitives + flat URP Lit/Unlit materials via `Prims`. No downloaded models.
- uGUI built with `UiKit` using the built-in `LegacyRuntime.ttf` (TextMeshPro resources are not imported).
- Input goes through `PointerInput` (Input System `Pointer.current`) so mouse and touch share one path.
- Materials/shaders are created at runtime; URP Lit and Unlit are in Always Included Shaders.

## Workflow

- `run-tests.bat` = EditMode tests in batchmode (results in `TestResults/`). Keep it green.
- `smoke-test.bat` = play-mode smoke run with screenshots. Run it after touching the Unity layer.
- Both need the project closed in the editor.
- Commit in small logical steps. Record uncovered design choices in `DECISIONS.md`.
- Out of scope unless asked: rebirth, saving, offline income, localization, monetization,
  real assets, new crops/helpers, performance work, store builds.
