# Till Winter

**A short, finite, one-thumb farming game for phones.** You strike the hard ground with a hoe,
holding to water what grows and swiping to reap what ripens. Autumn ends in frost, winter opens a
skill tree, and when a farm has given all it can you hand it to the next generation. Seven to eight
hours to the ending — then it ends, on purpose.

Unity 6000.3.22f1 · portrait mobile (iOS + Android) · English & Turkish · no ads, no in-app
purchases, no analytics, no network · 330 automated tests

| The field | Winter: the Almanac | Heritage: rebirth |
|---|---|---|
| ![The field](docs/screenshots/readme-1-field.png) | ![Almanac](docs/screenshots/readme-2-almanac.png) | ![Heritage](docs/screenshots/readme-3-heritage.png) |

---

## How it plays

**One finger, always busy.** Tap hard ground to strike it, harder on the beat than off it; break
through and a seed drops by itself. Hold your finger on what's growing to water it; swipe across
what's ripe to reap it — several crops in one swipe pay more than one at a time. Break through a
layer and the ground underneath is tougher, but pays more: **hard → growing → ripe → hard, one
layer deeper**. Let a crop stand too long and it dulls and pays less.

**A year is about four minutes.** Spring hits harder, summer softens growth, autumn stretches your
reap combo. The last ten seconds before frost are a rush: everything pays more and the timer beats
like a heart. Then winter: the year is graded on how fresh your harvests were, the ground keeps every
layer you dug to, hard patches heal, and whatever was ripe gets reaped for you, one tile at a time.

**Winter is the breath.** Coins buy nodes in the Almanac, a skill tree of 38 nodes across five
branches: a harder-hitting hoe, deeper stamina and faster growth, apprentices who dig, water and
harvest on their own, a tractor, a barn that stores part of each harvest for the market, scarecrows,
a dog, hens.

**Then you let go.** When a farm stalls, you pass it on. The Almanac, the coins and the field are
gone; Heritage Seeds remain and buy 20 permanent nodes for every generation that follows. An heir
brings a trait, you may take on a challenge generation for extra seeds, and twelve achievements
become heirlooms that never leave the family.

**It has an ending.** Finish the Heritage tree and you play one Golden Year on a full field of
golden wheat. Afterwards: the family album, New Game+ for a harder run, and a daily farm — the same
field and seed for everyone that day, one year, one score.

Other things living on the farm: weather (storms, heat waves, fog), yearly goals, crows, moles,
rabbits and locust swarms, four-leaf clovers and shooting stars, a travelling trader, crop rotation
and neighbour variety, stony and fertile ground, a greenhouse that earns through winter.

| Title screen | A storm and a goal | The family album | Turkish |
|---|---|---|---|
| ![Menu](docs/screenshots/readme-0-menu.png) | ![Storm](docs/screenshots/readme-5-weather.png) | ![Album](docs/screenshots/readme-4-album.png) | ![Turkish](docs/screenshots/readme-6-turkish.png) |

**Comfort.** A getting-started checklist for the first generation, a plan for what the helpers do
while the app is closed, reduce-motion, larger text, and a quality tier chosen from the device. A
farm code moves a save to another phone, and local reminders (opt-in) let you know when the passive
work is ready.

---

## For engineers

The interesting part of this repo is the split: **all rules live in a pure C# library that has never
heard of Unity.**

| Assembly | Size | Rule |
|---|---|---|
| `TillWinter.Core` | 22 files, ~6.4k lines | Pure C#, `noEngineReferences`. State, economy, timers, save format. Deterministic for a fixed `dt` and seed. Holds no user-facing strings, only keys. |
| `TillWinter.Unity` | 66 files, ~12.2k lines | Presentation and input only. Reads `FarmState`, plays VFX and sound, forwards taps. No game rules. |
| `TillWinter.Tests` | 51 files, ~7.3k lines | EditMode NUnit against Core alone. 330 tests. |

What that buys, and what it costs, is written down in [`CLAUDE.md`](CLAUDE.md) (the rules every
change must keep) and [`DECISIONS.md`](DECISIONS.md) (every choice the design left open, with the
reasoning). The design itself is [`docs/GDD.md`](docs/GDD.md), numbered section by section, and each
numbered rule has a test.

**Determinism.** The core takes a seed and a fixed timestep and produces the same farm every time.
That is what makes the balance simulator and the save fixtures possible.

**Data, not code.** Adding a skill-tree node is a table row: an id, a branch, prerequisites, a cost
curve and an effect type. `StatResolver` is the single place where levels become numbers, so no
system can quietly invent its own multiplier. Tunables live in `FarmConfig`.

**Save versioning.** One DTO, schema version 17 today. Every bump ships a migration step *and* a
hand-written JSON fixture of the previous version that must load and then play deterministically —
so a save written by any older build still opens.

**Offline is a rule, not a bonus.** `SimulateOffline` advances growth, apprentices (diggers included)
and the tractor for at most eight hours; the year timer, crows, weather and seasons never move while
the app is closed, so you cannot lose a year by living your life.

**Balance is measured, not argued.** `AutoPlayer` plays the whole game headlessly. `BalanceTests`
asserts the targets: year-1 income, hours to the ending, generations to a full Heritage tree, how
much of the take comes from your own hand rather than the helpers, seeds at the first rebirth. A
tuning change that breaks one fails the suite.

**Four gates, not one.**

| Gate | What it does |
|---|---|
| `run-tests.bat` | 330 EditMode tests against Core |
| `balance-sim.bat` | Headless full playthrough; prints the year table and the target summary |
| `smoke-test.bat` | Drives the real game in play mode with a virtual mouse: a full year, purchases, year two, a crow, the ending; checks draw calls, triangles and per-frame allocations |
| `ui-tour.bat <folder>` | Opens all 40-odd screens and sheets through their own buttons and writes a screenshot of each (the images above came from it) |

Plus `release-compile-check.bat`, which fails if debug-only code leaks into a release build.

[`docs/STORE.md`](docs/STORE.md) has the store listing package (texts, screenshots, form answers);
[`docs/RELEASE.md`](docs/RELEASE.md) has the steps to cut a release.

**Mobile discipline.** No per-frame allocation in gameplay code (numbers are written into char
buffers, not strings), pooled VFX, one rate limiter for repeatable triggers, haptics behind a
setting, safe-area aware layout, and two quality tiers picked from the device.

---

## Run it

1. Unity **6000.3.22f1** (`open-project.bat`, or Unity Hub).
2. First clone only: `ui-setup.bat`, `art-setup.bat`, `feel-setup.bat` — they generate the fonts,
   themes, prefabs, materials, particle prefabs and the audio mixer. All three are idempotent.
3. Open `Assets/TillWinter/Scenes/Menu.unity` and press Play.

Builds: `build-android.bat [-dev]` writes to `Builds\Android\…`; `build-ios.bat [-dev]` writes an
Xcode project for signing on a Mac. Android signing comes from four environment variables
(`TW_KEYSTORE_PATH`, `TW_KEYSTORE_PASS`, `TW_KEY_ALIAS`, `TW_KEY_PASS`) read for one build only —
no keystore, password or alias is ever committed.

Saves live in the app's `persistentDataPath`: `tillwinter.json` (+ `.bak`) for the farm and
`settings.json` for preferences, so resetting one never touches the other.

## Project layout

```
Assets/TillWinter/Core/     rules, state, economy, save format (no UnityEngine)
Assets/TillWinter/Unity/    views, HUD, screens, input, audio, localization
Assets/TillWinter/Tests/    EditMode tests, including save fixtures per schema version
Assets/TillWinter/Editor/   art/feel/UI generators, smoke test, UI tour, build pipeline
Assets/Art/, Assets/Audio/  Kenney CC0 kits with their licences
docs/                       GDD, device checklist, privacy, style, backlogs, screenshots
```

## Credits

Low-poly art and sound are [Kenney](https://kenney.nl) CC0 kits (Nature, Food, Mini Characters,
Game Icons, and four audio packs), each with its original licence kept beside it; everything the
kits do not cover is generated from primitives by the editor tooling. Type is Figtree and Rammetto
One (SIL OFL 1.1). Privacy: nothing leaves the device — [`docs/PRIVACY.md`](docs/PRIVACY.md).

Built by [Emirhan Furkan Sakal](https://github.com/emirsakal) (EFS Games), paired with
[Claude Code](https://claude.com/claude-code); `CLAUDE.md` is the contract those sessions work under.

---

## Türkçe özet

**Till Winter**, tek parmakla oynanan, kısa ve sonu olan bir mobil çiftlik oyunu. Sert toprağı çapayla
kırarsın, büyüyeni tutarak sularsın, olgunlaşanı kaydırarak biçersin — her kırdığın katman bir öncekinden
daha zor ama daha kazançlıdır. Sonbahar donla biter, kış Almanak'ı açar; bir çiftlik verebileceğini
verince onu bir sonraki nesle devredersin. Sona yedi ila sekiz saatte varılır ve oyun gerçekten biter.

Reklam yok, uygulama içi satın alma yok, analitik yok, internet gerekmiyor. Türkçe ve İngilizce
birlikte geliyor.

Kod tarafında dikkat çeken şey ayrım: **bütün kurallar Unity'yi hiç tanımayan saf bir C# kütüphanesinde.**
Bu sayede oyun sabit bir adım ve tohumla her seferinde aynı şekilde işliyor; denge bir simülasyonla
ölçülüyor, kayıt dosyasının her sürümü eski kayıtlarla test ediliyor ve 330 test yalnızca kuralları
sınıyor. Tasarım `docs/GDD.md`'de, mimari kuralları `CLAUDE.md`'de, açık bırakılan her karar
`DECISIONS.md`'de yazılı. Mağaza paketi `docs/STORE.md`'de, sürüm çıkarma adımları
`docs/RELEASE.md`'de.
