---
name: add-almanac-node
description: How to add or change an Almanac node in Till Winter (data table, effect type, resolver, tests, strings).
---

# Adding an Almanac node

All in `Assets/TillWinter/Core` unless noted. Use `repo-scout` for exact line numbers first.

1. `Types.cs`: add an `EffectType` member if the effect is new.
2. `AlmanacData.cs`: add one `N(id, Branch, prerequisites[], maxLevel, baseCost, EffectType, valuePerLevel)` row in the branch block. Ids are `snake_case`; `-1` maxLevel means dynamic (only `upgrade_plot` today). Prerequisites are any-of (GDD §6).
3. If the sim applies the effect now: add the case to `StatResolver.Resolve` (a new `Stats` field if needed) and add the `EffectType` to `AlmanacData.Implemented`. Purchase-time side effects (field size, plot tier) go in `FarmSim.ApplyPurchase`. If not applied yet, do neither; it will show `[not implemented yet]` automatically.
4. `Assets/TillWinter/Unity/Localize.cs`: add `almanac.<id>.name` and `almanac.<id>.desc` EN strings (Core holds no text).
5. Tests in `Assets/TillWinter/Tests/AlmanacTests.cs`: add the id to `Table_ContainsEveryGddNode...` and its max level to `Table_MaxLevelsMatchGdd`; `EveryEffectInTable_IsAppliedOrFlaggedNotImplemented` must still pass (an implemented effect has to change a stat at level 1, or be handled at purchase time). Add a `FarmSimTests` case for the number it changes.
6. `docs/GDD.md` §6 lists nodes; `DECISIONS.md` lists edges. Update both via `docs-writer`.
