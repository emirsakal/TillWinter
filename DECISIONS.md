# Decisions not covered by the kickoff brief

> **Demo section (September 2026).** Entries below that talk about a single `Growth` value, `UpgradeId`,
> instant ring harvest or a single apprentice were superseded by Session 1 (see the section at the end).

Each entry: what was open, what was chosen, why. Balance-affecting ones are exposed in `FarmConfig`.

## Setup

- **Input System was already installed and active** (`com.unity.inputsystem 1.20.0`,
  `activeInputHandler: 1`) in the Hub-created project, so no editor restart was needed.
- **Editor scripts have no asmdef.** `Assets/TillWinter/Editor/` compiles into
  Assembly-CSharp-Editor; the brief asked for exactly three asmdefs.
- **Template cruft removed**: `Readme.asset`, `TutorialInfo/`, `SampleScene`, `SampleSceneProfile`.
  The URP `DefaultVolumeProfile` is untouched (it is the pipeline's default component set).
- **URP trims applied to both quality tiers** (PC and Mobile RP assets): HDR off, 1024 shadow
  map, one cascade, soft shadows off, SSAO renderer feature disabled. Post-processing stays on;
  the runtime volume uses only Color Adjustments (season tint) and Vignette (frost/winter).
- **URP Lit + Unlit added to Always Included Shaders** because materials are created with
  `Shader.Find` at runtime; without this a device build would strip them.

## Core rules

- **Soil multiplies natural (irrigation) growth too.** Brief says Soil is "+25% growth speed" and
  natural growth is "0.15×L of ring speed"; ring speed includes soil, so natural does as well.
  Under the ring the natural rate is not added on top (ring rate replaces it).
- **A ripe plot the ring moves onto is harvested immediately**, and a zero-`dt` tick still
  harvests. This is what makes the tap "count as a momentary ring frame".
- **Harvesting a plot that has a crow scares the crow** (`CrowScared` fires). Otherwise the
  ring/apprentice would silently delete crows.
- **Winter sets every plot's growth to 0** (crops "lost"); `StartNextYear` keeps tiers and
  resets growth again. The year timer does not run in Winter; `Tick` is a no-op except for
  clearing the ring.
- **Field expansion is anchored bottom-left**: existing plots keep their (x, y); new plots fill
  the new right column and top row. A symmetric ring is impossible for 3→4. The camera
  re-centres the whole field, so visually it just grows.
- **`UpgradePlot` level = number of purchases; max = plots × 2**, so expanding un-maxes it.
  The cost curve `15 × 1.6^n` gets steep fast (18 purchases to make a 3×3 all corn); left as
  in the brief, not tuned.
- **Crow spawn timer accumulates always** (every 4 s from year 2), and the 25% roll only
  happens when at least one ripe, unprotected, crow-free plot exists. Crows never spawn under
  the ring. `DebugSpawnCrow` ripens a plot if needed and ignores year/scarecrow rules.
- **Apprentice** starts just below the field centre (plot-space `(n-1)/2, -1.2`), retargets if
  the ring steals its target, harvests any ripe plot (including one with a crow, which scares
  it). Speed: 1.5 + 0.5 × (level − 1).
- **`NumberFormat.Short` truncates** (never rounds up): `999999 → 999K`, `1250 → 1.2K`,
  `1000 → 1K`, one decimal only below 10 of a unit.
- **Extra events beyond the brief**: `FrostWarningStarted`, `YearStarted`, `Purchased`,
  `FieldExpanded` — the presentation layer needs them and they are cheap.
- **Season boundaries fire `SeasonChanged`** for Summer/Autumn/Winter/Spring; Winter also
  fires `WinterStarted`.

## Presentation

- **Camera tilt** = 40° from straight-down (X rotation 50°), orthographic, framing the field
  width with a 0.45-plot margin, field centre at 49% of screen height. Ortho size is derived
  from the current aspect every frame so 1080×1920 and 1080×2400 keep the same width fit.
- **Everything is built in code** from `GameBootstrap`. Scene YAML only holds the Bootstrap
  object; no prefabs, materials or volume profile assets. Rationale: nothing to drift, and the
  session had no editor UI access.
- **uGUI + built-in font** (`LegacyRuntime.ttf`) instead of TextMeshPro, because TMP's
  essential resources are not imported in a fresh project. Rounded-rect and circle sprites are
  generated at runtime.
- **Particles use mesh spheres + Lit material** (no textures, no transparent-shader setup).
  Coin flight is done in canvas space with pooled Images along a quadratic Bezier.
- **Displayed coin counter lags the sim** by the value still in flight, then punches on arrival.
- **HUD fades out while the Winter shop is open** so the panel stays readable.
- **Frost warning** uses the URP Vignette (cold blue) plus light/tint shift; Winter keeps a
  softer blue-white vignette and snow (mesh particles). Snow keeps falling a few seconds into
  Spring by design (particle lifetime), then stops.
- **Presses that start over UI are ignored entirely** (`EventSystem.IsPointerOverGameObject`
  sampled on press). The debug panel and shop therefore never leak into the ring.
- **Tap targets the plot under the finger (no offset)**, the ring uses the offset.

## Audio

- Downloaded from kenney.nl: **Impact Sounds** and **UI Audio** (CC0, licenses in
  `Assets/Audio/Kenney/`). **Casual Game Sounds no longer has a download on kenney.nl**, so the
  crow caw and crow-scared sounds are generated at runtime (`AudioClip.Create` noise bursts).
- Only the ~19 clips used are committed, under a `Resources/Kenney` folder so the code-built
  `AudioManager` can load them. Any missing clip falls back to a generated blip.

## Tooling

- **`smoke-test.bat`** (play-mode, real editor, virtual Input System mouse, screenshots) was
  added beyond the brief because it was the only way to verify the Unity layer runs without
  an interactive editor. It doubles as a quick regression check for the developer.
- `TestResults/` is git-ignored.

---

# Session 1 - three-phase core + data-driven Almanac

## Conflicts with the session prompt (GDD wins)

- **Prerequisites are any-of.** The prompt says a purchase is rejected "when a prerequisite is at
  level 0"; GDD 6 says a node is available when *at least one* prerequisite is >= 1. Implemented
  any-of (`FarmSim.IsAvailable`). `ring_harvest_speed` therefore opens after Ring Watering *or* Ring Growing.
- **Node count.** GDD 6 says "about 45 nodes" but lists 32; the table has exactly the 32 listed. More can
  be added as data.

## Core

- **Tree edges** (GDD lists nodes per branch, not edges). Chosen chains:
  Hand: ring_radius -> {ring_water_speed, ring_grow_speed} -> ring_harvest_speed -> ring_bonus_coins -> ring_combo.
  Soil: irrigation -> sun -> soil_quality -> {crop_value, fertile_start}.
  Field: expand_field -> unlock_tomato -> upgrade_plot -> unlock_corn -> unlock_pumpkin -> unlock_grapes -> unlock_golden_wheat; bulk_upgrade <- unlock_corn.
  Helpers: apprentice_count -> {apprentice_speed, apprentice_harvest_time, scarecrow}; apprentice_yield <- {speed, harvest_time}; tractor <- yield; helper_water <- harvest_time.
  Calendar: year_length -> {frost_warning, greenhouse}; late_frost, crow_bounty <- frost_warning; spring_head_start <- greenhouse.
  `upgrade_plot` sits behind `unlock_tomato` because it does nothing until a second tier exists.
- **Placeholder costs**: roots 30/40/60/80/50, deeper nodes 100-2000, growth 1.6 everywhere (prompt ranges). `upgrade_plot` base 25.
- **Ring speed nodes are +20 % per level** (multiplier `1 + 0.2 x L` over the crop's base time). The
  prompt only said "multipliers, level 0 = 1.0x"; 20 % makes five levels double the speed.
- **Passive rates use the crop's base speed, not the ring multipliers.** Irrigation L waters at
  `0.15 x L / Water`; Sun at `0.15 x L x soil / Grow`. Hand-branch upgrades do not leak into passive.
  Soil multiplies growing (ring and Sun) only, never watering or harvesting.
- **Progress does not carry over** between states (a transition resets to 0). At 60 fps this loses at
  most one frame per transition.
- **Harvest-scare pays nothing.** Only a tap-scare drops `2 x value` (base value, not x crop_value).
  `CrowScared.Coins` is 0 for a harvest-scare so the UI can tell them apart.
- **`upgrade_plot` level = number of purchases; max = plots x highest unlocked tier**; `IsMaxed` is
  "no plot below the unlocked cap", so expanding or unlocking a tier re-opens it.
- **`AlmanacNode` is a sealed class, not a C# record.** Records need `IsExternalInit`, which is not
  guaranteed in Unity's .NET profile; the shape is otherwise identical to the prompt.
- **`DebugSetLevel`** sets a level directly (tests use it instead of buying) and applies the
  field-size side effect; it does not retroactively upgrade plots for `upgrade_plot`.
- **Apprentice idle spots** are spread along the bottom edge at y = -1.2; they walk home when nothing
  is Ripe. Reservation: an apprentice never targets a plot another one is targeting; if the ring or
  another helper harvests the target first it retargets next tick.
- **`LifetimeCoins`** is tracked now (for Heritage in S2), not shown.
- **Not-implemented effects** (`ring_combo, helper_water, late_frost, fertile_start,
  spring_head_start, bulk_upgrade, tractor, greenhouse, crow_bounty`) are in the table, purchasable,
  resolve to a level/flag on `Stats`, and are flagged `[not implemented yet]` in the list.

## Presentation

- **Placeholder EN strings live in `TillWinter.Unity.Localize`** keyed by Core's `NameKey`/`DescKey`
  and crop keys. Core has no user-facing text. The TR table arrives with the localization session.
- **Almanac list is a `ScrollRect`** grouped by branch (headers); rows show LOCKED / BUY / MAX and the
  prerequisite names when locked. LOCKED takes priority over MAX in the label.
- **Dry plots show three crack slivers** (thin dark cubes) instead of a texture; they hide when Wet.
  Wet = dark soil + sprout that shrinks as the plant grows. Ring harvest squashes the plant with
  progress so the 0.5 s reads.
- **Apprentice hat colours** cycle through six presets by index.
- **Crop visuals for tiers 3-5** (pumpkin, grapes, golden wheat) are new primitive builds; no art.
- The **HUD clears in-flight coins on Winter** (review follow-up 5).

## Docs

- `docs/GDD.md` was found in `Assets/TillWinter/docs/` (Unity had generated a .meta); moved to the
  repo-root `docs/` the prompt asked for.

---

# Tooling: token hygiene (September 2026)

- **Read-deny rules live in the committed `.claude/settings.json`**: `Library/`, `Temp/`, `Logs/`,
  `obj/`, `TestResults/`, `UserSettings/`, `Assets/Audio/`, and all
  `.meta/.unity/.asset/.prefab/.mat/.csproj/.sln/.slnx` files. Reason: generated or binary content
  that never informs a decision. Scenes/assets that must change are edited by a targeted string
  replace against a known line, never read whole.
- **A `PreToolUse` hook (`.claude/hooks/rewrite_test_scripts.py`, matcher `Bash|PowerShell`)
  rewrites any call to `run-tests.bat` / `smoke-test.bat`** into `run-tests-summary.bat` /
  `smoke-test-summary.bat` via `updatedInput`. Verified live: the hook rewrote the session's own
  commands the moment settings.json existed. Raw scripts stay for the developer.
- **Summaries are produced by small Python scripts** (`.claude/hooks/summarize_tests.py`,
  `summarize_smoke.py`) because parsing NUnit XML in a .bat is unreadable and Python 3 is on the
  dev machine. Test summary capped at 100 lines; smoke summary prints only FAIL/CONSOLE lines and
  never opens screenshots.
- **Three Sonnet subagents (`test-runner`, `docs-writer`, `repo-scout`) take the verbose work**;
  the main session keeps its context for design and code edits. Five skills (`session-start`,
  `run-tests`, `open-pr`, `add-almanac-node`, `save-versioning`) hold the workflow text that used
  to live in `CLAUDE.md`, which is now 39 lines.
- **Subagents defined in `.claude/agents/` only register when a session starts**, so the session
  that created them used general-purpose agents with the same instructions inlined; from the next
  session on the named agents are available.
- **`save-versioning` is written as a contract ahead of Session 2 (save/offline)** so the rule
  "every new state field goes into SaveData + round-trip test" exists before the code does.
# Session 2 - Heritage (rebirth), save/load, offline

- **Generic `SkillTree`** (table + levels + currency + purchase rules via owner-supplied hooks:
  currency getter, spend, phase gate, dynamic-max override, cost multiplier). Almanac = coins,
  Winter only, reset on retire. Heritage = seeds, Winter + Heritage phase, never reset. Ids are
  unique across both trees (`SkillTree.ValidateAll`). `AlmanacNode` was renamed `SkillNode`.
- **Heritage table has the 16 nodes GDD 7 lists** (GDD says about 25); edges: Hand
  h_start_radius -> h_ring_speeds -> h_ring_coins; Soil h_start_irrigation -> h_start_sun ->
  h_global_growth -> h_unlock_rain_cloud; Field h_start_field -> h_start_tomato -> h_golden_crop;
  Helpers h_free_apprentice -> h_apprentice_yield -> h_scarecrow_immunity; Calendar
  h_start_year_length -> {h_greenhouse_x2, h_almanac_discount}. Seed costs 3-15, growth 1.5
  (placeholders).
- **Heritage effects are base modifiers applied before Almanac effects** in `StatResolver`: ring
  speeds x(1+0.1L), ring coins x(1+0.05L), global growth multiplies the soil multiplier,
  apprentice yield x(1+0.05L), start radius/year length add to the base, start field 4x4 sets
  `Stats.StartGridSize` (Almanac expansions add on top), free apprentice adds one to the count.
  "Start with Irrigation 1 / Sun 1" is a floor on the Almanac level (max, not additive). Almanac
  discount is a cost multiplier `1 - 0.05L` (floor 5%), applied only to Almanac nodes.
- **Not-implemented Heritage effects** (`h_unlock_rain_cloud`, `h_golden_crop`,
  `h_scarecrow_immunity`, `h_greenhouse_x2`) are purchasable and resolve to flags/values on
  `Stats` (RainCloudUnlocked, GoldenCropChance, ScarecrowImmunity, GreenhouseX2Level) for Session 3.
- **`Phase { Year, Winter, Heritage }` is explicit on `FarmState`**; `IsWinter` now means "phase
  != Year". In the Heritage phase nothing ticks; `Season` stays Winter.
- **`Retire()` rebuilds the field from scratch** at `Stats.StartGridSize` (no plot survives),
  clears Almanac, crows, helpers (helpers come back from stats, so a Heritage free apprentice is
  present immediately), resets year/coins/lifetime-this-generation/years-this-generation; keeps
  generation counter (+1), seeds, Heritage levels, lifetime total, crows scared, harvests.
  `StartNewGeneration()` re-resolves stats and rebuilds the field if a Heritage node bought in
  the Heritage phase changed the start size.
- **`GenerationStats` replaces the old `LifetimeCoins`**; `Harvests` and `CrowsScared` are
  counted in Core.
- **Save: `SaveData` is arrays of `{Id, Level}` pairs and `PlotSave` rows** (Unity `JsonUtility`,
  no Newtonsoft). Crow timers are stored on the plot row. Apprentices store positions only and
  retarget on load. RNG is a xorshift32 (`Rng`) whose state is saved, replacing `System.Random`,
  so a loaded game is deterministic and platform-independent. Unknown or malformed data (wrong
  plot count, out-of-bounds plot) returns null from `FromSave`. `SaveMigrations.Migrate` is the
  version hook (no migrations yet).
- **`SavedAtUnixSeconds` is stamped by the Unity `SaveController`**, not by Core (Core has no
  clock). Save file `Application.persistentDataPath/tillwinter.json`, atomic write via `.tmp` +
  rotate to `.bak`, corrupt files renamed `.corrupt-<timestamp>`. Triggers: Winter start, every
  purchase, retire, new generation, next year, pause/focus loss/quit, autosave every 30 s in the
  Year phase.
- **Offline: `SimulateOffline` runs `UpdatePlots` + `UpdateApprentices`** with the ring forced
  off at a 1 s step, capped at 8 h and a guarded loop count; no crows, no year timer, no seasons;
  no-op outside Phase.Year or for elapsed <= 0. Clock going backwards is treated as 0 elapsed by
  the Unity layer. The away card shows only when coins were earned.
- **Presentation: the winter panel now has Almanac/Heritage tabs**; the Almanac tab is disabled
  in the Heritage phase. "Pass on the farm" shows the seed preview or the coins still needed, and
  a confirm dialog listing what is kept and lost. Generation N shows N-1 cone trees around the
  field (placeholder). HUD shows "Year N - Gen G" and a "seeds if you retire" hint once CanRetire.
- **Smoke test: input injected through the Input System did not reach the game** in this editor
  session (Game view focus), so `GameController.DebugPointerScreen`/`DebugTapScreen` were added
  as a debug-only fallback the smoke test switches to after 1 s without a ring. The smoke test
  deletes the save file at start so runs are deterministic, and now exercises retire, two
  Heritage purchases, a new generation, and a save round-trip through `SaveController`.
- **Session ran on a branch from `main`** while the token-hygiene PR was still open, so the
  `.claude/` tooling was kept locally (git-excluded) and is not part of this branch.

---

# Session 3 - every node does something

- **Rain cloud spawn time is chosen at Spring (`ScheduleCloud`)** as a random point in the middle
  third of the year; the RNG is consumed only when the node is owned so seeds stay stable for
  existing tests. Winter, Retire and Spring reset it; position, time left, spawned flag and spawn
  time are saved.
- **`TapCloud` waters Dry plots to Wet/0** and adds +0.25 to Wet progress (clamped into Ripe/0,
  firing PlotRipened). Tapping does not affect Ripe plots.
- **Golden crops: the chance is rolled at every replant (`Replant`)** after any harvest source;
  the roll consumes RNG only when the chance is > 0. `DebugNextHarvestGolden` forces the next
  replant golden (tests and debug panel). Golden pays `value x 10` before CropValue/ring/apprentice
  multipliers; the flag is cleared by `Plot.Reset` (harvest, crow eat, winter, retire).
- **Tractor: parked between sweeps (`X = -0.5`)**; a sweep starts only if some row has a Ripe
  plot, otherwise it waits a full interval again. Plots are processed as the front passes their
  index (`Passed` counter is saved), so plots ripening mid-sweep ahead of the front are taken. A
  crow on a swept plot is scared with the tap bounty. Tractor harvests use no ring bonus and no
  apprentice yield. The tractor also runs in `SimulateOffline`.
- **Greenhouse accrues only while `Phase.Winter` ticks** (`Tick` now runs `UpdateGreenhouse` in
  Winter and returns). Rate = `level x 0.02 x sum(crop value of every plot)`, x2 with the Heritage
  node; cap 60 s per winter, counters reset at each Winter start and saved. Nothing accrues in the
  Heritage phase.
- **Ring combo: the multiplier applies to the harvest that raises the combo** (first hit = combo
  1), `1 + level x 0.01 x min(combo, 10)`; the timer runs during the year only and the combo
  resets at Winter/Spring.
- **late_frost harvests happen at the start of `EnterWinter`** before plots reset, at half value,
  source `LateFrost`; ring bonus, yield and combo do not apply. Threshold `>= 0.8` Wet progress or
  Ripe.
- **fertile_start applies to plots created by `expand_field`** (purchase or debug level); the
  starting field and retire rebuilds stay Dry. spring_head_start sets every plot Wet/0 in
  `BeginSpring` (Next Year and new generation).
- **bulk_upgrade: `ApplyPurchase` raises the lowest plot**, then the next lowest if one exists;
  the purchase still counts as one level.
- **crow_bounty adds to the base multiplier**: drop = `(2 + level) x crop value`, paid by tap and
  by the tractor.
- **scarecrow_immunity is resolved in `StatResolver`**: Almanac `scarecrow` 2 plus the node sets
  `CrowSpawnChance` to 0; `TrySpawnCrow` returns early on 0 so no RNG is consumed.
- **The `NotImplemented` mechanism (`AlmanacData.Implemented`, `HeritageData.Implemented`,
  `SkillNode.IsImplemented`) is deleted.** `EventsTests.EveryNodeInBothTables_IsAppliedByStatResolverOrAFeatureSwitch`
  keeps an explicit id -> kind map ("stat" changes a `Stats` field at level 1, "purchase" =
  `ApplyPurchase`, "stat+scarecrow2" for the immunity node); table and map must match exactly.
- **Save schema v2**: fields for cloud, tractor, combo, greenhouse and `PlotSave.Golden`.
  `SaveMigrations.V1ToV2` sets defaults (tractor waits a full interval if owned; greenhouse cap
  only if the save was in Winter). Every schema bump ships a hand-written JSON fixture of the
  previous version; because Core has no JSON library, the Tests assembly carries a ~100-line
  `MiniJson` (parser + writer + reflection mapper) used only by tests.
- **`AutoPlayer` (Core.Balance)**: ring target = highest urgency (Ripe > Wet progress > Dry,
  nearest wins ties) re-evaluated every 0.3 s, ring moves at 6 plots/s; taps crows and the cloud
  after the reaction delay; Winter = greedy `weight / cost` over an exposed weight table, waits
  for the greenhouse cap, retires at >= 10 seeds, then greedy Heritage. One row per year; `ToCsv` /
  `ToTable`. `balance-sim.bat [seed] [generations]` runs it in batchmode via
  `TillWinter.EditorTools.BalanceSim` and writes `TestResults/balance.csv` + `balance.txt`; the
  debug panel's "Balance table" button plays one generation from a copy of the current state and
  logs the table.
- **Presentation**: cloud is a three-sphere blob above the top row with a screen-space hit test
  (`GameController.CloudHitTest`) that takes priority over plot taps; tractor is a red box with
  four wheels parked left of the field; greenhouse is a translucent box right of the field with a
  coin trickle in Winter; golden plots pulse a gold emissive tint; HUD shows "combo xN" from 2.

---

# Session 4 - the Almanac tree screen

- **TextMeshPro everywhere**: `UiKit.Label` returns `TMP_Text`; the built-in `LegacyRuntime.ttf`
  is gone. TMP Essential Resources could not be imported in batchmode (`AssetDatabase.ImportPackage`
  is asynchronous and the editor exits first), so the `.unitypackage` (a tar.gz) was extracted into
  `Assets/TextMesh Pro/` with a Python script; the files are committed like any other asset.
- **Font: Nunito** (variable `Nunito[wght].ttf`, OFL) from google/fonts in `Assets/Fonts/` with
  `OFL.txt`. `ui-setup.bat` builds `Assets/Fonts/Resources/NunitoSDF.asset` (SDFAA, 1024 atlas,
  sampling 72, Latin + `ÇçĞğİıÖöŞşÜü` + a few symbols, static atlas). Nunito lacks `→` and `▶`;
  the TMP default font (LiberationSans SDF) is registered as fallback and the "next" arrows use `»`.
- **Strings: Core keeps `NameKey`/`DescKey`**; `TillWinter.Unity.Strings` loads
  `Assets/TillWinter/Unity/Localization/en.json` (flat JSON, own 60-line parser, no Newtonsoft)
  from a `TextAsset` assigned to `GameBootstrap.StringTable`. The scene reference was set by
  `UiSetup.WireBootstrap` (editor code opens Farm.unity and saves it) rather than by hand-editing
  YAML. Descriptions are templates with `{cur}`, `{next}`, `{level}`, `{max}`, `{cost}` filled by
  Core `NodeText.Values` (which resolves stats at the current and next level) and `NodeText.Fill`
  (unknown placeholders are left visible, never throw). `StringsTests` reads the JSON with the
  test-only `MiniJson` and checks every node key and every template at level 0 and max. TR only
  needs a `tr.json` with the same keys.
- **Layout**: `SkillTreeLayout.Compute` puts the five branch roots on a bottom arc
  (`0.25 x (lane-2)^2`), lanes 2.8 units apart, layer = prerequisite depth inside the branch
  (cross-branch prerequisites ignored for placement), siblings 0.9 apart around the lane centre,
  layer height 1.3; guaranteed minimum distance 0.85. `SkillNode.LayoutOverride` nudges a node.
  The Heritage table lays out with the same code.
- **`SkillTreeView` is generic** (tree + layout + `TreeTheme`): pan by drag with inertia, wheel
  zoom in the editor, pinch via `Touchscreen.current`, zoom 0.5-1.6x, soft-clamped pan (pull-back
  per frame), node prefab-less objects driven by a `NodeState` machine (Locked / Unaffordable /
  Affordable / Maxed), pips for max level <= 8 else a level text, edges in one `UILines` mesh,
  "flow" segments when a node becomes available, purchase punch and one-by-one pip fill. Tap
  selection also works on locked nodes. First open of a Winter centres on the roots at 0.85x;
  later opens restore the session's last pan/zoom.
- **`WinterScreen` replaces the list panel**: overlay + paper page (0.93 alpha so the frozen field
  shows through), top bar (title, coins with drain ticks and punch, greenhouse line, retire hint),
  the canvas, a bottom sheet that stays open after a purchase, Next Year / Pass on the farm (the
  S2 confirm dialog stays). The Heritage tree is reachable through a small toggle in Winter and is
  shown in the Heritage phase; S5 will build the dedicated screen on the same view.
- **Safe area is applied only on mobile platforms**: in the editor Game view `Screen.safeArea`
  returned window-sized values that shifted the whole page.
- **`TreeTheme` ScriptableObject** in `Resources/TreeTheme` (created by `ui-setup.bat`) holds
  every colour and metric; the defaults in code are only used when the asset is missing. Changing
  a default in code requires deleting the asset and re-running `ui-setup.bat`.
- **The list-style `WinterShopView` and the old `Localize` table are deleted**; the debug panel
  keeps a node dropdown with -/+.
- **`TillWinter.Tests.Unity` is a second EditMode test assembly** (references Unity + TMP) for
  presentation checks (theme colours per branch, node state enum, Turkish glyphs in the font
  asset); Core tests stay engine-free.
- **Smoke test now pans, zooms, selects `ring_radius`, buys it, asserts the child is available**,
  and screenshots the Winter screen at 1080x2340 and 1080x1920 (`docs/screenshots/`).

---

# Session 5 - Heritage screen, HUD, onboarding

- **Onboarding hints are Core flags, not UI state.** `Hint` enum + `OnboardingFlags` bitfield live
  on `FarmState`/saved; `FarmSim.MarkHint` returns true only the first time a hint fires. The UI
  never keeps its own "have I shown this" bool or touches `PlayerPrefs` — a hint that should only
  play once must round-trip through Core so it survives quit/relaunch and offline sim.
- **Themes are the only colour source, for both trees.** Heritage reuses `SkillTreeView` with a
  second `TreeTheme` asset (`HeritageTheme`: deep green paper, gold accents, seed currency, darker
  branch colours) rather than a fork of the view; `InitialZoom` moved onto `TreeTheme` per-tree
  (0.62 for Heritage so all five branches fit) instead of a shared constant. `HudTheme` does the
  same job for the HUD. No view class holds a literal `Color` for anything a designer would want
  to retune.
- **`GenerationCard` is a separate full-screen step, not a Heritage-screen overlay.** Retire ->
  confirm -> `Retire()` -> `GenerationCard` (flavour line, seeds counting up, skip after 0.5 s,
  auto-finish at 6 s) -> Heritage screen. Keeping it a distinct step means the seed-count beat and
  the flavour text don't have to coexist with the tree canvas's own animation (plots appearing,
  decor pop-in) on the same frame.
- **Away-card coins are held back from the HUD, not animated in before the card is read.**
  `HudView.HeldCoins` withholds offline earnings from the visible counter until the player taps OK
  on `AwayCard`; per-source lines (apprentices, tractor) come from the same offline-sim breakdown
  `SimulateOffline` already produced, so the card needed no new Core surface. Per-event FX are
  skipped while `FarmSim.IsSimulatingOffline` so the field doesn't visibly celebrate hours of
  harvests in one frame.
- **Decor is data, like Almanac/Heritage nodes.** `FarmDecorSet` (Resources) with `DecorItem
  {Id, Kind, MinGeneration, Offset, Rotation, Scale, Prefab}` replaces the placeholder cone trees;
  `FarmDecorView` builds primitives when no prefab is set and rebuilds on generation change. Adding
  a new decoration is a table row, matching the "trees are data" rule for skill nodes.
- **Smoke-test screenshots must not share a frame with a state change.** `ScreenCapture
  .CaptureScreenshot` captures at end of frame; the smoke test now separates "change state" and
  "take shot" into distinct steps so a screenshot never lands mid-transition (e.g. generation card
  fading, plots still popping in).
- Save schema bumped to v3 for `OnboardingBits` and per-tree pan/zoom (`TreeViewMemory`); v2->v3
  migration and `SaveV3Tests` fixture follow the usual save-versioning pattern.

---

# Session 6 - art pass

- **Kenney CC0 kits over authored primitives, per-asset.** The GDD (§11) left the choice open
  between low-poly Kenney kits and hand-authored primitives; each shipped asset used whichever was
  cheaper to get right: crops, trees, bush, rock, fences, flowers, log stack, mushroom and stump
  came from **Nature Kit 2.1**; tomato, grapes and the barrel from **Food Kit 2.0**; the six
  apprentices from **Mini Characters**; node icons from **Game Icons**. Everything the kits don't
  cover — house (small/medium/large), well, windmill, greenhouse, tractor, crow, cloud, plot, path
  tile, signpost, flowerbed, trellis — is still built from primitives, now inside prefabs by
  `Assets/TillWinter/Editor/ArtSetup.cs` (`art-setup.bat`, idempotent) rather than at runtime.
  Licenses live next to each kit (`Assets/Art/Kenney/<kit>/License.txt`), indexed in
  `Assets/Art/LICENSES.md`.
- **Kits were stripped hard on import**, keeping only what a prefab references: Nature Kit 329 FBX
  -> 30, Food Kit 200 -> 3, Mini Characters 26 -> 6, Game Icons 105 -> 37. Unused meshes/materials
  add nothing but repo weight and asmdef/AssetDatabase churn.
- **Mini Characters play their kit idle clip through an `AnimatorController`** instead of standing
  in the kit's default T-pose; this is the only animation added this session.
- **Hand-written HLSL shader instead of Shader Graph.** `Assets/Art/Shaders/TW_Toon.shader` (flat
  two-step ramp, vertex-colour x tint, optional emission, snow lerp, GPU instancing) plus
  `TW_Sky.shader` were written directly because Shader Graph's `.shadergraph` JSON could not be
  authored reliably from code in this session — hand-editing that format is fragile in a way a
  `.shader` text file is not. Every material under `Assets/Art` is asserted (ArtTests) to use one
  of these two shaders, or a UI shader.
- **One material per `PaletteSlot`, not per object.** `Assets/Art/Materials/TW_<Slot>.mat` lets
  every prefab sharing a slot's mesh GPU-instance; `Palette` (Resources) pushes its colours into
  those materials at boot, and per-object variation (plot Dry/Wet/ring soil, golden emission, ring
  lift) goes through a `PaletteBinder` + one `MaterialPropertyBlock` per renderer instead of a
  second material — per-material-index property blocks were tried first and rejected because they
  break GPU instancing. Season snow amount and leaf/grass tint are shader globals (`_TW_Snow`,
  `_TW_SeasonTint`) rather than per-material overrides, so Winter tints everything through the
  shader with no mesh or material swaps.
- **Ring stays a textured decal-style disc, not a URP decal projector.** Decal projectors did not
  render onto the field with this project's orthographic camera setup, so the ring keeps the
  existing soft-edge + inner-glow texture (`Assets/Art/Textures/RingDecal.png`). The decal code
  path is kept behind `ArtSetup.UseDecals = false` for a future session with more time to debug
  the projector, rather than deleted.
- **Quality tiers gate shadows/post, not mesh detail.** `QualityTiers` Low (no shadows, no post) /
  Default (soft shadows + colour volume) auto-selects Low on mobile devices under 3 GB RAM or
  1 GB VRAM; a debug-panel toggle overrides it for testing. Draw-call budgets (measured, not
  guessed): 3x3 generation 1 is 45 batches / 67 draw calls / 4.7k triangles; the stress case (6x6
  generation 3, seven apprentices, tractor) is 106 batches / 142 draw calls / 27.8k triangles,
  against a smoke-test budget of <=150 batches / <=60k triangles.

---

# Session 7 - feel and audio

- **Clip sources per `SfxId` group**: water splash/soil ripple and the crow sounds from Impact
  Sounds; UI-adjacent cues (node buy, counter punch, combo) from Interface Sounds; harvest pop,
  coin variants, winter chime, retire swell, new-generation clip and frost tick from RPG Audio and
  UI Audio. 38 clips total, licences kept next to each pack (`License-<Pack>.txt`) and indexed in
  `Assets/Audio/LICENSES.md`. Every generated-fallback path from earlier sessions was removed —
  `AudioManager` now hard-fails a missing mapping in tests (`FeelTests`) rather than silently
  falling back to a runtime blip.
- **Casual Game Sounds dropped, no ambience.** The pack's kenney.nl page could not be resolved for
  download this session (same issue noted for it back in the pre-Session-1 audio decisions), so
  the crow caw/scared clips came from the other three packs instead. None of the four packs used
  contain a wind/birds loop; the GDD's "optional and last" season ambience loop was therefore left
  unbuilt rather than synthesised (the brief explicitly ruled out generating new audio this
  session).
- **RateLimiter merge semantics**: a token-bucket per id (`TillWinter.Core.Feel.RateLimiter`,
  pure C#, tested standalone) allows N triggers/s; a request that arrives over budget is not
  dropped or queued — it merges into the next allowed trigger, which fires once at higher
  intensity (+0.25 per merged request, capped) instead of one instance per input event. `Poll`
  flushes the merged backlog the instant budget frees up, so a sustained burst (e.g. 6 apprentices
  harvesting the same frame) reads as fewer, punchier hits rather than audio/VFX stacking or
  silently missing triggers. Defaults: harvest 12/s, coin 20/s, water splash 10/s, haptics (Light)
  8/s.
- **One particle material slot, not per-VfxId.** Every `VfxCatalog` prefab uses the shared
  `TW_White` vertex-colour slot material so GPU instancing and ArtTests' "one shader per material"
  rule from Session 6 still hold; per-id and per-tier colour (e.g. golden vs. normal harvest burst)
  is set on the particle system's start colour / a `MaterialPropertyBlock` at play time from
  `Palette`, not by adding materials.
- **Mixer built via the editor's internal `AudioMixerController` API through reflection.** URP/Unity
  has no public runtime or editor API to create `AudioMixerGroup`s and expose parameters from code;
  `FeelSetup` reflects into `UnityEditor.Audio.AudioMixerController` (same approach as the Unity
  manual's undocumented workaround) rather than committing a hand-authored `.mixer` binary, keeping
  the mixer buildable/idempotent like every other `*-setup.bat`.
- **Camera shake reserved for golden harvest and retire only** — every other feel moment (regular
  harvest, combo, crow, frost) stays shake-free so the two "big" beats keep reading as bigger.
- **Budgets measured, not guessed**, in a dedicated smoke burst phase (6x6 field, 6 apprentices,
  tractor sweeping, ring over centre, every plot forced Ripe every ~20 frames for 5 s): peak 13
  active particle systems (budget 20), 8 concurrent voices (budget 8, `AudioManager`'s voice-steal
  path is exercised by this phase), 0 bytes allocated across 400 `VfxPlayer.Play` /
  `AudioManager.Play` calls, render stats unchanged from Session 6's measured budget. Screenshots
  `docs/screenshots/s7-feel-burst.png` and `s7-frost-warning.png`.

# Session 8 - mobile builds

- **Player settings chosen and why.** Product "Till Winter", company "EFS Games", bundle id
  `com.efsgames.tillwinter` on both platforms, version 0.9.0 (pre-1.0, still a demo). All of it is
  applied only by `BuildPipeline.cs`, idempotent and covered by `BuildTests`, so ProjectSettings
  never drifts from what the script would produce. Android `bundleVersionCode` / iOS
  `buildNumber` move only inside `BumpAndroidVersionCode` / `BumpIosBuildNumber`, called once per
  build — no other code path may touch them, so a re-run of the pipeline without a build can't
  silently burn a version.
- **Min Android API 25, not the brief's 24.** Unity 6000.3 refuses 24 outright ("Minimum supported
  Android API level is 25"), so 25 is the floor the engine allows. It still exercises the pre-26
  fixed-length haptics fallback (26+ gets amplitude-controlled haptics), so the two haptics paths
  the GDD/brief cared about are both reachable on real devices.
- **Keystore/signing handling.** Nothing signing-related is ever written to ProjectSettings or the
  repo. `BuildPipeline` reads `TW_KEYSTORE_PATH` / `TW_KEYSTORE_PASS` / `TW_KEY_ALIAS` /
  `TW_KEY_PASS` from the environment for the duration of one build only and clears the keystore
  fields immediately after; with the variables set it produces a signed `.aab` + `.apk`, without
  them a debug-signed `.apk` for sideloading, printing where to create a keystore inside Unity
  (Keystore Manager) so it lives outside the repo. iOS has no equivalent secret to handle: the
  pipeline only emits the Xcode project, the developer signs/archives/uploads in Xcode on a Mac.
- **TW_DEBUG gating.** `TW_DEBUG` never appears in the project's persistent scripting define
  symbols; a development build passes it in per invocation via `extraScriptingDefines` behind the
  `-dev` flag on the build scripts. `DebugPanel` and the allocation probes are compiled only under
  `#if TW_DEBUG || UNITY_EDITOR`, and `release-compile-check.bat` fails the build if those types
  are still present in `TillWinter.Unity.dll` after a release compile of either platform — this
  session's run confirmed both platforms compile clean with the types absent.
- **Quality thresholds** (`QualityTiers`, auto-selects once per device, never on desktop/Editor):
  Low when RAM < 3072 MB, dedicated GPU memory < 1024 MB (skipped on iOS, where GPU memory is
  shared and the value is meaningless), fewer than 6 cores, or shader level < 35; an unknown (0)
  reading from any of those never counts against the device, so a device the OS won't report on
  isn't punished. Low drops shadows and post-processing entirely; Default adds soft shadows and the
  colour volume. The choice is remembered in `SettingsData.QualityTier` (-1 = auto) in
  `settings.json`, editable from the debug panel toggle.
- **OfflineMinSeconds = 60**, new in `FarmConfig`/Core, tested (`OfflineMinTests`). Below that
  threshold `FarmSim.SimulateOffline` returns an empty report at both boot and resume, so a short
  call or an app switch resumes exactly where the player left rather than showing a trivial "away"
  card for a few coins.
- **Allocation-measurement correction for Session 7.** Session 7's claimed "0 bytes allocated by
  400 Play calls" relied on `GC.GetAllocatedBytesForCurrentThread`, which Unity's Mono backend
  always returns 0 from — it was not measuring anything. Session 8 replaces it with a probe that
  calibrates at runtime against `Profiler.GetMonoUsedSizeLong` (4 KB granularity) and additionally
  captures the Editor profiler's own per-script-marker GC.Alloc totals during the smoke burst
  (6x6 field, six apprentices, tractor sweeping, ring over the centre, everything forced Ripe),
  with an optional managed-callstack mode (`SmokeTest.RecordAllocCallstacks`) for tracking down a
  hit. Measured with the new method: 2002 `VfxPlayer.Play` / `AudioManager.Play` calls allocate
  0 B; the gameplay-script window during the burst is 0 B per frame over 145 frames. The fixes
  the profiler actually drove: `TractorView` no longer enumerates `Transform` children and reads
  `Object.name` every frame (was 41.8 KB over 146 frames; wheels are cached once now); HUD coin
  text no longer rebuilds a string per coin (TMP `SetText` with a char buffer, plus a new
  allocation-free `NumberFormat.Short(double, char[])`, tested identical to the existing
  `Short(double)`); the coin-flight pool is pre-warmed and capped at 96 in flight; `foreach` over
  `IReadOnlyList` was replaced by index loops in the hot paths the profiler flagged.
- **TMP Editor-only allocation, left alone.** The ~1-2 KB left across the whole burst traces to
  TMP's `#if UNITY_EDITOR` inspector-string sync inside `SetCharArray`/`SetText`
  (`TMP_Text.cs`) — code that is compiled out of players, so it is not a device number and was not
  chased further. The Editor's frame-wide "GC Allocated In Frame" (~20 KB/frame) includes the
  Editor's own overhead for the same reason and is likewise not a device number.
- **Icon approach.** Rendered from a dedicated scene (`Assets/Art/Icon/IconRenderer.unity`,
  `IconRenderer.cs`) rather than drawn by hand: the game's own ripe-pumpkin crop prefab on a soil
  block with a grass rim, spring-sky gradient with a frosted top edge and a few flakes, no text —
  reusing existing art keeps the icon visually consistent with the game with zero new asset
  sourcing. Rendered at 2048 and downsampled; the crop is matted from a black and a white render
  so the Android adaptive foreground layer is genuinely transparent, not just alpha-from-shader.
  Splash keeps Unity's own splash (Personal licence requirement) with the icon subject as the logo
  above it on `Palette.LeafDark`.
- **What is left to the device checklist** (`docs/DEVICE-CHECKLIST.md`, new): everything that
  cannot be measured in the Editor — cold start time, the 20-minute thermal/battery read, whether
  the default 0.80 ring offset still feels right on a real thumb, frost-warning legibility in
  sunlight, safe-area behaviour, the Android API 25 vs 26+ haptics fallback actually felt on two
  real devices, the iOS home-indicator double-swipe during a sweep, and the iOS haptics plugin
  (`TillWinterHaptics.mm`) compiling under Xcode, since it cannot be compiled on Windows at all.

# Session 9 — release candidate

- **Localization.** `tr.json` mirrors `en.json` key-for-key. Rule: never attach a Turkish suffix
  to a placeholder; numbers stand alone ("Halka yarıçapı: {cur} » {next}"). Language comes from
  `settings.json` `"Language"` (`""` follows `Application.systemLanguage`: Turkish → `tr`, else
  `en`). Changing language saves and reloads the scene so every label rebuilds — no live
  relabelling. `NumberFormat.Style` (Core) switches decimal separator (`1,2K`) and percent sign
  (`%10`) for Turkish; K/M/B suffixes are kept as-is. Nunito has no "→" glyph — every description
  showed a tofu box, which the new glyph test caught — so descriptions use "»" instead, and
  `ui-setup.bat` now tops up the existing NunitoSDF atlas in place (keeps its GUID) instead of
  skipping it when present. HUD literals ("Year/Gen", "Retire:", season names) moved into the
  string tables. `NodeText`'s leftover English words (unlocked/locked/owned/on/off) only feed
  placeholders that no shipped description uses, so they were left as-is (noted, not shown to
  players).
- **Pause & settings.** The pause button takes the old `DBG` corner; the debug panel now opens
  from Settings → Developer and is `TW_DEBUG`/editor-only (`DebugPanel` compiles out of release).
  Pause = no sim tick + `Time.timeScale` 0 (views freeze); play time is counted on unscaled time
  only while not paused. Settings screen: language, SFX, ambience, vibration, reduce motion (gates
  `CameraRig.Shake` and `HudView.Flash` only), quality Auto/Low/Default (Auto stays `-1` and is
  re-decided from hardware every launch instead of being written once; an explicit choice is
  remembered, including in the editor), reset save via a 3 s hold (detaches `SaveController` first
  so nothing writes the old farm back, deletes the save + `.bak` + `.tmp`, reloads; `settings.json`
  survives), credits (Kenney kits: Nature Kit, Food Kit, Mini Characters, Game Icons; audio: Impact
  Sounds, Interface Sounds, RPG Audio, UI Audio; Nunito SIL OFL 1.1; Made by EFS Games; Built with
  Unity), version line sourced from `BuildInfo` (shows "editor" in-editor). A statistics sheet is
  reachable from pause and shown after the ending; its colours were added to `HudTheme`.
- **Ending (GDD §8).** Heritage fully maxed → the next `StartNewGeneration` is the Golden Year: a
  6×6 golden-wheat field, all golden, no crows, no frost, a 300 s year
  (`FarmConfig.GoldenYearSeconds`), golden `SeasonPalette` look, `GoldMotes` VFX. The golden field
  is planted after `BeginSpring` (which resets plots). At its Winter: `GoldenYearEnded` fires
  before `WinterStarted`, the field returns to the normal starting size, and `EndingSeen` is set
  (once). Credits roll (skippable after 3 s, 24 s total), then the statistics sheet, then the
  normal Winter screen underneath. Heritage title shows "Complete". No music-free ambience loop
  was added for the ending: no CC0 loop exists in the imported packs and generated audio is
  forbidden (same call as Session 7), so the existing ambience simply continues.
- **Save v4.** Adds `EndingSeen`, `GoldenYearActive`, per-source harvests (ring/apprentice/tractor/
  late frost), golden harvests, best combo, time played, years total. v3→v4 migration: the v3
  total `Harvests` cannot be split by source, so the per-source counters start at 0; `YearsTotal`
  starts at the current generation's years (earlier generations were never counted, since nothing
  tracked them). Covered by a v3 fixture test.
- **Balance pass** (also feeds GDD §15). A local .NET harness compiled Core for fast iteration;
  official numbers still come from `balance-sim.bat`. Findings: (a) the greedy bot never saved for
  crop unlocks and valued several nodes at the default 1 — every node now has an explicit weight,
  and crop unlocks are weighted by their value jump; (b) the bot's retire rule is now "retire when
  this generation's seeds reach 1.5× all seeds earned before (at least 8), or cover the rest of the
  tree" — the usual prestige instinct, replacing a flat threshold; (c) with `upgrade_plot` at
  growth 1.6 the field never got past tomato (a plateau, 100-year generations); (d) the ring
  out-harvested every helper by construction (ring share 70–85% in generation 4 even after every
  non-rule lever and several rule-level ones) → rule change GDD §2.1 *(v1.4)*: the ring waters/
  grows every plot under it in parallel but harvests one Ripe plot at a time (furthest along, ties
  nearest centre); (e) `upgrade_plot` is deliberately the open-ended sink for leftover coins, so
  the "no node > 35% of a generation's coins" target is measured on finite nodes only (coins spent
  on the node / coins earned that generation) — `upgrade_plot`'s share is reported separately
  (86–96%). Growth is now tracked per branch (`AlmanacData.Growth` / `HeritageData.Growth`) instead
  of one flat curve.
- **Rule tests pin pre-balance numbers** via `TestConfig.Classic()` (carrot 1, threshold 5000,
  divisor 50), so tuning changes never touch rule tests; balance targets live only in
  `BalanceTests`.
- **Privacy.** `com.unity.modules.unityanalytics` removed from `Packages/manifest.json`;
  `docs/PRIVACY.md` added (no accounts, analytics, ads or network; all data stays on device).
- **Version 1.0.0**, set through `BuildPipeline.Version`.

## Follow-up: playtest polish and title screen (2026-09-16)

- **SoilRipple removed from watering.** Grown to 2.4x a 0.5 quad, larger than a tile, it read as a
  dark square spilling over the plot; the soil's own Dry->Wet colour blend already shows watering.
  The `VfxId` stays in the catalogue, unused.
- **Ring square tint dropped.** The ring is shown only by its round decal; plots under it no
  longer get a square tint/lift/emission, since that made a round ring look square. Which plots
  the ring affects was always a circle test on plot centres (`FarmState.IsUnderRing`) — no rule
  changed.
- **Diorama back strip + centred fence.** The island gets a 1.7-unit back strip
  (`DioramaView.BackDepth`); the house and back trees stand on it behind the fence instead of on
  the fence line. The fence is centred: panel count `floor(2*edge - 0.2)`, equal gaps left and
  right, the middle panel (two on an even count) is the gate.
- **Frost heartbeat is now a colour breath, not a scale pulse.** The sin^8 4-9 Hz scale pulse read
  as jitter; the season bar now breathes colour toward frost blue instead.
- **Skill tree screens go full screen, opaque, and the Almanac page is dark.** No 20 px inset, no
  rounded corners, opaque overlay so no sky shows. Almanac paper moved to dark (0.14/0.12/0.10,
  light ink) — the cream page tired the eyes. `TreeTheme.StyleVersion` lets `ui-setup.bat` restyle
  existing theme assets in place.
- **Carrot "green final stage" left open.** A new editor preview (`PreviewRender`, menu "Till
  Winter/Preview crop stages", batch `-executeMethod TillWinter.EditorTools.PreviewRender.CropsBatch
  -out <png> [-autumn]`) renders every tier's three stages. The carrot is sprout -> green leaves ->
  orange carrot in both spring and autumn; the reported "green final stage" could not be reproduced
  from the art, so it stays open pending an in-game screenshot.
- **Title screen is its own scene** (`Menu.unity`, built in code by `MenuBootstrap`; created and
  put first in the build order by `ui-setup.bat`; `BuildPipeline` builds Menu then Farm). The
  first overlay version drew the menu over the live farm and HUD, which read cluttered. The scene
  tells the game in one loop: a small floating 3x3 plot where mixed crops sprout, grow and ripen
  in a diagonal wave while the seasons turn every 5 s (petals in Spring, leaves in Autumn, snow
  and an empty field in Winter). Buttons: Play (Continue when a save exists), New game (confirm;
  deletes the save files, keeps settings), Settings and Credits (the pause sheets, which now work
  without a running farm), Quit (hidden on iOS). Statistics stay in the pause menu. Play fades to
  the boot colour and loads Farm.unity, whose boot fade continues it seamlessly. The pause menu's
  Main menu button saves and loads Menu.unity. SceneTests checks the build order.
- **Crow views pooled (6 pre-warmed).** A landing used to create a GameObject mid-play, which the
  smoke test's per-frame allocation check caught once the one-plot ring harvest left ripe plots
  waiting longer. The smoke test now prints the per-marker allocation breakdown inside the FAIL
  line (the smoke test opens Farm.unity directly).

## Follow-up: visual pass 1, foundations (2026-09-16)

- **Type scale shipped as `UiType`** (Display 140 / Hero 84 / Big 64 / Title 56 / Heading 44 /
  Body 34 / Label 28 / Caption 24) with a global `UiType.Scale` multiplier applied inside
  `UiKit.Label`; 71 literal sizes across the HUD, Winter screen, pause sheets, title scene, away
  and generation cards, ending and onboarding now use it.
- **Sheet shell shipped as `SheetTransition`** (scrim fades, page rises 54 px and scales 0.96 to
  1 over 0.2 s, unscaled time), attached to the four pause sheets, the new-game confirm, the away
  card and the Winter screen's confirm and first-retire sheets.
- **Onboarding hand is now a beating dot with an expanding ripple** (the Kenney icon set has no
  hand), and the arrow is the real arrowUp icon from the node atlas, turned over to point down.
- **A mild bloom (threshold 0.9, intensity 0.55, scatter 0.6, warm tint) was added to the season
  Volume**; it is off on the Low quality tier, which disables post entirely.
- **Skill tree branches now have a tinted rounded region behind their nodes** (branch colour at
  10% alpha, padded by 0.85 node sizes) and their localized name above them.
- `docs/VISUAL-BACKLOG.md` holds the full visual/UX list, worked through group by group; section
  1 is done.

## Follow-up: visual pass 2, world (2026-09-16)

- **Outline/rim.** A rim light was added to TW_Toon (rim colour, power 3, strength 0.25, scaled by
  how lit the surface is); no outline pass was needed.
- **Sky.** A SkyClouds view drifts three cloud prefabs high over the farm and the title scene,
  wrapping around, shadows off.
- **Island underside.** DioramaView.BuildBlock now tapers the bottom to 55% about the block centre
  and the block is thicker (1.6), so the island hangs instead of ending in a flat box.
- **Shadows.** A TW_Shadow shader (hand-written, unlit, alpha-blended) plus a BlobShadow prefab;
  apprentices, the house, the trees and the bush get a soft contact disc. The art rule in
  CLAUDE.md now lists three project shaders.
- **Water.** A Pond prefab (water disc with a stone rim) sits at the front-left of the island.
- **Season changes.** The default tree has an autumn twin (tree_default_fall); DioramaView spawns
  both and swaps them on SeasonChanged. Snow on trees already came from the shader's snow global.
- **Life.** A CrittersView flies two butterflies (body plus flapping wings) over the field in
  Spring and Summer; they shrink away in Autumn and Winter.
- **Crop mid-stages.** Tomato now grows through plant_bush and pumpkin through plant_bushLarge
  instead of both reusing the same leaf model.
- **Camera.** A slow breath (0.6% over ~3 s), a 3% push-in that follows the combo, and a 5%
  pull-back in Winter, all damped.
- **Harvest.** The burst scales with the streak (up to +35% size) and its colour warms toward
  gold.
- **New rule.** TW_Shadow.shader joins TW_Toon and TW_Sky as the project's own shaders (the
  contact-shadow disc needs alpha blending); ArtTests' allow-list and the CLAUDE.md art rule were
  updated.

## Follow-up: visual pass 3, UI infrastructure (2026-09-16)

- **Icon system.** `UiIcons` maps generation, year, coin, harvest, ring, apprentice, tractor, crow,
  golden, combo and time to keys in the Kenney icon atlas the skill tree already uses; the
  statistics sheet now shows them per row.
- **Shadow and gradient primitives.** `UiKit.Shadow` draws the same rounded shape behind a card,
  offset and darkened; `UiKit.Gradient` plus `Prims.VerticalFadeSprite` give vertical shades (the
  title scene's own copy was deleted in favour of it).
- **Button feedback.** `ButtonFeedback` is added by `UiKit.Button` to every button — a 0.96
  squeeze, the click sound and a Selection haptic; the hand-written `Play(SfxId.UiClick)` calls in
  the Winter screen and the away card were removed so a press clicks once.
- **Toggle.** `UiSwitch` is a real switch (rounded track, sliding knob, animated at
  `UiMotion.Fast`); Vibration and Reduce motion in Settings use it instead of a button whose label
  said On or Off.
- **Scroll view.** `UiKit.ScrollView` builds a masked vertical scroller; the statistics and
  credits sheets are inside one, so longer content no longer overflows.
- **Volume sliders.** Each row now shows the level as a percentage and plays a short sample sound
  while dragging (rate-limited to one every 0.12 s).
- **Motion constants.** `UiMotion` defines Fast 0.12 s, Normal 0.22 s and Slow 0.4 s plus
  EaseOut/EaseInOut and a damping helper; sheets, switches and button presses all read from it.

## Follow-up: visual pass 4, screen by screen (2026-09-16)

- **HUD season bar.** Partly done: the frost span is hatched (Prims.HatchSprite) and the progress
  marker has a round knob; season glyphs move to section 5 since the Kenney icon set has none.
- **HUD coin counter.** A "+N/s" earning rate under the counter, recomputed once a second and
  hidden outside the year.
- **Retire chip.** Arrives with a scale punch, a solid background and a Light haptic.
- **Winter screen.** A warm gradient strip and a year summary line ("This year: N harvests · M
  coins").
- **Skill tree nodes.** Level is a ring filling clockwise (pips gone), locked nodes carry the atlas
  padlock, a bought node sends a wave outward.
- **Node card.** Effect reads "now » next" with a step bar; hidden at max level or when the value
  does not change.
- **Generation card.** A stamped seal with the generation's number behind the title, easing in
  with a slight rotation.
- **Away card.** Earned coins fly into the counter from the card; source lines arrive one after
  another.
- **Ending credits.** Three groups fade in, hold and fade out, closing on the game's name inside a
  seal (linear scroll gone).
- **Stats screen.** Row icons and dividers shipped with section 3; coins and best combo are now
  larger and in the accent colour.
- **Title scene.** A soft glow behind the name, a slowly swaying island, and a "Generation N ·
  Year M" line above Continue read straight from the save file.
- Save schema v5 adds this year's coins and harvests for the Winter screen's summary; SaveV5Tests
  carries a v4 fixture and the counters reset every Spring.
