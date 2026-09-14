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
