# Till Winter — Game Design Document

**Version 1.1 — September 2026 (1.0 + Session 1 clarifications, marked *(v1.1)* inline).** This is the source of truth for what the game is. Session prompts reference it; Claude Code updates it at the end of every session that changes a rule. Numbers marked *(tune)* are first guesses and will be adjusted from playtests, not from reasoning.

---

## 1. Pitch and pillars

A short, finite, mobile incremental farming game. You drag a ring over a field; plots under it get watered, grow and pop into coins. Winter ends the year and opens the Almanac, a skill tree bought with coins. When the farm stalls you hand it to the next generation (rebirth) and spend Heritage Seeds on permanent upgrades. Six to eight hours to the ending.

**Pillars**
1. **One finger, always busy.** Every second of a year the ring has something worth doing.
2. **Three chains, one loop.** Water → Grow → Harvest, each with an active (ring) and a passive counterpart. Passive income only works when all three passive systems are built.
3. **Frost is the drum.** The last 10 s of every year is a panic; winter is the breath.
4. **Finite and finishable.** No infinite scaling, no live-ops. It ends, and the ending is earned.

**Hard constraints**
- No monetization of any kind: no ads, no IAP, no analytics SDKs that require consent flows.
- Portrait, one-handed, single player, offline. iOS + Android (Google Play, App Store) under EFS Games.
- Languages: EN + TR. Core contains no user-facing strings.
- Pure C# core, Unity-independent, unit-tested. Unity is a presentation layer.

---

## 2. Core loop

### 2.1 The ring
- The player holds/drags anywhere on the field. A ring (radius in plot units) follows the pointer, offset **0.8 plots toward the top of the screen** (tested in the demo; keep).
- Every plot whose centre is inside the ring is processed **independently and in parallel** according to its own state (see 2.2). There is no "mode"; the ring does whatever each plot needs.
- Tapping (down+up < 0.2 s, < 20 px) targets the plot under the finger with no offset: scares a crow, and counts as a one-frame ring.
- Ring radius: starts **0.7** (covers one plot, barely touches neighbours), **+0.25** per Almanac level, max **2.5**.

### 2.2 Plot state machine
Each plot holds a crop of a given **tier** and is in one of three states. Harvest replants the same tier at Dry.

| State | Under the ring | Passive counterpart |
|---|---|---|
| **Dry → Wet** (watering) | progresses at ring water speed | *Irrigation* waters Dry plots slowly |
| **Wet → Ripe** (growing) | progresses at ring grow speed × soil | *Sun* grows Wet plots slowly (never Dry ones) |
| **Ripe → harvested** (harvest) | progresses at ring harvest speed; on completion coins pop | *Apprentices* walk to Ripe plots and harvest |

Rules:
- Each state has its own progress 0..1. Ring rate replaces (does not stack with) the passive rate while the plot is under the ring. *(v1.1)* Passive rates are fractions of the crop's *base* speed (Hand-branch ring upgrades do not speed up Irrigation/Sun); Soil multiplies growing only. Progress resets to 0 on each state change.
- A Wet plot stays Wet until harvested (no drying out in v1; a "summer drought" event is a later option).
- A Ripe plot outside the ring waits, visibly wobbling. Ripe plots are crow targets.
- Ring harvest completing on a plot with a crow scares the crow first, then harvests.

### 2.3 Crops
Times are seconds under the ring at level 0. Value is coins per harvest. *(tune)*

| Tier | Crop | Water | Grow | Harvest | Value |
|---|---|---|---|---|---|
| 0 | Carrot | 1.0 | 1.5 | 0.5 | 1 |
| 1 | Tomato | 1.5 | 3.5 | 0.5 | 4 |
| 2 | Corn | 2.0 | 6.0 | 0.7 | 12 |
| 3 | Pumpkin | 3.0 | 10 | 1.0 | 35 |
| 4 | Grapes | 4.0 | 15 | 1.0 | 100 |
| 5 | Golden Wheat | 5.0 | 22 | 1.2 | 300 |

Crop tiers are per plot. `UpgradePlot` raises the lowest-tier plot by one (row-major tiebreak). New tiers are gated by Almanac nodes (see 5.3).

### 2.4 Field
- Starts **3×3**. Expands to 4×4, 5×5, 6×6. Expansion is anchored bottom-left (existing plots keep coordinates); the camera re-centres.
- New plots start at tier 0, Dry.

---

## 3. Year, seasons, winter

- One year = one timer through Spring → Summer → Autumn (boundaries at 1/3, 2/3; cosmetic) then Winter.
- Year length starts **90 s**, +15 s per Calendar level, cap **180 s** *(tune)*.
- **Frost warning**: last **10 s** of Autumn (extendable by Almanac). Cold vignette, blue light, timer heartbeat, tick SFX.
- **Winter**: all plots reset to Dry with progress 0 (unharvested crops lost). Coins are kept. Field freezes; ring does nothing; the Almanac opens. Greenhouse (if owned) still produces a trickle.
- **Next Year**: year counter +1, Spring starts, plots keep tiers.

---

## 4. Passive systems and helpers

| System | Effect | Levels |
|---|---|---|
| Irrigation | Dry plots water at `0.15 × L` of ring water speed | 0–5 |
| Sun | Wet plots grow at `0.15 × L` of ring grow speed | 0–5 |
| Soil | multiplies *all* growing (ring and passive) by `1 + 0.25 × L` | 0–6 |
| Apprentice count | number of apprentices on the field | 0–6 |
| Apprentice speed | walk speed `1.5 + 0.5 × L` plots/s | 0–4 |
| Apprentice harvest time | `1.0 → 0.4 s` | 0–3 |
| Apprentice yield | coin multiplier on apprentice harvests: `0.5 → 0.75 → 1.0 → 1.15 → 1.3` | 0–4 |
| Tractor | every N s harvests one full row of Ripe plots (single unit, level = shorter interval) | 0–3 |
| Scarecrow | crow spawn chance `25% → 15% → 8%`; never 0% in the Almanac | 0–2 |
| Greenhouse | during Winter, earns `X coins/s` based on field value *(tune)* | 0–3 |

Apprentices: each has its own position and target; they never target the same plot; they retarget if the ring or another helper takes their plot; they idle near the field edge when nothing is Ripe. Six apprentices running around is a deliberate visual goal.

---

## 5. Events

### 5.1 Crows
- From year 2. Spawn check every 4 s; roll at the Scarecrow-modified chance only when a Ripe, unprotected, crow-free plot exists outside the ring. Max 2 at once.
- A crow eats the crop after 4 s. Tap scares it: the crop stays and the crow drops **coins = 2 × crop value** *(tune)*. Being harvested also scares it *(v1.1: no coins dropped)*.
- Eaten crop → plot to Dry, progress 0.

### 5.2 Rain cloud *(unlocked in Heritage)*
- A cloud drifts across the top of the field once per year at a random time in Summer. Tap it: rain waters every Dry plot instantly and grows all Wet plots by 25%. Ignored, it drifts away.

### 5.3 Golden crop *(unlocked in Heritage)*
- On harvest, `chance%` that the replanted crop is golden: 10× value, glows. Same timings.

---

## 6. Almanac (winter skill tree, bought with coins, reset on rebirth)

- Opens only in Winter. Full-screen panel over the frozen field; pannable/zoomable canvas; nodes with prerequisites; a node is available when at least one prerequisite is at level ≥ 1.
- **Data-driven.** Nodes are rows in a data table (`AlmanacNode`: id, branch, prerequisites, maxLevel, baseCost, costGrowth, effect type, effect value per level, EN/TR name keys). Adding a node is data, not code.
- Cost of level *n* = `baseCost × costGrowth^n`, default growth **1.6** *(tune)*.

Branches and initial node set (32 nodes in v1.1; edges are listed in `DECISIONS.md`, Session 1). Levels/costs are placeholders to be tuned. *(v1.1)* `upgrade_plot` requires `unlock_tomato` (it does nothing before a second tier exists). Ring speed nodes are +20 % per level *(tune)*.

**Hand** (the ring)
- `ring_radius` (5) · `ring_water_speed` (5) · `ring_grow_speed` (5) · `ring_harvest_speed` (3) · `ring_bonus_coins` +10%/lvl on ring harvests (4) · `ring_combo` consecutive ring harvests within 1 s add a small stacking bonus (3)

**Soil**
- `irrigation` (5) · `sun` (5) · `soil_quality` (6) · `crop_value` +10%/lvl all harvests (5) · `fertile_start` new plots start Wet (1)

**Field**
- `expand_field` (3) · `upgrade_plot` (until all max) · `unlock_tomato` (1) · `unlock_corn` (1) · `unlock_pumpkin` (1) · `unlock_grapes` (1) · `unlock_golden_wheat` (1) · `bulk_upgrade` UpgradePlot raises 2 plots per purchase (1)

**Helpers**
- `apprentice_count` (6) · `apprentice_speed` (4) · `apprentice_harvest_time` (3) · `apprentice_yield` (4) · `tractor` (3) · `scarecrow` (2) · `helper_water` apprentices also water the plot they stand on (1)

**Calendar**
- `year_length` (6) · `frost_warning` +5 s per level (2) · `late_frost` at Winter, plots that are ≥80% grown are harvested at half value instead of lost (1) · `greenhouse` (3) · `crow_bounty` scared crows drop more (3) · `spring_head_start` year starts with all plots Wet (1)

Branch roots (`ring_radius`, `irrigation`, `expand_field`, `apprentice_count`, `year_length`) have no prerequisites.

---

## 7. Heritage (rebirth)

- **Trigger:** "Pass on the farm" unlocks once lifetime coins in this generation reach `HeritageThreshold` (first generation target: around year 6–8 of natural play) *(tune)*. The player chooses when to press it; pressing later yields more seeds.
- **Reset:** coins, plots (3×3, tier 0), Almanac levels, helpers, year counter → 1.
- **Kept:** Heritage tree, generation counter, statistics, cosmetics.
- **Heritage Seeds** = `floor( sqrt(lifetimeCoinsThisGeneration / K) )` with K *(tune)* so the first rebirth yields ~10 seeds. Shown on the Almanac screen as "seeds if you retire now", so the decision is visible every winter.
- **Heritage tree** (≈25 nodes, permanent, same visual language as the Almanac, five branches mirroring it):
  - Hand: starting radius +0.25 (3) · all ring speeds +10% (5) · ring harvests +5% coins (4)
  - Soil: start with Irrigation 1 / Sun 1 (1 each) · global growth +5% (5) · **unlock rain cloud** (1)
  - Field: start 4×4 (1) · start with Tomato unlocked (1) · **golden crop chance** 1%/lvl (5)
  - Helpers: first apprentice free (1) · apprentice yield +5% (4) · **scarecrow level 3 = no crows** (1)
  - Calendar: starting year length +10 s (4) · greenhouse ×2 (2) · Almanac costs −5% (4)
- Each generation adds a visible change to the farm (bigger house, a tree, a fence, a well). Story is exactly this: a farm handed down.

---

## 8. Ending

When every Heritage node is maxed, the next year is the **Golden Year**: the field is 6×6 Golden Wheat, no frost, a longer year, credits after it. Stats screen: generations, years, coins, crows scared. The game can be continued after but nothing new unlocks.

---

## 9. Save and offline

- JSON file in `Application.persistentDataPath`, versioned (`schemaVersion`), written on every winter, rebirth, purchase, and on app pause. Corrupt/unknown file → start fresh, never crash.
- Offline progress: on resume, simulate passive systems only (irrigation → sun → apprentices/tractor) for `min(elapsed, 8 h)` at a fixed dt in the pure core; the year timer does **not** advance offline (you never come back to a lost year). Show a "while you were away" card with coins earned.

---

## 10. Screens

1. **Farm** — field, ring, HUD (coins, year, season bar with frost segment, seeds-on-retire hint after threshold). No bottom bar during the year.
2. **Almanac** (Winter) — tree canvas, node detail card, "Next Year" and "Pass on the farm" buttons.
3. **Heritage** — after rebirth, before Spring of the new generation; also viewable from the pause menu.
4. **Pause / Settings** — language, sound, haptics, reset save, credits. Last.
5. **Onboarding** — no tutorial screen. Year 1: a hand icon pulses on a Dry plot; first Winter: the Almanac highlights `ring_radius` and `irrigation`. That's all.

---

## 11. Art direction

- 2.5D: orthographic camera ~40° tilt, portrait, flat/toon shading, no textures.
- Plot states must read at a glance: Dry = cracked light brown; Wet = dark soil + sprout; growing = scaling plant; Ripe = wobble + warm emissive.
- Seasons via one directional light, ambient colour, colour-adjust volume; frost/winter vignette; snow.
- Asset decision (after core is done): (a) Kenney CC0 low-poly kits, or (b) authored primitives in a consistent "toy farm" style. Paid packs only if cheap. Licenses live in the repo.
- Feel checklist: harvest pop + coin arc to counter + counter punch; wet splash on watering; sprout pop on Wet; crow flap; purchase punch; node-unlock burst on the tree; season lerps 1.5 s.

---

## 12. Audio

Kenney CC0 (Impact Sounds, UI Audio) plus generated fallbacks. Needed: water splash, sprout, harvest pop, coin arrive, crow caw, crow scared, frost tick, winter chime, node buy, rebirth swell, rain. Light ambient loop per season is optional and last.

---

## 13. Technical

- Assemblies: `TillWinter.Core` (pure C#, `noEngineReferences`), `TillWinter.Unity`, `TillWinter.Tests` (EditMode). Editor tooling in `Assets/TillWinter/Editor/`.
- `FarmSim` façade; all tunables in `FarmConfig`; all tree data in `AlmanacData` / `HeritageData` (plain C# tables in Core, optionally mirrored by ScriptableObjects for inspection).
- Tests: every rule in this document that has a number has a test. `run-tests.bat` must pass before any PR merges.
- `smoke-test.bat` play-mode check after presentation changes.
- Branch per feature, PR into `main`, squash merge. Commit messages `type: summary`.
- Target: iPhone 12 and mid/low-range Android at 60 fps; min iOS 15, min Android API 24 *(verify at build time)*.
- Presentation for the full game moves from "everything built in code" to scene-authored prefabs and TextMeshPro once the visual phase starts; the demo's code-built approach is fine until then.

---

## 14. Roadmap

| Phase | Sessions | Output |
|---|---|---|
| 1 Core | S1 three-phase plots + data-driven Almanac model · S2 Heritage/rebirth + save/offline · S3 events (rain, golden crop), tractor, greenhouse, apprentices ×6 | complete rules, tested, still programmer art |
| 2 UI | S4 Almanac tree screen · S5 Heritage screen + HUD rework + onboarding | playable end to end |
| 3 Visual | S6 art pass (asset decision first) · S7 VFX/feel second pass + audio | looks like a product |
| 4 Mobile | S8 iOS/Android builds, performance, safe area, haptics · S9 localization EN/TR, settings/pause, ending | release candidate |
| 5 Store | icons, screenshots, listing text, privacy page, closed test on Google Play | live |

Every session ends with: tests green, PR opened, developer plays, feedback to design chat, docs updated.

---

## 15. Tuning log

Empty. Entries added per playtest: date, what felt wrong, value before → after.
