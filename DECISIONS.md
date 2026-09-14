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
