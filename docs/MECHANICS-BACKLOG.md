# Till Winter — Mechanics Backlog

A mechanics review from 2026-09-17, worked through group by group like `docs/VISUAL-BACKLOG.md`.
Every item here must respect the pillars in `docs/GDD.md` §1 (one finger always busy, three
chains one loop, frost is the drum, finite and finishable), add no monetization, keep offline
simulation to passive systems only, keep `TillWinter.Core` pure C# and deterministic, and be
measured with `balance-sim.bat`/`BalanceTests` before it ships. A GDD rule changed by
implementation gets marked `*(vX.Y)*` inline, per `CLAUDE.md`.

## Findings

1. Coin spending has no decisions late: 86–96% of a generation's coins go into `upgrade_plot`
   (GDD §15).
2. The ring fades: ring share of harvests is 55% at first retire, 28% by generation 4.
3. Seasons are cosmetic (§3): no rule changes per season.
4. Few events: crows, rain cloud, golden crop; years feel alike after generation 2.
5. Rebirth adds no new content: Heritage is 16 head-start nodes.
6. Winter is only a shop.
7. The player never chooses what is planted (`upgrade_plot` raises the lowest plot).

## Groups

### M.1 Ring and core loop

- **Done.** [H] **Over-ripening.** A Ripe plot left waiting slowly loses value after a grace
  time; the ring must prioritise. Unity-side: an over-ripe crop dulls and droops.
- **Done.** [H] **Visible combo.** Milestone payouts at combo 10/25/50 (3x/8x/20x the harvested
  crop's value); breaks only when a crow eats a crop, not on over-ripening. Unity-side: a HUD
  milestone banner with a haptic.
- **Done.** [M] **Ring shapes as Hand unlocks** (rake strip, cross). Unity-side: the ring decal
  takes the shape (a second projector for the cross), a shape button on the HUD cycles unlocked
  shapes. Tour has shots `25-ring-rake` and `26-ring-cross`.
- **Done.** [M] **Flow bonus.** A moving ring works slightly faster than a still one. Unity-side:
  beads race with flow.
- **Done.** [L] **Tap to finish.** A tap completes one Ripe plot instantly, with a cooldown.

### M.2 Crops and field

- **Done.** [H] **Choose the crop per plot** (seed bag). `upgrade_plot` now raises a bed's quality
  (bounded, unlocked-crop capped); a HUD basket chip row picks the crop grown up to that quality.
- **Done.** [H] **Seasonal preferences.** Each crop likes one season and sells ×1.25 during it
  (Year phase only); timings stay fixed per the tuning rule, so this is value-only, not speed.
- **Done.** [M] **Neighbour bonuses.** Each orthogonal neighbour growing a different crop adds a
  small multiplicative value bonus, capped at four neighbours; stony neighbours and field edges
  don't count.
- **Done.** [M] **Crop rotation.** Growing a different crop than last year sells for a bonus all
  year; measured year-to-year, not harvest-to-harvest. Retiring clears the memory.
- **Done.** [M] **Special plots** (fertile, stony to clear). Fertile ground sells for more; stony
  ground grows nothing until the ring clears it. Compost is deferred — it would need an
  input/resource system that doesn't exist yet.

### M.3 Seasons, year and weather

- **Done.** [H] **Season rules.** Spring rain boosts passive watering ×1.5; summer drought dries an
  idle Wet plot back to Dry after 8 s unless Sun is owned; autumn harvest festival widens the combo
  window ×1.5. Crop timings never change.
- **Done.** [H] **Yearly goals** ("harvest 20 pumpkins this year") paying coins, from year 2.
- **Done.** [M] **New weather.** Storm (Sun stops, rain waters Dry plots, no crows), heat wave (Sun
  ×1.5, faster drought), fog (no over-ripening, no crows) — one spell a year, 60% chance, from
  year 2.
- **Done.** [M] **End-of-year grade**, 1–3 stars by harvest freshness with a small coin bonus.
- **Done.** [L] **Frost-warning boost.** Harvests pay ×1.25 during the frost warning (not double —
  ×2 pushed year-1 coins outside `BalanceTests`' range; settled at ×1.25 in the M.3 balance pass).

### M.4 Helpers and animals

- **Done.** [H] **Placeable scarecrows** protecting an area instead of a global chance. Each
  scarecrow level places one the player can move to any plot corner; it guards every plot within
  a fixed radius (12 plots on open ground) and the crow spawn chance goes flat.
- **Done.** [M] **Helper roles** (waterer/harvester). Tap an apprentice to switch; a Waterer walks
  to the nearest Dry, non-stony, untargeted plot and waters it. Dragging a helper to an area is
  left out.
- **Done.** [M] **Animals with jobs.** The dog chases off a crow that's sat a moment, no bounty,
  then rests; bees speed the Sun on the two columns by the sunflowers. Hens eating pests is moved
  to M.5 — pests don't exist yet.
- **Done.** [L] **Tractor control.** Trigger by hand once the charge bar is at least half full.
  Picking the row is left out — the busiest row is almost always the right one.

### M.5 Events and threats

- **Done.** [H] **More pests.** Moles dig a plot, rabbits eat carrots, a locust swarm driven off
  with the ring. Also completes the M.4 "hens eat pests" item, deferred there because pests didn't
  exist yet — the `hens` Almanac node now eats a pest and has a chance at a golden egg.
- **Done.** [M] **Travelling trader** once a year (harvest for seeds, rare seed).
- **Done.** [L] **Rare lucky moments** (four-leaf clover, golden egg, shooting star multiplier).

### M.6 Winter

- **Done.** [H] **Storage and market.** Keep part of the harvest, sell in another season at a
  better price. The `barn` Almanac node stores a deterministic share of harvests; each winter
  draws a market price (×0.8–×1.7) to sell into, or turn the stock into preserves.
- **Done.** [M] **Winter activity.** Preserves (stock sold at a fixed rate at next spring) are
  winter's activity. The one-finger ice-fishing mini-game is not done.
- **Done.** [M] **Almanac help.** Effect preview already existed; added a suggested marker
  (`AlmanacAdvisor`, gold star badge on the best-value affordable node) and one free respec per
  generation that refunds exactly what was spent.

### M.7 Progression and rebirth

- **Open.** [H] **Heirs / generation traits.** Pick one of two or three heirs at each rebirth.
- **Open.** [H] **Heirlooms** carried across generations.
- **Open.** [M] **Bigger Heritage**, with mutually exclusive choices.
- **Open.** [M] **Challenge generations** (no helpers, short years) for extra seeds.
- **Open.** [M] **In-game achievements** with small permanent rewards (offline, no platform
  services).

### M.8 Ending and after

- **Open.** [M] **Family album.** House, stats and a short story per generation.
- **Open.** [M] **New Game+.** Harder years after the ending with cosmetic rewards.
- **Open.** [L] **Daily farm seeded by the date** (offline only; at the edge of the no-live-ops
  pillar).

### M.9 Feel and accessibility

- **Open.** [M] **Early-year goal checklist** instead of one-off hints.
- **Open.** [M] **Hands-free mode.** Tap to place the ring, it follows plots on its own.
- **Open.** [L] **Before you leave.** Set the helpers' focus for offline passive income.

## Recommended first five

For reference, not sequence: crop choice + seasonal preferences (M.2), over-ripening + visible
combo (M.1), yearly goals (M.3), heirs (M.7), storage and market (M.6). Items 1, 3, 4 and 5 move
balance and each need their own `balance-sim.bat` pass.

## Deliberately out

Monetization, ads, live-ops, online services.
