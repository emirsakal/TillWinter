# Till Winter — Game Design Document

**Version 2.1 - September 2026 (through mechanics group M.5, events and threats — pests, hens, lucky moments and the travelling trader, marked *(v2.1)* inline).** This is the source of truth for what the game is. Session prompts reference it; Claude Code updates it at the end of every session that changes a rule. Numbers marked *(tune)* are first guesses and will be adjusted from playtests, not from reasoning.

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
- Every plot whose centre is inside the ring is processed **independently and in parallel** according to its own state (see 2.2). There is no "mode"; the ring does whatever each plot needs. *(v1.4)* Watering and growing stay parallel, but the ring **harvests one Ripe plot at a time** (the one furthest along, ties to the plot nearest the ring centre); other Ripe plots under the ring wait or go to the helpers. A big ring stays a big watering can while the late-game harvest shifts to apprentices and the tractor.
- Tapping (down+up < 0.2 s, < 20 px) targets the plot under the finger with no offset: scares a crow, and counts as a one-frame ring.
- Ring radius: starts **0.7** (covers one plot, barely touches neighbours), **+0.25** per Almanac level, max **2.5**.
- *(v1.6)* A moving ring works `FarmConfig.FlowBonus` (**15%**) faster than a still one; `State.Flow` eases in when the ring starts moving and eases back out when it stops, rather than switching instantly.
- *(v1.6)* `ring_shape` unlocks two more footprints beyond the circle, chosen by the player on the play screen and saved: **Rake**, a wide thin ellipse (1.7 × 0.5 radii), and **Cross**, two crossed ellipses (1.5 × 0.42 radii).
- *(v1.6)* With `tap_harvest`, a tap finishes one Ripe plot outright on a cooldown (**6 s**, **3 s** at level 2) instead of doing nothing.

### 2.2 Plot state machine
Each plot holds a crop of a given **tier** and is in one of three states. Harvest replants the same tier at Dry.

| State | Under the ring | Passive counterpart |
|---|---|---|
| **Dry → Wet** (watering) | progresses at ring water speed | *Irrigation* waters Dry plots slowly |
| **Wet → Ripe** (growing) | progresses at ring grow speed × soil | *Sun* grows Wet plots slowly (never Dry ones) |
| **Ripe → harvested** (harvest) | progresses at ring harvest speed, one plot at a time *(v1.4)*; on completion coins pop | *Apprentices* walk to Ripe plots and harvest |

Rules:
- Each state has its own progress 0..1. Ring rate replaces (does not stack with) the passive rate while the plot is under the ring. *(v1.1)* Passive rates are fractions of the crop's *base* speed (Hand-branch ring upgrades do not speed up Irrigation/Sun); Soil multiplies growing only. Progress resets to 0 on each state change.
- A Wet plot stays Wet until harvested (no drying out in v1; a "summer drought" event is a later option).
- A Ripe plot outside the ring waits, visibly wobbling. Ripe plots are crow targets.
- Ring harvest completing on a plot with a crow scares the crow first, then harvests.

### 2.3 Crops
Times are seconds under the ring at level 0. Value is coins per harvest. Timings never change with
season or anything else — only value does. *(tune)*

| Tier | Crop | Water | Grow | Harvest | Value | Likes |
|---|---|---|---|---|---|---|
| 0 | Carrot | 1.0 | 1.5 | 0.5 | 2.0 *(v1.9, was 2.2)* | Spring *(v1.7)* |
| 1 | Tomato | 1.5 | 3.5 | 0.5 | 4 | Summer *(v1.7)* |
| 2 | Corn | 2.0 | 6.0 | 0.7 | 12 | Summer *(v1.7)* |
| 3 | Pumpkin | 3.0 | 10 | 1.0 | 35 | Autumn *(v1.7)* |
| 4 | Grapes | 4.0 | 15 | 1.0 | 100 | Autumn *(v1.7)* |
| 5 | Golden Wheat | 5.0 | 22 | 1.2 | 300 | Spring *(v1.7)* |

Crop tiers are per plot. New tiers are gated by Almanac nodes (see 5.3).

*(v1.7)* **In-season value.** A crop harvested during its liked season, only during the Year phase
(not Golden Year), sells for `FarmConfig.InSeasonValue` (**×1.25**). Season preference is
value-only — crop timings stay fixed per the tuning rule (§15); a growth-speed bonus was tried and
rejected (see `DECISIONS.md`). Carrot's base value moved 2.3 → 2.2 to keep year-1 coins inside
`BalanceTests`' 40–70 range once the in-season bonus is in play (measured: year 1 = 69 coins, seed
1; ring share at first retire 58%; 5.59 h to the ending; 6 generations to max Heritage).

*(v1.8)* **Neighbour variety.** Each orthogonal neighbour (up/down/left/right) growing a different
crop tier adds `FarmConfig.NeighbourVarietyBonus` (**4%**) to harvest value, multiplicative:
`1 + 0.04 × count` (max **+16%** at four different neighbours). A stony neighbour doesn't count; a
field edge counts for nothing.

*(v1.8)* **Crop rotation.** Entering Winter, every plot remembers the crop it grew that year
(`Plot.LastYearTier`; a stony plot remembers none, **-1**). A plot growing a different crop than
last year sells for `FarmConfig.RotationBonus` (**×1.15**) all year; an upgraded bed moving to a
higher crop counts as a rotation. Retiring gives a fresh field with no memory.

### 2.4 Field
- Starts **3×3**. Expands to 4×4, 5×5, 6×6. Expansion is anchored bottom-left (existing plots keep coordinates); the camera re-centres.
- New plots start at tier 0, Dry.
- *(v1.7)* **Seed bag.** Each plot has a bed quality (`BedTier`), raised by `UpgradePlot` exactly as
  before (lowest bed first, bounded by the highest unlocked crop — a bounded purchase, not an
  endless sink), and a `Choice` (**-1** = follow the bed, i.e. "Best"). The crop actually growing
  (`Tier`) is the choice capped at the bed's quality. `FarmSim.SetPlotCrop(pos, tier)` plants during
  the Year phase only (not Golden Year): planting a different crop replants the plot from Dry at
  progress 0; picking the crop already growing keeps progress. A pick survives later bed upgrades.
  HUD: a basket button bottom-left (shown once a second crop is unlocked, Year phase, not Golden
  Year) opens a chip row — "Best" plus each unlocked crop with the season it likes, the season
  currently in play highlighted. With a chip picked, tapping a plot plants it (a crow on the plot
  still takes the tap first); a bed too low shows "Upgrade this bed first". Tapping the picked chip
  again puts the seed back.

*(v1.8)* **Special ground.** `PlotKind { Normal, Fertile, Stony }`. A plot added by `expand_field`
rolls with the sim RNG: **25%** stony (`FarmConfig.StonyChance`), **15%** fertile
(`FarmConfig.FertileChance`), else normal; the starting field and the Heritage starting field are
always plain, and a debug-set level doesn't roll. Fertile sells for `FarmConfig.FertileValue`
(**×1.3**). Stony grows nothing — not irrigation, not the rain cloud, no spring head start, no seed
bag — only the ring hovering over it clears it (`FarmConfig.StoneClearSeconds`, **3 s** of ring
time; progress is kept within a year, reset at year end), then it becomes plain ground
(`PlotCleared` event: dust puff + sound). Special ground lasts the generation; retiring gives a
plain field.

*(v1.8)* All value modifiers — in-season, fertile, rotation, neighbour variety — combine
multiplicatively in `FarmSim.PlotValueMultiplier`, applied to every harvest source (ring,
apprentice, tractor, late frost); crop timings are untouched by any of them.

### 2.5 Over-ripening *(v1.6)*
- A Ripe plot keeps full value for `RipeGraceSeconds` (**12 s**), then its value falls linearly over `OverripeDecaySeconds` (**24 s**) to `OverripeMinValue` (**50%**) and stays there. The crop is never lost to age — the ring, apprentices and tractor still harvest it, just for less.
- Age does not run while the game is closed (offline).
- The plot view dulls and droops as it goes.
- Reason: the ring had nothing to prioritise late game.

---

## 3. Year, seasons, winter

- One year = one timer through Spring → Summer → Autumn (boundaries at 1/3, 2/3) then Winter.
  *(v1.9)* Season identity now carries rules, not just colour — see §3.2.
- Year length starts **90 s**, +15 s per Calendar level, cap **180 s** *(tune)*.
- **Frost warning**: last **10 s** of Autumn (extendable by Almanac). Cold vignette, blue light, timer heartbeat, tick SFX. *(v1.9)* See §3.1 for the frost rush value bonus.
- **Winter**: all plots reset to Dry with progress 0 (unharvested crops lost). Coins are kept. Field freezes; ring does nothing; the Almanac opens. Greenhouse (if owned) still produces a trickle. *(v1.9)* The year's grade (§3.4) is computed and `YearGraded` fires before `WinterStarted`.
- **Next Year**: year counter +1, Spring starts, plots keep tiers. *(v1.9)* A new goal (§3.3) may be drawn and a weather spell (§5.4) may be planned.
- *(v1.5)* **End the year early**: the player may bring Winter forward at any point during a year from the HUD. It runs the same Winter transition the timer does, so the standing crop is lost (or half-harvested with `late_frost`) exactly as it would have been — the choice trades the rest of the year's income for reaching the Almanac sooner, and never skips a cost. *(v1.9)* This also clears the current goal (§3.3) and any in-progress weather (§5.4) on retire.

### 3.1 Frost rush *(v1.9)*

Harvests made during the frost warning pay `FarmConfig.FrostRushValue` (**×1.25**) on top of other
multipliers; a late-frost rescue (the half-harvest above) keeps its own **×0.5** independently. HUD
banner: "Frost rush: harvests pay +25%".

### 3.2 Season rules *(v1.9)*

Crop timings (§2.3) never change with season — only these systems do:

- **Spring rain**: passive watering from Irrigation runs at ×1.5 (`FarmConfig.SpringWaterBoost`).
- **Summer drought**: a Wet plot that nothing is growing (not under the ring, no Sun) dries back to
  Dry after `FarmConfig.SummerDryOutSeconds` (**8 s**) of neglect (`Plot.DryTimer`, event
  `PlotDriedOut`, a small dust puff). Owning Sun protects every Wet plot from drying out. Drought
  never runs while offline or during a storm (§5.4).
- **Autumn harvest festival**: the combo window runs at ×1.5 (`FarmConfig.AutumnComboWindow`).

### 3.3 Yearly goals *(v1.9)*

From `FarmConfig.GoalFirstYear` (year 2) of a generation, skipping the Golden Year, each Spring
draws one goal with the sim RNG:

- **Harvest a crop**: a random crop up to the best bed; target = `ceil(2 × non-stony plots)`.
- **Combo**: target = `8 + 2 × (year − 1)`, capped at 30.
- **Coins**: only offered if last year earned anything; target = `ceil(lastYearCoins × 1.2)`.

Reward = `max(GoalMinReward, GoalRewardShare × lastYearCoins)` (**10**, **5%**), paid once on
completion (event `GoalCompleted`, HUD banner "Goal met! +X"). The HUD shows a goal line under the
ring rate, e.g. "Goal: harvest 18 Tomato · 0/18". Retiring clears the current goal and its history.

### 3.4 Grade *(v1.9)*

Every harvest adds its freshness to `YearFreshSum`; a crop lost to a crow counts as 0
(`CropsLostThisYear`). At Winter, `quality = YearFreshSum / (harvests + lost)` (a year with no
harvests grades 1 star); stars are 3 at `quality ≥ 0.97`, 2 at `≥ 0.85`, else 1. The bonus is a
share of the year's coins by stars — `{0%, 0%, 2%, 4%}` (`FarmConfig.GradeBonusByStars`) — paid
into coins and lifetime coins but **not** counted in `CoinsThisYear` (shown separately, so it never
helps a Coins goal, §3.3). `LastGrade`, `LastGradeBonus` and `LastYearCoins` are saved. Event
`YearGraded` fires before `WinterStarted`. The Winter screen shows three star icons, "Grade bonus
+X" and "Goal met"/"Goal missed".

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
| Tractor | every N s harvests one full row of Ripe plots (single unit, level = shorter interval). *(v1.3)* Intervals 30/20/12 s, 0.15 s per plot, sweeps the row with the most Ripe plots (ties: lowest row), full value, no ring bonus or apprentice yield; plots ripening mid-sweep are taken; crows on the row are scared with the bounty. *(v2.0)* `FarmSim.TriggerTractor` sends it immediately once its timer is ≥50% charged (`TractorManualReady`); `TractorCharge` exposes 0..1 for the HUD's gold charge bar. Row choice stays automatic — the best row is almost always what the player wants, so picking the row by hand is out of scope. | 0–3 |
| Scarecrow | *(v2.0)* No longer a chance modifier. Each level places one scarecrow the player can move; crow spawn chance is a flat `FarmConfig.CrowSpawnChance` (25%), but a scarecrow guards every plot within `FarmConfig.ScarecrowRadius` (1.6 plots) of the field corner it stands on — 12 plots in open field, crow-free (§5.1). New scarecrows auto-place where they guard the most unguarded plots (ties: nearest field centre); `FarmSim.MoveScarecrow(index, corner)` moves one to any free corner, not in the Heritage phase. | 0–2 |
| Farm dog *(v2.0)* | Almanac node, Helpers branch, prereq `scarecrow`, max 1, cost 400. Chases off a crow that has sat `FarmConfig.DogReactSeconds` (1.5 s) — no bounty, that stays the tap's — then rests `FarmConfig.DogCooldownSeconds` (10 s, saved as `State.DogCooldown`). Fires event `DogChased`. | 0–1 |
| Beehive *(v2.0)* | Almanac node, Soil branch, prereq `sun`, max 1, cost 450. Sun grows the `FarmConfig.BeeColumns` (2) right-most columns, by the sunflowers, at `FarmConfig.BeeSunBoost` ×1.3. | 0–1 |
| Hens *(v2.1)* | Almanac node, Helpers branch, prereq `farm_dog`, max 1, cost 500. Eats a pest (§5.5) that has been present `FarmConfig.HenEatSeconds` (2.5 s), then rests `FarmConfig.HenCooldownSeconds` (12 s, saved as `State.HenCooldown`); after a meal, `FarmConfig.GoldenEggChance` (20%) chance of a golden egg worth `FarmConfig.GoldenEggValue` (6) crop values (§5.6). Completes the M.4 "hens eat pests" item, deferred until pests existed. | 0–1 |
| Greenhouse | during Winter, earns `X coins/s` based on field value *(tune)*. *(v1.3)* `level x 0.02 x field value` coins/s (field value = sum of the crop value of every plot), x2 with the Heritage node, at most 60 s per winter, only while the Winter phase is open (not in the Heritage phase, not offline). | 0–3 |

Apprentices: each has its own position and target; they never target the same plot; they retarget if the ring or another helper takes their plot; they idle near the field edge when nothing is Ripe. Six apprentices running around is a deliberate visual goal. *(v2.0)* Each apprentice has a role, `ApprenticeRole` (`Harvester` or `Waterer`): a Harvester works as before; a Waterer walks to the nearest Dry, non-stony plot not already targeted and waters it to Wet in the harvest time. Tapping an apprentice switches its role (banner "Harvester: picks ripe crops" / "Waterer: waters dry plots") and drops its current job; a waterer's thought bubble shows water colour. Works offline like any apprentice.

---

## 5. Events

### 5.1 Crows
- From year 2. Spawn check every 4 s; roll at `FarmConfig.CrowSpawnChance` (flat 25%) only when a Ripe, crow-free plot exists outside the ring and outside every scarecrow's guarded radius. Max 2 at once. *(v2.0)* Scarecrows guard an area instead of lowering the spawn chance (§4).
- A crow eats the crop after 4 s. Tap scares it: the crop stays and the crow drops **coins = 2 × crop value** *(tune)*. Being harvested also scares it *(v1.1: no coins dropped)*.
- Eaten crop → plot to Dry, progress 0.

### 5.2 Rain cloud *(unlocked in Heritage)*
- A cloud drifts across the top of the field once per year at a random time in Summer. Tap it: rain waters every Dry plot instantly and grows all Wet plots by 25%. Ignored, it drifts away.
- *(v1.3)* Spawn time is a seeded random point in Summer chosen at Spring; drifts 8 s; a tap sets Dry plots to Wet/0 and adds +0.25 to Wet plots (clamped to Ripe). Cancelled by Winter/Retire; saved mid-drift.

### 5.3 Golden crop *(unlocked in Heritage)*
- On harvest, `chance%` that the replanted crop is golden: 10× value, glows. Same timings.
- *(v1.3)* Rolled at every replant, 1 % per Heritage level (max 5). Pays 10x before other multipliers.

### 5.4 Weather *(v1.9)*
- From `FarmConfig.WeatherFirstYear` (year 2), each year has a `FarmConfig.WeatherChance` (**60%**)
  chance of one weather spell, planned at Spring with the sim RNG:
  - **Storm** (12 s, rolled to start anywhere from 15–85% through the year): Sun stops; rain waters
    every Dry plot at 0.5× the passive rate; no crows spawn; summer drought (§3.2) does not run.
  - **Heat wave** (`FarmConfig.HeatWaveSeconds`, 15 s, planned within Summer): Sun runs ×1.5, and
    drought (§3.2) dries Wet plots at double speed for the whole spell, even past Summer's boundary.
  - **Fog** (12 s, planned within Spring): ripe crops stop over-ripening (§2.5) for the duration; no
    crows spawn.
  - Weather always stops at Winter. Event `WeatherChanged` fires on start and end; the HUD shows a
    banner per kind.
- Visuals: a storm greys and dims the field light and brings rain (`VfxId.StormRain`, a new
  continuous effect built by `feel-setup.bat`); fog raises mist and washes the light pale; a heat
  wave warms the colour filter.

### 5.5 Pests *(v2.1)*
- From `FarmConfig.PestFirstYear` (year 3) of a generation, never in the Golden Year, never while
  the app is closed (pests are not part of offline simulation, §9).
- One pest at a time. A check every `FarmConfig.PestCheckSeconds` (15 s) rolls
  `FarmConfig.PestChance` (35%); the kind is drawn with the sim RNG.
- **Mole:** pops up on a non-stony plot; after 5 s digs it back to Dry — a ripe crop lost this way
  counts against the year's grade (§3.4) like a crow's. A tap bonks it for `FarmConfig.MoleBounty`
  (1 crop value) and it leaves.
- **Rabbit:** appears only while a carrot plot is Wet or Ripe outside the ring; eats it after 5 s.
  A tap, or the ring passing over it, sends it off with no coins.
- **Locust swarm:** settles over the 3×3 patch around its plot (`FarmConfig.LocustRadius` 1);
  nothing grows in the patch while it stays. Working the ring over any plot of the patch for
  `FarmConfig.LocustShooSeconds` (1.5 s) drives it off; left alone 12 s it strips the patch (ripe
  crops lost, Wet progress reset).
- Events `PestArrived`, `PestScared`, `PestStruck`; the HUD shows a banner per kind. Unity-side: new
  Mole/Rabbit/Locust prefabs (`ArtSetup`), `PestsView` (the mole rises from its mound and digs
  faster as its time runs out, the rabbit hops, a swarm of 14 locusts circles the patch and
  tightens as the ring drives it off). A tap on a pest never falls through to the seed bag
  underneath it.

### 5.6 Lucky moments *(v2.1)*
- From `FarmConfig.LuckyFirstYear` (year 2).
- **Four-leaf clover:** a check every 20 s, `FarmConfig.CloverChance` (12%) chance, appears on a
  plot outside the ring for 10 s; sweeping the ring over it pays `FarmConfig.CloverValue`
  (8 crop values).
- **Golden egg:** from the hens (§4), worth `FarmConfig.GoldenEggValue` at
  `FarmConfig.GoldenEggChance`.
- **Shooting star:** rolled at the frost warning, `FarmConfig.StarChance` (35%); it crosses the sky
  for 4 s (a HUD star button). A tap starts a 10 s rush where every harvest pays
  ×`FarmConfig.StarRushValue` (2); the rush is never applied while simulating offline.
- Events `LuckyAppeared`, `LuckyFound`; the HUD shows a banner per kind.

### 5.7 Travelling trader *(v2.1)*
- From `FarmConfig.TraderFirstYear` (year 2), `FarmConfig.TraderChance` (50%) of years. Planned at
  Spring with the sim RNG to arrive in Summer; stays `FarmConfig.TraderSeconds` (25 s).
- Two offers, each purchasable once a visit: a Heritage Seed for
  `max(60, 60% of last year's coins, or this year's coins so far if higher)`; a rare seed for
  `max(20, 15% of the same base)` that turns 2 random non-stony plots golden immediately.
- HUD trader card: two buttons, a time bar, "Sold" once a purchase is made. Events
  `TraderArrived`, `TraderLeft`, `TraderSold`.
- Pests, the clover, the star, its rush and the trader all end with the year (Winter clears them).

---

## 6. Almanac (winter skill tree, bought with coins, reset on rebirth)

- Opens only in Winter. Full-screen panel over the frozen field; pannable/zoomable canvas; nodes with prerequisites; a node is available when at least one prerequisite is at level ≥ 1.
- **Data-driven.** Nodes are rows in a data table (`AlmanacNode`: id, branch, prerequisites, maxLevel, baseCost, costGrowth, effect type, effect value per level, EN/TR name keys). Adding a node is data, not code.
- Cost of level *n* = `baseCost × costGrowth^n`, default growth **1.6** *(tune)*.

Branches and initial node set (32 nodes in v1.1; edges are listed in `DECISIONS.md`, Session 1). Levels/costs are placeholders to be tuned. *(v1.1)* `upgrade_plot` requires `unlock_tomato` (it does nothing before a second tier exists). Ring speed nodes are +20 % per level *(tune)*.

**Hand** (the ring)
- `ring_radius` (5) · `ring_water_speed` (5) · `ring_grow_speed` (5) · `ring_harvest_speed` (3) · `ring_bonus_coins` +10%/lvl on ring harvests (4) · `ring_combo` consecutive ring harvests within 1 s add a small stacking bonus (3) · *(v1.6)* `ring_shape` unlocks Rake and Cross footprints (2) · *(v1.6)* `tap_harvest` a tap finishes one Ripe plot on a cooldown (2)

**Soil**
- `irrigation` (5) · `sun` (5) · `soil_quality` (6) · `crop_value` +10%/lvl all harvests (5) · `fertile_start` new plots start Wet (1) · `beehive` *(v2.0)* prereq `sun`, Sun grows the two right-most columns ×1.3 (1)

**Field**
- `expand_field` (3) · `upgrade_plot` (until all max) · `unlock_tomato` (1) · `unlock_corn` (1) · `unlock_pumpkin` (1) · `unlock_grapes` (1) · `unlock_golden_wheat` (1) · `bulk_upgrade` UpgradePlot raises 2 plots per purchase (1)

**Helpers**
- `apprentice_count` (6) · `apprentice_speed` (4) · `apprentice_harvest_time` (3) · `apprentice_yield` (4) · `tractor` (3) · `scarecrow` (2) · `helper_water` apprentices also water the plot they stand on (1) · `farm_dog` *(v2.0)* prereq `scarecrow`, chases off a crow after it lands, no bounty (1) · `hens` *(v2.1)* prereq `farm_dog`, eats a pest, chance of a golden egg (1)

**Calendar**
- `year_length` (6) · `frost_warning` +5 s per level (2) · `late_frost` at Winter, plots that are ≥80% grown are harvested at half value instead of lost (1) · `greenhouse` (3) · `crow_bounty` scared crows drop more (3) · `spring_head_start` year starts with all plots Wet (1)

*(v1.3)* `ring_combo`: consecutive ring harvests within 1 s stack, `1 + level x 0.01 x min(combo, 10)` on ring harvests. `late_frost`: at Winter, Ripe plots and Wet plots at >= 80 % are harvested at half value. `crow_bounty`: scare drop = `(2 + level) x value`. `bulk_upgrade`: two lowest plots per purchase. `fertile_start`: expansion plots start Wet. `spring_head_start`: all plots Wet at Spring. `helper_water`: apprentice replants Wet.

*(v1.6)* Combo milestones pay out on top of `ring_combo`'s stacking bonus: combo 10/25/50 pays 3x/8x/20x the harvested crop's value (`ComboMilestones`/`ComboMilestoneBonus`). A crow eating a crop now breaks the combo, same as it already breaks on a miss.

*(v2.0)* `scarecrow` no longer changes crow spawn chance; each level places a scarecrow the player can move, guarding plots within `FarmConfig.ScarecrowRadius` (§4, §5.1) — replaces `CrowSpawnChanceByScarecrow`. `farm_dog`: prereq `scarecrow`, max 1, cost 400; chases a crow that has sat `FarmConfig.DogReactSeconds` (1.5 s), no bounty (stays the tap's), then rests `FarmConfig.DogCooldownSeconds` (10 s, saved as `State.DogCooldown`); event `DogChased`. `beehive`: prereq `sun`, max 1, cost 450; the Sun grows the `FarmConfig.BeeColumns` (2) right-most columns at `FarmConfig.BeeSunBoost` ×1.3. Hens eating pests is deferred to M.5 (pests don't exist yet).

*(v2.1)* `hens`: prereq `farm_dog`, max 1, cost 500; eats a pest (§5.5) that has been present `FarmConfig.HenEatSeconds` (2.5 s), then rests `FarmConfig.HenCooldownSeconds` (12 s, saved as `State.HenCooldown`); after a meal, `FarmConfig.GoldenEggChance` (20%) chance of a golden egg worth `FarmConfig.GoldenEggValue` (6) crop values (§5.6). Completes the M.4 "hens eat pests" item above.

Branch roots (`ring_radius`, `irrigation`, `expand_field`, `apprentice_count`, `year_length`) have no prerequisites.

---

## 7. Heritage (rebirth)

- **Trigger:** "Pass on the farm" unlocks once lifetime coins in this generation reach `HeritageThreshold` (first generation target: around year 6–8 of natural play) *(tune)*. The player chooses when to press it; pressing later yields more seeds. *(v1.2)* `HeritageThreshold` = 5 000 lifetime coins this generation, `SeedDivisor` = 50 (so 5 000 coins = 10 seeds). *(v1.4)* Balance pass: `HeritageThreshold` = 3 000, `SeedDivisor` = 30 (3 000 coins = 10 seeds), so the first retire lands in year 6–8. Retiring is a Winter action; it leads to a `Heritage` phase (no ticking, Heritage purchases only) before Spring of the new generation. *(v1.9)* Balance pass for M.3's new income: `HeritageThreshold` = 3 500, `SeedDivisor` = 33 (3 500 coins = 10 seeds).
- **Reset:** coins, plots (3×3, tier 0), Almanac levels, helpers, year counter → 1.
- **Kept:** Heritage tree, generation counter, statistics, cosmetics.
- **Heritage Seeds** = `floor( sqrt(lifetimeCoinsThisGeneration / K) )` with K *(tune)* so the first rebirth yields ~10 seeds. Shown on the Almanac screen as "seeds if you retire now", so the decision is visible every winter.
- **Heritage tree** (≈25 nodes, permanent, same visual language as the Almanac, five branches mirroring it):
  - Hand: starting radius +0.25 (3) · all ring speeds +10% (5) · ring harvests +5% coins (4)
  - Soil: start with Irrigation 1 / Sun 1 (1 each) · global growth +5% (5) · **unlock rain cloud** (1)
  - Field: start 4×4 (1) · start with Tomato unlocked (1) · **golden crop chance** 1%/lvl (5)
  - Helpers: first apprentice free (1) · apprentice yield +5% (4) · **scarecrow level 3 = no crows** (1) *(v1.3)* = Almanac scarecrow 2 + this node -> crow chance 0. *(v2.0)* Scarecrow spawn chance is now flat (§4); at Almanac scarecrow 2 the two placed scarecrows already guard every plot on a 3×3 field, so this node keeps its meaning (no crows can land) unchanged.
  - Calendar: starting year length +10 s (4) · greenhouse ×2 (2) · Almanac costs −5% (4)
- Each generation adds a visible change to the farm (bigger house, a tree, a fence, a well). Story is exactly this: a farm handed down.
- *(v1.2)* The Heritage table has the 16 nodes listed above (edges in `DECISIONS.md`, Session 2). 'Start with Irrigation 1 / Sun 1' acts as a floor on the Almanac level, not an addition. Heritage effects are base modifiers applied before Almanac effects. Nodes whose feature does not exist yet are purchasable and stored as flags.

---

## 8. Ending

When every Heritage node is maxed, starting the next generation begins the **Golden Year**
*(v1.4)*: the field is 6×6 Golden Wheat (all golden), no crows, no frost, a 300 s year
(`FarmConfig.GoldenYearSeconds`), golden season look. At its Winter the field returns to the
normal starting size and `EndingSeen` is set once; the ending does not repeat on later
generations. Credits roll next (skippable after 3 s, 24 s total), then a statistics sheet
(generations, years, coins, per-source harvests, crows scared, best combo, time played), then the
normal Winter screen underneath. The Heritage tree title shows "Complete". The game continues
after, but nothing new unlocks.

---

## 9. Save and offline

- JSON file in `Application.persistentDataPath`, versioned (`schemaVersion`), written on every winter, rebirth, purchase, and on app pause. Corrupt/unknown file → start fresh, never crash. *(v1.2)* Also written on Next Year, on starting a new generation, and every 30 s during a year. Atomic write with one `.bak`; a corrupt file is renamed `.corrupt-<timestamp>`.
- Offline progress: on resume, simulate passive systems only (irrigation → sun → apprentices/tractor) for `min(elapsed, 8 h)` at a fixed dt in the pure core; the year timer does **not** advance offline (you never come back to a lost year). Show a "while you were away" card with coins earned. *(v1.2)* Simulated at a 1 s step; the ring, crows and seasons are frozen. A clock that went backwards counts as 0 elapsed. *(v1.3)* The tractor also runs offline; the greenhouse does not (phase is not Year). *(v2.1)* Schema 11 → 12 adds `PestKind`/`X`/`Y`/`Timer`/`Shoo`, `HenCooldown`, `CloverX`/`Y`/`Left`, `StarLeft`, `RushLeft`, `Trader*` fields, `PestCheckTimer` and `LuckyCheckTimer` to `SaveData`. `SaveMigrations.V11ToV12` starts a loaded save with no pest, no lucky moment and no trader due (`PlannedTime` −1). Pests, lucky moments and the trader are Year-phase state only, never part of offline simulation (§5.5–§5.7).
- *(v1.6)* Schema is now **7**: adds each plot's `RipeAge` and the player's chosen ring shape. Migration `V6ToV7` is a no-op with safe defaults (age 0, circle shape).
- *(v1.7)* Schema is now **8**: adds each plot's `Choice` (default **-1**); `Tier` now means the bed's quality, not the crop growing. Migration `V7ToV8` sets `Choice = -1` on every plot. Fixture-tested in `SaveV8Tests` against a hand-written v7 JSON.
- *(v1.8)* Schema is now **9**: adds each plot's `Kind` (int) and `LastYearTier` (default **-1**). Migration `V8ToV9` sets every plot to plain ground with no rotation memory. Fixture-tested in `SaveV9Tests` against a hand-written v8 JSON.
- *(v1.9)* Schema is now **10**: adds `YearFreshSum`, `CropsLostThisYear`, `LastGrade`, `LastGradeBonus`, `LastYearCoins`, `GoalType`/`GoalTier`/`GoalTarget`/`GoalProgress`/`GoalDone`/`GoalReward`, `Weather`, `WeatherLeft`, `PlannedWeather`, `PlannedWeatherTime` to `SaveData`, and each plot's `DryTimer` to `PlotSave`. Migration `V9ToV10` starts with no active goal, no grade recorded, and clears any in-progress weather. Fixture-tested in `SaveV10Tests` against a hand-written v9 JSON.
- *(v2.0)* Schema is now **11**: adds `ScarecrowX`/`ScarecrowY` (int arrays) and `DogCooldown` to `SaveData`, and `Role` to `ApprenticeSave`. Migration `V10ToV11` starts with no scarecrows remembered (they are placed fresh on load), a rested dog, and every apprentice as a Harvester. Fixture-tested in `SaveV11Tests` against a hand-written v10 JSON.

---

## 10. Screens

1. **Farm** — field, ring, HUD. *(v1.3, Session 5)* HUD (`HudView`, every colour/spacing from `HudTheme`): coins big and centred with a punch on change, "Year N · Gen G" small underneath, a four-segment season bar (Spring/Summer/Autumn/Winter) filling left-to-right with a frost span and the season name fading in at each boundary, a combo "xN" readout from combo ≥ 2, and a seed chip "Retire now: N seeds" once `CanRetire`. Safe area is applied to the HUD only on mobile platforms (editor `Screen.safeArea` is unreliable). No bottom bar during the year.
2. **Almanac** (Winter) — tree canvas (`SkillTreeView` + `TreeTheme`), node detail card, "Next Year" and "Pass on the farm" buttons. A "Heritage" tab in the Winter top bar opens the Heritage tree without leaving Winter.
3. **Heritage** — *(v1.3, Session 5)* the same `SkillTreeView` shown full screen, themed by a second asset (`HeritageTheme`: deep green paper, gold accents, seed currency, darker branch colours, tighter initial zoom so all five branches fit). Reached after rebirth (Retire on the farm -> confirm dialog -> `Retire()` -> a full-screen **Generation card**: "Generation N", one flavour line, seeds counting up, skip after 0.5 s / auto-continue at 6 s -> Heritage screen), before Spring of the new generation; also reachable as the Winter tab above. Starting the new generation reveals plots one by one bottom-left to top-right, clears snow, and pops in generation-appropriate decor (`FarmDecorSet`/`FarmDecorView`, data-driven, `MinGeneration`-gated). Pan/zoom is remembered per tree (Almanac and Heritage separately) across sessions.
4. **Pause / Settings** — language, sound, haptics, reset save, credits. Last.
5. **Onboarding** — no tutorial screen; a `Hint` enum + `OnboardingFlags` in Core make every hint fire once and only once, saved. *(v1.3, Session 5)* Shipped hints: first touch on a Dry plot (pulsing hand + hold caption), first Ripe plot outside the ring, first frost warning, first crow; first Winter (the tree centres on `ring_radius`/`irrigation` with a pulse and caption until the first purchase); the first time `CanRetire` (a one-time explanatory sheet); first entry to Heritage ("Seeds never reset."). Hints never block input. **While-you-were-away card** (`AwayCard`, shown on resume when offline sim earned coins): duration in h/min, total coins, a line per source (apprentices, tractor), a note when capped at the 8 h offline cap; the HUD coin counter withholds the earned coins (`HudView.HeldCoins`) until the card is dismissed.

---

## 11. Art direction

- 2.5D: orthographic camera ~40° tilt, portrait, flat/toon shading, no textures.
- Plot states must read at a glance: Dry = cracked light brown; Wet = dark soil + sprout; growing = scaling plant; Ripe = wobble + warm emissive.
- Seasons via one directional light, ambient colour, colour-adjust volume; frost/winter vignette; snow.
- Feel checklist: harvest pop + coin arc to counter + counter punch; wet splash on watering; sprout pop on Wet; crow flap; purchase punch; node-unlock burst on the tree; season lerps 1.5 s.

*(v1.5, Session 7)* Feel checklist shipped: watering splash + soil ripple, sprout pop entering
growing, ripe sparkle + emission glow pulse, ring harvest burst scaled by tier with 3-8 coins by
value and counter punch (ring only; helpers get fewer coins + a tick), golden harvest (bigger gold
burst, gold coins, 60 ms 10% white flash, 0.15 s camera micro-shake, Medium haptic, own clip),
combo "xN" floater with ring glow/pulse scaling and fade on break, crow feathers on land/scare +
soil puff on eat, tractor exhaust + dust, apprentice step dust + footsteps, field-expansion pops
only the new plots, season ambience (petals/leaves/snow) cross-faded by `SeasonPresenter`, frost
edge overlay + Light haptic at warning start, winter chime + retire swell ducking SFX -6 dB for
1 s, retire swell + Heavy haptic + camera shake + snow burst, new-generation clip + melt sparkle.
Camera shake is reserved for golden harvest and retire only. One pooled particle prefab per
`VfxId` (`VfxCatalog`, Resources) built by `FeelSetup`/`feel-setup.bat` from specs (TW_Toon white
material, `Palette` colours applied at boot), played only through `VfxPlayer` (no runtime
Instantiate). Every repeatable trigger is rate-limited (`TillWinter.Core.Feel.RateLimiter`, token
bucket per id): over-budget requests merge into the next allowed trigger at higher intensity
instead of being dropped or queued unbounded. Haptics (`Haptics`: Light/Medium/Heavy/Selection) go
through Android `Vibrator`/`VibrationEffect` or the iOS `TillWinterHaptics.mm` plugin, gated by a
settings flag (`SettingsData.HapticsEnabled`, `settings.json`, separate file from the save).
Measured budget (6x6, 6 apprentices, tractor sweeping, ring centred, plots forced Ripe every ~20
frames for 5 s): peak 13 active particle systems (<=20), 0 bytes allocated by 400
`VfxPlayer.Play`/`AudioManager.Play` calls; render stats unchanged from Session 6.

*(v1.4, Session 6)* Asset decision resolved per-asset rather than kit-vs-primitives wholesale:
Kenney CC0 kits (Nature Kit, Food Kit, Mini Characters, Game Icons — stripped to only the files
used, licenses in `Assets/Art/LICENSES.md`) for crops, trees/decor, apprentices and node icons;
everything the kits don't cover (houses, well, windmill, greenhouse, tractor, crow, cloud, plot,
path tile, signpost, flowerbed, trellis) is built from primitives, generated into prefabs by
`ArtSetup`/`art-setup.bat` rather than at runtime. Every spawned object comes from one
`VisualCatalog` (Resources) through `VisualCatalog.Spawn`.

Look is unified by one hand-written URP shader, `TW_Toon` (flat two-step ramp, vertex-colour x
tint, optional emission, snow lerp, GPU instancing) plus `TW_Sky` for the diorama backdrop — Shader
Graph's file format proved unreliable to author from code, so these are HLSL text. One material
per `PaletteSlot` keeps identical meshes GPU-instanced; `Palette` (Resources) pushes colours into
those materials at boot, and per-object variation (plot Dry/Wet/ring soil, golden emission, ring
lift) goes through `PaletteBinder` + a `MaterialPropertyBlock` per renderer instead of extra
materials. `SeasonPalette` (Resources) holds per-season light, ambient, fog, grass/leaf tint, sky
and snow amount; `SeasonPresenter` lerps between them (1.5 s) and drives snow/tint as shader
globals, so Winter recolours everything through the shader with no mesh swaps. The diorama
(`DioramaView`) builds the soil block, path, fence and farmhouse (small/medium/large by
generation) around the field from the same catalogue. The ring stays a textured decal-style disc
(URP decal projectors did not render correctly on the orthographic camera in this project).

Quality tiers (`QualityTiers`): Low (no shadows, no post) auto-selected on mobile devices under
3 GB RAM / 1 GB VRAM, Default (soft shadows + colour volume) otherwise; a debug-panel toggle
overrides it. Measured draw-call budgets (Editor Game view, 1080x2340): 3x3 generation 1 is 45
batches / 67 draw calls / 4.7k triangles; 6x6 generation 3 with seven apprentices and the tractor
is 106 batches / 142 draw calls / 27.8k triangles, against a smoke-test budget of <=150 batches /
<=60k triangles.

---

## 12. Audio

Kenney CC0 (Impact Sounds, UI Audio) plus generated fallbacks. Needed: water splash, sprout, harvest pop, coin arrive, crow caw, crow scared, frost tick, winter chime, node buy, rebirth swell, rain. Light ambient loop per season is optional and last.

*(v1.5, Session 7)* Shipped with no generated fallback: `SfxTable` maps every `SfxId` to one of 38
Kenney CC0 clips across Impact Sounds, Interface Sounds, RPG Audio and UI Audio
(`Assets/Audio/Kenney/Resources/Kenney`, licences next to the clips + `Assets/Audio/LICENSES.md`
index). Casual Game Sounds could not be resolved for download this session, so it was not used;
none of the packs used contain a wind/birds loop, so the optional season ambience loop is not
implemented (and was not synthesised, per the session brief). `AudioManager` runs 8 voices max
(steals the voice ending soonest), rate-limits and merges repeats per id, pitches the harvest pop
+2% per combo step (capped +30%), and picks a value-scaled coin clip (richer variant at tier >= 3
or golden); `Duck()` lowers SFX under a chime/swell. Routing is
`Resources/TillWinterMixer` (Master/SFX/Ambience groups, exposed `MasterVolume`/`SfxVolume`/
`AmbienceVolume`), built by `FeelSetup` through the editor's internal `AudioMixerController` API
via reflection (no public API creates mixer groups from code). Volumes and the haptics toggle live
in `SettingsData`/`SettingsStore` (`settings.json`), independent of the save schema.

---

## 13. Technical

- Assemblies: `TillWinter.Core` (pure C#, `noEngineReferences`), `TillWinter.Unity`, `TillWinter.Tests` (EditMode). Editor tooling in `Assets/TillWinter/Editor/`.
- `FarmSim` façade; all tunables in `FarmConfig`; all tree data in `AlmanacData` / `HeritageData` (plain C# tables in Core, optionally mirrored by ScriptableObjects for inspection).
- Tests: every rule in this document that has a number has a test. `run-tests.bat` must pass before any PR merges.
- `smoke-test.bat` play-mode check after presentation changes.
- Branch per feature, PR into `main`, squash merge. Commit messages `type: summary`.
- Target: iPhone 12 and mid/low-range Android at 60 fps; min iOS 15, min Android API 24 *(verify at build time)*.
- Presentation for the full game moves from "everything built in code" to scene-authored prefabs and TextMeshPro once the visual phase starts; the demo's code-built approach is fine until then.
- *(v1.3)* Balance is tuned from `balance-sim.bat` tables (headless `AutoPlayer`), not from reasoning; every session that changes a rule re-runs it.
- *(v1.6, Session 8)* Shipped build setup: `BuildPipeline.cs` applies all Player Settings idempotently (product/company/bundle id, portrait-only, IL2CPP, .NET Standard 2.1, managed stripping Medium + `link.xml`); version/build numbers move only through it. Min Android API is 25, not 24 — Unity 6000.3 rejects 24. Android signs from four env vars for one build only (never in ProjectSettings); iOS signing/archiving is manual in Xcode. `TW_DEBUG` is a per-build define (`-dev` flag), never a persistent one; `release-compile-check.bat` guards against debug code leaking into a release build. Device auto quality tiers (`QualityTiers`) and `FarmConfig.OfflineMinSeconds` (60 s) are the two new runtime systems. Icon/splash render from the game's own art via `IconRenderer`. See `DECISIONS.md` Session 8 for the full rationale and measured numbers.

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

Entries added per playtest: date, what felt wrong, value before → after.

**2026-09-16 — Session 9 balance pass.** Measured with `balance-sim.bat` / `BalanceTests`
(`AutoPlayer` plays to the ending, seeds 1–5, averaged).

Targets: year-1 coins enough for at least one root node; first apprentice and first `CanRetire`
early enough to teach the loop inside the first few years; no finite node above 35% of a
generation's coins; the ring should not be the only harvest source once helpers are affordable.

Values changed:

| Value | Before | After |
|---|---|---|
| Carrot value | 1 | 2.3 |
| HeritageThreshold | 5000 | 3000 |
| SeedDivisor | 50 | 30 |
| Almanac growth — Hand | 1.6 | 1.6 (unchanged) |
| Almanac growth — Soil | 1.6 | 1.6 (unchanged) |
| Almanac growth — Calendar | 1.6 | 1.6 (unchanged) |
| Almanac growth — Field | 1.6 | 1.18 |
| Almanac growth — Helpers | 1.6 | 1.25 |
| Heritage growth (all branches) | 1.5 | 1.8 |
| Ring harvest (§2.1) | all Ripe plots under the ring harvest together | *(v1.4)* one Ripe plot at a time (furthest along, ties nearest centre); watering/growing still applies to every plot under the ring in parallel |

Results (average of seeds 1–5, `AutoPlayer` to the ending):

| Metric | Before | After |
|---|---|---|
| Year-1 coins | 29 (no root node affordable) | 67 (exactly one root node) |
| First apprentice, year | 7 | 3.8 (seeds: 4, 4, 3, 4, 4) |
| First `CanRetire`, year | 16 | 8 |
| Seeds at first retire | 10 | 10.2 |
| Generations to max Heritage | 6 (new retire rule) / 34 (old flat 10-seed rule) | 6 |
| Time to ending | 11.4 h | 6.39 h (6.40 / 6.36 / 6.41 / 6.36 / 6.42) |
| Ring share — year 1 / first retire / gen 4 | 100% / 83% / 68% | 100% / 55% / 28% |
| Largest node share | 98% (`upgrade_plot`) | 18% (`apprentice_count`, gen 1) finite nodes; `upgrade_plot` (open-ended) 86–96% |

Ring share at first retire averages 55% across seeds (seed 4: 57%).

**2026-09-17 — M.1 ring pass.** Measured with `balance-sim.bat` / `BalanceTests` (`AutoPlayer`, seeds 1–5).

What changed: over-ripening (§2.5), visible combo milestone payouts (§6), Hand-branch `ring_shape`
and `tap_harvest` nodes (§2.1, §6), and the flow bonus for a moving ring (§2.1).

Measured effect: ring share at first retire rose from ~0.55 to ~0.58, so the `BalanceTests` range
for it was widened from `(0.40, 0.57)` to `(0.40, 0.62)`. Everything else in `BalanceTests` is
unchanged and green (214 tests).

**2026-09-19 — M.2 field variety pass.** Measured with `balance-sim.bat` / `BalanceTests`
(`AutoPlayer`, seed 1).

What changed: neighbour variety bonus, crop rotation bonus, and special ground — fertile and stony
plots (§2.3, §2.4).

Measured effect: year-1 coins 69 (unchanged), first apprentice year 3, first `CanRetire` year 7,
seeds at first retire 11, 6 generations to max Heritage, 5.36 h to the ending (was 5.59 h), ring
share at first retire 52% (was 58%), generation 4 29%. No tuning needed; 233 tests green.

**2026-09-19 — M.3 seasons, year and weather pass.** Measured with `balance-sim.bat` / `BalanceTests`
(`AutoPlayer`, seed 1).

What changed: season rules (§3.2), frost rush (§3.1), yearly goals (§3.3), weather (§5.4) and the
end-of-year grade (§3.4). The new income sources pushed year 1 to 83 coins and the ending to
3.9 h. Tuned: carrot value 2.2 → 2.0 (§2.3), `HeritageThreshold` 3 000 → 3 500 and `SeedDivisor`
30 → 33 (§7); frost rush settled at ×1.25, grade bonus 2%/4%, goal reward share 5%.

Results (seed 1): year 1 = 67 coins, first apprentice year 3, first `CanRetire` year 7, 10 seeds at
first retire, 6 generations to max Heritage, 5.21 h to the ending, ring share 100% / 56% / 30%
(year 1 / first retire / generation 4). 249 tests green.

**2026-09-19 — M.4 helpers and animals pass.** Measured with `balance-sim.bat` / `BalanceTests`
(`AutoPlayer`, seed 1).

What changed: placeable scarecrows (§4, §5.1), apprentice roles (§4), the `farm_dog` and `beehive`
Almanac nodes (§6), and the manual tractor trigger (§4). None of these touch cost curves or crop
timings.

Results (seed 1): year 1 = 67 coins, first apprentice year 3, first `CanRetire` year 7, 10 seeds at
first retire, 6 generations to max Heritage, 5.19 h to the ending, ring share 100% / 56% / 30%
(year 1 / first retire / generation 4) — unchanged from M.3. No tuning needed; 258 tests green.

**2026-09-19 — M.5 events and threats pass.** Measured with `balance-sim.bat` / `BalanceTests`
(`AutoPlayer`, seed 1).

What changed: pests — moles, rabbits, locust swarms (§5.5); the `hens` Almanac node completing the
M.4 helpers item (§4, §6); lucky moments — four-leaf clover, golden egg, shooting star rush (§5.6);
the travelling trader (§5.7). `AutoPlayer` now taps moles and rabbits after its reaction delay like
a crow and steers the ring to a locust swarm first, but does not buy from the trader, so balance
targets keep measuring the core loop.

Results (seed 1): year 1 = 67 coins, first apprentice year 3, first `CanRetire` year 7, 11 seeds at
first retire, 6 generations to max Heritage, 5.27 h to the ending, ring share 100% / 54% / 29%
(year 1 / first retire / generation 4). No tuning needed; 270 tests green.
