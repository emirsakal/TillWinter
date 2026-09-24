# Till Winter — rules for every session

Mobile incremental farming game. Unity 6000.3.22f1, URP, Input System only, portrait 1080×2340.
Design source of truth: `docs/GDD.md` (read by section number, never whole). Namespace/assembly prefix `TillWinter`.

## Architecture (non-negotiable)

| Assembly | Folder | Rule |
|---|---|---|
| `TillWinter.Core` | `Assets/TillWinter/Core/` | Pure C#, **zero `UnityEngine`** (asmdef `noEngineReferences: true`). All rules, state, economy, timers. Deterministic for fixed `dt` + seed. No user-facing strings (keys only). |
| `TillWinter.Unity` | `Assets/TillWinter/Unity/` | Thin presentation + input. Reads `FarmState`, plays VFX/SFX, forwards input. **No game rules.** Gameplay numbers belong in `FarmConfig` or the data tables. |
| `TillWinter.Tests` | `Assets/TillWinter/Tests/` | EditMode NUnit tests against Core only. Every numbered rule in the GDD has a test. |

- Core façade is `FarmSim` (`Tick(dt)`, `Strike`, `SetWatering`, `Reap`, `TapAt`, `TryBuy(nodeId)`, `StartNextYear`, events). The Unity layer mutates state only through it (plus `Debug*` hooks).
- **Plots are a layered state machine**: `PlotState.Hard → Growing → Ripe → Hard`, one layer deeper each cycle, each with its own 0..1 `Progress` (GDD §2v3.2). Layer HP = `BaseHp × HpGrowth^Layer × HpMult`; striking breaks Hard, holding waters Growing, swiping reaps Ripe. `FarmConfig.MaxLayer` caps depth (bedrock). No fourth state, no single "growth" number.
- **Trees are data.** `AlmanacData.Nodes` and `HeritageData.Nodes` are the only places nodes live; adding a node is a table row with an `EffectType`. `StatResolver.Resolve` is the only place levels become numbers; `FarmSim` reads `State.Stats`. Unapplied effects stay in the table and are absent from `AlmanacData.Implemented`. `AlmanacData.Validate` must stay green. No `UpgradeId`-style enums.
- Tunables that are not node values live in `FarmConfig` with defaults. `FarmConfigAsset` is an optional ScriptableObject wrapper; Core never depends on it.
- **Save contract:** `SaveData` (Core) is the only persistent DTO. Every new persistent field on `FarmState`/`FarmSim` goes into `SaveData`, `ToSave`, `FromSave` and the round-trip test in `SaveTests` in the same commit. Bump `SaveData.CurrentSchemaVersion` and add a `SaveMigrations` step when a field changes meaning; unknown versions return null (start fresh), never throw. Arrays only, no dictionaries (JsonUtility). Every schema bump ships a hand-written JSON fixture of the previous version (parsed with the test-only `MiniJson`) that must load through `SaveMigrations` and play deterministically. See skill `save-versioning`.
- **Offline:** `FarmSim.SimulateOffline` advances passive systems only (growth, stamina refill, apprentices), never strikes, crows, year timer or seasons; capped at `FarmConfig.OfflineCapSeconds`. Nothing else may simulate time while the app is closed.
- **Phases:** `Phase { Year, Winter, Heritage }` gates ticking and purchases. `SkillTree` is generic; `AlmanacData` and `HeritageData` are the two tables; Heritage effects are base modifiers resolved before Almanac effects in `StatResolver`.
- `[System.Serializable]` on Core classes is fine; `UnityEngine.*` is not. Editor tooling lives in `Assets/TillWinter/Editor/`.
- **Every node does something.** No `NotImplemented` flag exists; a new node must be applied in `StatResolver` or a `FarmSim` switch and added to the explicit map in `EventsTests` in the same commit.
- **Onboarding hints are one-shot flags in Core.** `Hint` enum + `OnboardingFlags` on `FarmState` (saved); `FarmSim.MarkHint` returns true only the first time. The Unity layer never invents its own "shown before" bool or reads/writes `PlayerPrefs` for this — a hint that must fire once has to round-trip through Core.
- Presentation is built in code from one `Bootstrap` object in `Farm.unity` (`GameBootstrap`); visuals via `VisualCatalog`, uGUI via `UiKit`, input via `PointerInput`, strings via `Localize`. The title scene `Menu.unity` is built the same way from `MenuBootstrap` and opens first (build order Menu, Farm).

## Conventions

- Branch per feature from `main` (`feat/<topic>`, `chore/<topic>`), PR into `main`, squash merge. Never merge unless asked.
- Commits small, `type: summary` (feat/fix/chore/docs/test), ending with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
- `DECISIONS.md` gets one section per session for choices the prompt left open. `docs/GDD.md` changes only when implementation forces a rule change, marked `*(vX.Y)*` inline. When a session prompt and the GDD disagree, the GDD wins.
- Scenes, `.asset`, `.meta`, `.prefab`, `.mat` files are never read whole. If one must change, edit it with a targeted string replace against a known line (grep the line first).
- Out of scope unless a session prompt asks: monetization (never), performance work, real art, new mechanics.
- **Tuning rule:** balance targets live in `BalanceTests` (`AutoPlayer` plays to the ending over seeds 1–3); a tuning change that breaks one fails the suite. Tune per-branch cost growth before base costs; never change crop timings, the beat length or the crit window. Rule tests use `TestConfig.Classic()` so they don't move with tuning.

## UI rules

- TextMeshPro only (`UiKit.Label` -> `TMP_Text`, font `UiKit.Font` = Figtree SDF for body text, `UiKit.DisplayFont` = Rammetto One SDF for Title size and above, both from Resources). Never `UnityEngine.UI.Text` or `LegacyRuntime.ttf`.
- Every user-facing string comes from `Strings` (`en.json`, `tr.json`); Core carries keys only. Node descriptions are templates filled by `NodeText`; a new node needs its `name`/`desc` keys in both files (`StringsTests` fails otherwise). Strings in `en.json` and `tr.json` change together (key parity is tested). Never attach a suffix to a placeholder in Turkish; numbers stand alone. Use `NumberFormat` for numbers — the language sets its style.
- Colours and metrics of the tree screens live in `TreeTheme` (Resources asset, one per tree — e.g. `HeritageTheme` — including its `InitialZoom`); HUD colours/spacing live in `HudTheme` (Resources asset). No hard-coded colours in `HudView`, `SkillTreeView` or `WinterScreen`; `UiKit` palette constants are for debug/placeholder panels only.
- Skill trees render through the generic `SkillTreeView` + `SkillTreeLayout`; no hand-placed nodes.
- Scene edits go through editor code (`UiSetup.WireBootstrap` pattern), never by hand-editing YAML.

## Art rules

- All visuals are spawned through `VisualCatalog.Spawn` (Resources asset); `GameBootstrap` and views never build primitives at runtime — primitive builders live only in `Assets/TillWinter/Editor/ArtSetup.cs`.
- All colours come from `Palette`, `PaletteBinder` or `SeasonPalette`; no literal `Color` in a view. `PaletteBinder` (one `MaterialPropertyBlock` per renderer) is the only place per-object colour variation happens.
- Only `TW_Toon.shader` (+ `TW_Sky.shader`, `TW_Shadow.shader`, `TW_Particle.shader`, UI shaders) may appear on a material under `Assets/Art`; no Standard/Lit materials, no Shader Graph.
- Kenney assets are imported only through `ArtSetup`/`art-setup.bat`, with each kit's `License.txt` kept next to it and indexed in `Assets/Art/LICENSES.md`. Strip unused kit files on import.
- `art-setup.bat` regenerates prefabs, per-slot materials and `VisualCatalog`; it is idempotent — re-run after changing a prefab default in code.

## Feel and audio rules

- All effects go through the catalogues: VFX only via `VfxCatalog`/`VfxPlayer` (one pooled prefab per `VfxId`, built by `feel-setup.bat`), SFX only via `SfxTable`/`AudioManager`. Never `Instantiate` a particle prefab or create an ad hoc `AudioSource` in a view.
- Any repeatable-event trigger (harvest, coin, splash, etc.) must go through `TillWinter.Core.Feel.RateLimiter`; over-budget calls merge into the next allowed trigger instead of stacking unbounded.
- Camera shake is reserved for golden harvest and retire only — no other event may add it (and never when Reduce motion is on).
- Haptics only through `Haptics` (Light/Medium/Heavy/Selection), gated by `SettingsData.HapticsEnabled` (`settings.json`, separate from the save). No direct `Vibrator`/platform calls elsewhere.
- No generated/synthesised audio. Missing a clip for an `SfxId` is a bug to fix in `SfxTable`, never a runtime fallback.

## Build rules

- Never commit keystores, keystore/key passwords, aliases or provisioning profiles anywhere (code, docs, CI config). Android signing comes from four environment variables (`TW_KEYSTORE_PATH`, `TW_KEYSTORE_PASS`, `TW_KEY_ALIAS`, `TW_KEY_PASS`) read for the duration of one build only; iOS signing/archiving happens in Xcode on a Mac, never in the pipeline.
- Version and build numbers move only through `BuildPipeline` (`build-android.bat` / `build-ios.bat`), never by hand-editing Player Settings.
- Debug-only code sits under `#if TW_DEBUG || UNITY_EDITOR`; `TW_DEBUG` never goes into the project's persistent scripting define symbols, only into a `-dev` build's `extraScriptingDefines`.
- Run `release-compile-check.bat` before a PR that touches runtime code.
- Per-frame gameplay code must not allocate: no string building per frame (use `NumberFormat.Short(double, char[])` and TMP `SetText` char-buffer overloads), no `foreach` over `IReadOnlyList` or `Transform` in a hot path, no `Object.name` in `Update`.

## Token rules

- Run tests only through the `test-runner` subagent, which uses `run-tests-summary.bat` / `smoke-test-summary.bat`. A hook rewrites the raw scripts; never look at `TestResults/`.
- UI layout is checked by eye, not by guess: `ui-tour.bat <folder>` opens every screen and sheet in play mode through its own buttons and writes `NN-name.png` plus `report.txt` to that folder (use the scratchpad; `TestResults/` is off limits). It backs up and restores the save and settings. Close the editor first. An optional third argument `clean` hides the developer button for store screenshots. An optional second argument picks a screen preset (e.g. `"1080x1920 (Portrait)"` for 16:9, `"1536x2048 (Tablet)"`); the default is `"1080x2340 (Portrait)"`.
- Delegate: tests → `test-runner`; documentation, PR bodies, commit messages → `docs-writer`; "where is / how does" questions → `repo-scout`. The main session keeps its context for design decisions and code edits.
- Prefer grep and targeted reads (offset/limit) over whole-file reads. Read `docs/GDD.md` by section number only. Read `DECISIONS.md` only its last section.
- Never read the deny-listed paths (`Library/`, `Temp/`, `Logs/`, `obj/`, `TestResults/`, `UserSettings/`, `Assets/Audio/`, `*.meta`, `*.unity`, `*.asset`, `*.prefab`, `*.mat`, `*.csproj`, `*.sln`, `*.slnx`).
- Workflows are skills, loaded on demand: `session-start`, `run-tests`, `open-pr`, `add-almanac-node`, `save-versioning`.
- Balance is measured, not reasoned: `balance-sim.bat` (through `test-runner`) produces the year table the developer pastes into the design chat.

# Compact instructions
When compacting, keep: the current step of the session prompt, files changed so far, failing tests, and open decisions. Drop tool output and file contents.
