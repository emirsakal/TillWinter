---
name: save-versioning
description: How Till Winter save data is structured and versioned (SaveData in Core, schemaVersion, migrations, round-trip test). Applies from Session 2 (save/offline) on.
---

# Save format and versioning

Until Session 2 lands this is the contract; afterwards keep it true.

- `SaveData` is a plain serializable class in `Assets/TillWinter/Core` holding every persistent field: coins, lifetime coins, year, season/year time, grid size, per-plot tier/state/progress, Almanac levels, Heritage levels/seeds/generation, statistics, `schemaVersion`, `savedAtUtc`.
- **Every new persistent state field goes into `SaveData` in the same commit that adds it to `FarmState`**, plus the round-trip test (`FarmSim -> SaveData -> JSON -> SaveData -> FarmSim`, then assert the state matches field by field).
- `schemaVersion` is an integer. Bump it whenever a field is added, removed or changes meaning. Migrations are pure functions `Migrate_vN_to_vN+1(SaveData)` chained in order; a corrupt or unknown-version file starts a fresh farm and never throws (GDD §9).
- JSON is written by the Unity layer to `Application.persistentDataPath` on winter, rebirth, purchase and app pause; Core only produces/consumes `SaveData`.
- Offline progress: on load, Core simulates passive systems only for `min(elapsed, 8 h)` at a fixed dt; the year timer does not advance (GDD §9).
- Tests: round-trip, migration from each older version fixture (keep a JSON fixture per version under `Assets/TillWinter/Tests/Fixtures/`), corrupt-file-starts-fresh.
